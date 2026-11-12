using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// A gravação dos holerites. O que o banco recusa vira o erro do contrato aqui mesmo (<see cref="TradutorDeRecusaDaComunicacao"/>): a chave única da competência, tipo e versão (<c>HOLERITE_JA_EXISTE</c> no envio, <c>HOLERITE_VERSAO_DESATUALIZADA</c> na
/// nova versão: a corrida entre dois cliques) e as regras do trigger (competência futura, anterior à admissão ou posterior ao desligamento). O erro que não é da comunicação sobe como veio.
/// </summary>
public sealed class HoleritesRepositorio(AppDbContext db) : IHoleritesRepositorio
{
    public async Task<int> AdicionarAsync(Holerite holerite, OperacaoDeHolerite operacao, CancellationToken ct)
    {
        db.Holerites.Add(holerite);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro)
        {
            var contexto = new ContextoDaComunicacao(operacao == OperacaoDeHolerite.Envio ? OperacaoDaComunicacao.EnviarHolerite : OperacaoDaComunicacao.SubstituirHolerite, holerite.Tipo, holerite.Mes, holerite.Ano);
            var problema = TradutorDeRecusaDaComunicacao.Traduzir(erro, contexto);
            if (problema is null)
            {
                throw;
            }

            db.ChangeTracker.Clear();
            throw problema;
        }

        return holerite.Id;
    }
}

/// <summary>A leitura dos holerites para o envio, a nova versão, a lista do RH e o download do RH.</summary>
public sealed class ConsultaDeHolerites(AppDbContext db) : IConsultaDeHolerites
{
    /// <summary>A projeção por inicializador (e não por construtor): o EF compõe filtros e ordens sobre os membros dela.</summary>
    private sealed class Linha
    {
        public int Id { get; init; }

        public int FuncionarioId { get; init; }

        public TipoDeHolerite Tipo { get; init; }

        public int Mes { get; init; }

        public int Ano { get; init; }

        public int Versao { get; init; }

        public bool VersaoAtual { get; init; }

        public DateTime? PublicacaoEmUtc { get; init; }
    }

    /// <summary>Os holerites da pessoa, cada um com a marca de ser a maior versão da competência e do tipo (publicada ou não).</summary>
    private IQueryable<Linha> DaPessoa(int funcionarioId) =>
        db.Holerites.AsNoTracking()
            .Where(h => h.FuncionarioId == funcionarioId)
            .Select(h => new Linha
            {
                Id = h.Id,
                FuncionarioId = h.FuncionarioId,
                Tipo = h.Tipo,
                Mes = h.Mes,
                Ano = h.Ano,
                Versao = h.Versao,
                VersaoAtual = h.Versao == db.Holerites.Where(o => o.FuncionarioId == h.FuncionarioId && o.Ano == h.Ano && o.Mes == h.Mes && o.Tipo == h.Tipo).Max(o => o.Versao),
                PublicacaoEmUtc = h.PublicacaoEmUtc,
            });

    private static HoleriteDaListaDoRh Montar(Linha l) => new(l.Id, l.FuncionarioId, l.Tipo, l.Mes, l.Ano, l.Versao, l.VersaoAtual, l.PublicacaoEmUtc);

    public async Task<(IReadOnlyList<HoleriteDaListaDoRh> Itens, int Total, HoleriteDaListaDoRh? UltimoPublicado)> ListarDaPessoaAsync(
        int funcionarioId,
        FiltroDeHoleritesRh filtro,
        ParametrosDeLista parametros,
        DateTime agoraUtc,
        CancellationToken ct)
    {
        var pedidos = DaPessoa(funcionarioId);
        if (!filtro.TodasAsVersoes)
        {
            pedidos = pedidos.Where(l => l.VersaoAtual);
        }

        if (filtro.Ano is { } ano)
        {
            pedidos = pedidos.Where(l => l.Ano == ano);
        }

        if (filtro.Tipo is { } tipo)
        {
            pedidos = pedidos.Where(l => l.Tipo == tipo);
        }

        var total = await pedidos.CountAsync(ct);
        var pagina = await pedidos.OrderByDescending(l => l.Ano).ThenByDescending(l => l.Mes).ThenByDescending(l => l.Versao).ThenByDescending(l => l.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).ToListAsync(ct);

        // O último publicado olha TODOS os holerites da pessoa (qualquer versão, sem filtro nem página): o mais recente pela data de publicação.
        var ultimo = await DaPessoa(funcionarioId).Where(l => l.PublicacaoEmUtc != null && l.PublicacaoEmUtc <= agoraUtc).OrderByDescending(l => l.PublicacaoEmUtc).ThenByDescending(l => l.Id).FirstOrDefaultAsync(ct);
        return ([.. pagina.Select(Montar)], total, ultimo is null ? null : Montar(ultimo));
    }

    public async Task<HoleriteParaDownload?> ObterParaDownloadDoRhAsync(int holeriteId, CancellationToken ct) =>
        await (from h in db.Holerites.AsNoTracking()
               join f in db.Funcionarios.AsNoTracking() on h.FuncionarioId equals f.Id
               where h.Id == holeriteId && h.CaminhoArquivo != null
               select new HoleriteParaDownload(h.Id, f.Nome, h.Mes, h.Ano, h.Tipo, h.Versao, h.CaminhoArquivo!)).FirstOrDefaultAsync(ct);

    public async Task<PessoaDoHolerite?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => new PessoaDoHolerite(f.Id, f.Nome, f.Ativo, f.DataAdmissao, f.DataDesligamento)).FirstOrDefaultAsync(ct);

    public async Task<int?> MaiorVersaoAsync(int funcionarioId, int ano, int mes, TipoDeHolerite tipo, CancellationToken ct) =>
        await db.Holerites.AsNoTracking().Where(h => h.FuncionarioId == funcionarioId && h.Ano == ano && h.Mes == mes && h.Tipo == tipo).MaxAsync(h => (int?)h.Versao, ct);

    public async Task<HoleriteLido?> ObterAsync(int holeriteId, CancellationToken ct) =>
        await db.Holerites.AsNoTracking()
            .Where(h => h.Id == holeriteId)
            .Select(h => new HoleriteLido(
                h.Id,
                h.FuncionarioId,
                h.Mes,
                h.Ano,
                h.Tipo,
                h.Versao,
                h.PublicacaoEmUtc,
                h.CaminhoArquivo,
                h.Versao == db.Holerites.Where(o => o.FuncionarioId == h.FuncionarioId && o.Ano == h.Ano && o.Mes == h.Mes && o.Tipo == h.Tipo).Max(o => o.Versao)))
            .FirstOrDefaultAsync(ct);
}

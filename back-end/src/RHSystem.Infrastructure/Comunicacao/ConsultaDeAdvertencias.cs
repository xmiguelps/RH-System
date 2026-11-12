using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// As leituras das advertências. O escopo sai na PRIMEIRA consulta (<see cref="EscopoDeAdvertencias"/>): a pessoa vê as dela, o gestor as da equipe dele (nenhuma se não tem equipe) e o RH todas. O ano é o da data do ocorrido (um intervalo de datas, que usa o índice); o
/// setor do filtro é o ATUAL da pessoa. "Mais recente registrado" é o maior id (a tabela não guarda a data do registro). As colunas anuláveis do banco (a aplicação sempre as preenche) saem com um valor padrão inofensivo.
/// </summary>
public sealed class ConsultaDeAdvertencias(AppDbContext db) : IConsultaDeAdvertencias
{
    public async Task<PaginaDeAdvertencias> ListarAsync(EscopoDeAdvertencias escopo, FiltroDeAdvertencias filtro, ParametrosDeLista parametros, CancellationToken ct)
    {
        var advertencias = NoEscopo(escopo);
        if (filtro.Ano is { } ano)
        {
            var primeiro = new DateOnly(ano, 1, 1);
            var ultimo = new DateOnly(ano, 12, 31);
            advertencias = advertencias.Where(a => a.Data >= primeiro && a.Data <= ultimo);
        }

        if (filtro.Situacao is { } situacao)
        {
            advertencias = advertencias.Where(a => a.Situacao == situacao);
        }

        if (filtro.Tipo is { } tipo)
        {
            advertencias = advertencias.Where(a => a.Tipo == tipo);
        }

        if (filtro.SetorId is { } setorId)
        {
            advertencias = advertencias.Where(a => db.Funcionarios.Any(f => f.Id == a.FuncionarioId && f.SetorId == setorId));
        }

        if (filtro.FuncionarioId is { } funcionarioId)
        {
            advertencias = advertencias.Where(a => a.FuncionarioId == funcionarioId);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            advertencias = advertencias.Where(a => EF.Functions.Like(a.Motivo, like) || db.Funcionarios.Any(f => f.Id == a.FuncionarioId && EF.Functions.Like(f.Nome, like)));
        }

        var total = await advertencias.CountAsync(ct);
        var ordenadas = escopo.Papel == PapelNaConsulta.PropriaPessoa
            ? advertencias.OrderByDescending(a => a.Situacao == SituacaoDaAdvertencia.Pendente).ThenByDescending(a => a.Data).ThenByDescending(a => a.Id)
            : advertencias.OrderByDescending(a => a.Data).ThenByDescending(a => a.Id);
        var itens = await Projetar(ordenadas.Skip(parametros.Pular).Take(parametros.TamanhoPagina)).ToListAsync(ct);
        return new PaginaDeAdvertencias(itens, total);
    }

    public async Task<AdvertenciaLida?> ObterAsync(int advertenciaId, EscopoDeAdvertencias escopo, CancellationToken ct) =>
        await Projetar(NoEscopo(escopo).Where(a => a.Id == advertenciaId)).FirstOrDefaultAsync(ct);

    public async Task<PessoaParaAdvertencia?> ObterPessoaAsync(int funcionarioId, EscopoDeAdvertencias escopo, CancellationToken ct)
    {
        var pessoas = db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId);
        if (escopo.Papel == PapelNaConsulta.Gestor)
        {
            if (escopo.Equipe is not { } equipe)
            {
                return null;
            }

            pessoas = db.Funcionarios.AsNoTracking().DaEquipe(equipe).Where(f => f.Id == funcionarioId);
        }
        else if (escopo.Papel == PapelNaConsulta.PropriaPessoa)
        {
            pessoas = pessoas.Where(f => f.Id == escopo.FuncionarioId);
        }

        return await (
            from f in pessoas
            select new PessoaParaAdvertencia(
                f.Id,
                f.Nome,
                f.Ativo,
                f.EmailCorporativo,
                f.GestorId,
                f.GestorId != null && db.Gestores.Any(g => g.Id == f.GestorId && db.Funcionarios.Any(x => x.Id == g.FuncionarioId && x.Ativo)))).FirstOrDefaultAsync(ct);
    }

    public async Task<IReadOnlyList<AdvertenciaParaResumoRh>> DoAnoParaOResumoDoRhAsync(int ano, CancellationToken ct)
    {
        var primeiro = new DateOnly(ano, 1, 1);
        var ultimo = new DateOnly(ano, 12, 31);
        return await (
            from a in db.Advertencias.AsNoTracking()
            where a.Data >= primeiro && a.Data <= ultimo
            join f in db.Funcionarios.AsNoTracking() on a.FuncionarioId equals f.Id
            select new AdvertenciaParaResumoRh(a.Situacao ?? SituacaoDaAdvertencia.Pendente, f.Setor != null ? f.Setor.Nome : null)).ToListAsync(ct);
    }

    public async Task<(IReadOnlyList<FuncionarioResumo> Equipe, IReadOnlyList<AdvertenciaParaResumoDaEquipe> Advertencias)> DoAnoParaOResumoDaEquipeAsync(int ano, EquipeDoGestor equipe, CancellationToken ct)
    {
        var primeiro = new DateOnly(ano, 1, 1);
        var ultimo = new DateOnly(ano, 12, 31);
        var pessoas = await db.Funcionarios.AsNoTracking().DaEquipe(equipe).Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null)).ToListAsync(ct);
        var advertencias = await db.Advertencias.AsNoTracking()
            .Where(a => a.Data >= primeiro && a.Data <= ultimo && db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(a.FuncionarioId))
            .Select(a => new AdvertenciaParaResumoDaEquipe(a.FuncionarioId, a.Situacao ?? SituacaoDaAdvertencia.Pendente))
            .ToListAsync(ct);
        return (pessoas, advertencias);
    }

    public async Task<IReadOnlyList<DestinatarioDoRh>> DestinatariosDoRhAsync(CancellationToken ct)
    {
        var nivelDoRh = NomesDeNivel.DoPerfil(Perfil.RH);
        return await db.Funcionarios.AsNoTracking()
            .Where(f => f.Ativo && f.EmailCorporativo != null && f.NivelDeAcesso != null && f.NivelDeAcesso.Nome == nivelDoRh)
            .OrderBy(f => f.Id)
            .Select(f => new DestinatarioDoRh(f.Id, f.Nome, f.EmailCorporativo!))
            .ToListAsync(ct);
    }

    private IQueryable<Advertencia> NoEscopo(EscopoDeAdvertencias escopo)
    {
        var advertencias = db.Advertencias.AsNoTracking();
        switch (escopo.Papel)
        {
            case PapelNaConsulta.PropriaPessoa:
                var minhaId = escopo.FuncionarioId;
                return advertencias.Where(a => a.FuncionarioId == minhaId);
            case PapelNaConsulta.Gestor:
                if (escopo.Equipe is not { } equipe)
                {
                    return advertencias.Where(_ => false);
                }

                return advertencias.Where(a => db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(a.FuncionarioId));
            default:
                return advertencias;
        }
    }

    /// <summary>A projeção ordenada e já paginada: o nome do gestor vem da linha de <c>GESTORES</c> (e é <c>null</c> quando foi o RH).</summary>
    private IQueryable<AdvertenciaLida> Projetar(IQueryable<Advertencia> advertencias) =>
        from a in advertencias
        join f in db.Funcionarios on a.FuncionarioId equals f.Id
        select new AdvertenciaLida(
            a.Id,
            a.Data ?? default,
            new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null),
            a.Tipo ?? TipoDeAdvertencia.Verbal,
            a.Motivo ?? string.Empty,
            a.Descricao ?? string.Empty,
            a.Situacao ?? SituacaoDaAdvertencia.Pendente,
            a.GestorId == null ? null : db.Gestores.Where(g => g.Id == a.GestorId).Join(db.Funcionarios, g => g.FuncionarioId, x => x.Id, (g, x) => x.Nome).FirstOrDefault(),
            a.CienciaEmUtc,
            a.TextoContestacao,
            a.ContestacaoEmUtc);
}

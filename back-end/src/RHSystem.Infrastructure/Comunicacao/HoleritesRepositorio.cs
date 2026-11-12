using Microsoft.EntityFrameworkCore;
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

/// <summary>A leitura dos holerites para o envio e a nova versão (as listas do RH e da pessoa vêm na T11).</summary>
public sealed class ConsultaDeHolerites(AppDbContext db) : IConsultaDeHolerites
{
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

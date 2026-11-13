using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>Quem é do público do treinamento: <c>TODOS</c> sempre, <c>SETORES</c> se o setor da pessoa é um dos escolhidos, <c>PESSOAS</c> se ela é uma das escolhidas; sem público escolhido não alcança ninguém. É a mesma conta da lista, do detalhe, da inscrição e do início.</summary>
internal static class PublicoDaPessoa
{
    public static bool Alcanca(PublicoDoTreinamento? publico, IEnumerable<(int? SetorId, int? FuncionarioId)> linhas, int funcionarioId, int? setorDaPessoa) => publico switch
    {
        PublicoDoTreinamento.Todos => true,
        PublicoDoTreinamento.Setores => setorDaPessoa is { } setor && linhas.Any(l => l.SetorId == setor),
        PublicoDoTreinamento.Pessoas => linhas.Any(l => l.FuncionarioId == funcionarioId),
        _ => false,
    };
}

/// <summary>
/// A escrita da participação da pessoa logada. As entidades dos treinamentos NÃO são auditadas pelo interceptor e inscrever/iniciar não geram evento. Cada operação é UMA instrução (sem transação do chamador). A inscrição trata a chave única da linha da pessoa como "já estava"; a recusa do trigger (pessoa inativa) sobe traduzida em 422.
/// </summary>
public sealed class MinhaParticipacaoRepositorio(AppDbContext db) : IMinhaParticipacaoRepositorio
{
    public async Task<TreinamentoParaParticipar?> LerAsync(int funcionarioId, int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;
        var t = await db.Treinamentos.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Publicado, x.ComecaEm, x.Obrigatorio, x.EncerradoEmUtc, x.Publico }).SingleOrDefaultAsync(ct);
        if (t is null)
        {
            return null;
        }

        var linha = await db.TreinamentosFuncionarios.AsNoTracking().Where(l => l.TreinamentoId == id && l.FuncionarioId == funcionarioId).Select(l => new { l.Situacao }).SingleOrDefaultAsync(ct);
        var publico = t.Publico is PublicoDoTreinamento.Setores or PublicoDoTreinamento.Pessoas
            ? await db.TreinamentosPublico.AsNoTracking().Where(p => p.TreinamentoId == id).Select(p => new { p.SetorId, p.FuncionarioId }).ToListAsync(ct)
            : [];
        var meuSetor = t.Publico == PublicoDoTreinamento.Setores ? await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => f.SetorId).SingleOrDefaultAsync(ct) : null;
        var doPublico = PublicoDaPessoa.Alcanca(t.Publico, publico.Select(p => (p.SetorId, p.FuncionarioId)), funcionarioId, meuSetor);
        return new TreinamentoParaParticipar(t.Id, t.Publicado, t.ComecaEm, t.Obrigatorio, t.EncerradoEmUtc, doPublico, linha is not null, linha?.Situacao);
    }

    public async Task<bool> InscreverAsync(int funcionarioId, int treinamentoId, CancellationToken ct)
    {
        db.TreinamentosFuncionarios.Add(TreinamentoFuncionario.Criar(treinamentoId, funcionarioId, SituacaoNoTreinamento.Inscrito));
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveRepetida(erro) && string.Equals(ErrosDoBanco.NomeDaChaveRepetida(erro), RHSystem.Application.Treinamentos.Mensagens.RegrasDoBancoDosTreinamentos.ChaveDaLinhaDaPessoa, StringComparison.OrdinalIgnoreCase))
        {
            db.ChangeTracker.Clear(); // outra tela inscreveu antes: a linha nova não foi gravada
            return false;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDosTreinamentos.Traduzir(erro) is { } recusa)
        {
            db.ChangeTracker.Clear();
            throw recusa;
        }
    }

    public async Task<bool> IniciarAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct)
    {
        var id = treinamentoId;
        var eu = funcionarioId;
        DateOnly? inicio = hoje;
        SituacaoNoTreinamento? emAndamento = SituacaoNoTreinamento.EmAndamento;

        // AUDITORIA-OK: iniciar o treinamento não gera evento (a pessoa mexe só na própria linha); o UPDATE só vale se a linha ainda é INSCRITO e o treinamento não foi encerrado (repetir, ou iniciar depois do encerramento, não muda nada). A data vem do relógio da aplicação, não do CURDATE() do banco.
        var linhas = await db.TreinamentosFuncionarios
            .Where(l => l.TreinamentoId == id && l.FuncionarioId == eu && l.Situacao == SituacaoNoTreinamento.Inscrito && db.Treinamentos.Any(t => t.Id == id && t.EncerradoEmUtc == null))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Situacao, emAndamento).SetProperty(l => l.IniciadoEm, inicio), ct);
        return linhas == 1;
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Gestao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// As gravações de férias. O pedido nasce na transação em que a pessoa já foi travada (a mesma trava do ponto): é o que serializa dois pedidos, o pedido e o registro do RH, o aceite da sugestão e o pedido, e o saldo nunca estoura. A recusa de trigger do banco sobe já traduzida
/// (<see cref="TradutorDeRecusaDaGestao"/>); as entidades de férias não são auditadas pelo interceptor, os eventos são escritos à mão pelos casos de uso.
/// </summary>
public sealed class FeriasRepositorio(AppDbContext db) : IFeriasRepositorio
{
    public async Task<PessoaParaFerias?> TravarPessoaAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct);
        return pessoa is null ? null : new PessoaParaFerias(pessoa.Id, pessoa.Nome, pessoa.Ativo, pessoa.DataAdmissao, pessoa.DataDesligamento);
    }

    public async Task<bool> AprovarComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        ResultadoDoGestorNasFerias? aprovou = ResultadoDoGestorNasFerias.Aprovou;

        // AUDITORIA-OK: a decisão do gestor é auditada à mão pelo caso de uso (evento Aprovou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Não toca na OBSERVACAO (a mensagem da pessoa).
        var linhas = await NaEtapaDoGestor(feriasId, gestorId).ExecuteUpdateAsync(s => s.SetProperty(p => p.ResultadoDoGestor, aprovou).SetProperty(p => p.EtapaDoGestorEmUtc, agora), ct);
        return linhas == 1;
    }

    public async Task<bool> RecusarComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, string motivo, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        ResultadoDoGestorNasFerias? recusou = ResultadoDoGestorNasFerias.Recusou;
        string? motivoDaRecusa = motivo;

        // AUDITORIA-OK: a recusa do gestor é auditada à mão pelo caso de uso (evento Recusou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Um UPDATE só grava juntas as colunas que os CHECK exigem (status, resultado, datas e motivo).
        var linhas = await NaEtapaDoGestor(feriasId, gestorId).ExecuteUpdateAsync(
            s => s.SetProperty(p => p.Status, StatusDasFerias.Rejeitada)
                .SetProperty(p => p.ResultadoDoGestor, recusou)
                .SetProperty(p => p.EtapaDoGestorEmUtc, agora)
                .SetProperty(p => p.AnalisadaEmUtc, agora)
                .SetProperty(p => p.MotivoRecusa, motivoDaRecusa),
            ct);
        return linhas == 1;
    }

    public async Task<bool> SugerirComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, DateOnly inicio, DateOnly fim, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        ResultadoDoGestorNasFerias? sugeriu = ResultadoDoGestorNasFerias.SugeriuData;
        DateOnly? sugestaoInicio = inicio;
        DateOnly? sugestaoFim = fim;

        // AUDITORIA-OK: a sugestão é auditada à mão pelo caso de uso (evento Alterou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Resultado, data da etapa e as duas datas sugeridas vão juntos (CHECK).
        var linhas = await NaEtapaDoGestor(feriasId, gestorId).ExecuteUpdateAsync(
            s => s.SetProperty(p => p.ResultadoDoGestor, sugeriu).SetProperty(p => p.EtapaDoGestorEmUtc, agora).SetProperty(p => p.SugestaoDataInicio, sugestaoInicio).SetProperty(p => p.SugestaoDataFim, sugestaoFim),
            ct);
        return linhas == 1;
    }

    /// <summary>O pedido ainda <c>SOLICITADA</c>, com o <c>ID_GESTOR</c> do gestor e sem resultado dele: o estado de partida das três decisões.</summary>
    private IQueryable<PedidoDeFerias> NaEtapaDoGestor(int feriasId, int gestorId) =>
        db.PedidosDeFerias.Where(p => p.Id == feriasId && p.Status == StatusDasFerias.Solicitada && p.GestorId == gestorId && p.ResultadoDoGestor == null);

    public async Task<int> CriarAsync(PedidoDeFerias pedido, CancellationToken ct)
    {
        db.PedidosDeFerias.Add(pedido);
        try
        {
            await db.SaveChangesAsync(ct);
            return pedido.Id;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }
}

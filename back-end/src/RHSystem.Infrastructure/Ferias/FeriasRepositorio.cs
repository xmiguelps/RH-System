using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ferias.Regras;
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

    public async Task<bool> ConfirmarComoRhAsync(int feriasId, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        int? rh = funcionarioRhId;

        try
        {
            // AUDITORIA-OK: a confirmação do RH é auditada à mão pelo caso de uso (evento Aprovou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Status final, quem do RH, quando e a data da análise vão no mesmo UPDATE (CHECK e trigger).
            var linhas = await NaEtapaDoRh(feriasId).ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, StatusDasFerias.Aprovada)
                    .SetProperty(p => p.FuncionarioRhId, rh)
                    .SetProperty(p => p.ConfirmadaPeloRhEmUtc, agora)
                    .SetProperty(p => p.AnalisadaEmUtc, agora)
                    .SetProperty(p => p.GestorId, p => p.GestorId ?? gestorIdDoRh),
                ct);
            return linhas == 1;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            throw problema;
        }
        catch (MySqlConnector.MySqlException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            throw problema; // o ExecuteUpdate não embrulha o erro do MySQL em DbUpdateException
        }
    }

    public async Task<bool> RecusarComoRhAsync(int feriasId, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, string motivo, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        int? rh = funcionarioRhId;
        string? motivoDaRecusa = motivo;

        try
        {
            // AUDITORIA-OK: a recusa do RH é auditada à mão pelo caso de uso (evento Recusou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Status, quem do RH, quando, a data da análise e o motivo vão no mesmo UPDATE (CHECK e trigger).
            var linhas = await NaEtapaDoRh(feriasId).ExecuteUpdateAsync(
                s => s.SetProperty(p => p.Status, StatusDasFerias.Rejeitada)
                    .SetProperty(p => p.FuncionarioRhId, rh)
                    .SetProperty(p => p.ConfirmadaPeloRhEmUtc, agora)
                    .SetProperty(p => p.AnalisadaEmUtc, agora)
                    .SetProperty(p => p.GestorId, p => p.GestorId ?? gestorIdDoRh)
                    .SetProperty(p => p.MotivoRecusa, motivoDaRecusa),
                ct);
            return linhas == 1;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            throw problema;
        }
        catch (MySqlConnector.MySqlException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            throw problema;
        }
    }

    /// <summary>O pedido na etapa do RH (<c>COM_O_RH</c>): ainda <c>SOLICITADA</c>, e sem etapa do gestor, ou com o gestor aprovado, ou com a sugestão aceita.</summary>
    private IQueryable<PedidoDeFerias> NaEtapaDoRh(int feriasId) => db.PedidosDeFerias.Where(p => p.Id == feriasId).Where(ConsultaDeFerias.NaSituacao(SituacaoDasFerias.ComORh));

    public async Task<bool> AceitarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        RespostaDaSugestao? aceitou = RespostaDaSugestao.Aceitou;

        // AUDITORIA-OK: o aceite é auditado à mão pelo caso de uso (evento Alterou, com as datas de antes e de depois, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. As datas sugeridas ficam gravadas (CHECK).
        var linhas = await EsperandoARespostaDe(feriasId, funcionarioId).ExecuteUpdateAsync(
            s => s.SetProperty(p => p.DataInicio, p => p.SugestaoDataInicio ?? p.DataInicio)
                .SetProperty(p => p.DataFim, p => p.SugestaoDataFim ?? p.DataFim)
                .SetProperty(p => p.RespostaDaSugestao, aceitou)
                .SetProperty(p => p.RespostaDaSugestaoEmUtc, agora),
            ct);
        return linhas == 1;
    }

    public Task<bool> RecusarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct) => CancelarPorRespostaAsync(feriasId, funcionarioId, agoraUtc, RespostaDaSugestao.Recusou, ct);

    public Task<bool> ExpirarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct) => CancelarPorRespostaAsync(feriasId, funcionarioId, agoraUtc, RespostaDaSugestao.Expirou, ct);

    private async Task<bool> CancelarPorRespostaAsync(int feriasId, int funcionarioId, DateTime agoraUtc, RespostaDaSugestao resposta, CancellationToken ct)
    {
        DateTime? agora = InstanteAoSegundo.De(agoraUtc);
        RespostaDaSugestao? daPessoa = resposta;

        // AUDITORIA-OK: recusar ou deixar expirar a sugestão é auditado à mão pelo caso de uso (evento Alterou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF. Status e resposta vão no mesmo UPDATE (CHECK).
        var linhas = await EsperandoARespostaDe(feriasId, funcionarioId).ExecuteUpdateAsync(
            s => s.SetProperty(p => p.Status, StatusDasFerias.Cancelada).SetProperty(p => p.RespostaDaSugestao, daPessoa).SetProperty(p => p.RespostaDaSugestaoEmUtc, agora),
            ct);
        return linhas == 1;
    }

    /// <summary>O pedido do dono esperando a resposta dele à sugestão: <c>SOLICITADA</c>, <c>SUGERIU_DATA</c> e sem resposta ainda.</summary>
    private IQueryable<PedidoDeFerias> EsperandoARespostaDe(int feriasId, int funcionarioId) =>
        db.PedidosDeFerias.Where(p => p.Id == feriasId && p.FuncionarioId == funcionarioId && p.Status == StatusDasFerias.Solicitada && p.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && p.RespostaDaSugestao == null);

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

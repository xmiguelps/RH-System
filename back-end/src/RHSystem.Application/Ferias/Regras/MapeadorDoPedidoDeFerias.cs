using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>A decisão de uma etapa (<c>DecisaoDeEtapa</c> da base): quem decidiu (<c>null</c> na aprovação automática), quando, se foi automática e o motivo da recusa.</summary>
public sealed record DecisaoDeEtapaDeFerias(bool Aprovada, int? DecididaPorFuncionarioId, DateTime DecididaEmUtc, bool Automatica, string? Observacao);

/// <summary>A sugestão de outras datas do gestor (<c>FeriasSugestao</c>): as datas, quem sugeriu e quando, a resposta da pessoa e o último dia para ela responder.</summary>
public sealed record SugestaoDeFerias(DateOnly DataInicio, DateOnly DataFim, int Dias, int SugeridaPorFuncionarioId, DateTime SugeridaEmUtc, RespostaDaSugestao? Resposta, DateTime? RespondidaEmUtc, DateOnly ExpiraEm);

/// <summary>
/// A tradução do que o banco guarda para as três formas do contrato. O banco não tem coluna da decisão do RH (ela é o status final mais <c>ID_FUNCIONARIO_RH</c>) nem da aceitação do gestor (<c>SUGERIU_DATA</c> com resposta <c>ACEITOU</c> vira <c>APROVADA</c>, com a data da resposta
/// da pessoa). O motivo da recusa é UMA coluna: pertence ao gestor quando ele recusou e ao RH quando foi o RH.
/// </summary>
public static class MapeadorDoPedidoDeFerias
{
    /// <summary>Dias corridos depois da sugestão em que a pessoa ainda pode responder (o prazo é o menor entre este e 2 dias antes do início sugerido).</summary>
    public const int DiasParaResponder = 5;

    public static DecisaoDeEtapaDeFerias? DecisaoDoGestor(LinhaDoPedidoDeFerias linha) => linha.ResultadoDoGestor switch
    {
        ResultadoDoGestorNasFerias.Aprovou when linha.EtapaDoGestorEmUtc is { } quando =>
            new DecisaoDeEtapaDeFerias(true, linha.EtapaDoGestorAutomatica ? null : linha.GestorFuncionarioId, quando, linha.EtapaDoGestorAutomatica, null),
        ResultadoDoGestorNasFerias.Recusou when linha.EtapaDoGestorEmUtc is { } quando =>
            new DecisaoDeEtapaDeFerias(false, linha.GestorFuncionarioId, quando, false, linha.MotivoRecusa),
        ResultadoDoGestorNasFerias.SugeriuData when linha.Resposta == RespostaDaSugestao.Aceitou && linha.RespostaEmUtc is { } resposta =>
            new DecisaoDeEtapaDeFerias(true, linha.GestorFuncionarioId, resposta, false, null),
        _ => null,
    };

    public static DecisaoDeEtapaDeFerias? DecisaoDoRh(LinhaDoPedidoDeFerias linha)
    {
        if (linha.FuncionarioRhId is not { } rh || linha.ConfirmadaPeloRhEmUtc is not { } quando)
        {
            return null;
        }

        var rejeitada = linha.Status == StatusDasFerias.Rejeitada;
        return new DecisaoDeEtapaDeFerias(!rejeitada, rh, quando, false, rejeitada ? linha.MotivoRecusa : null);
    }

    public static SugestaoDeFerias? Sugestao(LinhaDoPedidoDeFerias linha)
    {
        if (linha.SugestaoInicio is not { } inicio || linha.SugestaoFim is not { } fim || linha.EtapaDoGestorEmUtc is not { } sugeridaEm || linha.GestorFuncionarioId is not { } gestor)
        {
            return null;
        }

        return new SugestaoDeFerias(inicio, fim, fim.DayNumber - inicio.DayNumber + 1, gestor, sugeridaEm, linha.Resposta, linha.RespostaEmUtc, ExpiraEm(sugeridaEm, inicio));
    }

    /// <summary>
    /// O gestor da etapa só aparece como <c>gestorResponsavel</c> se o pedido TEM etapa do gestor: já tem resultado dele, ou tem o <c>ID_GESTOR</c> e o RH ainda não decidiu. No pedido sem etapa do gestor, o RH grava em <c>ID_GESTOR</c> a linha de quem decidiu (o trigger exige) e isso NÃO é o gestor responsável.
    /// </summary>
    public static bool TemEtapaDoGestor(LinhaDoPedidoDeFerias linha) => linha.ResultadoDoGestor is not null || (linha.GestorId is not null && linha.FuncionarioRhId is null);

    private const string SufixoDaInativacao = "Cancelada automaticamente: funcionario inativado";

    /// <summary>A mensagem da pessoa sem o texto que o trigger de inativação acrescenta à <c>OBSERVACAO</c> (" | Cancelada automaticamente: funcionario inativado", sem acento): tira o sufixo e, se só havia ele, a observação some.</summary>
    public static string? ObservacaoDaPessoa(string? gravada)
    {
        if (string.IsNullOrEmpty(gravada))
        {
            return gravada;
        }

        var texto = gravada;
        while (true)
        {
            if (texto.EndsWith(" | " + SufixoDaInativacao, StringComparison.Ordinal))
            {
                texto = texto[..^(3 + SufixoDaInativacao.Length)];
            }
            else if (texto == SufixoDaInativacao)
            {
                return null;
            }
            else
            {
                return texto;
            }
        }
    }

    /// <summary>O último dia para a pessoa responder: o menor entre 5 dias corridos depois do dia (de Brasília) da sugestão e 2 dias antes do início sugerido.</summary>
    public static DateOnly ExpiraEm(DateTime sugeridaEmUtc, DateOnly inicioSugerido)
    {
        var porPrazo = FusoBrasil.DiaLocalDe(sugeridaEmUtc).AddDays(DiasParaResponder);
        var porInicio = inicioSugerido.AddDays(-2);
        return porPrazo < porInicio ? porPrazo : porInicio;
    }
}

namespace RHSystem.Application.Ferias.Regras;

/// <summary>O período escolhido (<c>null</c> quando nenhum comporta o pedido) e o período a que se referem os números da conferência quando ele não cabe.</summary>
public sealed record EscolhaDePeriodo(PeriodoComSaldo? Escolhido, PeriodoComSaldo? Referencia, bool SemPeriodoFechado);

/// <summary>
/// Em qual período aquisitivo o pedido entra: o <b>mais antigo</b> em que ele cabe INTEIRO (o pedido não se divide entre períodos), entre os fechados que <b>terminam antes do início</b> (o banco recusa início antes do fim do período). Quando nenhum comporta, os números ("faltam N dias") se
/// referem ao período do saldo (o mais antigo candidato com saldo; sem nenhum com saldo, o mais antigo candidato; sem candidato, o mais antigo fechado). Pedido já existente (aceite ou sugestão) tem o período fixo, que é o dele.
/// </summary>
public static class EscolhaDoPeriodo
{
    public static EscolhaDePeriodo Escolher(SaldoDaPessoa saldo, DateOnly inicio, int diasNoSaldo, DateOnly? inicioDoPeriodoFixo)
    {
        var fechados = saldo.Periodos.Where(p => p.Situacao != SituacaoDoPeriodo.EmAquisicao).ToList();
        if (inicioDoPeriodoFixo is { } fixo)
        {
            var doPedido = fechados.FirstOrDefault(p => p.Inicio == fixo);
            return new EscolhaDePeriodo(doPedido, doPedido, doPedido is null);
        }

        if (fechados.Count == 0)
        {
            return new EscolhaDePeriodo(null, null, true);
        }

        var candidatos = fechados.Where(p => p.Fim < inicio).ToList();
        var escolhido = candidatos.FirstOrDefault(p => Livre(p) >= diasNoSaldo);
        var referencia = escolhido ?? candidatos.FirstOrDefault(p => p.DiasDisponiveis > 0) ?? candidatos.FirstOrDefault() ?? fechados[0];
        return new EscolhaDePeriodo(escolhido, referencia, false);
    }

    /// <summary>O que ainda se pode pedir do período: disponível menos o que está reservado em pedido.</summary>
    public static int Livre(PeriodoComSaldo periodo) => periodo.DiasDisponiveis - periodo.DiasEmPedido;
}

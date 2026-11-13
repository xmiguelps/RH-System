using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

/// <summary>
/// Do pedido como o banco o guarda para o <see cref="FeriasPedido"/> do contrato. A situação, a etapa e as decisões saem das colunas (<see cref="SituacaoDoPedidoDeFerias"/> e <see cref="MapeadorDoPedidoDeFerias"/>); <c>podeDecidir</c> e o motivo valem para <b>quem consulta</b>. O gestor responsável só aparece se
/// o pedido tem etapa do gestor; a observação sai sem o sufixo do trigger de inativação.
/// </summary>
public static class MontadorDoPedidoDeFerias
{
    public static FeriasPedido Montar(FeriasLida p, QuemConsultaOPedido quem)
    {
        var linha = p.ComoLinha();
        var situacao = SituacaoDoPedidoDeFerias.Situacao(linha);
        var etapa = SituacaoDoPedidoDeFerias.Etapa(linha);
        var (pode, motivo) = RegrasDeDecisaoDeFerias.PodeDecidir(situacao, new PedidoParaDecisao(etapa, p.Funcionario.Id, p.GestorId), quem, p.Funcionario.Nome);
        var decisaoDoGestor = MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha);
        var decisaoDoRh = MapeadorDoPedidoDeFerias.DecisaoDoRh(linha);
        var sugestao = MapeadorDoPedidoDeFerias.Sugestao(linha);

        return new FeriasPedido(
            p.Id,
            p.Funcionario,
            new FeriasPeriodoDatas(p.PeriodoInicio, p.PeriodoFim),
            p.DataInicio,
            p.DataFim,
            p.DataFim.DayNumber - p.DataInicio.DayNumber + 1,
            p.AbonoPecuniario,
            p.DiasAbono,
            p.Adiantar13Salario,
            p.Status,
            etapa,
            pode,
            motivo,
            situacao,
            p.SolicitadaEmUtc ?? default,
            MapeadorDoPedidoDeFerias.ObservacaoDaPessoa(p.Observacao),
            MapeadorDoPedidoDeFerias.TemEtapaDoGestor(linha) ? p.Gestor : null,
            decisaoDoGestor is null ? null : DaDecisao(decisaoDoGestor, p.Gestor),
            decisaoDoRh is null ? null : DaDecisao(decisaoDoRh, p.FuncionarioRh),
            sugestao is null || p.Gestor is null ? null : new FeriasSugestao(sugestao.DataInicio, sugestao.DataFim, sugestao.Dias, p.Gestor, sugestao.SugeridaEmUtc, sugestao.Resposta, sugestao.RespondidaEmUtc, sugestao.ExpiraEm));
    }

    private static DecisaoDeEtapa DaDecisao(DecisaoDeEtapaDeFerias d, FuncionarioResumo? quem) =>
        new(d.Aprovada ? DecisaoDaEtapa.Aprovada : DecisaoDaEtapa.Rejeitada, d.DecididaEmUtc, d.Automatica)
        {
            DecididaPor = d.DecididaPorFuncionarioId is null ? null : quem,
            Observacao = d.Observacao,
        };
}

/// <summary>O saldo calculado (<see cref="SaldoDaPessoa"/>) nos tipos do contrato: os mesmos números em todas as telas.</summary>
public static class MontadorDoSaldoDeFerias
{
    public static FeriasResumo Resumo(SaldoDaPessoa saldo) => new(
        saldo.DiasDisponiveis,
        saldo.PeriodoDoSaldo is { } doSaldo ? new FeriasPeriodoDatas(doSaldo.Inicio, doSaldo.Fim) : null,
        saldo.PeriodoDoSaldo?.PrazoParaTirar,
        saldo.PeriodoAtual is { } atual ? new FeriasPeriodoDatas(atual.Inicio, atual.Fim) : null,
        saldo.ProximasFerias is { } proximas ? new FeriasProximas(proximas.DataInicio, proximas.DataFim, proximas.Dias) : null,
        saldo.PedidosEmAnalise);

    public static IReadOnlyList<FeriasPeriodoAquisitivo> Periodos(SaldoDaPessoa saldo) =>
        [.. saldo.Periodos.Select(p => new FeriasPeriodoAquisitivo(p.Inicio, p.Fim, p.Situacao, p.DiasDeDireito, p.DiasTirados, p.DiasEmPedido, p.DiasDisponiveis, p.PrazoParaTirar, p.MesesTrabalhados, p.DisponivelAPartirDe))];

    public static FeriasConferencia Conferencia(ConferenciaDeFerias c) =>
        new(c.TotalDias, c.DiasQueSobram, c.PodeEnviar, c.PagamentoAte, [.. c.Regras.Select(r => new FeriasRegraConferida(r.Regra, r.Atendida, r.Mensagem))]);
}

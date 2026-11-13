using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>As três situações do período aquisitivo (<c>FeriasPeriodoAquisitivo.situacao</c>).</summary>
public enum SituacaoDoPeriodo
{
    EmAquisicao,
    ComSaldo,
    SemSaldo,
}

/// <summary>O que o cálculo do saldo precisa saber de uma linha de <c>FERIAS</c> (o período a que pertence, as datas, o abono e os campos que dizem onde o pedido está).</summary>
public sealed record FeriasParaSaldo(DateOnly InicioDoPeriodo, DateOnly FimDoPeriodo, DateOnly DataInicio, DateOnly DataFim, int DiasAbono, StatusDasFerias Status, ResultadoDoGestorNasFerias? Resultado, RespostaDaSugestao? Resposta)
{
    /// <summary>Dias de gozo (o primeiro e o último contam).</summary>
    public int Dias => DataFim.DayNumber - DataInicio.DayNumber + 1;

    /// <summary>Gozo mais abono: o que o pedido consome do saldo.</summary>
    public int DiasNoSaldo => Dias + DiasAbono;

    /// <summary>Esperando a resposta da pessoa à sugestão do gestor (<c>SUGESTAO_DE_DATA</c>): não reserva dias.</summary>
    public bool EmSugestaoDeData => Status == StatusDasFerias.Solicitada && Resultado == ResultadoDoGestorNasFerias.SugeriuData && Resposta is null;

    /// <summary>Dias tirados: <c>APROVADA</c>, <c>EM_GOZO</c> e <c>CONCLUIDA</c>.</summary>
    public bool Tirada => Status is StatusDasFerias.Aprovada or StatusDasFerias.EmGozo or StatusDasFerias.Concluida;

    /// <summary>Dias reservados: espera o gestor ou o RH (<c>SOLICITADA</c>, menos a que espera a resposta da pessoa).</summary>
    public bool EmPedido => Status == StatusDasFerias.Solicitada && !EmSugestaoDeData;
}

public sealed record PeriodoComSaldo(DateOnly Inicio, DateOnly Fim, SituacaoDoPeriodo Situacao, int DiasDeDireito, int DiasTirados, int DiasEmPedido, int DiasDisponiveis, DateOnly PrazoParaTirar, int? MesesTrabalhados, DateOnly? DisponivelAPartirDe);

public sealed record ProximasFerias(DateOnly DataInicio, DateOnly DataFim, int Dias);

/// <summary>O resultado do cálculo: os períodos e o resumo do topo da tela (o mesmo número em todas as telas e no indicador da ficha).</summary>
public sealed record SaldoDaPessoa(IReadOnlyList<PeriodoComSaldo> Periodos, int DiasDisponiveis, PeriodoComSaldo? PeriodoDoSaldo, PeriodoComSaldo? PeriodoAtual, ProximasFerias? ProximasFerias, int PedidosEmAnalise);

/// <summary>
/// O saldo de férias, UM cálculo para as seis telas (a pessoa, a ficha do gestor, a do RH, o painel do funcionário, a coluna do painel do gestor e o indicador da ficha). <b>Disponível</b> = 30 menos o que foi tirado (gozo mais abono de <c>APROVADA</c>, <c>EM_GOZO</c> e <c>CONCLUIDA</c>);
/// o que espera resposta fica <b>reservado</b> dentro do disponível (<c>DiasEmPedido</c>) e a regra <c>CABE_NO_SALDO</c> confere disponível menos reservado. Cada linha de férias pertence ao período de datas EXATAMENTE iguais (readmissão: as férias do vínculo antigo não casam com nenhum período novo e não
/// entram no saldo). O saldo não vence e nunca fica negativo.
/// </summary>
public static class SaldoDeFerias
{
    public const int DiasDeDireito = 30;

    public static SaldoDaPessoa De(DateOnly admissao, DateOnly? desligamento, DateOnly hoje, IReadOnlyList<FeriasParaSaldo> ferias)
    {
        var periodos = PeriodosAquisitivos.Calcular(admissao, hoje, desligamento).Select(p => Calcular(p, hoje, ferias)).ToList();
        var fechados = periodos.Where(p => p.Situacao != SituacaoDoPeriodo.EmAquisicao).ToList();
        var proxima = ferias.Where(f => f.Status == StatusDasFerias.Aprovada && f.DataInicio > hoje).OrderBy(f => f.DataInicio).FirstOrDefault();
        return new SaldoDaPessoa(
            periodos,
            fechados.Sum(p => p.DiasDisponiveis),
            fechados.FirstOrDefault(p => p.DiasDisponiveis > 0),
            periodos.FirstOrDefault(p => p.Situacao == SituacaoDoPeriodo.EmAquisicao),
            proxima is null ? null : new ProximasFerias(proxima.DataInicio, proxima.DataFim, proxima.Dias),
            ferias.Count(f => f.Status == StatusDasFerias.Solicitada));
    }

    /// <summary>O mesmo cálculo para várias pessoas de uma vez (a consulta já trouxe as férias de todas): nunca uma consulta por pessoa.</summary>
    public static IReadOnlyDictionary<int, SaldoDaPessoa> DeVarias(DateOnly hoje, IEnumerable<(int FuncionarioId, DateOnly Admissao, DateOnly? Desligamento, IReadOnlyList<FeriasParaSaldo> Ferias)> pessoas) =>
        pessoas.ToDictionary(p => p.FuncionarioId, p => De(p.Admissao, p.Desligamento, hoje, p.Ferias));

    private static PeriodoComSaldo Calcular(PeriodoAquisitivo periodo, DateOnly hoje, IReadOnlyList<FeriasParaSaldo> ferias)
    {
        var doPeriodo = ferias.Where(f => f.InicioDoPeriodo == periodo.Inicio && f.FimDoPeriodo == periodo.Fim).ToList();
        var tirados = doPeriodo.Where(f => f.Tirada).Sum(f => f.DiasNoSaldo);
        var emPedido = doPeriodo.Where(f => f.EmPedido).Sum(f => f.DiasNoSaldo);
        var fechado = PeriodosAquisitivos.Fechado(periodo, hoje);
        var disponiveis = fechado ? Math.Max(0, DiasDeDireito - tirados) : 0;
        var situacao = !fechado ? SituacaoDoPeriodo.EmAquisicao : disponiveis > 0 ? SituacaoDoPeriodo.ComSaldo : SituacaoDoPeriodo.SemSaldo;
        return new PeriodoComSaldo(
            periodo.Inicio,
            periodo.Fim,
            situacao,
            DiasDeDireito,
            tirados,
            emPedido,
            disponiveis,
            periodo.PrazoParaTirar,
            fechado ? null : PeriodosAquisitivos.MesesTrabalhados(periodo, hoje),
            fechado ? null : periodo.Fim.AddDays(1));
    }
}

using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Uma batida do dia, no que as regras precisam (o <see cref="Id"/> desempata duas batidas no mesmo instante).</summary>
public sealed record MarcacaoDoDia(int Id, DateTime InstanteUtc, TipoDeMarcacao Tipo);

/// <summary>Um período do dia: <c>ENTRADA</c> abre e a <c>SAIDA</c> seguinte fecha. Os dois lados podem faltar (entrada sem saída; saída solta).</summary>
public sealed record PeriodoDoDia(MarcacaoDoDia? Entrada, MarcacaoDoDia? Saida)
{
    public bool Fechado => Entrada is not null && Saida is not null;
}

/// <summary>A próxima batida da pessoa: o tipo, em que dia ela cai (<see cref="NoDiaAnterior"/>: a SAÍDA de uma jornada que atravessou a meia-noite), o rótulo e o texto do botão.</summary>
public sealed record ProximaMarcacao(TipoDeMarcacao Tipo, bool NoDiaAnterior, string Rotulo, string TextoDoBotao);

/// <summary>
/// A sequência de batidas de um dia, sem banco. A regra de "consistente" é a de <c>FN_VERIFICAR_CONSISTENCIA_PONTO</c> (que serve de oráculo em teste): ordenar por horário e depois por id, alternar a partir de <c>ENTRADA</c> e terminar em
/// <c>SAIDA</c>. A jornada que atravessa a meia-noite pertence ao dia da ENTRADA: a próxima batida de quem tem a ENTRADA de ontem aberta há menos de 12 horas é a SAÍDA dela (decisão 4 do plano).
/// </summary>
public static class SequenciaDoDia
{
    /// <summary>A janela da jornada que atravessa a meia-noite (contrato, <c>x-provisorio</c>): com 12 horas ou mais a ENTRADA aberta já não puxa a batida de hoje.</summary>
    public static readonly TimeSpan JanelaDaJornada = TimeSpan.FromHours(12);

    public static IReadOnlyList<PeriodoDoDia> Periodos(IEnumerable<MarcacaoDoDia> marcacoes)
    {
        var periodos = new List<PeriodoDoDia>();
        MarcacaoDoDia? aberta = null;
        foreach (var batida in Ordenar(marcacoes))
        {
            if (batida.Tipo == TipoDeMarcacao.Entrada)
            {
                if (aberta is not null)
                {
                    periodos.Add(new PeriodoDoDia(aberta, null)); // entrada seguida de entrada: a primeira fica sem saída
                }

                aberta = batida;
            }
            else
            {
                periodos.Add(new PeriodoDoDia(aberta, batida)); // a saída fecha a entrada aberta, ou fica solta
                aberta = null;
            }
        }

        if (aberta is not null)
        {
            periodos.Add(new PeriodoDoDia(aberta, null));
        }

        return periodos;
    }

    public static bool EhConsistente(IEnumerable<MarcacaoDoDia> marcacoes)
    {
        var esperado = TipoDeMarcacao.Entrada;
        var total = 0;
        foreach (var batida in Ordenar(marcacoes))
        {
            if (batida.Tipo != esperado)
            {
                return false;
            }

            esperado = esperado == TipoDeMarcacao.Entrada ? TipoDeMarcacao.Saida : TipoDeMarcacao.Entrada;
            total++;
        }

        return total > 0 && esperado == TipoDeMarcacao.Entrada;
    }

    /// <summary>O rótulo da batida na posição <paramref name="ordem"/> (de 1): o da posição quando o tipo combina com ela, o do tipo quando não.</summary>
    public static string Rotulo(int ordem, TipoDeMarcacao tipo) => (ordem, tipo) switch
    {
        (1, TipoDeMarcacao.Entrada) => "Entrada",
        (2, TipoDeMarcacao.Saida) => "Saída almoço",
        (3, TipoDeMarcacao.Entrada) => "Volta",
        (4, TipoDeMarcacao.Saida) => "Saída",
        (_, TipoDeMarcacao.Entrada) => "Entrada",
        _ => "Saída",
    };

    /// <summary>
    /// O que a pessoa deve bater agora. Sem batida hoje e com a ENTRADA de ontem aberta há menos de 12 horas, é a SAÍDA de ontem; senão alterna o tipo da última batida de hoje (a primeira é <c>ENTRADA</c>).
    /// </summary>
    public static ProximaMarcacao ProximaMarcacao(IReadOnlyList<MarcacaoDoDia> diaAtual, IReadOnlyList<MarcacaoDoDia> diaAnterior, DateTime agoraUtc)
    {
        if (diaAtual.Count == 0 && EntradaAbertaQueAindaVale(diaAnterior, agoraUtc))
        {
            return Montar(TipoDeMarcacao.Saida, diaAnterior.Count + 1, noDiaAnterior: true);
        }

        var ultima = Ordenar(diaAtual).LastOrDefault();
        var proximoTipo = ultima is null || ultima.Tipo == TipoDeMarcacao.Saida ? TipoDeMarcacao.Entrada : TipoDeMarcacao.Saida;
        return Montar(proximoTipo, diaAtual.Count + 1, noDiaAnterior: false);
    }

    private static bool EntradaAbertaQueAindaVale(IReadOnlyList<MarcacaoDoDia> diaAnterior, DateTime agoraUtc)
    {
        var ultima = Ordenar(diaAnterior).LastOrDefault();
        return ultima is { Tipo: TipoDeMarcacao.Entrada } && agoraUtc - ultima.InstanteUtc < JanelaDaJornada;
    }

    private static ProximaMarcacao Montar(TipoDeMarcacao tipo, int ordem, bool noDiaAnterior) =>
        new(tipo, noDiaAnterior, Rotulo(ordem, tipo), (ordem, tipo) switch
        {
            (2, TipoDeMarcacao.Saida) => "Registrar saída para almoço",
            (3, TipoDeMarcacao.Entrada) => "Registrar volta",
            (_, TipoDeMarcacao.Entrada) => "Registrar entrada",
            _ => "Registrar saída",
        });

    private static IEnumerable<MarcacaoDoDia> Ordenar(IEnumerable<MarcacaoDoDia> marcacoes) => marcacoes.OrderBy(m => m.InstanteUtc).ThenBy(m => m.Id);
}

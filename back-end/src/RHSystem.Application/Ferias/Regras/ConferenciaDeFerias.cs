using System.Text.Json.Serialization;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>
/// Os códigos das regras (<c>FeriasRegraCodigo</c>), na ordem em que aparecem na conferência. O JSON escreve o nome em MAIÚSCULAS_COM_SUBLINHADO, mas a política do .NET não separa o número ("PARTE_DE14_DIAS"): os quatro que têm número no nome levam o texto do contrato.
/// </summary>
public enum RegraDeFerias
{
    DuracaoDaParte,
    CabeNoSaldo,
    [JsonStringEnumMemberName("PARTE_DE_14_DIAS")]
    ParteDe14Dias,
    [JsonStringEnumMemberName("MAXIMO_DE_3_PARTES")]
    MaximoDe3Partes,
    [JsonStringEnumMemberName("ABONO_ATE_10_DIAS")]
    AbonoAte10Dias,
    [JsonStringEnumMemberName("ANTECEDENCIA_DE_30_DIAS")]
    AntecedenciaDe30Dias,
    NaoComecaPertoDeFeriadoOuDescanso,
    SemSobreposicao,
}

/// <summary>
/// Quem pede e para quê muda a lista: <c>Pessoa</c> (todas as regras), <c>Rh</c> (registro direto, no passado se quiser), <c>AceiteDaSugestao</c> (a data foi escolhida pelo gestor) e <c>SugestaoDoGestor</c>: os três últimos não têm a antecedência.
/// </summary>
public enum ModoDaConferencia
{
    Pessoa,
    Rh,
    AceiteDaSugestao,
    SugestaoDoGestor,
}

public sealed record ComandoDeFerias(DateOnly DataInicio, DateOnly DataFim, bool AbonoPecuniario, int DiasAbono);

public sealed record ResultadoDaRegra(RegraDeFerias Regra, bool Atendida, string Mensagem);

/// <summary>
/// Tudo o que a conferência precisa: "hoje" (dia de Brasília), o modo, o saldo da pessoa, as férias dela (SEM o pedido que está sendo conferido, quando ele já existe: ele não pode colidir nem reservar contra si mesmo), o calendário de feriados e, para pedido que já existe, o início do período dele.
/// </summary>
public sealed record ContextoDaConferencia(DateOnly Hoje, ModoDaConferencia Modo, SaldoDaPessoa Saldo, IReadOnlyList<FeriasParaSaldo> Ferias, Func<DateOnly, bool> EhFeriado, DateOnly? InicioDoPeriodoFixo = null);

/// <summary>
/// A conferência das regras de férias (a mesma para pedir, conferir, aceitar a sugestão, sugerir e registrar). <see cref="Periodo"/> é o período em que o pedido entra (ou o de referência dos números quando não cabe); <see cref="SemPeriodoAquisitivo"/> quando a pessoa não tem período fechado.
/// <b>Parte de 14 dias</b>: com <c>P</c> partes já contadas no período, <c>M</c> a maior delas, <c>g</c> os dias deste pedido e <c>R</c> o que sobra depois dele, vale quando <c>max(M, g) &gt;= 14</c> ou (esta não é a última parte possível, <c>P + 1 &lt; 3</c>, e <c>R &gt;= 14</c>).
/// </summary>
public sealed record ConferenciaDeFerias(int TotalDias, int DiasQueSobram, bool PodeEnviar, DateOnly? PagamentoAte, IReadOnlyList<ResultadoDaRegra> Regras, PeriodoComSaldo? Periodo, bool SemPeriodoAquisitivo)
{
    public static ConferenciaDeFerias Conferir(ComandoDeFerias comando, ContextoDaConferencia contexto)
    {
        var datasValidas = comando.DataFim >= comando.DataInicio;
        var gozo = datasValidas ? comando.DataFim.DayNumber - comando.DataInicio.DayNumber + 1 : 0;
        var consumo = gozo + comando.DiasAbono;
        var escolha = EscolhaDoPeriodo.Escolher(contexto.Saldo, comando.DataInicio, consumo, contexto.InicioDoPeriodoFixo);
        var periodo = escolha.Escolhido ?? escolha.Referencia;
        var sobram = (periodo is null ? 0 : EscolhaDoPeriodo.Livre(periodo)) - consumo;
        var contam = contexto.Ferias.Where(f => f.Tirada || f.EmPedido).ToList();
        var doPeriodo = periodo is null ? [] : contam.Where(f => f.InicioDoPeriodo == periodo.Inicio && f.FimDoPeriodo == periodo.Fim).ToList();
        var textosDaPessoa = contexto.Modo is ModoDaConferencia.Pessoa or ModoDaConferencia.AceiteDaSugestao;

        var regras = new List<ResultadoDaRegra>
        {
            new(RegraDeFerias.DuracaoDaParte, gozo is >= 5 and <= 30, gozo is >= 5 and <= 30 ? MensagensDasRegrasDeFerias.DuracaoOk : MensagensDasRegrasDeFerias.DuracaoFalha),
            Saldo(periodo, sobram),
            ParteDe14(doPeriodo, gozo, sobram),
            MaximoDe3(doPeriodo.Count),
        };
        if (comando.AbonoPecuniario || comando.DiasAbono > 0)
        {
            var abonoValido = comando.AbonoPecuniario && comando.DiasAbono is >= 1 and <= 10;
            regras.Add(new ResultadoDaRegra(RegraDeFerias.AbonoAte10Dias, abonoValido, abonoValido ? MensagensDasRegrasDeFerias.AbonoOk(comando.DiasAbono) : MensagensDasRegrasDeFerias.AbonoFalha));
        }

        if (contexto.Modo == ModoDaConferencia.Pessoa)
        {
            var antecedencia = comando.DataInicio >= contexto.Hoje.AddDays(30);
            regras.Add(new ResultadoDaRegra(RegraDeFerias.AntecedenciaDe30Dias, antecedencia, antecedencia ? MensagensDasRegrasDeFerias.AntecedenciaOk : MensagensDasRegrasDeFerias.AntecedenciaFalha));
        }

        var longe = !PertoDeFeriadoOuDescanso(comando.DataInicio, contexto.EhFeriado);
        regras.Add(new ResultadoDaRegra(RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso, longe, longe ? MensagensDasRegrasDeFerias.DescansoOk(textosDaPessoa) : MensagensDasRegrasDeFerias.DescansoFalha(textosDaPessoa)));
        var livre = !contam.Any(f => f.DataInicio <= comando.DataFim && f.DataFim >= comando.DataInicio);
        regras.Add(new ResultadoDaRegra(RegraDeFerias.SemSobreposicao, livre, livre ? MensagensDasRegrasDeFerias.SobreposicaoOk(textosDaPessoa) : MensagensDasRegrasDeFerias.SobreposicaoFalha(textosDaPessoa)));

        return new ConferenciaDeFerias(gozo, sobram, regras.All(r => r.Atendida), datasValidas ? comando.DataInicio.AddDays(-2) : null, regras, periodo, escolha.SemPeriodoFechado);
    }

    private static ResultadoDaRegra Saldo(PeriodoComSaldo? periodo, int sobram)
    {
        if (periodo is null)
        {
            return new ResultadoDaRegra(RegraDeFerias.CabeNoSaldo, false, MensagensDasRegrasDeFerias.SemUmAnoDeCasa);
        }

        return sobram >= 0
            ? new ResultadoDaRegra(RegraDeFerias.CabeNoSaldo, true, MensagensDasRegrasDeFerias.CabeOk)
            : new ResultadoDaRegra(RegraDeFerias.CabeNoSaldo, false, MensagensDasRegrasDeFerias.CabeFalha(-sobram));
    }

    private static ResultadoDaRegra ParteDe14(List<FeriasParaSaldo> doPeriodo, int gozo, int sobram)
    {
        var maior = doPeriodo.Count == 0 ? 0 : doPeriodo.Max(f => f.Dias);
        if (gozo >= 14)
        {
            return new ResultadoDaRegra(RegraDeFerias.ParteDe14Dias, true, MensagensDasRegrasDeFerias.Parte14Propria);
        }

        if (maior >= 14)
        {
            return new ResultadoDaRegra(RegraDeFerias.ParteDe14Dias, true, MensagensDasRegrasDeFerias.Parte14JaTem);
        }

        return doPeriodo.Count + 1 < 3 && sobram >= 14
            ? new ResultadoDaRegra(RegraDeFerias.ParteDe14Dias, true, MensagensDasRegrasDeFerias.Parte14AindaDa)
            : new ResultadoDaRegra(RegraDeFerias.ParteDe14Dias, false, MensagensDasRegrasDeFerias.Parte14Falha);
    }

    private static ResultadoDaRegra MaximoDe3(int existentes) => existentes + 1 <= 3
        ? new ResultadoDaRegra(RegraDeFerias.MaximoDe3Partes, true, MensagensDasRegrasDeFerias.MaximoDe3Ok(existentes + 1))
        : new ResultadoDaRegra(RegraDeFerias.MaximoDe3Partes, false, MensagensDasRegrasDeFerias.MaximoDe3Falha);

    /// <summary>Proibido começar em feriado, sábado ou domingo e nos 2 dias corridos antes de feriado ou de sábado.</summary>
    private static bool PertoDeFeriadoOuDescanso(DateOnly inicio, Func<DateOnly, bool> ehFeriado)
    {
        if (ehFeriado(inicio) || inicio.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            return true;
        }

        return Antes(inicio.AddDays(1), ehFeriado) || Antes(inicio.AddDays(2), ehFeriado);
    }

    private static bool Antes(DateOnly dia, Func<DateOnly, bool> ehFeriado) => ehFeriado(dia) || dia.DayOfWeek == DayOfWeek.Saturday;
}

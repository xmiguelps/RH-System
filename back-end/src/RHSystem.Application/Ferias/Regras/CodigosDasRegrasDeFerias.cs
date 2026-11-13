namespace RHSystem.Application.Ferias.Regras;

/// <summary>Os códigos das regras como o contrato os escreve (<c>FeriasRegraCodigo</c>), usados na conferência, no 422 e nas listas.</summary>
public static class CodigosDasRegrasDeFerias
{
    public static string Texto(RegraDeFerias regra) => regra switch
    {
        RegraDeFerias.DuracaoDaParte => "DURACAO_DA_PARTE",
        RegraDeFerias.CabeNoSaldo => "CABE_NO_SALDO",
        RegraDeFerias.ParteDe14Dias => "PARTE_DE_14_DIAS",
        RegraDeFerias.MaximoDe3Partes => "MAXIMO_DE_3_PARTES",
        RegraDeFerias.AbonoAte10Dias => "ABONO_ATE_10_DIAS",
        RegraDeFerias.AntecedenciaDe30Dias => "ANTECEDENCIA_DE_30_DIAS",
        RegraDeFerias.NaoComecaPertoDeFeriadoOuDescanso => "NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO",
        RegraDeFerias.SemSobreposicao => "SEM_SOBREPOSICAO",
        _ => throw new ArgumentOutOfRangeException(nameof(regra), regra, "Regra de férias sem código no contrato."),
    };
}

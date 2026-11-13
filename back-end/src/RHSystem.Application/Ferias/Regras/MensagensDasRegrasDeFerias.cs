namespace RHSystem.Application.Ferias.Regras;

/// <summary>
/// Os textos de cada regra na conferência (os que o contrato mostra por exemplo ficam exatos; os outros são do executor). Três deles mudam conforme quem olha: o descanso ("fim de semana" para a pessoa, "descanso semanal" para o RH e o gestor) e a sobreposição ("suas" × "da pessoa").
/// </summary>
public static class MensagensDasRegrasDeFerias
{
    public const string DuracaoOk = "A parte tem entre 5 e 30 dias.";

    public const string DuracaoFalha = "A parte precisa ter entre 5 e 30 dias.";

    public const string CabeOk = "Cabe no saldo disponível.";

    public const string SemUmAnoDeCasa = "Você ainda não completou um ano de casa.";

    public const string Parte14Propria = "Tem pelo menos 14 dias seguidos.";

    public const string Parte14JaTem = "O período já tem uma parte de 14 dias.";

    public const string Parte14AindaDa = "Ainda dá para ter uma parte de 14 dias.";

    public const string Parte14Falha = "Depois deste pedido ainda precisa dar para ter uma parte de 14 dias seguidos.";

    public const string MaximoDe3Falha = "O período já tem 3 partes.";

    public const string AbonoFalha = "O abono é de 1 a 10 dias.";

    public const string AntecedenciaOk = "Começa daqui a mais de 30 dias.";

    public const string AntecedenciaFalha = "Precisa começar pelo menos 30 dias depois de hoje.";

    public static string CabeFalha(int faltam) => faltam == 1 ? "Passa do saldo disponível: falta 1 dia." : $"Passa do saldo disponível: faltam {faltam} dias.";

    public static string MaximoDe3Ok(int numeroDaParte) => $"É a {numeroDaParte}ª parte do período.";

    public static string AbonoOk(int dias) => dias == 1 ? "Abono de 1 dia." : $"Abono de {dias} dias.";

    public static string DescansoOk(bool textosDaPessoa) => textosDaPessoa
        ? "Não começa nos 2 dias antes de um feriado ou do fim de semana."
        : "Não começa nos 2 dias antes de um feriado ou do descanso semanal.";

    public static string DescansoFalha(bool textosDaPessoa) => textosDaPessoa
        ? "Não pode começar em feriado, no fim de semana nem nos 2 dias antes deles."
        : "Não pode começar em feriado, no descanso semanal nem nos 2 dias antes deles.";

    public static string SobreposicaoOk(bool textosDaPessoa) => textosDaPessoa ? "Não coincide com outras férias suas." : "Não coincide com outras férias da pessoa.";

    public static string SobreposicaoFalha(bool textosDaPessoa) => textosDaPessoa ? "Coincide com outras férias suas." : "Coincide com outras férias da pessoa.";
}

namespace RHSystem.Domain.Colaboradores;

/// <summary>
/// Os tipos de contrato que o contrato da API conhece. No banco é texto livre (<c>"CLT"</c>, <c>"ESTAGIO"</c>). Os valores começam em 1 para que o campo omitido no JSON (o valor zero) não passe por
/// <c>CLT</c> em silêncio: a validação do cadastro o recusa como <c>tipoContrato</c> (o mesmo que <c>Perfil</c> faz).
/// </summary>
public enum TipoDeContrato
{
    Clt = 1,
    Estagio = 2,
}

public static class TiposDeContrato
{
    public static string ParaTexto(TipoDeContrato tipo) => tipo switch
    {
        TipoDeContrato.Clt => "CLT",
        TipoDeContrato.Estagio => "ESTAGIO",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de contrato desconhecido."),
    };

    /// <summary>Texto fora de <c>CLT</c> e <c>ESTAGIO</c> (o banco aceita texto livre), vazio ou nulo devolve <c>null</c>: a leitura não cai.</summary>
    public static TipoDeContrato? DoTexto(string? texto) => texto switch
    {
        "CLT" => TipoDeContrato.Clt,
        "ESTAGIO" => TipoDeContrato.Estagio,
        _ => null,
    };
}

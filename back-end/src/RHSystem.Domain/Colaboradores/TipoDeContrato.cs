namespace RHSystem.Domain.Colaboradores;

/// <summary>Os tipos de contrato que o contrato da API conhece. No banco é texto livre (<c>"CLT"</c>, <c>"ESTAGIO"</c>).</summary>
public enum TipoDeContrato
{
    Clt,
    Estagio,
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

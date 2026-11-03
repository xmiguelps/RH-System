using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// O gerador de OpenAPI não enxerga o tipo de uma propriedade que tem conversor de JSON próprio (o <see cref="JsonDoContrato"/> tem para
/// <c>DateTime</c> e <c>TimeOnly</c>) e escreve o esquema sem <c>type</c>. Aqui o esquema dessas duas voltam a ser o do contrato: instante é
/// <c>string</c> <c>date-time</c>; horário do dia é <c>string</c> no padrão <c>HH:mm</c>.
/// </summary>
public static class EsquemasDoContrato
{
    private const string PadraoDoHorario = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

    public static OpenApiOptions UsarEsquemasDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddSchemaTransformer((esquema, contexto, _) =>
        {
            Aplicar(esquema, contexto.JsonTypeInfo.Type);
            return Task.CompletedTask;
        });

    /// <summary>Ajusta o esquema de <paramref name="tipo"/> se ele for <c>DateTime</c> ou <c>TimeOnly</c> (anuláveis também); os outros tipos ficam como estão.</summary>
    public static void Aplicar(OpenApiSchema esquema, Type tipo)
    {
        var subjacente = Nullable.GetUnderlyingType(tipo) ?? tipo;
        var texto = subjacente == tipo ? JsonSchemaType.String : JsonSchemaType.String | JsonSchemaType.Null;

        if (subjacente == typeof(DateTime))
        {
            esquema.Type = texto;
            esquema.Format = "date-time";
        }
        else if (subjacente == typeof(TimeOnly))
        {
            esquema.Type = texto;
            esquema.Pattern = PadraoDoHorario;
        }
    }
}

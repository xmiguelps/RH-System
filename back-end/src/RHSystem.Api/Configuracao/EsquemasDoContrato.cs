using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using RHSystem.Domain.Empresa;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// O gerador de OpenAPI não enxerga o tipo de uma propriedade que tem conversor de JSON próprio (o <see cref="JsonDoContrato"/> tem para
/// <c>DateTime</c> e <c>TimeOnly</c>) e escreve o esquema sem <c>type</c>. Aqui o esquema dessas duas voltam a ser o do contrato: instante é
/// <c>string</c> <c>date-time</c>; horário do dia é <c>string</c> no padrão <c>HH:mm</c>.
/// </summary>
public static class EsquemasDoContrato
{
    public const string PadraoDoHorario = "^([01][0-9]|2[0-3]):[0-5][0-9]$";

    public static OpenApiOptions UsarEsquemasDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddSchemaTransformer((esquema, contexto, _) =>
        {
            Aplicar(esquema, contexto.JsonTypeInfo.Type);
            AplicarUfDaEmpresa(esquema, contexto.JsonPropertyInfo);
            AplicarInteiroSemNull(esquema, contexto.JsonPropertyInfo);
            return Task.CompletedTask;
        });

    /// <summary>
    /// Ajusta o esquema de <paramref name="tipo"/> se ele for <c>DateTime</c> ou <c>TimeOnly</c> (anuláveis também) e tira o <c>null</c> de dentro de um enum (o gerador o escreve no componente compartilhado
    /// quando alguma propriedade usa o enum anulável, e aí toda propriedade que o referencia, até a obrigatória, passa a aceitar null no documento; a propriedade anulável já leva o seu próprio
    /// <c>oneOf</c> com null). Os outros tipos ficam como estão.
    /// </summary>
    /// <summary>
    /// A UF da empresa é texto no C# (o caso de uso a confere e responde 400 em <c>errors.uf</c>, o que um enum no corpo não daria), mas o contrato a declara como o enum das 27 siglas: o schema da propriedade
    /// <c>uf</c> de <see cref="EmpresaConfiguracao"/> leva essa lista, a MESMA que o domínio usa para validar.
    /// </summary>
    /// <summary>A propriedade que usa o <see cref="InteiroOpcionalSemNullConversor"/> é um inteiro opcional que NÃO aceita null (o contrato diz <c>type: integer</c>): o esquema dela perde o <c>null</c> que o <c>int?</c> do C# acrescentaria.</summary>
    private static void AplicarInteiroSemNull(OpenApiSchema esquema, System.Text.Json.Serialization.Metadata.JsonPropertyInfo? propriedade)
    {
        if (propriedade?.CustomConverter is InteiroOpcionalSemNullConversor)
        {
            esquema.Type = JsonSchemaType.Integer;
            esquema.Format = "int32";
        }
    }

    private static void AplicarUfDaEmpresa(OpenApiSchema esquema, System.Text.Json.Serialization.Metadata.JsonPropertyInfo? propriedade)
    {
        if (propriedade is { Name: "uf" } && propriedade.DeclaringType == typeof(EmpresaConfiguracao))
        {
            esquema.Enum = [.. ConfiguracaoDaEmpresa.UfsValidas.Order(StringComparer.Ordinal).Select(uf => (JsonNode)JsonValue.Create(uf)!)];
        }
    }

    public static void Aplicar(OpenApiSchema esquema, Type tipo)
    {
        var subjacente = Nullable.GetUnderlyingType(tipo) ?? tipo;
        if (subjacente.IsEnum && esquema.Enum is { } valores)
        {
            for (var i = valores.Count - 1; i >= 0; i--)
            {
                if (valores[i] is null)
                {
                    valores.RemoveAt(i);
                }
            }
        }

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

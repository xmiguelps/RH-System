using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// Os filtros de enum chegam como TEXTO (<c>FiltrosDaListaDeAuditoria.Area</c> é <c>string?</c>) para o caso de uso ler os dois pelo nome do contrato e
/// devolver os erros de <c>area</c> e de <c>acao</c> juntos num 400 só. Por isso o gerador de OpenAPI os descreve como texto livre, e o contrato os
/// descreve como <c>enum</c>. Aqui o parâmetro volta a ter a lista de valores (<c>ACESSO_NEGADO</c>, não <c>AcessoNegado</c>), pelo par
/// <c>operationId</c> + nome do parâmetro. Toda rota nova com filtro de enum acrescenta o par aqui; <c>ContratoDivergenciaTests</c> acusa se esquecer.
/// </summary>
public static class EnumsDeParametrosDoContrato
{
    private static readonly Dictionary<(string Operacao, string Parametro), Type> Enums = new()
    {
        [("listarEventosDeAuditoria", "area")] = typeof(AuditoriaArea),
        [("listarEventosDeAuditoria", "acao")] = typeof(AuditoriaAcao),
        [("exportarAuditoria", "area")] = typeof(AuditoriaArea),
        [("exportarAuditoria", "acao")] = typeof(AuditoriaAcao),
    };

    /// <summary>Tem de vir DEPOIS de <see cref="NomesDeParametrosDoContrato"/>: a chave usa o nome já em camelCase.</summary>
    public static OpenApiOptions UsarEnumsDeParametrosDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddOperationTransformer((operacao, _, _) =>
        {
            Aplicar(operacao);
            return Task.CompletedTask;
        });

    public static void Aplicar(OpenApiOperation operacao)
    {
        foreach (var parametro in operacao.Parameters ?? [])
        {
            if (parametro is OpenApiParameter { In: ParameterLocation.Query, Name: { } nome } concreto
                && Enums.TryGetValue((operacao.OperationId ?? string.Empty, nome), out var tipo))
            {
                concreto.Schema = new OpenApiSchema
                {
                    Type = JsonSchemaType.String,
                    Enum = [.. Enum.GetNames(tipo).Select(n => (JsonNode)JsonValue.Create(JsonNamingPolicy.SnakeCaseUpper.ConvertName(n))!)],
                };
            }
        }
    }
}

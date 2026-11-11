using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// Os parâmetros de consulta que vêm de um objeto em <c>[AsParameters]</c> (<c>FiltrosDaListaDeAuditoria.Pagina</c>) saem no documento OpenAPI com o
/// nome da propriedade em PascalCase (<c>Pagina</c>), e o contrato os escreve em camelCase (<c>pagina</c>). O binder já aceita os dois (a chave da consulta
/// não diferencia maiúsculas), então só o documento muda: a primeira letra do nome de todo parâmetro de consulta vira minúscula. Vale para todas as listas
/// seguintes, sem cada objeto de consulta repetir <c>[FromQuery(Name = ...)]</c>.
/// </summary>
public static class NomesDeParametrosDoContrato
{
    public static OpenApiOptions UsarNomesDeParametrosDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddOperationTransformer((operacao, _, _) =>
        {
            Aplicar(operacao);
            return Task.CompletedTask;
        });

    public static void Aplicar(OpenApiOperation operacao)
    {
        foreach (var parametro in operacao.Parameters ?? [])
        {
            if (parametro is OpenApiParameter { In: ParameterLocation.Query, Name: { Length: > 0 } nome } concreto)
            {
                concreto.Name = char.ToLowerInvariant(nome[0]) + nome[1..];
            }
        }
    }
}

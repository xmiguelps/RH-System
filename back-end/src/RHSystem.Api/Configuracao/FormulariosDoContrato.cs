using System.Text.Json.Nodes;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Documentos;
using RHSystem.Application.Importacoes;
using RHSystem.Domain.Documentos;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// O corpo <c>multipart/form-data</c> das rotas de envio de arquivo, escrito à mão no documento da API. O gerador de OpenAPI descreveria o formulário a partir do tipo do C# (texto anulável, sem o enum, sem <c>format: date</c>, e as
/// DUAS mídias do formulário, <c>application/x-www-form-urlencoded</c> também) e o contrato fixa outra coisa. Por isso estas rotas <b>não</b> declaram o corpo no tipo do handler: o handler lê o formulário de <c>HttpRequest</c>
/// (e responde 415 se o corpo não é <c>multipart/form-data</c>) e este transformador escreve o schema do contrato, pelo <c>operationId</c>. Sem <c>[FromForm]</c> nem <c>Accepts</c> o roteamento não escolhe endpoint pelo tipo de
/// mídia: a autorização roda primeiro (401 e 403 antes do 415), como a matriz de perfis exige. Toda rota nova de envio acrescenta a dela aqui; <c>ContratoDivergenciaTests</c> compara.
/// </summary>
public static class FormulariosDoContrato
{
    public const string AdicionarDocumento = "adicionarDocumentoDoFuncionario";
    public const string EnviarMeuDocumento = "enviarMeuDocumento";
    public const string BaixarModeloDeImportacao = "baixarModeloDeImportacao";
    public const string ValidarPlanilhaDeImportacao = "validarPlanilhaDeImportacao";

    public static OpenApiOptions UsarFormulariosDoContrato(this OpenApiOptions opcoes) =>
        opcoes.AddOperationTransformer((operacao, _, _) =>
        {
            Aplicar(operacao);
            return Task.CompletedTask;
        });

    public static void Aplicar(OpenApiOperation operacao)
    {
        if (operacao.OperationId == AdicionarDocumento)
        {
            operacao.RequestBody = Multipart(
                obrigatorios: ["modo", "tipo"],
                ("modo", Enum<ModoDeDocumento>()),
                ("tipo", Enum<TipoDeDocumento>()),
                ("descricao", new OpenApiSchema { Type = JsonSchemaType.String, MaxLength = 255 }),
                ("dataValidade", new OpenApiSchema { Type = JsonSchemaType.String, Format = "date" }),
                ("arquivo", Binario()));
        }
        else if (operacao.OperationId == EnviarMeuDocumento)
        {
            operacao.RequestBody = Multipart(obrigatorios: ["arquivo"], ("arquivo", Binario()));
        }
        else if (operacao.OperationId == ValidarPlanilhaDeImportacao)
        {
            operacao.RequestBody = Multipart(obrigatorios: ["arquivo"], ("arquivo", Binario()));
        }
        else if (operacao.OperationId == BaixarModeloDeImportacao)
        {
            operacao.Responses ??= [];
            operacao.Responses["200"] = ArquivoXlsx();
        }
    }

    /// <summary>
    /// A resposta 200 de <c>baixarModeloDeImportacao</c>: o arquivo. O gerador de OpenAPI não descreve o corpo de um <c>Results.File</c> (sem mídia, sem schema), e o contrato fixa a mídia do Excel, o schema
    /// <c>string</c> com <c>format: binary</c> e o cabeçalho <c>Content-Disposition</c> do anexo.
    /// </summary>
    private static OpenApiResponse ArquivoXlsx() =>
        new()
        {
            Description = "O arquivo do modelo.",
            Headers = new Dictionary<string, IOpenApiHeader>
            {
                ["Content-Disposition"] = new OpenApiHeader
                {
                    Description = "Anexo: `attachment; filename=\"modelo-importacao-colaboradores.xlsx\"`.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String },
                },
            },
            Content = new Dictionary<string, OpenApiMediaType>
            {
                [ModeloDeImportacaoCasoDeUso.TipoDeConteudo] = new OpenApiMediaType { Schema = Binario() },
            },
        };

    private static OpenApiRequestBody Multipart(string[] obrigatorios, params (string Nome, OpenApiSchema Esquema)[] campos) =>
        new()
        {
            Required = true,
            Content = new Dictionary<string, OpenApiMediaType>
            {
                ["multipart/form-data"] = new OpenApiMediaType
                {
                    Schema = new OpenApiSchema
                    {
                        Type = JsonSchemaType.Object,
                        Required = new HashSet<string>(obrigatorios),
                        Properties = campos.ToDictionary(c => c.Nome, c => (IOpenApiSchema)c.Esquema),
                    },
                },
            },
        };

    /// <summary>O campo de arquivo: <c>string</c> com <c>format: binary</c>.</summary>
    private static OpenApiSchema Binario() => new() { Type = JsonSchemaType.String, Format = "binary" };

    /// <summary>O enum como o contrato o escreve: os nomes em <c>MAIÚSCULAS_COM_SUBLINHADO</c>, os MESMOS que o caso de uso aceita.</summary>
    private static OpenApiSchema Enum<TEnum>()
        where TEnum : struct, System.Enum =>
        new()
        {
            Type = JsonSchemaType.String,
            Enum = [.. System.Enum.GetValues<TEnum>().Select(valor => (JsonNode)JsonValue.Create(NomesDoContrato.Enum(valor))!)],
        };
}

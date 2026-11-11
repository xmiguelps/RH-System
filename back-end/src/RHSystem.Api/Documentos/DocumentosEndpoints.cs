using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;

namespace RHSystem.Api.Documentos;

/// <summary>
/// Os documentos da ficha do colaborador, pelo RH (<c>GrupoRh</c>): lista, anexa ou pede, e baixa; e "Meus documentos" (<c>GrupoEu</c>): a própria pessoa lista, envia o arquivo do que o RH pediu e baixa. Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>. O envio
/// recebe <c>multipart/form-data</c> LIDO de <c>HttpRequest</c> (não por <c>[FromForm]</c>, para o roteamento não escolher endpoint pelo tipo de mídia e a autorização rodar antes do 415; o corpo do contrato está em
/// <see cref="FormulariosDoContrato"/>): corpo que não é multipart responde 415 e multipart quebrado responde 400. Leva <c>DisableAntiforgery</c> (a autenticação é por cabeçalho, sem cookie: não há o que forjar entre sites) e o
/// teto de 16 MiB para a requisição inteira. O arquivo nunca passa pela API de negócio no download: a rota devolve um link temporário.
/// </summary>
public static class DocumentosEndpoints
{
    private const string Etiqueta = "Documentos do colaborador";

    public static IServiceCollection AdicionarCasosDeUsoDeDocumentos(this IServiceCollection servicos)
    {
        servicos.AddScoped<EnvioDeArquivoDeDocumento>();
        servicos.AddScoped<DocumentosDoColaboradorCasoDeUso>();
        servicos.AddScoped<MeusDocumentosCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapDocumentosDoRh(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/documentos", Listar)
            .WithName("listarDocumentosDoFuncionario")
            .Produces<DocumentoColaboradorListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/funcionarios/{funcionarioId:int}/documentos", Adicionar)
            .WithName(FormulariosDoContrato.AdicionarDocumento)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeDocumento.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeDocumento.TetoDaRequisicaoBytes })
            .Produces<DocumentoColaborador>(StatusCodes.Status201Created)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status500InternalServerError);

        grupo.MapGet("/funcionarios/{funcionarioId:int}/documentos/{documentoId:int}/download", Baixar)
            .WithName("baixarDocumentoDoFuncionario")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    /// <summary>"Meus documentos" (<c>GrupoEu</c>, qualquer pessoa logada, sem 403): só os da própria pessoa; o id dela é o do token.</summary>
    public static IEndpointRouteBuilder MapMeusDocumentos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoEu().WithTags(Etiqueta);

        grupo.MapGet("/documentos", ListarMeus)
            .WithName("listarMeusDocumentos")
            .Produces<DocumentoColaboradorListaResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/documentos/{documentoId:int}/arquivo", EnviarMeu)
            .WithName(FormulariosDoContrato.EnviarMeuDocumento)
            .DisableAntiforgery()
            .WithMetadata(new RequestSizeLimitAttribute(LimitesDeDocumento.TetoDaRequisicaoBytes), new RequestFormLimitsAttribute { MultipartBodyLengthLimit = LimitesDeDocumento.TetoDaRequisicaoBytes })
            .Produces<DocumentoColaborador>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status400BadRequest,
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        grupo.MapGet("/documentos/{documentoId:int}/download", BaixarMeu)
            .WithName("baixarMeuDocumento")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(
                StatusCodes.Status401Unauthorized,
                StatusCodes.Status404NotFound,
                StatusCodes.Status422UnprocessableEntity,
                StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<DocumentoColaboradorListaResposta> Listar(int funcionarioId, DocumentosDoColaboradorCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ListarAsync(funcionarioId, ct);

    private static async Task<IResult> Adicionar(int funcionarioId, HttpRequest requisicao, DocumentosDoColaboradorCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LerFormularioAsync(requisicao, ct);
        var arquivo = ArquivoDoFormulario(formulario);
        var novo = new NovoDocumentoDoRh(
            Texto(formulario, "modo"),
            Texto(formulario, "tipo"),
            Texto(formulario, "descricao"),
            Texto(formulario, "dataValidade"),
            arquivo,
            formulario.Files.Count); // conta as partes de arquivo de QUALQUER campo
        var documento = await casoDeUso.AdicionarAsync(funcionarioId, novo, ct);
        return Results.Created($"/api/rh/funcionarios/{funcionarioId}/documentos/{documento.Id}/download", documento);
    }

    private static async Task<DocumentoColaboradorListaResposta> ListarMeus(MeusDocumentosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ListarAsync(ct);

    private static async Task<DocumentoColaborador> EnviarMeu(int documentoId, HttpRequest requisicao, MeusDocumentosCasoDeUso casoDeUso, CancellationToken ct)
    {
        var formulario = await LerFormularioAsync(requisicao, ct);
        return await casoDeUso.EnviarArquivoAsync(documentoId, ArquivoDoFormulario(formulario), formulario.Files.Count, ct); // conta as partes de arquivo de QUALQUER campo
    }

    private static async Task<LinkTemporario> BaixarMeu(int documentoId, MeusDocumentosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.BaixarAsync(documentoId, ct);

    private static async Task<LinkTemporario> Baixar(int funcionarioId, int documentoId, DocumentosDoColaboradorCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.BaixarAsync(funcionarioId, documentoId, ct);

    /// <summary>O formulário multipart da requisição. Outro tipo de corpo (JSON, urlencoded, vazio) é 415; multipart que não dá para ler é 400.</summary>
    private static async Task<IFormCollection> LerFormularioAsync(HttpRequest requisicao, CancellationToken ct)
    {
        if (!MediaTypeHeaderValue.TryParse(requisicao.ContentType, out var tipo) || !tipo.MediaType.Equals("multipart/form-data", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException("O corpo precisa ser multipart/form-data.", StatusCodes.Status415UnsupportedMediaType);
        }

        try
        {
            return await requisicao.ReadFormAsync(ct);
        }
        catch (InvalidDataException)
        {
            throw new BadHttpRequestException("O corpo multipart está malformado.", StatusCodes.Status400BadRequest);
        }
        catch (IOException erro) when (erro.GetType() == typeof(IOException) && erro.Message.StartsWith("Unexpected end of Stream", StringComparison.Ordinal))
        {
            // O leitor multipart do ASP.NET lança esta IOException (e só esta, medido) quando o corpo diz ser multipart e não tem as partes: dado inválido, não falha do servidor. Qualquer outra falha
            // de E/S (disco cheio ao guardar o arquivo, conexão derrubada) continua sendo erro de verdade.
            throw new BadHttpRequestException("O corpo multipart está malformado.", StatusCodes.Status400BadRequest);
        }
    }

    private static ArquivoRecebido? ArquivoDoFormulario(IFormCollection formulario) =>
        formulario.Files.GetFile("arquivo") is { } recebido ? new ArquivoRecebido(recebido.FileName, recebido.Length, recebido.OpenReadStream) : null;

    private static string? Texto(IFormCollection formulario, string campo) => formulario.TryGetValue(campo, out var valor) ? valor.ToString() : null;
}

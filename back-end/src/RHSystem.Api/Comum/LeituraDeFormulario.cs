using Microsoft.Net.Http.Headers;
using RHSystem.Application.Documentos;

namespace RHSystem.Api.Comum;

/// <summary>
/// A leitura do formulário <c>multipart/form-data</c> das rotas de envio de arquivo (documentos e importação). O corpo é LIDO de <c>HttpRequest</c> e não por <c>[FromForm]</c>, para o roteamento não escolher endpoint pelo tipo de mídia e a
/// autorização rodar antes do 415 (ver <c>FormulariosDoContrato</c>): corpo que não é multipart responde 415 e multipart quebrado responde 400.
/// </summary>
public static class LeituraDeFormulario
{
    /// <summary>O formulário multipart da requisição. Outro tipo de corpo (JSON, urlencoded, vazio) é 415; multipart que não dá para ler é 400.</summary>
    public static async Task<IFormCollection> LerAsync(HttpRequest requisicao, CancellationToken ct)
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

    /// <summary>O arquivo do campo <c>arquivo</c>, ou <c>null</c> se não veio. O chamador conta as partes de arquivo de QUALQUER campo em <c>formulario.Files.Count</c>.</summary>
    public static ArquivoRecebido? Arquivo(IFormCollection formulario) =>
        formulario.Files.GetFile("arquivo") is { } recebido ? new ArquivoRecebido(recebido.FileName, recebido.Length, recebido.OpenReadStream) : null;
}

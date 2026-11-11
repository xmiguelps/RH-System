using System.Net.Http.Headers;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>Os formulários <c>multipart/form-data</c> dos documentos, montados como o front os mandaria (e como ele nunca mandaria: campo faltando, arquivo demais, arquivo em outro campo).</summary>
internal static class FormulariosDeTeste
{
    /// <summary>Uma parte de arquivo. <paramref name="TipoDeConteudo"/> é o que o CLIENTE diz: a API o ignora (o tipo vem dos primeiros bytes).</summary>
    public sealed record Parte(string Campo, string NomeDoArquivo, byte[] Bytes, string TipoDeConteudo = "application/octet-stream");

    /// <summary>Um formulário do RH. <c>null</c> em <paramref name="modo"/>, <paramref name="tipo"/>, <paramref name="descricao"/> ou <paramref name="dataValidade"/> não manda o campo.</summary>
    public static MultipartFormDataContent Documento(string? modo, string? tipo, string? descricao = null, string? dataValidade = null, params Parte[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        Texto(formulario, "modo", modo);
        Texto(formulario, "tipo", tipo);
        Texto(formulario, "descricao", descricao);
        Texto(formulario, "dataValidade", dataValidade);
        foreach (var parte in arquivos)
        {
            AdicionarArquivo(formulario, parte);
        }

        return formulario;
    }

    /// <summary><c>ANEXAR_AGORA</c> com um arquivo no campo <c>arquivo</c>.</summary>
    public static MultipartFormDataContent Anexo(string tipo, string nome, byte[] bytes, string? descricao = null, string? dataValidade = null, string tipoDeConteudo = "application/octet-stream") =>
        Documento("ANEXAR_AGORA", tipo, descricao, dataValidade, new Parte("arquivo", nome, bytes, tipoDeConteudo));

    /// <summary><c>PEDIR_PARA_ENVIAR</c>, sem arquivo.</summary>
    public static MultipartFormDataContent Pedido(string tipo, string? descricao = null, string? dataValidade = null) => Documento("PEDIR_PARA_ENVIAR", tipo, descricao, dataValidade);

    /// <summary>O formulário de "Meus documentos": só o campo <c>arquivo</c>.</summary>
    public static MultipartFormDataContent Envio(string nome, byte[] bytes, string tipoDeConteudo = "application/octet-stream")
    {
        var formulario = new MultipartFormDataContent();
        AdicionarArquivo(formulario, new Parte("arquivo", nome, bytes, tipoDeConteudo));
        return formulario;
    }

    private static void Texto(MultipartFormDataContent formulario, string campo, string? valor)
    {
        if (valor is not null)
        {
            formulario.Add(new StringContent(valor), campo);
        }
    }

    private static void AdicionarArquivo(MultipartFormDataContent formulario, Parte parte)
    {
        var conteudo = new ByteArrayContent(parte.Bytes);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue(parte.TipoDeConteudo);
        formulario.Add(conteudo, parte.Campo, parte.NomeDoArquivo);
    }
}

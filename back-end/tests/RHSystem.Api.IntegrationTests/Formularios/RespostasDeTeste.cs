using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>O que os testes de responder formulário (com nome e anônimo) têm em comum: a pessoa com login num setor, as pessoas escolhidas, o envio <c>multipart/form-data</c> e as contagens no banco.</summary>
internal static class RespostasDeTeste
{
    public static async Task<(HttpClient Cliente, int Id)> PessoaAsync(ApiDeAcesso api)
    {
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var (id, _) = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        return (await AdvertenciasDeTeste.ClienteDeAsync(api, id), id);
    }

    public static Task EscolherPessoasAsync(ApiDeAcesso api, int formularioId, params int[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({formularioId}, {p})"))}");

    public static string Json(params object[] itens) => JsonSerializer.Serialize(itens);

    public static MultipartFormDataContent Envio(string? respostas, int? avaliado = null, params (string Nome, byte[] Bytes)[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        if (respostas is null && avaliado is null && arquivos.Length == 0)
        {
            formulario.Add(new StringContent("x"), "outro"); // um multipart sem nenhuma parte é "malformado" (400 do corpo); aqui falta só a parte respostas
        }

        if (respostas is not null)
        {
            formulario.Add(new StringContent(respostas, Encoding.UTF8), "respostas");
        }

        if (avaliado is { } id)
        {
            formulario.Add(new StringContent(id.ToString(System.Globalization.CultureInfo.InvariantCulture)), "avaliadoId");
        }

        foreach (var (nome, bytes) in arquivos)
        {
            formulario.Add(new ByteArrayContent(bytes), "arquivos", nome);
        }

        return formulario;
    }

    public static Task<HttpResponseMessage> ResponderAsync(HttpClient cliente, int formularioId, HttpContent envio) => cliente.PostAsync($"/api/eu/formularios/{formularioId}/respostas", envio);

    public static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) => (await CorpoAsync(resposta)).GetProperty("errors");

    public static byte[] Docx(bool comODocumento = true)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry(comODocumento ? "word/document.xml" : "outro/arquivo.txt");
            using var escritor = new StreamWriter(entrada.Open());
            escritor.Write("<w:document/>");
        }

        return memoria.ToArray();
    }

    public static byte[] Doc() => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[200]];

    public static async Task<long> LinhasAsync(ApiDeAcesso api, int formularioId) => await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {formularioId}");

    public static async Task<long> AuditoriaAsync(ApiDeAcesso api) => await ContarAsync(api, "SELECT (SELECT COUNT(*) FROM AUDITORIA_EVENTOS) + (SELECT COUNT(*) FROM AUDITORIA_MUDANCAS)");
}

using System.Text;
using System.Text.Json;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>O que os testes dos cadastros de apoio (cargos, setores, gestores, benefícios) repetiriam: ler a lista, ler o erro e disparar várias chamadas juntas.</summary>
internal static class CadastrosDeTeste
{
    public static async Task<JsonElement> CorpoAsync(HttpResponseMessage resposta) => JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());

    /// <summary>Os <c>itens</c> de uma lista, depois de conferir que foi um 200.</summary>
    public static async Task<List<JsonElement>> ItensAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(200, (int)resposta.StatusCode);
        return [.. (await CorpoAsync(resposta)).GetProperty("itens").EnumerateArray()];
    }

    /// <summary>O status e o <c>codigo</c> e o <c>detail</c> de uma resposta de erro do contrato.</summary>
    public static async Task<(int Status, string Codigo, string Detalhe)> ProblemaAsync(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        return ((int)resposta.StatusCode, corpo.GetProperty("codigo").GetString()!, corpo.GetProperty("detail").GetString()!);
    }

    /// <summary>Um 400 de validação: confere o status e devolve o objeto <c>errors</c> (campo → mensagens).</summary>
    public static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(400, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("errors");
    }

    /// <summary>O corpo JSON escrito à mão: para mandar o que o front nunca mandaria (campo faltando, tipo trocado).</summary>
    public static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

    public static string Codificar(string texto) => Uri.EscapeDataString(texto);

    /// <summary>
    /// Dispara <paramref name="quantidade"/> chamadas juntas: todas esperam o mesmo portão e saem na mesma hora, cada uma no seu fio do pool. É o que prova a corrida do nome repetido (um 201 e o resto 409, nunca 500).
    /// </summary>
    public static async Task<HttpResponseMessage[]> EmSimultaneoAsync(int quantidade, Func<int, Task<HttpResponseMessage>> envio)
    {
        var portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tarefas = Enumerable.Range(0, quantidade).Select(async i =>
        {
            await portao.Task;
            return await envio(i);
        }).ToArray();
        portao.SetResult();
        return await Task.WhenAll(tarefas);
    }
}

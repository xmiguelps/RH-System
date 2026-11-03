using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Matriz;

internal sealed record ResultadoDaMatriz(IReadOnlyList<string> Falhas, int RotasConferidas);

/// <summary>
/// Chama toda rota da API (<c>ContratoDaApi.RotasImplementadas</c>) sem login e como cada perfil, e compara com o <c>x-perfis</c> do contrato.
/// Os ids de caminho valem <c>2147483647</c> e o corpo é <c>{}</c> em JSON, sem efeito colateral: uma rota nova que executasse algo com isso precisa
/// validar antes e dar 400 ou 404. As falhas são TODAS coletadas, no formato "MÉTODO caminho, perfil: esperado X, veio Y".
/// </summary>
internal static partial class MatrizDePerfis
{
    private const int IdDeCaminho = int.MaxValue;
    private const string TituloSemPermissao = "Sem permissão.";
    private const string DetalheSemPermissao = "Você não tem permissão para esta ação.";

    private static readonly string[] NomesDosPerfis = ["Funcionario", "Gestor", "RH"];

    public static async Task<ResultadoDaMatriz> ExecutarAsync(WebApplicationFactory<Program> fabrica, ElencoDeTeste elenco)
    {
        var falhas = new List<string>();
        var conferidas = 0;
        var anonimo = fabrica.CreateClient();
        var clientes = new Dictionary<string, HttpClient>
        {
            ["Funcionario"] = ComToken(fabrica, elenco.TokensPorUsuario[elenco.MembroA.UsuarioId]),
            ["Gestor"] = ComToken(fabrica, elenco.TokensPorUsuario[elenco.Gestor.UsuarioId]),
            ["RH"] = ComToken(fabrica, elenco.TokensPorUsuario[elenco.Rh.UsuarioId]),
        };

        foreach (var (metodo, caminho) in ContratoDaApi.RotasImplementadas(fabrica.Services))
        {
            var operacao = ContratoDaApi.Operacoes.FirstOrDefault(o => o.Metodo == metodo && Normalizar(o.Caminho) == Normalizar(caminho));
            if (operacao is null)
            {
                falhas.Add($"{metodo} {caminho}: rota fora do contrato (a API tem a rota, o contrato não tem a operação)");
                continue;
            }

            conferidas++;
            var url = NomeDeParametro().Replace(caminho, IdDeCaminho.ToString(System.Globalization.CultureInfo.InvariantCulture));

            if (operacao.Perfis.Contains("Publico"))
            {
                await ConferirAsync(anonimo, "anônimo", metodo, caminho, url, deve: status => status is not 401 and not 403, esperado: "fora de 401 e 403", falhas);
                foreach (var nome in NomesDosPerfis)
                {
                    await ConferirAsync(clientes[nome], nome, metodo, caminho, url, deve: status => status is not 401 and not 403, esperado: "fora de 401 e 403", falhas);
                }

                continue;
            }

            await ConferirProblemaAsync(anonimo, "anônimo", metodo, caminho, url, 401, "Não autenticado.", "Entre novamente para continuar.", falhas);
            foreach (var nome in NomesDosPerfis)
            {
                if (operacao.Perfis.Contains(nome))
                {
                    await ConferirAsync(clientes[nome], nome, metodo, caminho, url, deve: status => status is not 401 and not 403 and < 500, esperado: "fora de 401, 403 e 5xx", falhas);
                }
                else
                {
                    await ConferirProblemaAsync(clientes[nome], nome, metodo, caminho, url, 403, TituloSemPermissao, DetalheSemPermissao, falhas);
                }
            }
        }

        return new ResultadoDaMatriz(falhas, conferidas);
    }

    private static HttpClient ComToken(WebApplicationFactory<Program> fabrica, string token)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string metodo, string url)
    {
        using var pedido = new HttpRequestMessage(new HttpMethod(metodo), url);
        if (metodo is "POST" or "PUT" or "PATCH")
        {
            pedido.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        }

        return await cliente.SendAsync(pedido);
    }

    private static async Task ConferirAsync(HttpClient cliente, string perfil, string metodo, string caminho, string url, Func<int, bool> deve, string esperado, List<string> falhas)
    {
        using var resposta = await EnviarAsync(cliente, metodo, url);
        var status = (int)resposta.StatusCode;
        if (!deve(status))
        {
            falhas.Add($"{metodo} {caminho}, {perfil}: esperado {esperado}, veio {status}");
        }
    }

    private static async Task ConferirProblemaAsync(HttpClient cliente, string perfil, string metodo, string caminho, string url, int statusEsperado, string titulo, string detalhe, List<string> falhas)
    {
        using var resposta = await EnviarAsync(cliente, metodo, url);
        var status = (int)resposta.StatusCode;
        var midia = resposta.Content.Headers.ContentType?.MediaType;
        if (status != statusEsperado || midia != "application/problem+json")
        {
            falhas.Add($"{metodo} {caminho}, {perfil}: esperado {statusEsperado} application/problem+json, veio {status} {midia ?? "(sem corpo)"}");
            return;
        }

        var corpo = JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());
        if (corpo.GetProperty("title").GetString() != titulo || corpo.GetProperty("detail").GetString() != detalhe)
        {
            falhas.Add($"{metodo} {caminho}, {perfil}: esperado {statusEsperado} com title \"{titulo}\" e detail \"{detalhe}\", veio \"{corpo.GetProperty("title").GetString()}\" e \"{corpo.GetProperty("detail").GetString()}\"");
        }
    }

    private static string Normalizar(string caminho) => NomeDeParametro().Replace(caminho, "{}");

    [GeneratedRegex(@"\{[^}]*\}")]
    private static partial Regex NomeDeParametro();
}

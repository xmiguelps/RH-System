using System.Net.Http.Json;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// Os dois limites das rotas de senha, com limite 5 (uma fábrica própria; o <c>ApiFactory</c> padrão põe 1000): o das três rotas públicas, por IP e por rota, e o de
/// <c>trocarMinhaSenha</c>, por pessoa, que só conta os ERROS de senha atual.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimiteDeSenhaTests(MySqlFixture mysql)
{
    private const string NovaSenha = "NovaSenha#2026";

    private async Task<(ApiDeAcesso Api, HttpClient Cliente)> ComLimiteDe5Async()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(LimiteDeSenhaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var cliente = api.Api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Autenticacao:LimiteSenhaPorMinuto", "5");
            b.UseSetting("Autenticacao:LimiteSenhaAtualPorMinuto", "5");
        }).CreateClient();
        return (api, cliente);
    }

    private static async Task<HttpResponseMessage> TrocarAsync(HttpClient cliente, string tokenDeAcesso, string senhaAtual, string novaSenha)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/eu/senha") { Content = JsonContent.Create(new { senhaAtual, novaSenha }) };
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenDeAcesso);
        return await cliente.SendAsync(requisicao);
    }

    [Fact]
    public async Task Sexta_chamada_do_mesmo_IP_a_cada_rota_de_senha_devolve_429_com_Retry_After_e_as_rotas_tem_baldes_separados()
    {
        var (_, cliente) = await ComLimiteDe5Async();
        var rotas = new (string Caminho, object Corpo, int StatusDentroDoLimite)[]
        {
            ("/api/auth/senha/esqueci", new { email = "ninguem@empresa.com.br" }, 202),
            ("/api/auth/senha/validar-link", new { token = new string('a', 64) }, 422),
            ("/api/auth/senha/definir", new { token = new string('a', 64), novaSenha = NovaSenha }, 422),
        };

        foreach (var (caminho, corpo, statusDentroDoLimite) in rotas)
        {
            // O balde desta rota está cheio de outro jeito: as outras rotas já foram esgotadas e a primeira chamada desta ainda passa.
            for (var i = 1; i <= 5; i++)
            {
                Assert.Equal(statusDentroDoLimite, (int)(await PostAsync(cliente, caminho, corpo)).StatusCode);
            }

            var sexta = await PostAsync(cliente, caminho, corpo);

            Assert.Equal(429, (int)sexta.StatusCode);
            Assert.Equal("application/problem+json", sexta.Content.Headers.ContentType?.MediaType);
            var problema = await CorpoAsync(sexta);
            Assert.Equal("Muitas tentativas.", problema.GetProperty("title").GetString());
            Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.", problema.GetProperty("detail").GetString());
            Assert.InRange(int.Parse(sexta.Headers.GetValues("Retry-After").Single()), 1, 60);
        }
    }

    [Fact]
    public async Task Trocar_senha_com_5_senhas_atuais_erradas_em_um_minuto_devolve_429_na_sexta_com_Retry_After_mesmo_com_a_senha_certa()
    {
        var (api, cliente) = await ComLimiteDe5Async();
        var pessoa = await api.NovaPessoaAsync("limite-erradas", login: TipoDeLogin.SenhaDefinida);
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);

        for (var i = 1; i <= 5; i++)
        {
            Assert.Equal(422, (int)(await TrocarAsync(cliente, acesso, $"errada {i}", NovaSenha)).StatusCode);
        }

        var sexta = await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha); // a senha agora está CERTA e mesmo assim bloqueia

        Assert.Equal(429, (int)sexta.StatusCode);
        var problema = await CorpoAsync(sexta);
        Assert.Equal("Muitas tentativas.", problema.GetProperty("title").GetString());
        Assert.Equal("Muitas tentativas. Aguarde um minuto e tente de novo.", problema.GetProperty("detail").GetString());
        Assert.Equal("60", Assert.Single(sexta.Headers.GetValues("Retry-After"))); // todos os erros no mesmo instante do relógio falso: o mais antigo sai em 60 s
        await EntrarAsync(cliente, pessoa.Email!); // e a senha não mudou
    }

    [Fact]
    public async Task Trocar_senha_so_conta_o_erro_de_senha_atual_da_propria_pessoa()
    {
        var (api, cliente) = await ComLimiteDe5Async();
        var pessoaA = await api.NovaPessoaAsync("limite-conta-a", login: TipoDeLogin.SenhaDefinida);
        var pessoaB = await api.NovaPessoaAsync("limite-conta-b", login: TipoDeLogin.SenhaDefinida);
        var (acessoA, _) = await EntrarAsync(cliente, pessoaA.Email!);
        var (acessoB, _) = await EntrarAsync(cliente, pessoaB.Email!);

        for (var i = 1; i <= 4; i++)
        {
            Assert.Equal(422, (int)(await TrocarAsync(cliente, acessoA, $"errada {i}", NovaSenha)).StatusCode); // 4 erros de senha atual
        }

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(400, (int)(await TrocarAsync(cliente, acessoA, ApiFactory.SenhaPadrao, "curta")).StatusCode); // 400 de campo: não conta
        }

        for (var i = 0; i < 2; i++)
        {
            Assert.Equal(400, (int)(await TrocarAsync(cliente, acessoA, ApiFactory.SenhaPadrao, ApiFactory.SenhaPadrao)).StatusCode); // senha certa, mas igual: não conta
        }

        Assert.Equal(422, (int)(await TrocarAsync(cliente, acessoA, "errada 5", NovaSenha)).StatusCode); // o quinto erro ainda responde 422
        Assert.Equal(429, (int)(await TrocarAsync(cliente, acessoA, "errada 6", NovaSenha)).StatusCode); // e a próxima já bloqueia
        Assert.Equal(422, (int)(await TrocarAsync(cliente, acessoB, "errada da B", NovaSenha)).StatusCode); // erros de OUTRA pessoa não contam
        Assert.Equal(204, (int)(await TrocarAsync(cliente, acessoB, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);
    }

    [Fact]
    public async Task Trocar_senha_volta_a_aceitar_60_segundos_depois_do_erro_mais_antigo()
    {
        var (api, cliente) = await ComLimiteDe5Async();
        var pessoa = await api.NovaPessoaAsync("limite-volta", login: TipoDeLogin.SenhaDefinida);
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);
        for (var i = 1; i <= 5; i++)
        {
            await TrocarAsync(cliente, acesso, $"errada {i}", NovaSenha);
        }

        try
        {
            Assert.Equal(429, (int)(await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);

            api.Api.Relogio.Avancar(TimeSpan.FromSeconds(60)); // o erro mais antigo saiu da janela

            Assert.Equal(204, (int)(await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }
}

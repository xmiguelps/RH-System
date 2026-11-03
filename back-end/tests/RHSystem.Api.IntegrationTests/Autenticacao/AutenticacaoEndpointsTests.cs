using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Autenticacao;

/// <summary>Comportamento real das rotas de /api/auth: a API em memória, o MySQL de verdade e os textos do contrato.</summary>
[Collection(MySqlCollection.Nome)]
public class AutenticacaoEndpointsTests(MySqlFixture mysql)
{
    // Um banco com os scripts por classe de teste (15 s); os testes seguem em ordem dentro da classe.
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static ApiFactory? _compartilhada;

    private async Task<ApiFactory> ApiAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return _compartilhada ??= await ApiFactory.CriarAsync(mysql);
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta)
    {
        // ReadAsStringAsync guarda o corpo em memória: pode ser lido mais de uma vez no mesmo teste (ReadFromJsonAsync fecharia o fluxo).
        return JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());
    }

    private static Task<HttpResponseMessage> EntrarAsync(HttpClient cliente, string email, string senha = ApiFactory.SenhaPadrao) =>
        cliente.PostAsJsonAsync("/api/auth/entrar", new { email, senha });

    private static async Task<JsonElement> SessaoAsync(HttpClient cliente, string email, string senha = ApiFactory.SenhaPadrao)
    {
        var resposta = await EntrarAsync(cliente, email, senha);
        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        return await LerAsync(resposta);
    }

    private static async Task<HttpResponseMessage> EuAsync(HttpClient cliente, string? token)
    {
        using var pedido = new HttpRequestMessage(HttpMethod.Get, "/api/auth/eu");
        if (token is not null)
        {
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        return await cliente.SendAsync(pedido);
    }

    private static async Task AssertProblemaAsync(HttpResponseMessage resposta, int status, string titulo, string detalhe)
    {
        Assert.Equal(status, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await LerAsync(resposta);
        Assert.Equal(status, corpo.GetProperty("status").GetInt32());
        Assert.Equal(titulo, corpo.GetProperty("title").GetString());
        Assert.Equal(detalhe, corpo.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()));
    }

    [Fact]
    public async Task Entrar_com_credenciais_validas_devolve_200_com_tokens()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await EntrarAsync(cliente, ApiFactory.EmailRh);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var sessao = await LerAsync(resposta);
        Assert.False(string.IsNullOrWhiteSpace(sessao.GetProperty("tokenDeAcesso").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(sessao.GetProperty("refreshToken").GetString()));
        Assert.Equal("RH", sessao.GetProperty("usuario").GetProperty("perfil").GetString());
    }

    [Fact]
    public async Task Resposta_de_entrar_tem_exatamente_as_chaves_do_contrato_em_camelCase_e_perfil_como_texto()
    {
        // O teste de divergência não prova a serialização real: aqui estão as chaves literais do contrato.
        var cliente = (await ApiAsync()).CreateClient();

        var sessao = await SessaoAsync(cliente, ApiFactory.EmailRh);

        Assert.Equal(["acessoExpiraEmUtc", "refreshToken", "tokenDeAcesso", "usuario"], sessao.EnumerateObject().Select(p => p.Name).Order());
        Assert.EndsWith("Z", sessao.GetProperty("acessoExpiraEmUtc").GetString());
        var usuario = sessao.GetProperty("usuario");
        Assert.Equal(["cargo", "email", "funcionarioId", "id", "nome", "perfil", "setor"], usuario.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(JsonValueKind.String, usuario.GetProperty("perfil").ValueKind);
    }

    [Fact]
    public async Task Entrar_devolve_cargo_e_setor_quando_o_funcionario_tem_e_null_nos_dois_quando_nao_tem()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var com = (await SessaoAsync(cliente, ApiFactory.EmailComCargo)).GetProperty("usuario");
        var sem = (await SessaoAsync(cliente, ApiFactory.EmailRh)).GetProperty("usuario");

        Assert.Equal("Analista de RH", com.GetProperty("cargo").GetString());
        Assert.Equal("Recursos Humanos", com.GetProperty("setor").GetString());
        Assert.Equal(JsonValueKind.Null, sem.GetProperty("cargo").ValueKind); // a chave existe, com null
        Assert.Equal(JsonValueKind.Null, sem.GetProperty("setor").ValueKind);
    }

    [Fact]
    public async Task Entrar_renovar_e_sair_com_campo_vazio_devolvem_400_com_errors_por_campo()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var entrar = await LerAsync(await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = "", senha = "" }));
        var renovar = await LerAsync(await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = "" }));
        var sair = await LerAsync(await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = "  " }));

        Assert.Equal(["email", "senha"], entrar.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["refreshToken"], renovar.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(["refreshToken"], sair.GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal("O e-mail é obrigatório.", entrar.GetProperty("errors").GetProperty("email")[0].GetString());
    }

    [Theory]
    [InlineData("entrar-400", 400, "Dados inválidos.", "Preencha o e-mail e a senha.")]
    [InlineData("renovar-400", 400, "Dados inválidos.", "Confira os campos indicados.")]
    [InlineData("sair-400", 400, "Dados inválidos.", "Confira os campos indicados.")]
    [InlineData("login-401", 401, "E-mail ou senha inválidos.", "E-mail ou senha inválidos.")]
    [InlineData("sem-token-401", 401, "Não autenticado.", "Entre novamente para continuar.")]
    [InlineData("refresh-401", 401, "Não autenticado.", "Entre novamente para continuar.")]
    [InlineData("inativo-403", 403, "Acesso desativado.", "Seu acesso está desativado. Fale com o RH.")]
    [InlineData("sem-perfil-403", 403, "Perfil não configurado.", "Seu perfil de acesso não está configurado. Fale com o RH.")]
    [InlineData("trigger-422", 422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.")]
    [InlineData("inesperado-500", 500, "Erro no servidor.", "Erro no servidor. Tente novamente em instantes.")]
    public async Task Textos_dos_erros_sao_os_do_contrato(string caso, int status, string titulo, string detalhe)
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = caso switch
        {
            "entrar-400" => await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = "", senha = "" }),
            "renovar-400" => await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = "" }),
            "sair-400" => await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = "" }),
            "login-401" => await EntrarAsync(cliente, ApiFactory.EmailRh, "senha errada"),
            "sem-token-401" => await EuAsync(cliente, null),
            "refresh-401" => await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = "nunca-emitido" }),
            "inativo-403" => await EntrarAsync(cliente, ApiFactory.EmailUsuarioInativo),
            "sem-perfil-403" => await EntrarAsync(cliente, ApiFactory.EmailSemNivel),
            "trigger-422" => await cliente.GetAsync("/__teste/trigger"),
            "inesperado-500" => await cliente.GetAsync("/__teste/erro"),
            _ => throw new InvalidOperationException(caso),
        };

        await AssertProblemaAsync(resposta, status, titulo, detalhe);
    }

    [Fact]
    public async Task Erro_de_trigger_do_banco_devolve_422_ProblemDetails_com_detail_generico_e_a_mensagem_do_trigger_na_extensao_regraDoBanco()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await cliente.GetAsync("/__teste/trigger");

        await AssertProblemaAsync(resposta, 422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.");
        var corpo = await LerAsync(resposta);
        Assert.Contains("Nao e possivel criar usuario para funcionario inativo.", corpo.GetProperty("regraDoBanco").GetString());
    }

    [Fact]
    public async Task Entrar_com_usuario_inativo_e_senha_correta_devolve_403_com_a_mensagem_de_acesso_desativado()
    {
        var cliente = (await ApiAsync()).CreateClient();

        await AssertProblemaAsync(await EntrarAsync(cliente, ApiFactory.EmailUsuarioInativo), 403, "Acesso desativado.", "Seu acesso está desativado. Fale com o RH.");
        // O funcionário inativado: o trigger do grupo inativou o usuário junto.
        await AssertProblemaAsync(await EntrarAsync(cliente, ApiFactory.EmailDesligado), 403, "Acesso desativado.", "Seu acesso está desativado. Fale com o RH.");
    }

    [Fact]
    public async Task Entrar_com_email_inexistente_senha_errada_e_inativo_com_senha_errada_devolvem_o_mesmo_status_title_e_detail() // Review Focus 4
    {
        var cliente = (await ApiAsync()).CreateClient();

        var respostas = new[]
        {
            await EntrarAsync(cliente, "ninguem@empresa.com.br"),
            await EntrarAsync(cliente, ApiFactory.EmailRh, "senha errada"),
            await EntrarAsync(cliente, ApiFactory.EmailUsuarioInativo, "senha errada"),
        };

        foreach (var resposta in respostas)
        {
            await AssertProblemaAsync(resposta, 401, "E-mail ou senha inválidos.", "E-mail ou senha inválidos."); // o traceId não entra na comparação
        }
    }

    [Fact]
    public async Task Entrar_com_senha_acima_de_128_caracteres_devolve_400()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await EntrarAsync(cliente, ApiFactory.EmailRh, new string('x', 129));

        await AssertProblemaAsync(resposta, 400, "Dados inválidos.", "Preencha o e-mail e a senha.");
        Assert.Equal("senha", (await LerAsync(resposta)).GetProperty("errors").EnumerateObject().Single().Name);
    }

    [Fact]
    public async Task Entrar_com_senha_unicode_funciona()
    {
        var fabrica = await ApiAsync();
        await fabrica.CriarUsuarioAsync("unicode@empresa.com.br", "Sênha-ção-😀-#1");
        var cliente = fabrica.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await EntrarAsync(cliente, "unicode@empresa.com.br", "Sênha-ção-😀-#1")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await EntrarAsync(cliente, "unicode@empresa.com.br", "Senha-cao-😀-#1")).StatusCode);
    }

    [Fact]
    public async Task Entrar_com_funcionario_sem_nivel_devolve_403_perfil_nao_configurado()
    {
        var cliente = (await ApiAsync()).CreateClient();

        await AssertProblemaAsync(await EntrarAsync(cliente, ApiFactory.EmailSemNivel), 403, "Perfil não configurado.", "Seu perfil de acesso não está configurado. Fale com o RH.");
    }

    [Fact]
    public async Task Renovar_rotaciona_e_o_token_antigo_passa_a_devolver_401()
    {
        var cliente = (await ApiAsync()).CreateClient();
        var antigo = (await SessaoAsync(cliente, ApiFactory.EmailRh)).GetProperty("refreshToken").GetString()!;

        var renovada = await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = antigo });

        Assert.Equal(HttpStatusCode.OK, renovada.StatusCode);
        var nova = (await LerAsync(renovada)).GetProperty("refreshToken").GetString();
        Assert.NotEqual(antigo, nova);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = antigo })).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = nova })).StatusCode);
    }

    [Fact]
    public async Task Renovar_simultaneo_com_o_mesmo_token_nunca_devolve_500_no_maximo_um_200_e_o_token_do_vencedor_continua_valido() // Review Focus 3
    {
        var cliente = (await ApiAsync()).CreateClient();
        var refresh = (await SessaoAsync(cliente, ApiFactory.EmailComCargo)).GetProperty("refreshToken").GetString()!;

        var respostas = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = refresh })));

        Assert.DoesNotContain(respostas, r => r.StatusCode == HttpStatusCode.InternalServerError);
        Assert.All(respostas, r => Assert.True(r.StatusCode is HttpStatusCode.OK or HttpStatusCode.Unauthorized));
        var vencedoras = respostas.Where(r => r.StatusCode == HttpStatusCode.OK).ToList();
        Assert.Single(vencedoras);
        var novoRefresh = (await LerAsync(vencedoras[0])).GetProperty("refreshToken").GetString();
        Assert.Equal(HttpStatusCode.OK, (await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = novoRefresh })).StatusCode);
    }

    [Fact]
    public async Task Sair_revoga_o_refresh_e_e_idempotente()
    {
        var cliente = (await ApiAsync()).CreateClient();
        var refresh = (await SessaoAsync(cliente, ApiFactory.EmailRh)).GetProperty("refreshToken").GetString()!;

        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = refresh })).StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, (await cliente.PostAsJsonAsync("/api/auth/sair", new { refreshToken = "nunca-emitido" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = refresh })).StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("isto-nao-e-um-jwt")]
    [InlineData("aaa.bbb.ccc")]
    public async Task Eu_sem_token_ou_com_token_malformado_devolve_401_ProblemDetails_sem_stack_trace(string? token) // Review Focus 5
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await EuAsync(cliente, token);

        await AssertProblemaAsync(resposta, 401, "Não autenticado.", "Entre novamente para continuar.");
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("   at ", texto);
        Assert.DoesNotContain("Exception", texto);
    }

    [Fact]
    public async Task Eu_com_token_expirado_devolve_401()
    {
        var fabrica = await ApiAsync();
        var cliente = fabrica.CreateClient();
        var token = (await SessaoAsync(cliente, ApiFactory.EmailRh)).GetProperty("tokenDeAcesso").GetString();
        Assert.Equal(HttpStatusCode.OK, (await EuAsync(cliente, token)).StatusCode);

        fabrica.Relogio.Avancar(TimeSpan.FromMinutes(20));
        try
        {
            await AssertProblemaAsync(await EuAsync(cliente, token), 401, "Não autenticado.", "Entre novamente para continuar.");
        }
        finally
        {
            fabrica.Relogio.Avancar(TimeSpan.FromMinutes(-20));
        }
    }

    [Fact]
    public async Task Erro_inesperado_em_Production_devolve_500_ProblemDetails_sem_stack_trace()
    {
        var fabrica = (await ApiAsync()).WithWebHostBuilder(b => b.UseEnvironment("Production"));
        var cliente = fabrica.CreateClient();

        var resposta = await cliente.GetAsync("/__teste/erro");

        await AssertProblemaAsync(resposta, 500, "Erro no servidor.", "Erro no servidor. Tente novamente em instantes.");
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("segredo interno", texto); // a mensagem da exceção não vaza
        Assert.DoesNotContain("   at ", texto);
    }

    [Fact]
    public async Task Eu_com_token_valido_devolve_o_usuario_com_perfil_como_texto_cargo_e_setor()
    {
        var cliente = (await ApiAsync()).CreateClient();
        var token = (await SessaoAsync(cliente, ApiFactory.EmailComCargo)).GetProperty("tokenDeAcesso").GetString();

        var resposta = await EuAsync(cliente, token);

        Assert.Equal(HttpStatusCode.OK, resposta.StatusCode);
        var usuario = await LerAsync(resposta);
        Assert.Equal("Funcionario", usuario.GetProperty("perfil").GetString());
        Assert.Equal("Analista de RH", usuario.GetProperty("cargo").GetString());
        Assert.Equal("Recursos Humanos", usuario.GetProperty("setor").GetString());
        Assert.Equal(ApiFactory.EmailComCargo, usuario.GetProperty("email").GetString());

        var rh = await LerAsync(await EuAsync(cliente, (await SessaoAsync(cliente, ApiFactory.EmailRh)).GetProperty("tokenDeAcesso").GetString()));
        Assert.Equal("RH", rh.GetProperty("perfil").GetString());
    }
}

using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Emails;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// O fluxo de acesso na API do começo ao fim (sem navegador), com tudo de verdade: o RH cadastra, a fila envia o e-mail (em memória), o link vira senha, a pessoa entra, esquece, redefine, é desligada e volta.
/// Os e-mails saem pelo ciclo da fila (<c>ProcessarCicloAsync</c> chamado direto) e o token é lido do LINK DA MENSAGEM, como a pessoa faria: depois do envio o banco já não o tem (a carga fica nula).
/// <b>Se algum passo falhar, o defeito é da tarefa que o implementou (4 a 10), não deste teste.</b>
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FluxoDeAcessoDePontaAPontaTests(MySqlFixture mysql)
{
    private const string SenhaNova = "Outra#Senha456";
    private static int _cpf = 800_000;
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(FluxoDeAcessoDePontaAPontaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        api.Api.Enviador.Limpar();
        return api;
    }

    /// <summary>A mesma API, com o log capturado: o fluxo inteiro passa por ela, então o que a API escreveu em log em qualquer passo está no coletor.</summary>
    private static WebApplicationFactory<Program> ComLog(ApiDeAcesso api, ColetorDeLogs logs) =>
        api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));

    private static HttpClient ComoRh(WebApplicationFactory<Program> fabrica, ApiDeAcesso api)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = api.Api.ClienteComo(api.Elenco.Rh).DefaultRequestHeaders.Authorization;
        return cliente;
    }

    /// <summary>Um ciclo da fila, num escopo novo da mesma API: devolve quantos e-mails saíram.</summary>
    private static async Task<int> CicloDaFilaAsync(WebApplicationFactory<Program> fabrica)
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
    }

    /// <summary>O token do link da mensagem enviada (<c>{UrlDoFront}/definir-senha?token=...</c>), como a pessoa o copiaria do e-mail.</summary>
    private static string TokenDoLink(MensagemDeEmail mensagem)
    {
        var achado = Regex.Match(mensagem.Texto, @"definir-senha\?token=([^\s&""'<>]+)");
        Assert.True(achado.Success, "O e-mail não traz o link com o token.");
        return Uri.UnescapeDataString(achado.Groups[1].Value);
    }

    private static object NovoColaborador(int cargo, int setor, string email, string nome) => new Dictionary<string, object?>
    {
        ["nome"] = nome,
        ["cpf"] = CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf)),
        ["dataNascimento"] = "1995-02-09",
        ["telefone"] = "11991234567",
        ["emailCorporativo"] = email,
        ["dataAdmissao"] = "2026-02-03",
        ["tipoContrato"] = "CLT",
        ["cargoId"] = cargo,
        ["setorId"] = setor,
        ["perfil"] = "Funcionario",
        ["enviarConvite"] = true,
    };

    private static async Task<int> CadastrarComConviteAsync(HttpClient rh, ApiDeAcesso api, string rotulo)
    {
        var n = Interlocked.Increment(ref _sequencia);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo fluxo {rotulo} {n}", 44m);
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor fluxo {rotulo} {n}");
        var resposta = await rh.PostAsJsonAsync("/api/rh/funcionarios", NovoColaborador(cargo, setor, $"fluxo.{rotulo}.{n}@fluxo.com.br", $"Pessoa do fluxo {rotulo} {n}"));
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    [Fact]
    public async Task RH_cadastra_a_pessoa_recebe_o_convite_define_a_senha_e_entra()
    {
        var api = await ApiAsync();
        var logs = new ColetorDeLogs();
        await using var fabrica = ComLog(api, logs);
        var rh = ComoRh(fabrica, api);
        var anonimo = fabrica.CreateClient();

        // 1) O RH cadastra, com convite, e recebe 201.
        var id = await CadastrarComConviteAsync(rh, api, "completo");
        var email = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {id}"))!;

        // 2) O ciclo da fila envia o e-mail CONVITE, e a carga (onde o token está em claro) fica nula.
        Assert.True(await CicloDaFilaAsync(fabrica) >= 1);
        var mensagem = Assert.Single(api.Api.Enviador.Enviadas, m => m.Para == email);
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT CARGA FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {id} AND TIPO = 'CONVITE'"));

        // 3) O token vem do link da mensagem; 4) o link vale e é de CONVITE.
        var token = TokenDoLink(mensagem);
        var validado = await anonimo.PostAsJsonAsync("/api/auth/senha/validar-link", new { token });
        Assert.Equal(200, (int)validado.StatusCode);
        Assert.Equal("CONVITE", (await CorpoAsync(validado)).GetProperty("tipo").GetString());

        // 5) Antes de definir a senha não dá para entrar: o hash provisório é inutilizável (a senha que vai ser escolhida também não entra ainda).
        Assert.Equal(401, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email, senha = SenhaNova })).StatusCode);

        // 6) Definir a senha pelo link: 204. 7) Entrar: 200, com o perfil certo.
        Assert.Equal(204, (int)(await anonimo.PostAsJsonAsync("/api/auth/senha/definir", new { token, novaSenha = SenhaNova })).StatusCode);
        var entrada = await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email, senha = SenhaNova });
        Assert.Equal(200, (int)entrada.StatusCode);
        Assert.Equal("Funcionario", (await CorpoAsync(entrada)).GetProperty("usuario").GetProperty("perfil").GetString());

        // 8) O link não vale mais.
        var usado = await anonimo.PostAsJsonAsync("/api/auth/senha/validar-link", new { token });
        Assert.Equal(422, (int)usado.StatusCode);
        Assert.Equal(CodigoDeLinkInvalido, (await CorpoAsync(usado)).GetProperty("codigo").GetString());

        // 9) Nem o token, nem a senha, nem o hash dela estão em evento de auditoria nem no log capturado.
        var hash = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {id}"))!;
        var auditoria = await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS") + await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_MUDANCAS");
        // Os controles: a varredura só vale se havia o que varrer (o log da API e o evento do cadastro desta pessoa).
        Assert.NotEmpty(logs.Mensagens);
        Assert.Contains("Pessoa do fluxo completo", auditoria);
        string[] segredos = [token, SenhaNova, hash];
        foreach (var segredo in segredos)
        {
            Assert.DoesNotContain(segredo, auditoria);
            Assert.DoesNotContain(segredo, string.Join("\n", logs.Mensagens));
        }
    }

    [Fact]
    public async Task Esqueci_minha_senha_redefine_encerra_as_sessoes_e_a_senha_antiga_deixa_de_valer()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("fluxo-esqueci", login: TipoDeLogin.SenhaDefinida);
        var anonimo = api.Api.ClienteAnonimo();
        var (_, primeiraSessao) = await EntrarAsync(anonimo, pessoa.Email!);
        var (_, segundaSessao) = await EntrarAsync(anonimo, pessoa.Email!);
        Assert.Equal(200, await RenovarAsync(anonimo, primeiraSessao)); // as duas sessões valem

        // Esqueci: 202 sempre; o ciclo envia o REDEFINICAO_SENHA e o link é de redefinição.
        Assert.Equal(202, (int)(await anonimo.PostAsJsonAsync("/api/auth/senha/esqueci", new { email = pessoa.Email })).StatusCode);
        await using var escopo = api.Api.Services.CreateAsyncScope();
        Assert.True(await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None) >= 1);
        var mensagem = Assert.Single(api.Api.Enviador.Enviadas, m => m.Para == pessoa.Email);
        var token = TokenDoLink(mensagem);
        Assert.Equal("REDEFINICAO", (await CorpoAsync(await anonimo.PostAsJsonAsync("/api/auth/senha/validar-link", new { token }))).GetProperty("tipo").GetString());

        // Definir a nova senha: 204; as DUAS sessões antigas deixam de renovar, e a senha antiga deixa de entrar.
        Assert.Equal(204, (int)(await anonimo.PostAsJsonAsync("/api/auth/senha/definir", new { token, novaSenha = SenhaNova })).StatusCode);
        Assert.Equal(401, await RenovarAsync(anonimo, primeiraSessao));
        Assert.Equal(401, await RenovarAsync(anonimo, segundaSessao));
        Assert.Equal(401, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = ApiFactory.SenhaPadrao })).StatusCode);
        Assert.Equal(200, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = SenhaNova })).StatusCode);
        Assert.Equal(422, (int)(await anonimo.PostAsJsonAsync("/api/auth/senha/validar-link", new { token })).StatusCode); // o link também gastou
    }

    [Fact]
    public async Task Inativar_com_ultimo_dia_no_passado_derruba_o_login_na_proxima_renovacao_e_Readmitir_com_convite_traz_a_pessoa_de_volta()
    {
        var api = await ApiAsync();
        var rh = api.Api.ClienteComo(api.Elenco.Rh);
        var anonimo = api.Api.ClienteAnonimo();

        // A pessoa que JÁ definiu a senha: tem sessão, é desligada (último dia ontem: efetiva na hora) e a próxima renovação cai.
        var comSenha = await api.NovaPessoaAsync("fluxo-desliga", login: TipoDeLogin.SenhaDefinida);
        var (_, sessao) = await EntrarAsync(anonimo, comSenha.Email!);
        Assert.Equal(200, await RenovarAsync(anonimo, sessao));
        var desligada = await rh.PostAsJsonAsync($"/api/rh/funcionarios/{comSenha.FuncionarioId}/desligamento", new { ultimoDiaDeTrabalho = "2026-10-06" });
        Assert.Equal(200, (int)desligada.StatusCode);
        var fichaInativa = await CorpoAsync(await rh.GetAsync($"/api/rh/funcionarios/{comSenha.FuncionarioId}"));
        Assert.Equal("INATIVO", fichaInativa.GetProperty("situacao").GetString());
        Assert.Equal(401, await RenovarAsync(anonimo, sessao)); // o login caiu junto com o cadastro
        // Pelo contrato, o usuário inativo com a senha CERTA recebe o 403 informativo; com a senha errada recebe o mesmo 401 de quem não existe.
        Assert.Equal(403, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = comSenha.Email, senha = ApiFactory.SenhaPadrao })).StatusCode);
        Assert.Equal(401, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = comSenha.Email, senha = "Senha#Errada789" })).StatusCode);

        // Quem já definiu a senha não recebe convite na readmissão (422); volta sem convite, com o login ainda desligado, e o RH o libera em "Ativar".
        var corpo = await CorpoDaReadmissaoAsync(api);
        var comConvite = await rh.PostAsJsonAsync($"/api/rh/funcionarios/{comSenha.FuncionarioId}/readmissao", (corpo with { Convite = true }).Json());
        Assert.Equal(422, (int)comConvite.StatusCode);
        Assert.Equal("SENHA_JA_DEFINIDA", (await CorpoAsync(comConvite)).GetProperty("codigo").GetString());
        Assert.Equal(200, (int)(await rh.PostAsJsonAsync($"/api/rh/funcionarios/{comSenha.FuncionarioId}/readmissao", corpo.Json())).StatusCode);
        Assert.Equal(403, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = comSenha.Email, senha = ApiFactory.SenhaPadrao })).StatusCode); // o login não volta sozinho: segue desativado
        Assert.Equal(200, (int)(await rh.PostAsync($"/api/rh/usuarios/{comSenha.FuncionarioId}/ativar", null)).StatusCode);
        Assert.Equal(200, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = comSenha.Email, senha = ApiFactory.SenhaPadrao })).StatusCode); // volta com a senha que já tinha

        // A pessoa que NUNCA definiu a senha (cadastrada com convite, ainda sem aceitar): é desligada, readmitida COM convite e entra pelo link novo.
        var semSenha = await CadastrarComConviteAsync(rh, api, "readmitida");
        var emailDela = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {semSenha}"))!;
        Assert.True(await CicloDaFilaAsync(api.Api) >= 1); // o convite do cadastro sai agora (e o link dele deixa de valer quando o da readmissão for emitido)
        Assert.Equal(200, (int)(await rh.PostAsJsonAsync($"/api/rh/funcionarios/{semSenha}/desligamento", new { ultimoDiaDeTrabalho = "2026-10-06" })).StatusCode);
        api.Api.Enviador.Limpar();
        var readmitida = await rh.PostAsJsonAsync($"/api/rh/funcionarios/{semSenha}/readmissao", ((await CorpoDaReadmissaoAsync(api)) with { Convite = true }).Json());
        Assert.Equal(200, (int)readmitida.StatusCode);
        Assert.True(await CicloDaFilaAsync(api.Api) >= 1);
        var mensagem = Assert.Single(api.Api.Enviador.Enviadas, m => m.Para == emailDela && m.Texto.Contains("definir-senha?token=", StringComparison.Ordinal));
        var token = TokenDoLink(mensagem);
        Assert.Equal(204, (int)(await anonimo.PostAsJsonAsync("/api/auth/senha/definir", new { token, novaSenha = SenhaNova })).StatusCode);
        Assert.Equal(200, (int)(await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = emailDela, senha = SenhaNova })).StatusCode);
    }

    /// <summary>O corpo da readmissão: um cargo e um setor novos, tipo de contrato CLT e admissão em 2026-10-07.</summary>
    private sealed record Readmissao(int Cargo, int Setor, bool Convite = false)
    {
        public object Json() => new Dictionary<string, object?> { ["dataAdmissao"] = "2026-10-07", ["cargoId"] = Cargo, ["setorId"] = Setor, ["tipoContrato"] = "CLT", ["enviarConvite"] = Convite };
    }

    private static async Task<Readmissao> CorpoDaReadmissaoAsync(ApiDeAcesso api)
    {
        var n = Interlocked.Increment(ref _sequencia);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo readmissao fluxo {n}", 44m);
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor readmissao fluxo {n}");
        return new Readmissao(cargo, setor);
    }
}

using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.JsonWebTokens;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// <c>POST /api/eu/senha</c>: a troca de senha de quem está logado, mantendo a sessão atual (a claim <c>rth</c> do token de acesso é o hash do refresh token da sessão) e encerrando as outras.
/// Entra de verdade pelo <c>/api/auth/entrar</c>: cada login cria um refresh token e um token de acesso que leva o <c>rth</c> dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TrocarSenhaTests(MySqlFixture mysql)
{
    private const string NovaSenha = "NovaSenha#2026";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(TrocarSenhaTests));

    private static string HashDoRefresh(string refresh) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(refresh))).ToLowerInvariant();

    private static async Task<HttpResponseMessage> TrocarAsync(HttpClient anonimo, string tokenDeAcesso, string senhaAtual, string novaSenha)
    {
        using var requisicao = new HttpRequestMessage(HttpMethod.Post, "/api/eu/senha") { Content = JsonContent.Create(new { senhaAtual, novaSenha }) };
        requisicao.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", tokenDeAcesso);
        return await anonimo.SendAsync(requisicao);
    }

    [Fact]
    public async Task Trocar_com_a_senha_atual_certa_devolve_204_e_o_login_com_a_nova_funciona_e_o_com_a_antiga_nao()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-ok", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);

        var resposta = await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(string.Empty, await resposta.Content.ReadAsStringAsync());
        await EntrarAsync(cliente, pessoa.Email!, NovaSenha);
        Assert.Equal(401, (int)(await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = ApiFactory.SenhaPadrao })).StatusCode);
    }

    [Fact]
    public async Task Trocar_com_a_senha_atual_errada_devolve_422_SENHA_ATUAL_INCORRETA_e_nada_muda()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-errada", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, refresh) = await EntrarAsync(cliente, pessoa.Email!);
        var hashAntes = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}"))!.ToString();

        var resposta = await TrocarAsync(cliente, acesso, "senha atual errada", NovaSenha);

        Assert.Equal(422, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("Operação recusada.", "A senha atual está incorreta.", "SENHA_ATUAL_INCORRETA"), (corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString(), corpo.GetProperty("codigo").GetString()));
        Assert.Equal(hashAntes, (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}"))!.ToString());
        Assert.Equal(200, await RenovarAsync(cliente, refresh)); // o front não desloga: a sessão continua valendo
    }

    [Fact]
    public async Task Trocar_revoga_os_outros_refresh_tokens_e_mantem_o_da_sessao_atual()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-sessoes", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acessoDoPrimeiro, primeiro) = await EntrarAsync(cliente, pessoa.Email!);
        var (_, segundo) = await EntrarAsync(cliente, pessoa.Email!);

        Assert.Equal(204, (int)(await TrocarAsync(cliente, acessoDoPrimeiro, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);

        Assert.Equal(401, await RenovarAsync(cliente, segundo)); // o do outro aparelho foi encerrado
        Assert.Equal(200, await RenovarAsync(cliente, primeiro)); // o da sessão atual continua renovando
    }

    [Fact]
    public async Task Trocar_com_token_de_acesso_sem_a_claim_rth_revoga_todos_os_refresh_tokens()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-sem-rth", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (_, refresh) = await EntrarAsync(cliente, pessoa.Email!);
        string semRth;
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var usuario = await db.Usuarios.Include(u => u.Funcionario).ThenInclude(f => f.NivelDeAcesso).SingleAsync(u => u.Id == pessoa.UsuarioId);
            semRth = api.Api.Services.GetRequiredService<IEmissorDeTokens>().EmitirAcesso(usuario).Token; // como um token emitido antes desta fatia: sem a claim
        }

        Assert.False(new JsonWebTokenHandler().ReadJsonWebToken(semRth).TryGetClaim("rth", out _));
        Assert.Equal(204, (int)(await TrocarAsync(cliente, semRth, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);

        Assert.Equal(401, await RenovarAsync(cliente, refresh));
    }

    [Fact]
    public async Task Trocar_com_o_rth_de_um_refresh_ja_renovado_revoga_o_refresh_vigente_e_a_sessao_cai_na_proxima_renovacao()
    {
        // LIMITAÇÃO ACEITA (decisão 2 do plano): entrar (R1, acesso A1 com o rth de R1); renovar com R1 (R2, A2); trocar com A1 devolve 204; renovar com R2 devolve 401.
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-rth-velho", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (a1, r1) = await EntrarAsync(cliente, pessoa.Email!);
        var renovado = await CorpoAsync(await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = r1 }));
        var r2 = renovado.GetProperty("refreshToken").GetString()!;

        Assert.Equal(204, (int)(await TrocarAsync(cliente, a1, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);

        Assert.Equal(401, await RenovarAsync(cliente, r2));
    }

    [Fact]
    public async Task Jwt_do_entrar_e_do_renovar_leva_a_claim_rth_igual_ao_hash_do_refresh_emitido_junto()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-claim-rth", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, refresh) = await EntrarAsync(cliente, pessoa.Email!);

        var doEntrar = new JsonWebTokenHandler().ReadJsonWebToken(acesso).GetClaim("rth").Value;
        var renovado = await CorpoAsync(await cliente.PostAsJsonAsync("/api/auth/renovar", new { refreshToken = refresh }));
        var doRenovar = new JsonWebTokenHandler().ReadJsonWebToken(renovado.GetProperty("tokenDeAcesso").GetString()!).GetClaim("rth").Value;

        Assert.Equal(HashDoRefresh(refresh), doEntrar);
        Assert.Equal(HashDoRefresh(renovado.GetProperty("refreshToken").GetString()!), doRenovar);
        Assert.NotEqual(doEntrar, doRenovar);
    }

    [Fact]
    public async Task Trocar_registra_Acesso_Alterou_sem_senha_nem_hash()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-auditoria", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);

        Assert.Equal(204, (int)(await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha)).StatusCode);

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", pessoa.UsuarioId!.Value), e => e.Descricao == "Senha trocada");
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Alterou, pessoa.FuncionarioId, TipoDeAtor.Pessoa), (evento.Area, evento.Acao, evento.FuncionarioAtorId, evento.AtorTipo));
        var hash = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}"))!.ToString()!;
        foreach (var tabela in new[] { "AUDITORIA_EVENTOS", "AUDITORIA_MUDANCAS" })
        {
            var texto = await Sql.TextoDaTabelaAsync(api.Conexao, tabela);
            Assert.DoesNotContain(NovaSenha, texto, StringComparison.Ordinal);
            Assert.DoesNotContain(ApiFactory.SenhaPadrao, texto, StringComparison.Ordinal);
            Assert.DoesNotContain(hash, texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Trocar_para_a_mesma_senha_ou_senha_nova_curta_da_400_em_novaSenha()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-400", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);

        var igual = await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, ApiFactory.SenhaPadrao);
        var curta = await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, "curta");

        Assert.Equal(400, (int)igual.StatusCode);
        Assert.Equal(["A nova senha precisa ser diferente da atual."], (await CorpoAsync(igual)).GetProperty("errors").GetProperty("novaSenha").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(400, (int)curta.StatusCode);
        Assert.True((await CorpoAsync(curta)).GetProperty("errors").TryGetProperty("novaSenha", out _));
        await EntrarAsync(cliente, pessoa.Email!); // a senha não mudou
    }

    [Fact]
    public async Task Anonimo_em_trocar_recebe_401()
    {
        var api = await ApiAsync();

        var resposta = await api.Api.ClienteAnonimo().PostAsJsonAsync("/api/eu/senha", new { senhaAtual = "qualquer", novaSenha = NovaSenha });

        Assert.Equal(401, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Login_com_o_colaborador_inativado_depois_de_entrar_nao_troca_a_senha()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("trocar-inativado", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (acesso, _) = await EntrarAsync(cliente, pessoa.Email!);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");

        var resposta = await TrocarAsync(cliente, acesso, ApiFactory.SenhaPadrao, NovaSenha);

        Assert.Equal(401, (int)resposta.StatusCode); // o token ainda vale por minutos, mas quem não pode mais entrar não troca a senha
    }
}

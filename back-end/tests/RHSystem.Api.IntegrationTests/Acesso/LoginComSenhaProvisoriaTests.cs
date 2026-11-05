using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Acesso;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>Quem recebeu o convite e ainda não definiu a senha não entra, e a API não diz que o convite está pendente.</summary>
[Collection(MySqlCollection.Nome)]
public class LoginComSenhaProvisoriaTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(LoginComSenhaProvisoriaTests));

    private static async Task<(int Status, string Title, string Detail)> EntrarAsync(HttpClient cliente, string email, string senha)
    {
        var resposta = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email, senha });
        var corpo = JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());
        return ((int)resposta.StatusCode, corpo.GetProperty("title").GetString()!, corpo.GetProperty("detail").GetString()!);
    }

    [Fact]
    public async Task Login_de_convite_nao_aceito_devolve_o_mesmo_401_da_senha_errada_e_grava_um_AcessoNegado_por_minuto_com_ip_e_sem_senha()
    {
        var api = await ApiAsync();
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var comConvite = await api.NovaPessoaAsync("convite-pendente");
        await api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([comConvite.FuncionarioId], CancellationToken.None));
        var comSenha = await api.NovaPessoaAsync("senha-definida", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        const string senhaDigitada = "Digitada#999";

        var daSenhaErrada = await EntrarAsync(cliente, comSenha.Email!, senhaDigitada);
        var primeira = await EntrarAsync(cliente, comConvite.Email!, senhaDigitada);
        await EntrarAsync(cliente, comConvite.Email!, senhaDigitada);
        await EntrarAsync(cliente, comConvite.Email!, senhaDigitada);

        Assert.Equal(401, primeira.Status);
        Assert.Equal(daSenhaErrada, primeira); // status, title e detail iguais aos da senha errada de quem já definiu a senha
        var eventos = (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, comConvite.FuncionarioId)).Where(e => e.Acao == AuditoriaAcao.AcessoNegado).ToList();
        var evento = Assert.Single(eventos); // 3 tentativas no mesmo minuto = 1 evento
        Assert.Equal("Tentativa de login com senha incorreta", evento.Descricao);
        Assert.Equal(TipoDeAtor.Pessoa, evento.AtorTipo);
        Assert.False(string.IsNullOrWhiteSpace(evento.Ip)); // o IP de quem tentou
        Assert.DoesNotContain(senhaDigitada, (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto, StringComparison.Ordinal);

        api.Api.Relogio.Avancar(TimeSpan.FromSeconds(61));
        await EntrarAsync(cliente, comConvite.Email!, senhaDigitada); // a quarta, 61 s depois, grava o segundo

        Assert.Equal(2, (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, comConvite.FuncionarioId)).Count(e => e.Acao == AuditoriaAcao.AcessoNegado));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task Login_de_convite_nao_aceito_nao_entra_nem_com_o_token_ou_o_hash_provisorio_como_senha()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("nao-entra");
        await api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([pessoa.FuncionarioId], CancellationToken.None));
        var hash = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"))!.ToString()!;
        var cliente = api.Api.ClienteAnonimo();

        foreach (var tentativa in new[] { hash, "x", "Segredo#123" })
        {
            var resposta = await cliente.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = tentativa });
            Assert.Equal(401, (int)resposta.StatusCode);
        }
    }
}

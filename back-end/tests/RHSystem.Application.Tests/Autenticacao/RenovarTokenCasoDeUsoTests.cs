using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;

namespace RHSystem.Application.Tests.Autenticacao;

public class RenovarTokenCasoDeUsoTests
{
    [Fact]
    public async Task Renovar_devolve_o_nome_do_cargo_e_do_setor_do_funcionario()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario(cargo: "Analista de RH", setor: "Recursos Humanos");
        var refresh = cenario.NovoRefreshDoCliente(usuario);

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.Equal("Analista de RH", sessao!.Usuario.Cargo);
        Assert.Equal("Recursos Humanos", sessao.Usuario.Setor);
        Assert.Equal("ana@empresa.com.br", sessao.Usuario.Email);
    }

    [Fact]
    public async Task Renovar_rotaciona_revoga_o_antigo_e_emite_um_novo_na_mesma_transacao()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var refresh = cenario.NovoRefreshDoCliente(usuario);

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.NotNull(sessao);
        Assert.NotEqual(refresh, sessao.RefreshToken);
        Assert.Equal(2, cenario.Tokens.Todos.Count);
        var antigo = cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(refresh));
        var novo = cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(sessao.RefreshToken));
        Assert.Equal(cenario.Relogio.AgoraUtc, antigo.RevogadoEm);
        Assert.Null(novo.RevogadoEm);
        Assert.Equal(1, cenario.Unidade.Transacoes);
        Assert.True(cenario.Tokens.RevogouDentroDeTransacao);
        Assert.True(cenario.Tokens.AdicionouDentroDeTransacao);
    }

    [Fact]
    public async Task Renovar_com_token_ja_revogado_devolve_null_e_nao_revoga_os_outros_tokens_do_usuario()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var usado = cenario.NovoRefreshDoCliente(usuario, "refresh-ja-usado");
        var outro = cenario.NovoRefreshDoCliente(usuario, "refresh-de-outro-aparelho");
        cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(usado)).Revogar(cenario.Relogio.AgoraUtc);

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(usado), CancellationToken.None);

        Assert.Null(sessao);
        Assert.Null(cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(outro)).RevogadoEm);
    }

    [Fact]
    public async Task Renovar_perdendo_a_corrida_pela_revogacao_devolve_null_sem_emitir_token_novo()
    {
        // Duas abas renovam ao mesmo tempo: só quem de fato revoga (TentarRevogar = true) recebe a sessão nova.
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var refresh = cenario.NovoRefreshDoCliente(usuario);
        var outro = cenario.NovoRefreshDoCliente(usuario, "refresh-de-outro-aparelho");
        cenario.Tokens.SimularPerdaDaCorrida = true;

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.Null(sessao);
        Assert.Equal(2, cenario.Tokens.Todos.Count); // nenhum token novo
        Assert.All(cenario.Tokens.Todos, t => Assert.Null(t.RevogadoEm)); // e os outros tokens do usuário seguem valendo
        Assert.NotNull(outro);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Renovar_para_usuario_ou_funcionario_inativo_devolve_null_e_revoga_todos_os_tokens_do_usuario(bool usuarioInativo, bool funcionarioInativo)
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario(usuarioAtivo: !usuarioInativo, funcionarioAtivo: !funcionarioInativo);
        var refresh = cenario.NovoRefreshDoCliente(usuario);
        cenario.NovoRefreshDoCliente(usuario, "refresh-de-outro-aparelho");

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.Null(sessao);
        Assert.All(cenario.Tokens.Todos, t => Assert.NotNull(t.RevogadoEm));
    }

    [Fact]
    public async Task Renovar_com_token_expirado_devolve_null()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var refresh = cenario.NovoRefreshDoCliente(usuario);
        cenario.Relogio.AgoraUtc = cenario.Relogio.AgoraUtc.AddDays(7).AddSeconds(1);

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.Null(sessao);
    }

    [Fact]
    public async Task Renovar_com_token_desconhecido_devolve_null()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario();

        var sessao = await cenario.Renovar().ExecutarAsync(new RenovarComando("nunca-emitido"), CancellationToken.None);

        Assert.Null(sessao);
    }
}

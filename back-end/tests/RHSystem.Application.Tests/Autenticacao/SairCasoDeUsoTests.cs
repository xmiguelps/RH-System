using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;

namespace RHSystem.Application.Tests.Autenticacao;

public class SairCasoDeUsoTests
{
    [Fact]
    public async Task Sair_revoga_o_refresh_informado()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var refresh = cenario.NovoRefreshDoCliente(usuario);
        var outroAparelho = cenario.NovoRefreshDoCliente(usuario, "refresh-de-outro-aparelho");

        await cenario.Sair().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);

        Assert.Equal(cenario.Relogio.AgoraUtc, cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(refresh)).RevogadoEm);
        Assert.Null(cenario.Tokens.Todos.Single(t => t.TokenHash == cenario.Emissor.HashDoRefresh(outroAparelho)).RevogadoEm); // só este aparelho sai
    }

    [Fact]
    public async Task Sair_com_token_desconhecido_ou_ja_revogado_nao_falha()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var refresh = cenario.NovoRefreshDoCliente(usuario);

        await cenario.Sair().ExecutarAsync(new RenovarComando("nunca-emitido"), CancellationToken.None);
        await cenario.Sair().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None);
        await cenario.Sair().ExecutarAsync(new RenovarComando(refresh), CancellationToken.None); // de novo: continua sem erro

        Assert.NotNull(cenario.Tokens.Todos.Single().RevogadoEm);
    }
}

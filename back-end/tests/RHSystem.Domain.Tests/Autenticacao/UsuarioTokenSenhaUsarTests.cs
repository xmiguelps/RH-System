using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class UsuarioTokenSenhaUsarTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
    private static readonly string Hash = new('a', 64);

    private static UsuarioTokenSenha Novo() => UsuarioTokenSenha.Criar(1, TipoDeTokenDeSenha.Convite, Hash, Agora, TimeSpan.FromDays(7));

    [Fact]
    public void Usar_grava_o_instante_do_uso()
    {
        var token = Novo();

        token.Usar(Agora.AddMinutes(5));

        Assert.Equal(Agora.AddMinutes(5), token.UsadoEm);
        Assert.Null(token.InvalidadoEm);
    }

    [Fact]
    public void Invalidar_grava_o_instante_da_invalidacao()
    {
        var token = Novo();

        token.Invalidar(Agora.AddMinutes(5));

        Assert.Equal(Agora.AddMinutes(5), token.InvalidadoEm);
        Assert.Null(token.UsadoEm);
    }

    [Fact]
    public void Usar_de_quem_ja_foi_usado_ou_invalidado_lanca_InvalidOperationException()
    {
        var usado = Novo();
        usado.Usar(Agora);
        var invalidado = Novo();
        invalidado.Invalidar(Agora);

        Assert.Throws<InvalidOperationException>(() => usado.Usar(Agora.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => invalidado.Usar(Agora.AddMinutes(1)));
        Assert.Equal(Agora, usado.UsadoEm); // o primeiro uso não mudou
        Assert.Null(invalidado.UsadoEm);
    }

    [Fact]
    public void Invalidar_de_quem_ja_foi_usado_ou_invalidado_lanca_InvalidOperationException()
    {
        // O CHECK do banco nunca aceita "usado e invalidado" juntos: o domínio nem tenta.
        var usado = Novo();
        usado.Usar(Agora);
        var invalidado = Novo();
        invalidado.Invalidar(Agora);

        Assert.Throws<InvalidOperationException>(() => usado.Invalidar(Agora.AddMinutes(1)));
        Assert.Throws<InvalidOperationException>(() => invalidado.Invalidar(Agora.AddMinutes(1)));
    }

    [Fact]
    public void Usar_e_Invalidar_exigem_instante_UTC()
    {
        Assert.Throws<ArgumentException>(() => Novo().Usar(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => Novo().Invalidar(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Local)));
    }
}

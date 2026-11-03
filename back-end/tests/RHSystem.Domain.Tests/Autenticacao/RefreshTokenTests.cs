using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class RefreshTokenTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void RefreshToken_novo_esta_ativo_ate_expirar()
    {
        var token = RefreshToken.Criar(1, "hash", Agora, TimeSpan.FromDays(7));

        Assert.True(token.EstaAtivo(Agora));
        Assert.True(token.EstaAtivo(Agora.AddDays(7).AddTicks(-1)));
        Assert.Equal(Agora, token.CriadoEm);
        Assert.Equal(Agora.AddDays(7), token.ExpiraEm);
    }

    [Fact]
    public void RefreshToken_expirado_ou_revogado_nao_esta_ativo()
    {
        var expirado = RefreshToken.Criar(1, "hash", Agora, TimeSpan.FromDays(7));
        Assert.False(expirado.EstaAtivo(Agora.AddDays(7)));
        Assert.False(expirado.EstaAtivo(Agora.AddDays(8)));

        var revogado = RefreshToken.Criar(1, "hash", Agora, TimeSpan.FromDays(7));
        revogado.Revogar(Agora.AddMinutes(1));
        Assert.False(revogado.EstaAtivo(Agora.AddMinutes(2)));
        Assert.Equal(Agora.AddMinutes(1), revogado.RevogadoEm);
    }
}

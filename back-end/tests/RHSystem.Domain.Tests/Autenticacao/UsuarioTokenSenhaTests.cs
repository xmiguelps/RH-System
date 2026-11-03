using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class UsuarioTokenSenhaTests
{
    private static readonly string Hash = new('a', 64);

    [Fact]
    public void UsuarioTokenSenha_Criar_usa_o_instante_recebido_CriadoEm_e_ExpiraEm_e_agora_mais_a_validade()
    {
        var agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

        var token = UsuarioTokenSenha.Criar(7, TipoDeTokenDeSenha.Convite, Hash, agora, TimeSpan.FromHours(48));

        Assert.Equal(7, token.UsuarioId);
        Assert.Equal(TipoDeTokenDeSenha.Convite, token.Tipo);
        Assert.Equal(Hash, token.TokenHash);
        Assert.Equal(agora, token.CriadoEm);
        Assert.Equal(agora.AddHours(48), token.ExpiraEm);
        Assert.Equal(DateTimeKind.Utc, token.CriadoEm.Kind);
        Assert.Null(token.UsadoEm);
        Assert.Null(token.InvalidadoEm);

        // O relógio vem de fora, nunca de DateTime.UtcNow: outro instante dá outros valores.
        var outroInstante = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var outro = UsuarioTokenSenha.Criar(1, TipoDeTokenDeSenha.Redefinicao, Hash, outroInstante, TimeSpan.FromMinutes(30));
        Assert.Equal(outroInstante, outro.CriadoEm);
        Assert.Equal(outroInstante.AddMinutes(30), outro.ExpiraEm);
    }

    [Fact]
    public void UsuarioTokenSenha_Criar_recusa_o_que_o_CHECK_do_banco_recusaria()
    {
        var agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

        // EXPIRA_EM > CRIADO_EM
        Assert.Throws<ArgumentException>(() => UsuarioTokenSenha.Criar(1, TipoDeTokenDeSenha.Convite, Hash, agora, TimeSpan.Zero));
        Assert.Throws<ArgumentException>(() => UsuarioTokenSenha.Criar(1, TipoDeTokenDeSenha.Convite, Hash, agora, TimeSpan.FromMinutes(-1)));

        // TOKEN_HASH REGEXP '^[0-9A-Fa-f]{64}$' (SHA-256 em hexadecimal)
        foreach (var hashInvalido in new[] { "curto", new string('g', 64), new string('a', 65), string.Empty, null! })
        {
            Assert.Throws<ArgumentException>(() => UsuarioTokenSenha.Criar(1, TipoDeTokenDeSenha.Convite, hashInvalido, agora, TimeSpan.FromHours(1)));
        }
    }
}

using System.Reflection;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Autenticacao;

public class EmissorDeTokensJwtTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private sealed class RelogioFixo(DateTime agora) : IRelogio
    {
        public DateTime AgoraUtc { get; } = agora;
    }

    private static OpcoesJwt Opcoes(string? chave = null) =>
        new() { Chave = chave ?? new string('k', 48), Emissor = "rh-system", Audiencia = "rh-system-web" };

    private static Usuario NovoUsuario(string nivel = "RH")
    {
        var funcionario = Funcionario.Criar("José Ação", "jose@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso(nivel));
        Definir(funcionario, nameof(Funcionario.Id), 7);
        var usuario = Usuario.Criar(funcionario, "hash");
        Definir(usuario, nameof(Usuario.Id), 42);
        return usuario;
    }

    private static void Definir<T>(T objeto, string propriedade, object valor)
        where T : class =>
        typeof(T).GetProperty(propriedade, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(objeto, valor);

    [Fact]
    public async Task Jwt_emitido_contem_sub_fid_email_nome_perfil_e_expira_em_15_minutos()
    {
        var opcoes = Opcoes();
        var emissor = new EmissorDeTokensJwt(opcoes, new RelogioFixo(Agora));

        var acesso = emissor.EmitirAcesso(NovoUsuario());

        Assert.Equal(Agora.AddMinutes(15), acesso.ExpiraEmUtc);
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(acesso.Token);
        Assert.Equal("42", jwt.GetClaim("sub").Value);
        Assert.Equal("7", jwt.GetClaim("fid").Value);
        Assert.Equal("jose@empresa.com.br", jwt.GetClaim("email").Value);
        Assert.Equal("José Ação", jwt.GetClaim("nome").Value);
        Assert.Equal("RH", jwt.GetClaim("perfil").Value);
        Assert.Equal(Agora.AddMinutes(15), jwt.ValidTo);
        Assert.Equal("rh-system", jwt.Issuer);
        Assert.Contains("rh-system-web", jwt.Audiences);

        // Valida com a chave certa e recusa com outra: é uma assinatura de verdade, não só um texto com cara de JWT.
        var parametros = new TokenValidationParameters
        {
            ValidIssuer = "rh-system",
            ValidAudience = "rh-system-web",
            IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(opcoes.Chave)),
            ValidateLifetime = false,
        };
        Assert.True((await new JsonWebTokenHandler().ValidateTokenAsync(acesso.Token, parametros)).IsValid);
        parametros.IssuerSigningKey = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(new string('x', 48)));
        Assert.False((await new JsonWebTokenHandler().ValidateTokenAsync(acesso.Token, parametros)).IsValid);
    }

    [Fact]
    public void Jwt_com_perfil_diferente_leva_o_perfil_como_texto()
    {
        var emissor = new EmissorDeTokensJwt(Opcoes(), new RelogioFixo(Agora));

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(emissor.EmitirAcesso(NovoUsuario("Funcionario Gestor")).Token);

        Assert.Equal("Gestor", jwt.GetClaim("perfil").Value);
    }

    [Fact]
    public void Usuario_sem_perfil_configurado_nao_recebe_token()
    {
        var emissor = new EmissorDeTokensJwt(Opcoes(), new RelogioFixo(Agora));

        Assert.Throws<InvalidOperationException>(() => emissor.EmitirAcesso(NovoUsuario("Nivel Desconhecido")));
    }

    [Fact]
    public void Refresh_novo_tem_32_bytes_em_Base64Url_e_o_hash_e_SHA256_em_hexadecimal()
    {
        var emissor = new EmissorDeTokensJwt(Opcoes(), new RelogioFixo(Agora));

        var primeiro = emissor.NovoRefresh();
        var segundo = emissor.NovoRefresh();

        Assert.Equal(43, primeiro.Token.Length); // 32 bytes em Base64Url, sem preenchimento
        Assert.Matches("^[A-Za-z0-9_-]{43}$", primeiro.Token);
        Assert.Equal(64, primeiro.Hash.Length);
        Assert.Matches("^[0-9a-f]{64}$", primeiro.Hash);
        Assert.Equal(primeiro.Hash, emissor.HashDoRefresh(primeiro.Token));
        Assert.NotEqual(primeiro.Token, segundo.Token);
        Assert.NotEqual(primeiro.Hash, segundo.Hash);
        Assert.Equal(TimeSpan.FromDays(7), emissor.ValidadeDoRefresh);
    }

    [Theory]
    [InlineData("curta")]
    [InlineData("")]
    public void Chave_com_menos_de_32_bytes_e_recusada(string chave)
    {
        Assert.Throws<InvalidOperationException>(() => new EmissorDeTokensJwt(Opcoes(chave), new RelogioFixo(Agora)));
    }
}

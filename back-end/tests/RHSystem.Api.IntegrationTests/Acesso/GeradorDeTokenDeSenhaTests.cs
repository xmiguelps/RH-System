using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using RHSystem.Application.Acesso;
using RHSystem.Infrastructure.Acesso;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Acesso;

public partial class GeradorDeTokenDeSenhaTests
{
    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Hexadecimal64Minusculo();

    [Fact]
    public void Token_tem_64_hexadecimais_minusculos_e_dois_tokens_diferem()
    {
        var gerador = new GeradorDeTokenDeSenha();

        var tokens = Enumerable.Range(0, 50).Select(_ => gerador.Novo()).ToList();

        Assert.All(tokens, t => Assert.Matches(Hexadecimal64Minusculo(), t.Token));
        Assert.All(tokens, t => Assert.Matches(Hexadecimal64Minusculo(), t.Hash));
        Assert.Equal(50, tokens.Select(t => t.Token).Distinct().Count());
        Assert.Equal(50, tokens.Select(t => t.Hash).Distinct().Count());
    }

    [Fact]
    public void Hash_e_o_SHA_256_do_texto_do_token_e_nao_e_o_proprio_token()
    {
        var gerador = new GeradorDeTokenDeSenha();
        var gerado = gerador.Novo();

        var esperado = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(gerado.Token))).ToLowerInvariant();

        Assert.Equal(esperado, gerado.Hash);
        Assert.Equal(esperado, gerador.Hash(gerado.Token));
        Assert.NotEqual(gerado.Token, gerado.Hash);
        // Um valor conhecido, para o teste não depender só da mesma conta repetida: SHA-256 de "abc".
        Assert.Equal("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad", gerador.Hash("abc"));
    }

    [Theory]
    [InlineData("qualquer coisa")]
    [InlineData("")]
    [InlineData("Segredo#123")]
    [InlineData("ÿÿÿÿ")]
    public void HashProvisorio_nunca_confere_no_SenhaHasher_real_e_Verificar_nao_lanca(string senha)
    {
        var hasher = new SenhaHasher();

        // Prova a suposição do hash provisório: o PasswordHasher do ASP.NET lê o primeiro byte como o marcador de formato (0x00 e 0x01 são os conhecidos): 0xFF não é nenhum.
        var resultado = Record.Exception(() => hasher.Verificar(HashProvisorio.Gerar(), senha));

        Assert.Null(resultado);
        Assert.False(hasher.Verificar(HashProvisorio.Gerar(), senha));
    }
}

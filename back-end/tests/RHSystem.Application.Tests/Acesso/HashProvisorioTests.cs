using RHSystem.Application.Acesso;

namespace RHSystem.Application.Tests.Acesso;

public class HashProvisorioTests
{
    [Fact]
    public void HashProvisorio_tem_40_ou_mais_caracteres_difere_a_cada_chamada_e_comeca_pelo_byte_0xFF()
    {
        var hashes = Enumerable.Range(0, 20).Select(_ => HashProvisorio.Gerar()).ToList();

        Assert.All(hashes, h => Assert.True(h.Length >= 40, $"só {h.Length} caracteres (o trigger de USUARIOS exige 40)"));
        Assert.Equal(20, hashes.Distinct().Count());
        // O hash é o Base64 dos bytes; o primeiro byte, 0xFF, não é nenhum marcador de formato do PasswordHasher (0x00 e 0x01): nunca confere com senha alguma.
        Assert.All(hashes, h => Assert.Equal(0xFF, Convert.FromBase64String(h)[0]));
    }

    [Fact]
    public void HashProvisorio_e_so_ASCII_e_cabe_na_coluna_de_senha()
    {
        var hash = HashProvisorio.Gerar();

        Assert.All(hash, c => Assert.True(c < 128));
        Assert.True(hash.Length <= 255); // USUARIOS.SENHA é varchar(255)
    }
}

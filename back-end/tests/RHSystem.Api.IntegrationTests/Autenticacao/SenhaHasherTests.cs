using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Autenticacao;

public class SenhaHasherTests
{
    private readonly SenhaHasher _hasher = new();

    [Fact]
    public void Hash_verifica_senha_correta_e_rejeita_errada()
    {
        var hash = _hasher.Gerar("Segredo#123");

        Assert.True(_hasher.Verificar(hash, "Segredo#123"));
        Assert.False(_hasher.Verificar(hash, "segredo#123"));
        Assert.False(_hasher.Verificar(hash, "Segredo#1234"));
        Assert.NotEqual(hash, _hasher.Gerar("Segredo#123")); // sal aleatório: o mesmo texto nunca gera o mesmo hash
    }

    [Fact]
    public void Hash_gerado_tem_pelo_menos_40_caracteres()
    {
        // O trigger de USUARIOS recusa senha com menos de 40 caracteres ("armazene a senha como hash").
        Assert.True(_hasher.Gerar("a").Length >= 40);
        Assert.True(_hasher.Gerar(new string('x', 128)).Length >= 40);
    }

    [Fact]
    public void Senha_com_acentos_e_emoji_gera_e_verifica()
    {
        const string senha = "Sênha-ção-😀-#1";

        var hash = _hasher.Gerar(senha);

        Assert.True(_hasher.Verificar(hash, senha));
        Assert.False(_hasher.Verificar(hash, "Senha-cao-😀-#1"));
    }

    [Fact]
    public void Senha_com_mais_de_128_caracteres_lanca_ArgumentException()
    {
        Assert.NotEmpty(_hasher.Gerar(new string('x', 128)));
        Assert.Throws<ArgumentException>(() => _hasher.Gerar(new string('x', 129)));
    }

    [Fact]
    public void Verificar_com_hash_invalido_ou_senha_gigante_devolve_false_sem_lancar()
    {
        var hash = _hasher.Gerar("Segredo#123");

        Assert.False(_hasher.Verificar("isto-nao-e-um-hash", "Segredo#123"));
        Assert.False(_hasher.Verificar(string.Empty, "Segredo#123"));
        Assert.False(_hasher.Verificar(hash, new string('x', 5000)));
    }
}

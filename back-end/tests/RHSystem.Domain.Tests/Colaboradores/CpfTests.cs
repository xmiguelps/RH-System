using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Tests.Colaboradores;

public class CpfTests
{
    [Fact]
    public void Cpf_Normalizar_tira_pontos_e_traco()
    {
        Assert.Equal("52998224725", Cpf.Normalizar("529.982.247-25"));
        Assert.Equal("52998224725", Cpf.Normalizar("  529 982 247 25 "));
        Assert.Equal(string.Empty, Cpf.Normalizar(null));
        Assert.Equal(string.Empty, Cpf.Normalizar("sem digitos"));
    }

    [Fact]
    public void Cpf_Normalizar_so_conta_digitos_ASCII()
    {
        // O banco confere ^[0-9]{11}$: um dígito de outro alfabeto (aqui o árabe-índico) não pode passar por "dígito".
        Assert.Equal("529", Cpf.Normalizar("529٢٣"));
    }

    [Theory]
    [InlineData("52998224725", true)]
    [InlineData("11144477735", true)]
    [InlineData("12345678900", false)] // dígitos verificadores errados
    [InlineData("11111111111", false)] // todos iguais (a conta fecharia, o banco recusa)
    [InlineData("5299822472", false)] // 10 dígitos
    [InlineData("529982247250", false)] // 12 dígitos
    [InlineData("5299822472a", false)] // letra
    [InlineData("529.982.247-25", false)] // EhValido espera só os dígitos: quem recebe texto chama Normalizar antes
    [InlineData("", false)]
    public void Cpf_EhValido_aceita_52998224725_e_11144477735_e_recusa_12345678900_11111111111_10_digitos_e_letras(string cpf, bool esperado) =>
        Assert.Equal(esperado, Cpf.EhValido(cpf));

    [Fact]
    public void Cpf_EhValido_recusa_cada_digito_verificador_trocado()
    {
        // 52998224725: troca só o 10º e depois só o 11º dígito. A conta tem de olhar os dois.
        Assert.False(Cpf.EhValido("52998224715"));
        Assert.False(Cpf.EhValido("52998224724"));
    }
}

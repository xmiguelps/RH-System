using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

public class TelefoneMascaradoTests
{
    [Fact]
    public void Mascarar_11_digitos_da_DDD_o_primeiro_digito_e_os_4_ultimos() =>
        Assert.Equal("(11) 9****-7783", TelefoneMascarado.Mascarar("11987657783"));

    [Fact]
    public void Mascarar_10_digitos_da_DDD_e_os_4_ultimos() =>
        Assert.Equal("(11) ****-7783", TelefoneMascarado.Mascarar("1133447783"));

    [Theory]
    [InlineData("(11) 98765-7783", "(11) 9****-7783")]
    [InlineData("11 98765 7783", "(11) 9****-7783")]
    [InlineData("(11) 3344-7783", "(11) ****-7783")]
    [InlineData(null, null)]
    [InlineData("", null)]
    [InlineData("   ", null)]
    [InlineData("123", null)]
    [InlineData("551198765778312", null)] // tamanho estranho: não adivinha onde fica o DDD
    public void Mascarar_aceita_telefone_ja_com_mascara_e_devolve_null_para_nulo_vazio_ou_tamanho_estranho(string? entrada, string? esperado) =>
        Assert.Equal(esperado, TelefoneMascarado.Mascarar(entrada));

    [Fact]
    public void Mascarar_nunca_deixa_o_meio_do_numero_passar()
    {
        var mascarado = TelefoneMascarado.Mascarar("11987654410")!;

        Assert.DoesNotContain("8765", mascarado, StringComparison.Ordinal);
        Assert.DoesNotContain("87654", mascarado, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("(11) 98765-4410", "11987654410")]
    [InlineData("+55 11 98765-4410", "5511987654410")]
    [InlineData("abc", "")]
    [InlineData(null, null)]
    public void SoDigitos_tira_tudo_que_nao_e_digito(string? entrada, string? esperado) =>
        Assert.Equal(esperado, TelefoneMascarado.SoDigitos(entrada));
}

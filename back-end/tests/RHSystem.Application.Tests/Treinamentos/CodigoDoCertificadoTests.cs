using RHSystem.Application.Treinamentos.Regras;

namespace RHSystem.Application.Tests.Treinamentos;

/// <summary>
/// O código de validação do certificado: só UUID versão 4 em minúsculas vale (o v1 que o trigger do banco gera é previsível e nunca sai em resposta, PDF nem conferência); <see cref="CodigoDoCertificado.Novo"/> sempre gera um v4.
/// </summary>
public class CodigoDoCertificadoTests
{
    [Theory]
    [InlineData("a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b57")]
    [InlineData("00000000-0000-4000-8000-000000000000")]
    [InlineData("ffffffff-ffff-4fff-bfff-ffffffffffff")]
    [InlineData("12345678-1234-4234-9234-123456789abc")]
    [InlineData("12345678-1234-4234-a234-123456789abc")]
    public void Aceita_o_uuid_versao_4_em_minusculas(string codigo) => Assert.True(CodigoDoCertificado.EhVersao4(codigo));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b5")] // 35 caracteres
    [InlineData("a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b577")] // 37 caracteres
    [InlineData("a7f3c1d25e9b4c68b2d18f04e6a93b57")] // sem hífens
    [InlineData("A7F3C1D2-5E9B-4C68-B2D1-8F04E6A93B57")] // maiúsculas: o código gravado é sempre minúsculo
    [InlineData("a7f3c1d2-5e9b-1c68-b2d1-8f04e6a93b57")] // versão 1 (o do trigger do banco)
    [InlineData("a7f3c1d2-5e9b-5c68-b2d1-8f04e6a93b57")] // versão 5
    [InlineData("a7f3c1d2-5e9b-4c68-c2d1-8f04e6a93b57")] // variante errada
    [InlineData("a7f3c1d2-5e9b-4c68-72d1-8f04e6a93b57")] // variante errada
    [InlineData("a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b5g")] // fora de hexadecimal
    [InlineData("a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b5 ")] // espaço no fim
    public void Recusa_o_que_nao_e_uuid_versao_4_em_minusculas(string? codigo) => Assert.False(CodigoDoCertificado.EhVersao4(codigo));

    [Fact]
    public void Cada_codigo_novo_e_um_uuid_versao_4_diferente()
    {
        var codigos = Enumerable.Range(0, 50).Select(_ => CodigoDoCertificado.Novo()).ToList();

        Assert.All(codigos, codigo => Assert.True(CodigoDoCertificado.EhVersao4(codigo)));
        Assert.Equal(50, codigos.Distinct().Count());
    }
}

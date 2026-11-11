using RHSystem.Application.Importacoes;

namespace RHSystem.Application.Tests.Importacoes;

public class NomesNormalizadosTests
{
    [Theory]
    [InlineData("Estágio", "ESTAGIO")]
    [InlineData("  Analista de RH  ", "ANALISTA DE RH")]
    [InlineData("\tVale-Refeição\n", "VALE-REFEICAO")]
    [InlineData("NÍVEL DE ACESSO", "NIVEL DE ACESSO")]
    [InlineData("ação", "ACAO")]
    [InlineData("   ", "")]
    [InlineData(null, "")]
    public void Chave_apara_tira_acentos_e_ignora_maiusculas(string? entrada, string esperado) =>
        Assert.Equal(esperado, NomesNormalizados.Chave(entrada));

    [Fact]
    public void Chave_nao_mexe_nos_espacos_do_meio()
    {
        Assert.Equal("VALE  REFEICAO", NomesNormalizados.Chave("Vale  Refeição"));
    }

    [Fact]
    public void Nomes_que_so_diferem_em_acento_caixa_ou_espaco_nas_pontas_tem_a_mesma_chave()
    {
        Assert.Equal(NomesNormalizados.Chave("Estagiario"), NomesNormalizados.Chave(" ESTAGIÁRIO "));
        Assert.NotEqual(NomesNormalizados.Chave("Estagiário"), NomesNormalizados.Chave("Estagiários"));
    }
}

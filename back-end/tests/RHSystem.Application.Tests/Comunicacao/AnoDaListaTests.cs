using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>O filtro <c>ano</c> das listas da comunicação (holerites e advertências): de 2000 a 2100, os dois extremos valem.</summary>
public class AnoDaListaTests
{
    [Theory]
    [InlineData(2000)]
    [InlineData(2026)]
    [InlineData(2100)]
    public void Ano_de_2000_a_2100_e_aceito(int ano) => AnoDaLista.Conferir(ano);

    [Fact]
    public void Sem_ano_nao_ha_o_que_conferir() => AnoDaLista.Conferir(null);

    [Theory]
    [InlineData(1999)]
    [InlineData(2101)]
    [InlineData(0)]
    [InlineData(-2026)]
    [InlineData(int.MaxValue)]
    public void Ano_fora_de_2000_a_2100_e_400_com_o_erro_em_ano(int ano)
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => AnoDaLista.Conferir(ano));

        Assert.Equal(["ano"], erro.Erros.Keys);
        Assert.Equal(["Informe um ano de 2000 a 2100."], erro.Erros["ano"]);
    }
}

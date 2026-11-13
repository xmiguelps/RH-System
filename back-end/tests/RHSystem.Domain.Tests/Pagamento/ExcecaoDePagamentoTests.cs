using RHSystem.Domain.Pagamento;

namespace RHSystem.Domain.Tests.Pagamento;

public class ExcecaoDePagamentoTests
{
    [Theory]
    [InlineData(2020, 1, "2020-01-31")]
    [InlineData(2100, 12, "2100-12-01")]
    [InlineData(2031, 1, "2031-01-15")]
    [InlineData(2031, 12, "2031-12-18")]
    public void Os_limites_do_ano_e_do_mes_valem(int ano, int mes, string data)
    {
        var excecao = ExcecaoDePagamento.Criar(ano, mes, DateOnly.Parse(data));

        Assert.Equal((ano, mes, DateOnly.Parse(data)), (excecao.Ano, excecao.Mes, excecao.DataDoPagamento));
    }

    [Theory]
    [InlineData(2019, 12, "2019-12-18", "ano")]
    [InlineData(2101, 1, "2101-01-18", "ano")]
    [InlineData(2031, 0, "2031-01-18", "mes")]
    [InlineData(2031, 13, "2031-12-18", "mes")]
    public void Ano_ou_mes_fora_da_faixa_e_recusado_com_o_nome_do_parametro(int ano, int mes, string data, string parametro)
    {
        var erro = Assert.Throws<ArgumentOutOfRangeException>(() => ExcecaoDePagamento.Criar(ano, mes, DateOnly.Parse(data)));

        Assert.Equal(parametro, erro.ParamName);
    }

    [Theory]
    [InlineData("2031-11-30")] // o mês antes
    [InlineData("2032-12-18")] // o mesmo mês, outro ano
    [InlineData("2031-01-18")] // o mesmo ano, outro mês
    public void A_data_tem_de_cair_no_mes_e_no_ano_da_excecao(string data)
    {
        var erro = Assert.Throws<ArgumentOutOfRangeException>(() => ExcecaoDePagamento.Criar(2031, 12, DateOnly.Parse(data)));

        Assert.Equal("data", erro.ParamName);
    }

    [Fact]
    public void Substituir_troca_a_data_dentro_do_mes_e_recusa_fora_dele()
    {
        var excecao = ExcecaoDePagamento.Criar(2031, 12, new DateOnly(2031, 12, 18));

        excecao.Substituir(new DateOnly(2031, 12, 5));

        Assert.Equal(new DateOnly(2031, 12, 5), excecao.DataDoPagamento);
        Assert.Throws<ArgumentOutOfRangeException>(() => excecao.Substituir(new DateOnly(2032, 12, 5)));
        Assert.Throws<ArgumentOutOfRangeException>(() => excecao.Substituir(new DateOnly(2031, 11, 5)));
        Assert.Equal(new DateOnly(2031, 12, 5), excecao.DataDoPagamento);
    }
}

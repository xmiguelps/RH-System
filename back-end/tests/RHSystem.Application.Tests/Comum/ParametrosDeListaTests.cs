using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Tests.Comum;

public class ParametrosDeListaTests
{
    [Fact]
    public void Padroes_sao_pagina_1_e_tamanho_20()
    {
        var p = new ParametrosDeLista().Validado();

        Assert.Equal(1, p.Pagina);
        Assert.Equal(20, p.TamanhoPagina);
        Assert.Null(p.Busca);
        Assert.Null(p.OrdenarPor);
        Assert.Null(p.Direcao);
        Assert.Equal(0, p.Pular);
    }

    [Theory]
    [InlineData(0, 20, "pagina")]
    [InlineData(-3, 20, "pagina")]
    [InlineData(1, 0, "tamanhoPagina")]
    [InlineData(1, 101, "tamanhoPagina")]
    [InlineData(1, -1, "tamanhoPagina")]
    public void Pagina_menor_que_1_e_tamanho_0_ou_101_lancam_ErroDeValidacao_com_o_nome_do_campo(int pagina, int tamanho, string campo)
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => new ParametrosDeLista(pagina, tamanho).Validado());

        Assert.Equal([campo], erro.Erros.Keys);
    }

    [Fact]
    public void Pagina_e_tamanho_invalidos_juntos_aparecem_juntos_no_mesmo_erro()
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => new ParametrosDeLista(0, 500).Validado());

        Assert.Equal(["pagina", "tamanhoPagina"], erro.Erros.Keys.Order());
    }

    [Theory]
    [InlineData(1, 100)]
    [InlineData(2_000_000, 1)]
    public void Limites_de_pagina_e_de_tamanho_sao_aceitos(int pagina, int tamanho)
    {
        var p = new ParametrosDeLista(pagina, tamanho).Validado();

        Assert.Equal(pagina, p.Pagina);
        Assert.Equal(tamanho, p.TamanhoPagina);
    }

    [Fact]
    public void Busca_acima_de_100_caracteres_lanca_e_busca_so_com_espacos_vira_null()
    {
        var longa = Assert.Throws<ErroDeValidacaoException>(() => new ParametrosDeLista(Busca: new string('a', 101)).Validado());
        Assert.Equal(["busca"], longa.Erros.Keys);

        Assert.Null(new ParametrosDeLista(Busca: "   ").Validado().Busca);
        Assert.Null(new ParametrosDeLista(Busca: string.Empty).Validado().Busca);
        Assert.Equal("ana", new ParametrosDeLista(Busca: "  ana  ").Validado().Busca);
        Assert.Equal(100, new ParametrosDeLista(Busca: new string('a', 100)).Validado().Busca!.Length);
    }

    [Fact]
    public void Busca_de_100_caracteres_com_espacos_nas_pontas_vale_pelo_texto_aparado()
    {
        var p = new ParametrosDeLista(Busca: " " + new string('a', 100) + " ").Validado();

        Assert.Equal(100, p.Busca!.Length);
    }

    [Fact]
    public void Pular_e_a_pagina_anterior_vezes_o_tamanho_e_nao_estoura_para_pagina_gigante()
    {
        Assert.Equal(40, new ParametrosDeLista(3, 20).Pular);

        // (int.MaxValue - 1) * 100 não cabe em int: sem o cuidado daria um número negativo e o Skip do EF lançaria.
        Assert.Equal(int.MaxValue, new ParametrosDeLista(int.MaxValue, 100).Pular);
    }

    [Theory]
    [InlineData(25, 10, 3)]
    [InlineData(20, 20, 1)]
    [InlineData(21, 20, 2)]
    [InlineData(1, 20, 1)]
    [InlineData(0, 20, 0)]
    public void RespostaPaginada_De_calcula_TotalPaginas_pelo_teto_e_zero_quando_nao_ha_itens(int total, int tamanho, int totalPaginas)
    {
        var resposta = RespostaPaginada<string>.De([], total, new ParametrosDeLista(2, tamanho));

        Assert.Equal(total, resposta.Total);
        Assert.Equal(totalPaginas, resposta.TotalPaginas);
        Assert.Equal(2, resposta.Pagina);
        Assert.Equal(tamanho, resposta.TamanhoPagina);
        Assert.Empty(resposta.Itens);
    }
}

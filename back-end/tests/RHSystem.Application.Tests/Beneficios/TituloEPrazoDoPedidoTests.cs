using RHSystem.Application.Beneficios.Adesoes.Regras;

namespace RHSystem.Application.Tests.Beneficios;

/// <summary>O texto pronto de "Seus pedidos" (as três preposições fixas) e o prazo de 5 dias úteis do RH, com os três exemplos do contrato.</summary>
public class TituloEPrazoDoPedidoTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto) => new(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

    private static readonly IReadOnlySet<DateOnly> SemFeriados = new HashSet<DateOnly>();

    [Theory]
    [InlineData(TipoDoPedidoDeBeneficio.Adesao, "Plano odontológico", "Adesão ao plano odontológico")]
    [InlineData(TipoDoPedidoDeBeneficio.Dependente, "Plano de saúde", "Inclusão de dependente no plano de saúde")]
    [InlineData(TipoDoPedidoDeBeneficio.Trajeto, "Vale-transporte", "Atualização do trajeto do vale-transporte")]
    public void Os_tres_titulos_do_contrato(TipoDoPedidoDeBeneficio tipo, string nome, string esperado)
    {
        Assert.Equal(esperado, TituloDoPedido.De(tipo, nome));
    }

    [Fact]
    public void So_a_primeira_letra_do_nome_vira_minuscula_e_as_preposicoes_nao_mudam_com_o_genero()
    {
        Assert.Equal("Adesão ao auxílio-Creche", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Adesao, "Auxílio-Creche"));
        Assert.Equal("Adesão ao água mineral", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Adesao, "Água mineral"));
        Assert.Equal("Inclusão de dependente no seguro de vida", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Dependente, "Seguro de vida"));
    }

    [Fact]
    public void Uma_sigla_no_inicio_do_nome_fica_como_foi_escrita_e_os_espacos_das_pontas_saem()
    {
        Assert.Equal("Adesão ao VR", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Adesao, "VR"));
        Assert.Equal("Atualização do trajeto do VT urbano", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Trajeto, "  VT urbano  "));
        Assert.Equal("Adesão ao x", TituloDoPedido.De(TipoDoPedidoDeBeneficio.Adesao, " X "));
    }

    [Fact]
    public void Um_tipo_que_nao_existe_e_recusado()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TituloDoPedido.De((TipoDoPedidoDeBeneficio)99, "Plano"));
    }

    [Fact]
    public void Exemplo_do_contrato_pedido_numa_quarta_com_o_feriado_de_segunda_vale_ate_quinta_da_semana_seguinte()
    {
        var prazo = RespostaPrevista.De(Utc(2026, 10, 7, 12, 41), new HashSet<DateOnly> { D(2026, 10, 12) });

        Assert.Equal(D(2026, 10, 15), prazo);
    }

    [Fact]
    public void Exemplo_do_contrato_pedido_numa_segunda_vale_ate_a_segunda_seguinte()
    {
        Assert.Equal(D(2026, 1, 19), RespostaPrevista.De(Utc(2026, 1, 12, 13, 0), SemFeriados));
    }

    [Fact]
    public void Exemplo_do_contrato_pedido_num_domingo_conta_a_partir_da_segunda()
    {
        Assert.Equal(D(2025, 8, 8), RespostaPrevista.De(Utc(2025, 8, 3, 12, 0), SemFeriados));
    }

    [Fact]
    public void Pedido_na_sexta_pula_o_fim_de_semana()
    {
        Assert.Equal(D(2026, 10, 16), RespostaPrevista.De(Utc(2026, 10, 9, 15, 0), SemFeriados));
    }

    [Fact]
    public void Dois_feriados_de_semana_empurram_o_prazo_dois_dias()
    {
        var feriados = new HashSet<DateOnly> { D(2026, 10, 8), D(2026, 10, 9) };

        Assert.Equal(D(2026, 10, 16), RespostaPrevista.De(Utc(2026, 10, 7, 12, 41), feriados));
    }

    [Fact]
    public void Feriado_num_sabado_nao_muda_nada()
    {
        Assert.Equal(D(2026, 10, 14), RespostaPrevista.De(Utc(2026, 10, 7, 12, 41), new HashSet<DateOnly> { D(2026, 10, 10) }));
    }

    [Fact]
    public void A_conta_parte_do_dia_em_menos_tres_horas_e_nao_do_dia_em_UTC()
    {
        // 02:30Z de segunda 12/10 ainda é domingo 11/10 às 23:30 no Brasil: o 1º dia útil é a própria segunda.
        Assert.Equal(D(2026, 10, 16), RespostaPrevista.De(Utc(2026, 10, 12, 2, 30), SemFeriados));
    }

    [Fact]
    public void A_janela_de_feriados_vai_do_dia_do_pedido_em_menos_tres_horas_a_21_dias_depois()
    {
        Assert.Equal((D(2026, 10, 7), D(2026, 10, 28)), RespostaPrevista.JanelaDeFeriados(Utc(2026, 10, 7, 12, 41)));
        Assert.Equal((D(2026, 10, 11), D(2026, 11, 1)), RespostaPrevista.JanelaDeFeriados(Utc(2026, 10, 12, 2, 30)));
    }
}

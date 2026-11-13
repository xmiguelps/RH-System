using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>
/// As bordas que a mutação do incremento 7a mostrou sem teste: um dia só de gozo, a parte de 14 dias com exatamente 14 sobrando, o período que termina no dia em que o pedido começa, o período com exatamente os dias pedidos, o conflito que termina no dia em que o pedido começa e os limites
/// do corpo (anos 2000 e 2100, abono 10, texto de 500).
/// </summary>
public class BordasDasFeriasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateOnly P25Inicio = new(2025, 3, 14);
    private static readonly DateOnly P25Fim = new(2026, 3, 13);
    private static readonly HashSet<DateOnly> Feriados = [new(2026, 11, 15), new(2026, 11, 20), new(2026, 12, 25)];

    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static FeriasParaSaldo No2025(DateOnly inicio, DateOnly fim) => new(P25Inicio, P25Fim, inicio, fim, 0, StatusDasFerias.Concluida, null, null);

    private static FeriasParaSaldo Dias(int dias) => No2025(D(2026, 1, 5), D(2026, 1, 5).AddDays(dias - 1));

    private static ContextoDaConferencia Contexto25(List<FeriasParaSaldo> ferias) => new(Hoje, ModoDaConferencia.Pessoa, SaldoDeFerias.De(P25Inicio, null, Hoje, ferias), ferias, Feriados.Contains);

    private static ComandoDeFerias Pedido(DateOnly inicio, DateOnly fim) => new(inicio, fim, false, 0);

    // ---------------------------------------------------------------- a conferência

    [Fact]
    public void Um_dia_so_de_gozo_conta_1_dia_e_ainda_tem_a_data_limite_do_pagamento()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 16)), Contexto25([]));

        Assert.Equal((1, (DateOnly?)D(2026, 11, 14)), (c.TotalDias, c.PagamentoAte));
        Assert.False(c.Regras.Single(r => r.Regra == RegraDeFerias.DuracaoDaParte).Atendida);
    }

    [Fact]
    public void Fim_antes_do_inicio_nao_conta_dias_nem_data_de_pagamento()
    {
        var c = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 15)), Contexto25([]));

        Assert.Equal((0, (DateOnly?)null), (c.TotalDias, c.PagamentoAte));
    }

    [Fact]
    public void A_parte_de_14_dias_vale_quando_a_primeira_parte_deixa_exatamente_14_e_nao_vale_com_13()
    {
        // período de 30 dias, uma parte de 3 já tirada (M = 3, P = 1), este pedido de 13 dias: sobram 30 - 3 - 13 = 14 e ainda cabe uma 3ª parte
        var comExatos14 = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 28)), Contexto25([Dias(3)]));
        var com13 = ConferenciaDeFerias.Conferir(Pedido(D(2026, 11, 16), D(2026, 11, 28)), Contexto25([Dias(4)]));

        Assert.Equal((14, true), (comExatos14.DiasQueSobram, comExatos14.Regras.Single(r => r.Regra == RegraDeFerias.ParteDe14Dias).Atendida));
        Assert.Equal((13, false), (com13.DiasQueSobram, com13.Regras.Single(r => r.Regra == RegraDeFerias.ParteDe14Dias).Atendida));
    }

    // ---------------------------------------------------------------- a escolha do período

    [Fact]
    public void O_periodo_so_serve_se_terminar_ANTES_do_inicio_do_pedido()
    {
        var saldo = SaldoDeFerias.De(P25Inicio, null, Hoje, []);

        Assert.Null(EscolhaDoPeriodo.Escolher(saldo, P25Fim, 5, null).Escolhido);
        Assert.Equal(P25Inicio, EscolhaDoPeriodo.Escolher(saldo, P25Fim.AddDays(1), 5, null).Escolhido!.Inicio);
    }

    [Fact]
    public void O_periodo_com_exatamente_os_dias_pedidos_serve_e_com_um_dia_a_menos_nao()
    {
        var saldo = SaldoDeFerias.De(P25Inicio, null, Hoje, []);

        Assert.Equal(P25Inicio, EscolhaDoPeriodo.Escolher(saldo, D(2026, 11, 16), 30, null).Escolhido!.Inicio);
        Assert.Null(EscolhaDoPeriodo.Escolher(saldo, D(2026, 11, 16), 31, null).Escolhido);
    }

    // ---------------------------------------------------------------- os conflitos

    [Fact]
    public void Ferias_aprovadas_que_terminam_no_dia_em_que_o_pedido_comeca_coincidem_em_um_dia()
    {
        FuncionarioResumo Pessoa(int id) => new(id, $"Pessoa {id}", true, "Cargo", "Setor");
        IReadOnlyList<MembroDoSetor> setor = [new(1, Pessoa(1), D(2022, 3, 14)), new(2, Pessoa(2), D(2022, 3, 14))];

        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, setor, [new FeriasAprovadaDoSetor(2, D(2026, 11, 10), D(2026, 11, 16))]);

        var conflito = Assert.Single(r.Conflitos);
        Assert.Equal((2, 1), (conflito.Funcionario.Id, conflito.DiasEmComum));
    }

    // ---------------------------------------------------------------- o corpo

    [Fact]
    public void Os_limites_do_corpo_valem_anos_2000_e_2100_um_dia_so_abono_10_e_texto_de_500()
    {
        ValidacaoDeFerias.Validar(D(2000, 1, 1), D(2000, 1, 10), 0, null, "mensagem");
        ValidacaoDeFerias.Validar(D(2100, 12, 1), D(2100, 12, 31), 10, new string('x', 500), "mensagem");
        ValidacaoDeFerias.Validar(D(2026, 11, 16), D(2026, 11, 16), 0, " " + new string('x', 500) + " ", "mensagem");
    }

    [Theory]
    [InlineData(1999, 12, 31, 2000, 1, 5, 0, 0, "dataInicio")]
    [InlineData(2101, 1, 1, 2101, 1, 5, 0, 0, "dataInicio")]
    [InlineData(2000, 1, 1, 1999, 12, 31, 0, 0, "dataFim")]
    [InlineData(2100, 12, 1, 2101, 1, 5, 0, 0, "dataFim")]
    [InlineData(2026, 11, 16, 2026, 11, 15, 0, 0, "dataFim")]
    [InlineData(2026, 11, 16, 2026, 11, 30, 11, 0, "diasAbono")]
    [InlineData(2026, 11, 16, 2026, 11, 30, -1, 0, "diasAbono")]
    [InlineData(2026, 11, 16, 2026, 11, 30, 0, 501, "mensagem")]
    public void Fora_dos_limites_o_corpo_e_recusado_no_campo_certo(int ai, int mi, int di, int af, int mf, int df, int abono, int tamanhoDoTexto, string campo)
    {
        var texto = tamanhoDoTexto == 0 ? null : new string('x', tamanhoDoTexto);

        var erro = Assert.Throws<ErroDeValidacaoException>(() => ValidacaoDeFerias.Validar(D(ai, mi, di), D(af, mf, df), abono, texto, "mensagem"));

        Assert.Contains(campo, erro.Erros.Keys);
    }
}

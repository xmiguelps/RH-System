using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>
/// O que a consolidação lança para um dia que já passou: o saldo do dia (horas menos a carga, em minutos) vira CRÉDITO "Hora extra" ou DÉBITO ("Falta" no dia útil sem batida, "Saldo negativo do dia" nos outros), saldo zero apaga a linha, e o dia que ainda não se resolve (hoje,
/// sequência quebrada, pedido em análise) não mexe em nada. A carga do dia é de 8h48 (528 min, a de um cargo de 44 horas por semana); hoje é 07/10/2026 (quarta).
/// </summary>
public class RegraDoLancamentoDoDiaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateOnly SegundaFeira = new(2026, 10, 5);
    private static readonly DateOnly Sabado = new(2026, 10, 3);

    private static BatidaLida Batida(int id, TipoDeMarcacao tipo, DateOnly dia, int hora, int minuto = 0) =>
        new(id, new DateTime(dia.Year, dia.Month, dia.Day, hora, minuto, 0, DateTimeKind.Utc), tipo, OrigemDaMarcacao.Sistema, null);

    /// <summary>Períodos de entrada e saída em horário LOCAL (Brasília), na ordem: <c>(8, 0, 12, 0)</c> é das 08:00 às 12:00.</summary>
    private static List<BatidaLida> Periodos(DateOnly dia, params (int EntradaHora, int EntradaMinuto, int SaidaHora, int SaidaMinuto)[] periodos)
    {
        var batidas = new List<BatidaLida>();
        foreach (var p in periodos)
        {
            batidas.Add(Batida(batidas.Count + 1, TipoDeMarcacao.Entrada, dia, p.EntradaHora + 3, p.EntradaMinuto));
            batidas.Add(Batida(batidas.Count + 1, TipoDeMarcacao.Saida, dia, p.SaidaHora + 3, p.SaidaMinuto));
        }

        return batidas;
    }

    private static DecisaoDoLancamento Decidir(DateOnly dia, IReadOnlyList<BatidaLida>? batidas = null, string? feriado = null, bool ferias = false, bool atestadoDeDiaInteiro = false, int minutosDeAtestadoDeHoras = 0, int? pedido = null, int carga = 528, DateOnly? hoje = null) =>
        RegraDoLancamentoDoDia.Decidir(ConstrutorDoDiaDoEspelho.Construir(new DadosDoDiaDoEspelho(dia, hoje ?? Hoje, batidas ?? [], feriado, ferias, atestadoDeDiaInteiro, minutosDeAtestadoDeHoras, pedido, carga, new TimeOnly(8, 0), 10)));

    [Fact]
    public void Mais_horas_que_a_carga_e_credito_de_hora_extra_nos_minutos_exatos()
    {
        var decisao = Decidir(SegundaFeira, Periodos(SegundaFeira, (8, 0, 12, 0), (13, 0, 18, 50))); // 4h + 5h50 = 590 min

        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, 62, "Hora extra"), decisao);
    }

    [Fact]
    public void Menos_horas_que_a_carga_e_debito_de_saldo_negativo_do_dia()
    {
        var decisao = Decidir(SegundaFeira, Periodos(SegundaFeira, (8, 0, 12, 0), (13, 0, 17, 0))); // 480 min

        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Debito, 48, "Saldo negativo do dia"), decisao);
    }

    [Fact]
    public void Dia_util_sem_batida_e_debito_de_falta_com_a_carga_inteira()
    {
        var decisao = Decidir(SegundaFeira);

        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Debito, 528, "Falta"), decisao);
    }

    [Fact]
    public void Saldo_zero_apaga_a_linha_do_dia()
    {
        var decisao = Decidir(SegundaFeira, Periodos(SegundaFeira, (8, 0, 12, 0), (13, 0, 17, 48))); // 240 + 288 = 528 min

        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), decisao);
    }

    [Fact]
    public void Os_segundos_das_batidas_nao_contam_como_na_tela()
    {
        var batidas = new List<BatidaLida>
        {
            new(1, new DateTime(2026, 10, 5, 11, 0, 50, DateTimeKind.Utc), TipoDeMarcacao.Entrada, OrigemDaMarcacao.Sistema, null),
            new(2, new DateTime(2026, 10, 5, 19, 48, 10, DateTimeKind.Utc), TipoDeMarcacao.Saida, OrigemDaMarcacao.Sistema, null), // 08:00 a 16:48 na tela: 528 min
        };

        Assert.Equal(AcaoDoLancamento.Apagar, Decidir(SegundaFeira, batidas).Acao);
    }

    [Fact]
    public void Dia_de_hoje_sequencia_quebrada_e_pedido_em_analise_nao_mexem_em_nada()
    {
        var entradaSemSaida = new List<BatidaLida> { Batida(1, TipoDeMarcacao.Entrada, SegundaFeira, 11) };
        var fechado = Periodos(SegundaFeira, (8, 0, 17, 0));

        Assert.Equal(AcaoDoLancamento.Manter, Decidir(SegundaFeira, entradaSemSaida).Acao); // incompleto: lança depois de resolvido
        Assert.Equal(AcaoDoLancamento.Manter, Decidir(SegundaFeira, fechado, pedido: 297).Acao); // o pedido em análise pode mudar o dia
        Assert.Equal(AcaoDoLancamento.Manter, Decidir(Hoje, Periodos(Hoje, (8, 0, 9, 0))).Acao); // hoje ainda não acabou
        Assert.Equal(AcaoDoLancamento.Manter, Decidir(Hoje).Acao);
    }

    [Fact]
    public void Fim_de_semana_sem_batida_apaga_e_com_batidas_e_tudo_hora_extra()
    {
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(Sabado));
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, 240, "Hora extra"), Decidir(Sabado, Periodos(Sabado, (8, 0, 12, 0))));
    }

    [Fact]
    public void Feriado_sem_batida_apaga_e_trabalhado_vira_hora_extra_inteira()
    {
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, feriado: "Dia de teste"));
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, 180, "Hora extra"), Decidir(SegundaFeira, Periodos(SegundaFeira, (9, 0, 12, 0)), feriado: "Dia de teste"));
    }

    [Fact]
    public void Ferias_sem_batida_apagam()
    {
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, ferias: true));
    }

    [Fact]
    public void Atestado_de_dia_inteiro_zera_a_carga_e_o_que_foi_trabalhado_vira_credito()
    {
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, atestadoDeDiaInteiro: true)); // a falta deixa de ser débito
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, 480, "Hora extra"), Decidir(SegundaFeira, Periodos(SegundaFeira, (8, 0, 16, 0)), atestadoDeDiaInteiro: true));
    }

    [Fact]
    public void Atestado_de_algumas_horas_diminui_a_carga_sem_passar_de_zero()
    {
        var seteHoras = Periodos(SegundaFeira, (8, 0, 15, 0)); // 420 min

        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, seteHoras, minutosDeAtestadoDeHoras: 108)); // 528 - 108 = 420: saldo zero
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Lancar, TipoDeLancamentoDoBanco.Credito, 12, "Hora extra"), Decidir(SegundaFeira, seteHoras, minutosDeAtestadoDeHoras: 120));
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, minutosDeAtestadoDeHoras: 600)); // atestado maior que a carga: carga zero, sem falta
    }

    [Fact]
    public void Sem_jornada_configurada_a_carga_e_zero_e_o_dia_util_sem_batida_nao_e_falta()
    {
        Assert.Equal(new DecisaoDoLancamento(AcaoDoLancamento.Apagar), Decidir(SegundaFeira, carga: 0));
    }
}

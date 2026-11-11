using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

public class SequenciaDoDiaTests
{
    private static readonly DateTime Dia = new(2026, 10, 7, 0, 0, 0, DateTimeKind.Utc);

    private static MarcacaoDoDia E(int id, int hora, int minuto = 0, int segundo = 0) => new(id, Dia.AddHours(hora).AddMinutes(minuto).AddSeconds(segundo), TipoDeMarcacao.Entrada);

    private static MarcacaoDoDia S(int id, int hora, int minuto = 0, int segundo = 0) => new(id, Dia.AddHours(hora).AddMinutes(minuto).AddSeconds(segundo), TipoDeMarcacao.Saida);

    // ---------------------------------------------------------------- períodos

    [Fact]
    public void Quatro_batidas_viram_dois_periodos_fechados()
    {
        var periodos = SequenciaDoDia.Periodos([E(1, 8), S(2, 12), E(3, 13), S(4, 17)]);

        Assert.Equal([(1, 2), (3, 4)], periodos.Select(p => (p.Entrada!.Id, p.Saida!.Id)));
    }

    [Fact]
    public void Entrada_seguida_de_entrada_deixa_a_primeira_sem_saida()
    {
        var periodos = SequenciaDoDia.Periodos([E(1, 8), E(2, 9), S(3, 12)]);

        Assert.Equal([(1, (int?)null), (2, 3)], periodos.Select(p => (p.Entrada!.Id, p.Saida?.Id)));
    }

    [Fact]
    public void Saida_solta_vira_periodo_sem_entrada()
    {
        var periodos = SequenciaDoDia.Periodos([S(1, 8), E(2, 9), S(3, 12)]);

        Assert.Equal([((int?)null, 1), (2, 3)], periodos.Select(p => (p.Entrada?.Id, p.Saida?.Id)));
    }

    [Fact]
    public void Entrada_aberta_no_fim_e_um_periodo_sem_saida()
    {
        var periodos = SequenciaDoDia.Periodos([E(1, 8), S(2, 12), E(3, 13)]);

        Assert.Equal([(1, (int?)2), (3, null)], periodos.Select(p => (p.Entrada!.Id, p.Saida?.Id)));
        Assert.Equal([true, false], periodos.Select(p => p.Fechado));
    }

    [Fact]
    public void Sem_batidas_nao_ha_periodo() => Assert.Empty(SequenciaDoDia.Periodos([]));

    [Fact]
    public void As_batidas_sao_ordenadas_por_horario_e_depois_por_id()
    {
        // Fora de ordem na lista; a saída (id 2) e a entrada (id 3) caem no mesmo instante: o menor id vem primeiro, então a saída fecha o primeiro período.
        var periodos = SequenciaDoDia.Periodos([S(4, 17), E(3, 12), S(2, 12), E(1, 8)]);

        Assert.Equal([(1, (int?)2), (3, 4)], periodos.Select(p => (p.Entrada!.Id, p.Saida?.Id)));
    }

    [Fact]
    public void A_ordem_e_a_do_horario_mesmo_quando_o_id_da_entrada_e_maior_que_o_da_saida()
    {
        // A entrada (id 5) foi gravada depois de uma saída de horário posterior (id 3): ordenar só pelo id inverteria o par.
        var marcacoes = new[] { E(5, 8), S(3, 12) };

        Assert.True(SequenciaDoDia.EhConsistente(marcacoes));
        Assert.Equal([(5, (int?)3)], SequenciaDoDia.Periodos(marcacoes).Select(p => (p.Entrada!.Id, p.Saida?.Id)));
    }

    [Fact]
    public void Cinco_batidas_deixam_a_quinta_aberta()
    {
        var periodos = SequenciaDoDia.Periodos([E(1, 8), S(2, 12), E(3, 13), S(4, 17), E(5, 18)]);

        Assert.Equal(3, periodos.Count);
        Assert.False(periodos[2].Fechado);
    }

    // ---------------------------------------------------------------- consistência

    [Fact]
    public void Entrada_e_saida_alternadas_terminando_em_saida_e_consistente()
    {
        Assert.True(SequenciaDoDia.EhConsistente([E(1, 8), S(2, 12)]));
        Assert.True(SequenciaDoDia.EhConsistente([E(1, 8), S(2, 12), E(3, 13), S(4, 17)]));
    }

    [Theory]
    [InlineData("E")]
    [InlineData("S")]
    [InlineData("SE")]
    [InlineData("EE")]
    [InlineData("ESE")]
    [InlineData("EES")]
    [InlineData("ESES E")]
    public void Sequencia_quebrada_ou_aberta_nao_e_consistente(string sequencia)
    {
        var marcacoes = sequencia.Replace(" ", string.Empty).Select((c, i) => c == 'E' ? E(i + 1, 8 + i) : S(i + 1, 8 + i)).ToList();

        Assert.False(SequenciaDoDia.EhConsistente(marcacoes));
    }

    [Fact]
    public void Sem_batidas_nao_e_consistente() => Assert.False(SequenciaDoDia.EhConsistente([]));

    [Fact]
    public void A_ordem_da_lista_nao_muda_a_consistencia()
    {
        Assert.True(SequenciaDoDia.EhConsistente([S(2, 12), E(1, 8)]));
    }

    // ---------------------------------------------------------------- rótulos

    [Theory]
    [InlineData(1, TipoDeMarcacao.Entrada, "Entrada")]
    [InlineData(2, TipoDeMarcacao.Saida, "Saída almoço")]
    [InlineData(3, TipoDeMarcacao.Entrada, "Volta")]
    [InlineData(4, TipoDeMarcacao.Saida, "Saída")]
    [InlineData(5, TipoDeMarcacao.Entrada, "Entrada")]
    [InlineData(6, TipoDeMarcacao.Saida, "Saída")]
    [InlineData(1, TipoDeMarcacao.Saida, "Saída")]
    [InlineData(2, TipoDeMarcacao.Entrada, "Entrada")]
    [InlineData(3, TipoDeMarcacao.Saida, "Saída")]
    [InlineData(4, TipoDeMarcacao.Entrada, "Entrada")]
    public void O_rotulo_vem_da_posicao_quando_o_tipo_combina_e_do_tipo_quando_nao(int ordem, TipoDeMarcacao tipo, string esperado) =>
        Assert.Equal(esperado, SequenciaDoDia.Rotulo(ordem, tipo));

    // ---------------------------------------------------------------- próxima batida

    [Fact]
    public void Dia_vazio_a_proxima_e_a_entrada()
    {
        var proxima = SequenciaDoDia.ProximaMarcacao([], [], Dia.AddHours(8));

        Assert.Equal((TipoDeMarcacao.Entrada, false, "Entrada", "Registrar entrada"), (proxima.Tipo, proxima.NoDiaAnterior, proxima.Rotulo, proxima.TextoDoBotao));
    }

    [Theory]
    [InlineData(1, TipoDeMarcacao.Saida, "Saída almoço", "Registrar saída para almoço")]
    [InlineData(2, TipoDeMarcacao.Entrada, "Volta", "Registrar volta")]
    [InlineData(3, TipoDeMarcacao.Saida, "Saída", "Registrar saída")]
    [InlineData(4, TipoDeMarcacao.Entrada, "Entrada", "Registrar entrada")]
    [InlineData(5, TipoDeMarcacao.Saida, "Saída", "Registrar saída")]
    public void A_proxima_alterna_o_tipo_da_ultima_e_o_rotulo_segue_a_posicao(int jaBatidas, TipoDeMarcacao tipo, string rotulo, string botao)
    {
        var batidas = Enumerable.Range(0, jaBatidas).Select(i => i % 2 == 0 ? E(i + 1, 6 + i) : S(i + 1, 6 + i)).ToList();

        var proxima = SequenciaDoDia.ProximaMarcacao(batidas, [], Dia.AddHours(20));

        Assert.Equal((tipo, false, rotulo, botao), (proxima.Tipo, proxima.NoDiaAnterior, proxima.Rotulo, proxima.TextoDoBotao));
    }

    [Fact]
    public void Entrada_do_dia_anterior_aberta_ha_menos_de_12_horas_a_proxima_e_a_saida_dela()
    {
        // A pessoa entrou às 22:00 de ontem e agora são 01:00: a próxima batida é a SAÍDA da jornada de ontem.
        var ontem = new[] { new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(22), TipoDeMarcacao.Entrada) };

        var proxima = SequenciaDoDia.ProximaMarcacao([], ontem, Dia.AddHours(1));

        Assert.Equal((TipoDeMarcacao.Saida, true, "Saída almoço", "Registrar saída para almoço"), (proxima.Tipo, proxima.NoDiaAnterior, proxima.Rotulo, proxima.TextoDoBotao));
    }

    [Fact]
    public void Entrada_do_dia_anterior_aberta_ha_exatamente_12_horas_ja_nao_vale_e_a_proxima_e_a_entrada_de_hoje()
    {
        var ontem = new[] { new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(22), TipoDeMarcacao.Entrada) };

        var proxima = SequenciaDoDia.ProximaMarcacao([], ontem, Dia.AddDays(-1).AddHours(22).AddHours(12));

        Assert.Equal((TipoDeMarcacao.Entrada, false), (proxima.Tipo, proxima.NoDiaAnterior));
    }

    [Fact]
    public void Onze_horas_e_59_minutos_ainda_valem()
    {
        var ontem = new[] { new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(22), TipoDeMarcacao.Entrada) };

        var proxima = SequenciaDoDia.ProximaMarcacao([], ontem, Dia.AddDays(-1).AddHours(22).AddHours(12).AddMinutes(-1));

        Assert.Equal((TipoDeMarcacao.Saida, true), (proxima.Tipo, proxima.NoDiaAnterior));
    }

    [Fact]
    public void Dia_anterior_fechado_nao_puxa_a_batida_de_hoje()
    {
        var ontem = new[]
        {
            new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(8), TipoDeMarcacao.Entrada),
            new MarcacaoDoDia(2, Dia.AddDays(-1).AddHours(17), TipoDeMarcacao.Saida),
        };

        var proxima = SequenciaDoDia.ProximaMarcacao([], ontem, Dia.AddHours(1));

        Assert.Equal((TipoDeMarcacao.Entrada, false), (proxima.Tipo, proxima.NoDiaAnterior));
    }

    [Fact]
    public void Se_hoje_ja_tem_batida_a_sequencia_de_hoje_manda_mesmo_com_o_dia_anterior_aberto()
    {
        var ontem = new[] { new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(22), TipoDeMarcacao.Entrada) };
        var hoje = new[] { E(2, 0, 30) };

        var proxima = SequenciaDoDia.ProximaMarcacao(hoje, ontem, Dia.AddHours(5));

        Assert.Equal((TipoDeMarcacao.Saida, false), (proxima.Tipo, proxima.NoDiaAnterior));
    }

    [Fact]
    public void O_dia_anterior_com_a_ultima_batida_saida_nao_conta_mesmo_com_entrada_antes()
    {
        var ontem = new[]
        {
            new MarcacaoDoDia(1, Dia.AddDays(-1).AddHours(8), TipoDeMarcacao.Entrada),
            new MarcacaoDoDia(2, Dia.AddDays(-1).AddHours(12), TipoDeMarcacao.Saida),
            new MarcacaoDoDia(3, Dia.AddDays(-1).AddHours(13), TipoDeMarcacao.Entrada),
            new MarcacaoDoDia(4, Dia.AddDays(-1).AddHours(23), TipoDeMarcacao.Saida),
        };

        Assert.False(SequenciaDoDia.ProximaMarcacao([], ontem, Dia.AddHours(1)).NoDiaAnterior);
    }
}

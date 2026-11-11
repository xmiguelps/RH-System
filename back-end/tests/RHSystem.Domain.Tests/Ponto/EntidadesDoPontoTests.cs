using RHSystem.Domain.Ponto;

namespace RHSystem.Domain.Tests.Ponto;

public class EntidadesDoPontoTests
{
    private static readonly DateTime Instante = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    [Fact]
    public void A_batida_e_cortada_ao_segundo_sem_arredondar_para_cima()
    {
        var batida = MarcacaoDePonto.Nova(1, Instante.AddMilliseconds(999), TipoDeMarcacao.Entrada);

        Assert.Equal(Instante, batida.InstanteUtc);
        Assert.Equal(DateTimeKind.Utc, batida.InstanteUtc.Kind);
    }

    [Fact]
    public void Duas_batidas_a_0_4_s_e_a_0_6_s_do_mesmo_segundo_ficam_iguais()
    {
        var a = MarcacaoDePonto.Nova(1, Instante.AddMilliseconds(400), TipoDeMarcacao.Entrada);
        var b = MarcacaoDePonto.Nova(1, Instante.AddMilliseconds(600), TipoDeMarcacao.Entrada);

        Assert.Equal(a.InstanteUtc, b.InstanteUtc);
    }

    [Theory]
    [InlineData(DateTimeKind.Local)]
    [InlineData(DateTimeKind.Unspecified)]
    public void Instante_que_nao_e_utc_e_recusado(DateTimeKind tipo) =>
        Assert.Throws<ArgumentException>(() => MarcacaoDePonto.Nova(1, DateTime.SpecifyKind(Instante, tipo), TipoDeMarcacao.Saida));

    [Fact]
    public void Batida_nova_vem_do_sistema_e_sem_data_de_correcao()
    {
        var batida = MarcacaoDePonto.Nova(7, Instante, TipoDeMarcacao.Saida);

        Assert.Equal((7, TipoDeMarcacao.Saida, OrigemDaMarcacao.Sistema, null), (batida.PontoId, batida.Tipo, batida.Origem, batida.CorrigidaEmUtc));
    }

    [Fact]
    public void Batida_incluida_pelo_RH_e_corrigida_e_leva_a_data_da_correcao()
    {
        var batida = MarcacaoDePonto.NovaCorrigidaPeloRh(7, Instante, TipoDeMarcacao.Entrada, Instante.AddHours(2).AddMilliseconds(500));

        Assert.Equal(OrigemDaMarcacao.CorrigidaRh, batida.Origem);
        Assert.Equal(Instante.AddHours(2), batida.CorrigidaEmUtc);
    }

    [Fact]
    public void Corrigir_uma_batida_do_sistema_muda_o_horario_o_tipo_a_origem_e_a_data()
    {
        var batida = MarcacaoDePonto.Nova(7, Instante, TipoDeMarcacao.Entrada);

        batida.CorrigirPeloRh(Instante.AddMinutes(-30), TipoDeMarcacao.Saida, Instante.AddDays(1));

        Assert.Equal((Instante.AddMinutes(-30), TipoDeMarcacao.Saida, OrigemDaMarcacao.CorrigidaRh, (DateTime?)Instante.AddDays(1)), (batida.InstanteUtc, batida.Tipo, batida.Origem, batida.CorrigidaEmUtc));
    }

    [Fact]
    public void Pedido_novo_nasce_pendente_sem_etapa_automatica_e_com_os_itens_cortados_ao_segundo()
    {
        var item = ItemDoPedidoDeAjuste.Novo(3, null, Instante.AddMilliseconds(700), TipoDeMarcacao.Entrada);

        var pedido = PedidoDeAjuste.Criar(3, 9, "Esqueci de bater", Instante.AddMilliseconds(300), gestorId: 4, [item]);

        Assert.Equal((StatusDoPedidoDeAjuste.Pendente, false, 4, null, null), (pedido.Status, pedido.EtapaDoGestorAutomatica, pedido.GestorId, pedido.ResultadoDoGestor, pedido.FuncionarioRhId));
        Assert.Equal(Instante, pedido.SolicitadoEmUtc);
        Assert.Equal(Instante, Assert.Single(pedido.Itens).InstanteProposto);
    }

    [Fact]
    public void Atestado_nasce_enviado_com_a_data_de_envio_do_relogio()
    {
        var atestado = Atestado.Enviar(5, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7), new TimeOnly(8, 0), new TimeOnly(12, 0), "atestados/x.pdf", "a.pdf", Instante.AddMilliseconds(800));

        Assert.Equal((SituacaoDoAtestado.Enviado, Instante, null), (atestado.Situacao, atestado.EnviadoEmUtc, atestado.FuncionarioRhId));
    }

    [Fact]
    public void Feriado_novo_nasce_ativo()
    {
        var feriado = Feriado.Criar("Aniversário da cidade", new DateOnly(2026, 1, 25), TipoDeFeriado.Municipal, "SP", "São Paulo", repeteTodoAno: true);

        Assert.Equal((true, true), (feriado.Ativo, feriado.RepeteTodoAno));
    }

    [Theory]
    [InlineData(0, "0.00")]
    [InlineData(1, "0.02")]
    [InlineData(30, "0.50")]
    [InlineData(83, "1.38")]
    [InlineData(480, "8.00")]
    public void Minutos_viram_horas_com_duas_casas_arredondando(int minutos, string esperado) =>
        Assert.Equal(decimal.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture), LancamentoDoBancoDeHoras.MinutosParaHoras(minutos));

    [Fact]
    public void Ida_e_volta_de_minutos_para_horas_e_de_horas_para_minutos_nao_perde_nenhum_minuto()
    {
        for (var minutos = 0; minutos <= 2000; minutos++)
        {
            Assert.Equal(minutos, LancamentoDoBancoDeHoras.HorasParaMinutos(LancamentoDoBancoDeHoras.MinutosParaHoras(minutos)));
        }
    }

    [Fact]
    public void Lancamento_criado_leva_o_dia_de_referencia_a_data_de_hoje_e_as_horas()
    {
        var lancamento = LancamentoDoBancoDeHoras.Criar(5, new DateOnly(2026, 10, 6), TipoDeLancamentoDoBanco.Credito, 83, "Hora extra", new DateOnly(2026, 10, 7));

        Assert.Equal((new DateOnly(2026, 10, 6), new DateOnly(2026, 10, 7), TipoDeLancamentoDoBanco.Credito, 1.38m, "Hora extra"), (lancamento.DiaDeReferencia!.Value, lancamento.DataDoLancamento!.Value, lancamento.Tipo!.Value, lancamento.Quantidade!.Value, lancamento.Observacao));
    }

    [Fact]
    public void Refazer_troca_tipo_quantidade_e_observacao_e_mantem_o_dia()
    {
        var lancamento = LancamentoDoBancoDeHoras.Criar(5, new DateOnly(2026, 10, 6), TipoDeLancamentoDoBanco.Credito, 83, "Hora extra", new DateOnly(2026, 10, 7));

        lancamento.Refazer(TipoDeLancamentoDoBanco.Debito, 30, "Saldo negativo do dia");

        Assert.Equal((new DateOnly(2026, 10, 6), TipoDeLancamentoDoBanco.Debito, 0.5m, "Saldo negativo do dia"), (lancamento.DiaDeReferencia!.Value, lancamento.Tipo!.Value, lancamento.Quantidade!.Value, lancamento.Observacao));
    }
}

using RHSystem.Application.Comum;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>As regras puras do pedido de ajuste (etapa, quem pode decidir, texto da correção e aplicação dos itens), com os exemplos do contrato (pedidos 297, 302, 306).</summary>
public class RegrasDoPedidoDeAjusteTests
{
    // ---------------------------------------------------------------- etapa

    [Theory]
    [InlineData(StatusDoPedidoDeAjuste.Pendente, 7, null, EtapaDoPedido.Gestor)] // com gestor e sem resultado dele
    [InlineData(StatusDoPedidoDeAjuste.Pendente, null, null, EtapaDoPedido.Rh)] // sem etapa do gestor
    [InlineData(StatusDoPedidoDeAjuste.Pendente, 7, ResultadoDaEtapaDoGestor.Aprovou, EtapaDoPedido.Rh)] // o gestor já aprovou
    [InlineData(StatusDoPedidoDeAjuste.Aprovada, 7, ResultadoDaEtapaDoGestor.Aprovou, EtapaDoPedido.Concluida)]
    [InlineData(StatusDoPedidoDeAjuste.Rejeitada, 7, ResultadoDaEtapaDoGestor.Recusou, EtapaDoPedido.Concluida)] // o gestor recusou: encerra
    [InlineData(StatusDoPedidoDeAjuste.Rejeitada, 3, null, EtapaDoPedido.Concluida)] // recusado pelo RH num pedido sem etapa do gestor
    public void A_etapa_sai_das_colunas(StatusDoPedidoDeAjuste status, int? gestorId, ResultadoDaEtapaDoGestor? resultado, EtapaDoPedido esperada) =>
        Assert.Equal(esperada, EtapaDoPedidoDeAjuste.De(status, gestorId, resultado));

    // ---------------------------------------------------------------- quem pode decidir (os cinco textos)

    private static PedidoParaDecisao Pedido(EtapaDoPedido etapa, int pessoa = 22, int? gestorDoPedido = 7) => new(etapa, pessoa, gestorDoPedido);

    private static QuemConsultaOPedido Quem(PapelNaConsulta papel, int funcionario, int? gestorId = null) => new(papel, funcionario, gestorId);

    [Fact]
    public void O_gestor_do_pedido_decide_na_etapa_do_gestor()
    {
        Assert.Equal((true, (string?)null), RegrasDeDecisaoDoPedido.PodeDecidir(Pedido(EtapaDoPedido.Gestor), Quem(PapelNaConsulta.Gestor, 21, gestorId: 7)));
    }

    [Fact]
    public void Outro_gestor_ou_o_RH_ou_a_propria_pessoa_na_etapa_do_gestor_aguardam_o_gestor()
    {
        var pedido = Pedido(EtapaDoPedido.Gestor);

        Assert.Equal((false, "Aguardando a análise do gestor."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(PapelNaConsulta.Gestor, 21, gestorId: 8))); // outro gestor
        Assert.Equal((false, "Aguardando a análise do gestor."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(PapelNaConsulta.Rh, 3, gestorId: 9)));
        Assert.Equal((false, "Aguardando a análise do gestor."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(PapelNaConsulta.PropriaPessoa, 22)));
    }

    [Fact]
    public void Na_etapa_do_RH_o_gestor_e_a_propria_pessoa_aguardam_o_RH()
    {
        var pedido = Pedido(EtapaDoPedido.Rh);

        Assert.Equal((false, "Aguardando a análise do RH."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(PapelNaConsulta.Gestor, 21, gestorId: 7)));
        Assert.Equal((false, "Aguardando a análise do RH."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(PapelNaConsulta.PropriaPessoa, 22)));
        Assert.Equal((false, "Aguardando a análise do RH."), RegrasDeDecisaoDoPedido.PodeDecidir(Pedido(EtapaDoPedido.Rh, gestorDoPedido: null), Quem(PapelNaConsulta.PropriaPessoa, 22))); // também sem etapa do gestor
    }

    [Fact]
    public void Concluida_para_todos_e_este_pedido_ja_foi_decidido()
    {
        var pedido = Pedido(EtapaDoPedido.Concluida);

        foreach (var papel in new[] { PapelNaConsulta.PropriaPessoa, PapelNaConsulta.Gestor, PapelNaConsulta.Rh })
        {
            Assert.Equal((false, "Este pedido já foi decidido."), RegrasDeDecisaoDoPedido.PodeDecidir(pedido, Quem(papel, 3, gestorId: 7)));
        }
    }

    [Fact]
    public void O_RH_decide_na_etapa_do_RH_o_pedido_que_passou_pelo_gestor_mesmo_sem_estar_em_GESTORES_e_mesmo_sendo_a_pessoa_do_pedido()
    {
        var passouPeloGestor = Pedido(EtapaDoPedido.Rh, pessoa: 3, gestorDoPedido: 7);

        Assert.Equal((true, (string?)null), RegrasDeDecisaoDoPedido.PodeDecidir(passouPeloGestor, Quem(PapelNaConsulta.Rh, 4)));
        Assert.Equal((true, (string?)null), RegrasDeDecisaoDoPedido.PodeDecidir(passouPeloGestor, Quem(PapelNaConsulta.Rh, 3))); // o RH confirma o próprio pedido
    }

    [Fact]
    public void Pedido_sem_etapa_do_gestor_so_e_decidido_por_RH_que_esta_em_GESTORES()
    {
        var semGestor = Pedido(EtapaDoPedido.Rh, pessoa: 21, gestorDoPedido: null);

        Assert.Equal((true, (string?)null), RegrasDeDecisaoDoPedido.PodeDecidir(semGestor, Quem(PapelNaConsulta.Rh, 3, gestorId: 9)));
        Assert.Equal((false, "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH."), RegrasDeDecisaoDoPedido.PodeDecidir(semGestor, Quem(PapelNaConsulta.Rh, 4)));
    }

    [Fact]
    public void Pedido_sem_etapa_do_gestor_nao_e_decidido_pela_propria_pessoa_e_esse_texto_vale_antes_do_de_GESTORES()
    {
        var proprio = Pedido(EtapaDoPedido.Rh, pessoa: 3, gestorDoPedido: null);

        Assert.Equal((false, "Você não pode decidir o seu próprio pedido."), RegrasDeDecisaoDoPedido.PodeDecidir(proprio, Quem(PapelNaConsulta.Rh, 3, gestorId: 9))); // está em GESTORES
        Assert.Equal((false, "Você não pode decidir o seu próprio pedido."), RegrasDeDecisaoDoPedido.PodeDecidir(proprio, Quem(PapelNaConsulta.Rh, 3))); // não está: o texto do próprio vence
    }

    // ---------------------------------------------------------------- texto da correção (exemplos do contrato)

    private static ItemParaAplicar Novo(TipoDeMarcacao tipo, string utc) => new(null, DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), tipo);

    private static ItemParaAplicar Existente(int id, TipoDeMarcacao tipo, string utc) => new(id, DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), tipo);

    [Fact]
    public void Batida_nova_isolada_e_incluir_a_entrada_ou_a_saida_com_o_horario_de_Brasilia()
    {
        Assert.Equal("Incluir a saída às 17:52", ResumoDaCorrecao.Montar([Novo(TipoDeMarcacao.Saida, "2026-10-02T20:52:00Z")])); // pedido 297
        Assert.Equal("Incluir a entrada às 08:00", ResumoDaCorrecao.Montar([Novo(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z")])); // pedido 302
    }

    [Fact]
    public void Entrada_e_saida_novas_juntas_sao_um_periodo()
    {
        Assert.Equal("Incluir o período 13:05 - 17:50", ResumoDaCorrecao.Montar([Novo(TipoDeMarcacao.Entrada, "2026-10-05T16:05:00Z"), Novo(TipoDeMarcacao.Saida, "2026-10-05T20:50:00Z")])); // pedido 306
        Assert.Equal("Incluir o período 13:05 - 17:50", ResumoDaCorrecao.Montar([Novo(TipoDeMarcacao.Saida, "2026-10-05T20:50:00Z"), Novo(TipoDeMarcacao.Entrada, "2026-10-05T16:05:00Z")])); // a ordem dos itens não importa
    }

    [Fact]
    public void Saida_nova_antes_da_entrada_nova_nao_forma_periodo()
    {
        Assert.Equal("Incluir a saída às 08:00; Incluir a entrada às 09:00", ResumoDaCorrecao.Montar([Novo(TipoDeMarcacao.Entrada, "2026-10-05T12:00:00Z"), Novo(TipoDeMarcacao.Saida, "2026-10-05T11:00:00Z")]));
    }

    [Fact]
    public void Batida_existente_e_corrigir_o_tipo_para_o_horario()
    {
        Assert.Equal("Corrigir a entrada para 08:10", ResumoDaCorrecao.Montar([Existente(5, TipoDeMarcacao.Entrada, "2026-10-05T11:10:00Z")]));
        Assert.Equal("Corrigir a saída para 17:48", ResumoDaCorrecao.Montar([Existente(6, TipoDeMarcacao.Saida, "2026-10-05T20:48:00Z")]));
    }

    [Fact]
    public void Varias_partes_saem_em_ordem_de_horario_separadas_por_ponto_e_virgula()
    {
        var itens = new[] { Novo(TipoDeMarcacao.Saida, "2026-10-05T21:00:00Z"), Existente(5, TipoDeMarcacao.Entrada, "2026-10-05T11:10:00Z"), Novo(TipoDeMarcacao.Entrada, "2026-10-05T16:05:00Z") };

        Assert.Equal("Corrigir a entrada para 08:10; Incluir o período 13:05 - 18:00", ResumoDaCorrecao.Montar(itens));
    }

    // ---------------------------------------------------------------- aplicação dos itens

    private static int _id;

    private static BatidaLida Batida(TipoDeMarcacao tipo, string utc) => new(++_id, DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal), tipo, OrigemDaMarcacao.Sistema, null);

    [Fact]
    public void Incluir_a_saida_que_faltava_fecha_o_dia_e_so_a_nova_precisa_ser_gravada()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");

        var resultado = AplicacaoDosItens.Aplicar([entrada], [Novo(TipoDeMarcacao.Saida, "2026-10-02T20:52:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([(entrada.Id, TipoDeMarcacao.Entrada, false), ((int?)null, TipoDeMarcacao.Saida, true)], resultado.Dia.Select(b => (b.Id, b.Tipo, b.Mudou)));
    }

    [Fact]
    public void Corrigir_o_horario_de_uma_batida_existente_a_marca_como_mudada_e_igual_ao_que_ja_esta_nao_muda()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");
        var saida = Batida(TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z");

        var corrigida = AplicacaoDosItens.Aplicar([entrada, saida], [Existente(saida.Id, TipoDeMarcacao.Saida, "2026-10-02T20:48:00Z")]);
        var igual = AplicacaoDosItens.Aplicar([entrada, saida], [Existente(saida.Id, TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z")]);

        Assert.Equal([false, true], corrigida.Dia.Select(b => b.Mudou));
        Assert.Equal([false, false], igual.Dia.Select(b => b.Mudou));
    }

    [Fact]
    public void Varias_batidas_de_uma_vez_uma_corrigida_e_uma_nova_aplicam_todas()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");
        var saidaDoAlmoco = Batida(TipoDeMarcacao.Saida, "2026-10-02T15:00:00Z");

        var resultado = AplicacaoDosItens.Aplicar([entrada, saidaDoAlmoco], [Existente(saidaDoAlmoco.Id, TipoDeMarcacao.Saida, "2026-10-02T15:05:00Z"), Novo(TipoDeMarcacao.Entrada, "2026-10-02T16:05:00Z"), Novo(TipoDeMarcacao.Saida, "2026-10-02T20:50:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([TipoDeMarcacao.Entrada, TipoDeMarcacao.Saida, TipoDeMarcacao.Entrada, TipoDeMarcacao.Saida], resultado.Dia.Select(b => b.Tipo));
        Assert.Equal([false, true, true, true], resultado.Dia.Select(b => b.Mudou));
    }

    [Fact]
    public void Sequencia_que_fica_quebrada_depois_de_aplicar_e_erro_e_o_dia_nao_e_aplicado()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");
        var saida = Batida(TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z");

        // O dia mudou depois do pedido: uma nova entrada às 15:00 deixa ENTRADA, ENTRADA, SAÍDA.
        var resultado = AplicacaoDosItens.Aplicar([entrada, saida], [Novo(TipoDeMarcacao.Entrada, "2026-10-02T15:00:00Z")]);

        Assert.Equal(ErroDeAplicacao.SequenciaInvalida, resultado.Erro);
    }

    [Fact]
    public void Batida_no_mesmo_segundo_de_outra_e_duplicada_mesmo_com_fracao_no_instante()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");

        var noMesmoInstante = AplicacaoDosItens.Aplicar([entrada], [Novo(TipoDeMarcacao.Saida, "2026-10-02T11:00:00Z")]);
        var comFracao = AplicacaoDosItens.Aplicar([entrada], [new ItemParaAplicar(null, entrada.InstanteUtc.AddMilliseconds(700), TipoDeMarcacao.Saida)]);

        Assert.Equal((ErroDeAplicacao.BatidaDuplicada, ErroDeAplicacao.BatidaDuplicada), (noMesmoInstante.Erro, comFracao.Erro));
    }

    [Fact]
    public void Dois_itens_no_mesmo_instante_sao_duplicados_e_dois_itens_na_mesma_batida_sao_repetidos()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");

        var iguais = AplicacaoDosItens.Aplicar([entrada], [Novo(TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z"), Novo(TipoDeMarcacao.Entrada, "2026-10-02T20:00:00Z")]);
        var repetida = AplicacaoDosItens.Aplicar([entrada], [Existente(entrada.Id, TipoDeMarcacao.Entrada, "2026-10-02T11:05:00Z"), Existente(entrada.Id, TipoDeMarcacao.Entrada, "2026-10-02T11:10:00Z")]);

        Assert.Equal((ErroDeAplicacao.BatidaDuplicada, ErroDeAplicacao.MarcacaoRepetida), (iguais.Erro, repetida.Erro));
    }

    [Fact]
    public void Item_que_corrige_batida_que_nao_e_do_dia_e_erro()
    {
        var entrada = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");

        var resultado = AplicacaoDosItens.Aplicar([entrada], [Existente(999999, TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z")]);

        Assert.Equal(ErroDeAplicacao.MarcacaoInexistenteNoDia, resultado.Erro);
    }

    [Fact]
    public void Corrigir_o_tipo_pode_consertar_a_sequencia()
    {
        var primeira = Batida(TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z");
        var segunda = Batida(TipoDeMarcacao.Entrada, "2026-10-02T20:00:00Z"); // devia ser SAÍDA

        var resultado = AplicacaoDosItens.Aplicar([primeira, segunda], [Existente(segunda.Id, TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([false, true], resultado.Dia.Select(b => b.Mudou));
    }
}

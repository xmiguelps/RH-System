using RHSystem.Application.Comum;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>
/// A situação e a etapa de um pedido de férias deduzidas das colunas (a tabela de estados do plano da fatia 7), a tradução do que o banco guarda para <c>decisaoGestor</c>, <c>decisaoRh</c> e <c>sugestao</c> (o banco não tem coluna da decisão do RH nem da aceitação do gestor), o prazo da sugestão
/// e o <c>podeDecidir</c> com o sexto texto.
/// </summary>
public class SituacaoEDecisoesDasFeriasTests
{
    private static readonly DateTime T1 = new(2026, 10, 6, 13, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T2 = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime T3 = new(2026, 10, 8, 14, 0, 0, DateTimeKind.Utc);

    private static LinhaDoPedidoDeFerias Linha(
        StatusDasFerias status = StatusDasFerias.Solicitada,
        int? gestorId = 8,
        int? gestorFuncionarioId = 80,
        ResultadoDoGestorNasFerias? resultado = null,
        bool automatica = false,
        int? rhId = null,
        RespostaDaSugestao? resposta = null,
        string? motivo = null,
        bool comSugestao = false) =>
        new(
            status,
            gestorId,
            gestorFuncionarioId,
            resultado,
            resultado is null ? null : T1,
            automatica,
            rhId,
            rhId is null ? null : T3,
            comSugestao ? new DateOnly(2026, 11, 2) : null,
            comSugestao ? new DateOnly(2026, 11, 9) : null,
            resposta,
            resposta is null ? null : T2,
            motivo);

    [Theory]
    [InlineData(StatusDasFerias.Solicitada, 8, null, null, SituacaoDasFerias.ComOGestor, EtapaDoPedido.Gestor)]
    [InlineData(StatusDasFerias.Solicitada, 8, ResultadoDoGestorNasFerias.SugeriuData, null, SituacaoDasFerias.SugestaoDeData, EtapaDoPedido.Gestor)]
    [InlineData(StatusDasFerias.Solicitada, 8, ResultadoDoGestorNasFerias.SugeriuData, RespostaDaSugestao.Aceitou, SituacaoDasFerias.ComORh, EtapaDoPedido.Rh)]
    [InlineData(StatusDasFerias.Solicitada, 8, ResultadoDoGestorNasFerias.Aprovou, null, SituacaoDasFerias.ComORh, EtapaDoPedido.Rh)]
    [InlineData(StatusDasFerias.Solicitada, null, null, null, SituacaoDasFerias.ComORh, EtapaDoPedido.Rh)]
    [InlineData(StatusDasFerias.Aprovada, null, null, null, SituacaoDasFerias.Aprovada, EtapaDoPedido.Concluida)]
    [InlineData(StatusDasFerias.EmGozo, 8, ResultadoDoGestorNasFerias.Aprovou, null, SituacaoDasFerias.EmGozo, EtapaDoPedido.Concluida)]
    [InlineData(StatusDasFerias.Concluida, 8, ResultadoDoGestorNasFerias.Aprovou, null, SituacaoDasFerias.Concluida, EtapaDoPedido.Concluida)]
    [InlineData(StatusDasFerias.Rejeitada, 8, ResultadoDoGestorNasFerias.Recusou, null, SituacaoDasFerias.Rejeitada, EtapaDoPedido.Concluida)]
    [InlineData(StatusDasFerias.Cancelada, 8, ResultadoDoGestorNasFerias.SugeriuData, RespostaDaSugestao.Recusou, SituacaoDasFerias.Cancelada, EtapaDoPedido.Concluida)]
    [InlineData(StatusDasFerias.Cancelada, 8, null, null, SituacaoDasFerias.Cancelada, EtapaDoPedido.Concluida)]
    public void A_situacao_e_a_etapa_saem_das_colunas_conforme_a_tabela_de_estados(StatusDasFerias status, int? gestorId, ResultadoDoGestorNasFerias? resultado, RespostaDaSugestao? resposta, SituacaoDasFerias situacao, EtapaDoPedido etapa)
    {
        var linha = Linha(status, gestorId, resultado: resultado, resposta: resposta);

        Assert.Equal((situacao, etapa), (SituacaoDoPedidoDeFerias.Situacao(linha), SituacaoDoPedidoDeFerias.Etapa(linha)));
    }

    [Fact]
    public void O_gestor_que_aprovou_aparece_como_quem_decidiu_com_a_data_da_etapa_e_sem_motivo()
    {
        var decisao = MapeadorDoPedidoDeFerias.DecisaoDoGestor(Linha(resultado: ResultadoDoGestorNasFerias.Aprovou));

        Assert.Equal(new DecisaoDeEtapaDeFerias(true, 80, T1, false, null), decisao);
    }

    [Fact]
    public void Na_aprovacao_automatica_ninguem_decidiu_e_ela_e_marcada_automatica()
    {
        var decisao = MapeadorDoPedidoDeFerias.DecisaoDoGestor(Linha(resultado: ResultadoDoGestorNasFerias.Aprovou, automatica: true));

        Assert.Equal(new DecisaoDeEtapaDeFerias(true, null, T1, true, null), decisao);
    }

    [Fact]
    public void O_gestor_que_recusou_leva_o_motivo_e_o_RH_nao_decidiu()
    {
        var linha = Linha(StatusDasFerias.Rejeitada, resultado: ResultadoDoGestorNasFerias.Recusou, motivo: "Equipe desfalcada no período.");

        Assert.Equal(new DecisaoDeEtapaDeFerias(false, 80, T1, false, "Equipe desfalcada no período."), MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
        Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
    }

    [Theory]
    [InlineData(StatusDasFerias.Aprovada)]
    [InlineData(StatusDasFerias.EmGozo)]
    [InlineData(StatusDasFerias.Concluida)]
    [InlineData(StatusDasFerias.Cancelada)] // aprovada pelo RH e depois cancelada pela inativação: a decisão existiu
    public void O_RH_que_confirmou_aparece_com_a_data_da_confirmacao_e_sem_motivo(StatusDasFerias status)
    {
        var linha = Linha(status, resultado: ResultadoDoGestorNasFerias.Aprovou, rhId: 1);

        Assert.Equal(new DecisaoDeEtapaDeFerias(true, 1, T3, false, null), MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
    }

    [Fact]
    public void O_RH_que_recusou_o_que_o_gestor_aprovou_leva_o_motivo_e_o_gestor_continua_aprovado_sem_motivo()
    {
        var linha = Linha(StatusDasFerias.Rejeitada, resultado: ResultadoDoGestorNasFerias.Aprovou, rhId: 1, motivo: "Fechamento do trimestre.");

        Assert.Equal(new DecisaoDeEtapaDeFerias(false, 1, T3, false, "Fechamento do trimestre."), MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
        Assert.Equal(new DecisaoDeEtapaDeFerias(true, 80, T1, false, null), MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
    }

    [Fact]
    public void O_RH_que_decidiu_pedido_sem_etapa_do_gestor_nao_cria_decisao_do_gestor_mesmo_com_o_ID_GESTOR_gravado_nele()
    {
        var linha = Linha(StatusDasFerias.Aprovada, gestorId: 9, gestorFuncionarioId: 1, resultado: null, rhId: 1);

        Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
        Assert.Equal(new DecisaoDeEtapaDeFerias(true, 1, T3, false, null), MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
    }

    [Fact]
    public void A_sugestao_pendente_nao_e_decisao_do_gestor_e_vem_no_campo_sugestao_com_o_prazo()
    {
        var linha = Linha(resultado: ResultadoDoGestorNasFerias.SugeriuData, comSugestao: true);

        Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
        Assert.Equal(new SugestaoDeFerias(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 9), 8, 80, T1, null, null, new DateOnly(2026, 10, 11)), MapeadorDoPedidoDeFerias.Sugestao(linha));
    }

    [Fact]
    public void Depois_de_aceita_a_sugestao_vira_decisao_APROVADA_do_gestor_com_a_data_da_resposta_da_pessoa()
    {
        var linha = Linha(resultado: ResultadoDoGestorNasFerias.SugeriuData, resposta: RespostaDaSugestao.Aceitou, comSugestao: true);

        Assert.Equal(new DecisaoDeEtapaDeFerias(true, 80, T2, false, null), MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
        Assert.Equal(RespostaDaSugestao.Aceitou, MapeadorDoPedidoDeFerias.Sugestao(linha)!.Resposta);
        Assert.Equal(T2, MapeadorDoPedidoDeFerias.Sugestao(linha)!.RespondidaEmUtc);
    }

    [Theory]
    [InlineData(RespostaDaSugestao.Recusou)]
    [InlineData(RespostaDaSugestao.Expirou)]
    public void Sugestao_recusada_ou_expirada_cancela_o_pedido_sem_nenhuma_decisao_e_a_sugestao_guarda_a_resposta(RespostaDaSugestao resposta)
    {
        var linha = Linha(StatusDasFerias.Cancelada, resultado: ResultadoDoGestorNasFerias.SugeriuData, resposta: resposta, comSugestao: true);

        Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
        Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
        Assert.Equal(resposta, MapeadorDoPedidoDeFerias.Sugestao(linha)!.Resposta);
    }

    [Fact]
    public void O_registro_direto_do_RH_e_o_pedido_cancelado_pela_tarefa_nao_tem_nenhuma_das_duas_decisoes_nem_sugestao()
    {
        var registro = Linha(StatusDasFerias.Aprovada, gestorId: null, gestorFuncionarioId: null);
        var vencido = Linha(StatusDasFerias.Cancelada);

        foreach (var linha in new[] { registro, vencido })
        {
            Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoGestor(linha));
            Assert.Null(MapeadorDoPedidoDeFerias.DecisaoDoRh(linha));
            Assert.Null(MapeadorDoPedidoDeFerias.Sugestao(linha));
        }
    }

    [Theory]
    [InlineData(2026, 10, 6, 13, 0, 2026, 11, 2, 2026, 10, 11)] // 5 dias depois da sugestão (o exemplo do contrato)
    [InlineData(2026, 10, 7, 12, 42, 2026, 11, 2, 2026, 10, 12)] // o outro exemplo do contrato
    [InlineData(2026, 10, 7, 2, 30, 2026, 11, 2, 2026, 10, 11)] // 23:30 do dia 6 em Brasília: o dia local é o 6
    [InlineData(2026, 10, 6, 13, 0, 2026, 10, 10, 2026, 10, 8)] // o início sugerido está perto: 2 dias antes dele
    [InlineData(2026, 10, 6, 13, 0, 2026, 10, 13, 2026, 10, 11)] // empate entre os dois critérios
    [InlineData(2026, 10, 6, 13, 0, 2026, 10, 14, 2026, 10, 11)]
    public void O_prazo_da_sugestao_e_o_menor_entre_5_dias_depois_dela_e_2_dias_antes_do_inicio_sugerido(int a, int m, int d, int h, int min, int ia, int im, int id, int ea, int em, int ed)
    {
        var sugeridaEm = new DateTime(a, m, d, h, min, 0, DateTimeKind.Utc);

        Assert.Equal(new DateOnly(ea, em, ed), MapeadorDoPedidoDeFerias.ExpiraEm(sugeridaEm, new DateOnly(ia, im, id)));
    }

    [Theory]
    [InlineData(StatusDasFerias.Solicitada, 8, null, null, true)] // esperando o gestor: ele é o responsável
    [InlineData(StatusDasFerias.Solicitada, 8, ResultadoDoGestorNasFerias.Aprovou, null, true)]
    [InlineData(StatusDasFerias.Solicitada, null, null, null, false)] // nasceu sem etapa do gestor
    [InlineData(StatusDasFerias.Aprovada, 8, ResultadoDoGestorNasFerias.Aprovou, 1, true)] // o gestor aprovou e o RH confirmou
    [InlineData(StatusDasFerias.Aprovada, 9, null, 1, false)] // sem etapa do gestor: o ID_GESTOR é o da linha de quem do RH decidiu
    [InlineData(StatusDasFerias.Rejeitada, 9, null, 1, false)]
    [InlineData(StatusDasFerias.Aprovada, null, null, null, false)] // o registro direto do RH
    [InlineData(StatusDasFerias.Cancelada, 8, null, null, true)] // vencido ainda com o gestor: ele era o responsável
    public void O_gestor_so_e_responsavel_quando_o_pedido_tem_etapa_do_gestor(StatusDasFerias status, int? gestorId, ResultadoDoGestorNasFerias? resultado, int? rhId, bool tem)
    {
        var linha = Linha(status, gestorId, resultado: resultado, rhId: rhId);

        Assert.Equal(tem, MapeadorDoPedidoDeFerias.TemEtapaDoGestor(linha));
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("", "")]
    [InlineData("Queria emendar com o feriado.", "Queria emendar com o feriado.")]
    [InlineData("Queria emendar. | Cancelada automaticamente: funcionario inativado", "Queria emendar.")]
    [InlineData("Cancelada automaticamente: funcionario inativado", null)]
    [InlineData("Cancelada automaticamente: funcionario inativado | Cancelada automaticamente: funcionario inativado", null)]
    [InlineData("A tarefa diz: Cancelada automaticamente: funcionario inativado e outras coisas", "A tarefa diz: Cancelada automaticamente: funcionario inativado e outras coisas")]
    public void O_sufixo_que_o_trigger_de_inativacao_acrescenta_nao_aparece_para_a_pessoa(string? gravada, string? mostrada) =>
        Assert.Equal(mostrada, MapeadorDoPedidoDeFerias.ObservacaoDaPessoa(gravada));

    private static readonly PedidoParaDecisao DaMaria = new(EtapaDoPedido.Gestor, 12, 8);

    [Fact]
    public void Em_SUGESTAO_DE_DATA_ninguem_decide_e_o_motivo_e_o_sexto_texto_com_o_nome_de_quem_pediu()
    {
        var pedido = new PedidoParaDecisao(EtapaDoPedido.Gestor, 12, 8);
        var esperado = (false, "Esperando a resposta de Bruno Lima à sugestão de outra data.");

        Assert.Equal(esperado, RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.SugestaoDeData, pedido, new QuemConsultaOPedido(PapelNaConsulta.Gestor, 80, 8), "Bruno Lima"));
        Assert.Equal(esperado, RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.SugestaoDeData, pedido, new QuemConsultaOPedido(PapelNaConsulta.Rh, 1, 1), "Bruno Lima"));
        Assert.Equal(esperado, RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.SugestaoDeData, pedido, new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, 12, null), "Bruno Lima"));
    }

    [Fact]
    public void Fora_da_sugestao_valem_os_cinco_textos_da_fatia_3()
    {
        var gestor = new QuemConsultaOPedido(PapelNaConsulta.Gestor, 80, 8);
        var rh = new QuemConsultaOPedido(PapelNaConsulta.Rh, 1, 1);
        var pessoa = new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, 12, null);

        Assert.Equal((true, (string?)null), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComOGestor, DaMaria, gestor, "Bruno"));
        Assert.Equal((false, "Aguardando a análise do gestor."), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComOGestor, DaMaria, rh, "Bruno"));
        Assert.Equal((false, "Aguardando a análise do gestor."), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComOGestor, DaMaria, pessoa, "Bruno"));
        var noRh = new PedidoParaDecisao(EtapaDoPedido.Rh, 12, 8);
        Assert.Equal((true, (string?)null), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComORh, noRh, rh, "Bruno"));
        Assert.Equal((false, "Aguardando a análise do RH."), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComORh, noRh, gestor, "Bruno"));
        Assert.Equal((false, "Aguardando a análise do RH."), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComORh, noRh, pessoa, "Bruno"));
        var concluido = new PedidoParaDecisao(EtapaDoPedido.Concluida, 12, 8);
        Assert.Equal((false, "Este pedido já foi decidido."), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.Aprovada, concluido, rh, "Bruno"));
        var semGestor = new PedidoParaDecisao(EtapaDoPedido.Rh, 12, null);
        Assert.Equal((false, RegrasDeDecisaoDoPedido.RhForaDeGestores), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComORh, semGestor, new QuemConsultaOPedido(PapelNaConsulta.Rh, 2, null), "Bruno"));
        Assert.Equal((false, RegrasDeDecisaoDoPedido.PedidoProprio), RegrasDeDecisaoDeFerias.PodeDecidir(SituacaoDasFerias.ComORh, new PedidoParaDecisao(EtapaDoPedido.Rh, 1, null), rh, "Ana"));
    }
}

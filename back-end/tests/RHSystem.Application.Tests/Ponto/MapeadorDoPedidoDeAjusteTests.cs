using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>Do pedido do banco para o do contrato: a etapa, quem decidiu, a observação (uma só no banco) e o que cada pessoa pode fazer. Os casos são os exemplos do contrato.</summary>
public class MapeadorDoPedidoDeAjusteTests
{
    private static readonly FuncionarioResumo Julia = new(22, "Júlia Nunes", true, "Vendedora", "Comercial");
    private static readonly FuncionarioResumo Carlos = new(7, "Carlos Ribeiro", true, "Gerente comercial", "Comercial");
    private static readonly FuncionarioResumo Roberto = new(3, "Roberto Alves", true, "Analista de RH", "Recursos Humanos");
    private static readonly DateTime Criado = new(2026, 10, 6, 13, 10, 0, DateTimeKind.Utc);
    private static readonly QuemConsultaOPedido Eu = new(PapelNaConsulta.PropriaPessoa, 22, null);

    private static DateTime Utc(string texto) => DateTime.Parse(texto, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);

    private static PedidoLido Pedido(
        StatusDoPedidoDeAjuste status = StatusDoPedidoDeAjuste.Pendente,
        int? gestorId = 7,
        FuncionarioResumo? gestor = null,
        string? observacao = null,
        ResultadoDaEtapaDoGestor? resultado = null,
        DateTime? etapaDoGestorEm = null,
        bool automatica = false,
        FuncionarioResumo? rh = null,
        DateTime? confirmadoEm = null,
        IReadOnlyList<BatidaLida>? batidas = null) =>
        new(
            303,
            Julia,
            new DateOnly(2026, 10, 5),
            Criado,
            "Esqueci de registrar quando voltei do almoço.",
            status,
            gestorId,
            gestor ?? (gestorId is null ? null : Carlos),
            observacao,
            resultado,
            etapaDoGestorEm,
            automatica,
            rh,
            confirmadoEm,
            [new ItemLido(703, null, TipoDeMarcacao.Saida, Utc("2026-10-05T20:50:00Z")), new ItemLido(702, null, TipoDeMarcacao.Entrada, Utc("2026-10-05T16:05:00Z"))],
            batidas ?? []);

    [Fact]
    public void Pedido_novo_com_gestor_e_da_etapa_do_gestor_sem_decisoes_e_com_o_texto_do_periodo()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(), Eu);

        Assert.Equal((303, "2026-10-05", Criado, StatusDoPedidoDeAjuste.Pendente, EtapaDoPedido.Gestor), (dto.Id, dto.Data.ToString("yyyy-MM-dd"), dto.CriadoEmUtc, dto.Status, dto.Etapa));
        Assert.Equal(("Carlos Ribeiro", (DecisaoDeEtapa?)null, (DecisaoDeEtapa?)null), (dto.GestorResponsavel!.Nome, dto.DecisaoGestor, dto.DecisaoRh));
        Assert.Equal("Incluir o período 13:05 - 17:50", dto.ResumoDaCorrecao);
        Assert.Equal([702, 703], dto.Itens.Select(i => i.Id)); // os itens saem por id
        Assert.Equal((false, "Aguardando a análise do gestor."), (dto.PodeDecidir, dto.MotivoNaoPodeDecidir));
    }

    [Fact]
    public void O_gestor_do_pedido_pode_decidir_na_etapa_dele_e_o_RH_nao()
    {
        var comGestor = MapeadorDoPedidoDeAjuste.Montar(Pedido(), new QuemConsultaOPedido(PapelNaConsulta.Gestor, 7, 7));
        var comRh = MapeadorDoPedidoDeAjuste.Montar(Pedido(), new QuemConsultaOPedido(PapelNaConsulta.Rh, 3, null));

        Assert.Equal((true, (string?)null), (comGestor.PodeDecidir, comGestor.MotivoNaoPodeDecidir));
        Assert.Equal((false, "Aguardando a análise do gestor."), (comRh.PodeDecidir, comRh.MotivoNaoPodeDecidir));
    }

    [Fact]
    public void Gestor_aprovou_o_pedido_espera_o_RH_com_a_decisao_e_a_observacao_do_gestor()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(observacao: "Confirmo: ela saiu às 17:50.", resultado: ResultadoDaEtapaDoGestor.Aprovou, etapaDoGestorEm: Utc("2026-10-05T13:00:00Z")), Eu);

        Assert.Equal((EtapaDoPedido.Rh, "Carlos Ribeiro"), (dto.Etapa, dto.GestorResponsavel!.Nome));
        Assert.Equal((DecisaoDaEtapa.Aprovada, "Carlos Ribeiro", Utc("2026-10-05T13:00:00Z"), false, "Confirmo: ela saiu às 17:50."), (dto.DecisaoGestor!.Decisao, dto.DecisaoGestor.DecididaPor!.Nome, dto.DecisaoGestor.DecididaEmUtc, dto.DecisaoGestor.Automatica, dto.DecisaoGestor.Observacao));
        Assert.Null(dto.DecisaoRh);
        Assert.Equal((false, "Aguardando a análise do RH."), (dto.PodeDecidir, dto.MotivoNaoPodeDecidir));
    }

    [Fact]
    public void Gestor_ausente_aprova_sozinho_sem_decididaPor_e_o_pedido_segue_para_o_RH()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(resultado: ResultadoDaEtapaDoGestor.Aprovou, etapaDoGestorEm: Criado, automatica: true), Eu);

        Assert.Equal((EtapaDoPedido.Rh, "Carlos Ribeiro", DecisaoDaEtapa.Aprovada, true, Criado), (dto.Etapa, dto.GestorResponsavel!.Nome, dto.DecisaoGestor!.Decisao, dto.DecisaoGestor.Automatica, dto.DecisaoGestor.DecididaEmUtc));
        Assert.Null(dto.DecisaoGestor.DecididaPor);
    }

    [Fact]
    public void Gestor_recusou_encerra_o_pedido_a_observacao_e_a_dele_e_nao_ha_decisao_do_RH()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(StatusDoPedidoDeAjuste.Rejeitada, observacao: "Sem comprovação.", resultado: ResultadoDaEtapaDoGestor.Recusou, etapaDoGestorEm: Utc("2026-10-06T14:00:00Z")), Eu);

        Assert.Equal((EtapaDoPedido.Concluida, StatusDoPedidoDeAjuste.Rejeitada, DecisaoDaEtapa.Rejeitada, "Sem comprovação."), (dto.Etapa, dto.Status, dto.DecisaoGestor!.Decisao, dto.DecisaoGestor.Observacao));
        Assert.Null(dto.DecisaoRh);
        Assert.Equal((false, "Este pedido já foi decidido."), (dto.PodeDecidir, dto.MotivoNaoPodeDecidir));
    }

    [Fact]
    public void Quando_o_RH_decide_a_observacao_dele_substitui_a_do_gestor_que_volta_nula()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(
            Pedido(StatusDoPedidoDeAjuste.Aprovada, observacao: "Conferido com o controle da portaria.", resultado: ResultadoDaEtapaDoGestor.Aprovou, etapaDoGestorEm: Utc("2026-09-19T13:00:00Z"), rh: Roberto, confirmadoEm: Utc("2026-09-19T17:30:00Z")), Eu);

        Assert.Equal((EtapaDoPedido.Concluida, (string?)null), (dto.Etapa, dto.DecisaoGestor!.Observacao));
        Assert.Equal((DecisaoDaEtapa.Aprovada, "Roberto Alves", Utc("2026-09-19T17:30:00Z"), false, "Conferido com o controle da portaria."), (dto.DecisaoRh!.Decisao, dto.DecisaoRh.DecididaPor!.Nome, dto.DecisaoRh.DecididaEmUtc, dto.DecisaoRh.Automatica, dto.DecisaoRh.Observacao));
    }

    [Fact]
    public void O_RH_pode_recusar_o_que_o_gestor_aprovou_e_a_decisao_do_RH_e_rejeitada()
    {
        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(StatusDoPedidoDeAjuste.Rejeitada, resultado: ResultadoDaEtapaDoGestor.Aprovou, etapaDoGestorEm: Utc("2026-10-02T14:10:00Z"), rh: Roberto, confirmadoEm: Utc("2026-10-05T12:15:00Z"), observacao: "Fora do prazo."), Eu);

        Assert.Equal((DecisaoDaEtapa.Aprovada, DecisaoDaEtapa.Rejeitada, "Fora do prazo."), (dto.DecisaoGestor!.Decisao, dto.DecisaoRh!.Decisao, dto.DecisaoRh.Observacao));
    }

    [Fact]
    public void Pedido_sem_etapa_do_gestor_nasce_no_RH_e_o_banco_gravar_o_gestor_do_RH_no_fim_nao_vira_gestorResponsavel()
    {
        var pendente = MapeadorDoPedidoDeAjuste.Montar(Pedido(gestorId: null), Eu);
        // O RH decidiu: o banco grava em ID_GESTOR a linha de GESTORES de quem decidiu (o trigger exige), mas o pedido nunca teve etapa do gestor.
        var decidido = MapeadorDoPedidoDeAjuste.Montar(Pedido(StatusDoPedidoDeAjuste.Aprovada, gestorId: 9, gestor: Roberto, rh: Roberto, confirmadoEm: Utc("2026-10-07T12:00:00Z")), Eu);

        Assert.Equal((EtapaDoPedido.Rh, (FuncionarioResumo?)null, (DecisaoDeEtapa?)null), (pendente.Etapa, pendente.GestorResponsavel, pendente.DecisaoGestor));
        Assert.Equal((EtapaDoPedido.Concluida, (FuncionarioResumo?)null, (DecisaoDeEtapa?)null, DecisaoDaEtapa.Aprovada), (decidido.Etapa, decidido.GestorResponsavel, decidido.DecisaoGestor, decidido.DecisaoRh!.Decisao));
    }

    [Fact]
    public void Os_periodos_do_dia_sao_o_registro_atual_com_ordem_e_rotulo()
    {
        var batidas = new[]
        {
            new BatidaLida(11, Utc("2026-10-05T11:00:00Z"), TipoDeMarcacao.Entrada, OrigemDaMarcacao.Sistema, null),
            new BatidaLida(12, Utc("2026-10-05T15:00:00Z"), TipoDeMarcacao.Saida, OrigemDaMarcacao.CorrigidaRh, Utc("2026-10-06T10:00:00Z")),
        };

        var dto = MapeadorDoPedidoDeAjuste.Montar(Pedido(batidas: batidas), Eu);

        var periodo = dto.PeriodosDoDia.Single();
        Assert.Equal(("Entrada", "Saída almoço", OrigemDaMarcacao.CorrigidaRh), (periodo.Entrada!.Rotulo, periodo.Saida!.Rotulo, periodo.Saida.Origem));
    }
}

using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Emails;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary><c>GET /api/rh/ponto/pedidos-de-ajuste</c>: os pedidos de TODA a empresa, com a contagem por etapa (<c>resumo.comRh</c> é a fila do RH, a mesma bolinha do menu). <c>podeDecidir</c> vale para quem consulta.</summary>
public sealed class ListarPedidosParaRhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta, IPedidosDeAjusteRepositorio repositorio)
{
    public async Task<PontoPedidoAjusteFilaPagina> ExecutarAsync(int pagina, int tamanhoPagina, string? status, string? etapa, int? funcionarioId, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var (parametros, filtro) = LeitorDeFiltrosDePedidos.Ler(pagina, tamanhoPagina, status, etapa, ordenarPor, direcao, funcionarioId);
        var gestorId = await repositorio.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct);
        var resultado = await consulta.ListarAsync(new EscopoDePedidos(PapelNaConsulta.Rh, escopo.FuncionarioId, null, gestorId), filtro, parametros, ct);
        var quem = new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, gestorId);
        return new PontoPedidoAjusteFilaPagina(
            [.. resultado.Itens.Select(p => MapeadorDoPedidoDeAjuste.Montar(p, quem))],
            parametros.Pagina,
            parametros.TamanhoPagina,
            resultado.Total,
            (int)(((long)resultado.Total + parametros.TamanhoPagina - 1) / parametros.TamanhoPagina),
            resultado.Resumo);
    }
}

/// <summary><c>GET /api/rh/ponto/pedidos-de-ajuste/{pedidoId}</c>: qualquer pedido da empresa; <c>podeDecidir</c> e o motivo valem para quem do RH está consultando.</summary>
public sealed class ObterPedidoParaRhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta, IPedidosDeAjusteRepositorio repositorio)
{
    public async Task<PontoPedidoAjuste> ExecutarAsync(int pedidoId, CancellationToken ct)
    {
        var gestorId = await repositorio.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct);
        var pedido = (await consulta.ObterAsync(pedidoId, new EscopoDePedidos(PapelNaConsulta.Rh, escopo.FuncionarioId, null, gestorId), ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(pedido, new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, gestorId));
    }
}

/// <summary>
/// A decisão FINAL do RH (<c>aprovar</c> e <c>recusar</c>), numa transação só, com a pessoa do pedido travada (a mesma trava do bater ponto). Recusas, nesta ordem: pedido ainda na etapa do gestor = 409 <c>PEDIDO_ETAPA_INCORRETA</c>; já decidido = 409
/// <c>PEDIDO_JA_DECIDIDO</c>; pedido SEM etapa do gestor, decidido pela própria pessoa = 422 <c>PEDIDO_PROPRIO</c> (vence) ou por quem do RH não está em <c>GESTORES</c> = 422 <c>PEDIDO_RH_FORA_DE_GESTORES</c> (o trigger do banco exige o gestor; aqui a mesma
/// resposta sai antes de gravar). <b>Aprovar é tudo ou nada</b>: os itens se aplicam ao dia em memória e a conferência (sequência que fecha, nenhuma batida no mesmo segundo) vem ANTES de gravar; o dia pode ter mudado desde o pedido (outra correção do RH).
/// O banco de horas do dia é recalculado (aprovando ou recusando: o dia com pedido em análise não lança), e o evento e o e-mail do resultado saem na mesma transação.
/// </summary>
public sealed class DecidirComoRhCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDePedidosDeAjuste consulta,
    IPedidosDeAjusteRepositorio repositorio,
    IPontoRepositorio ponto,
    IRecalculoDoBancoDeHoras recalculo,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IFilaDeEmails fila,
    IRelogio relogio)
{
    public Task<PontoPedidoAjuste> AprovarAsync(int pedidoId, PontoPedidoDecisao? corpo, CancellationToken ct) => DecidirAsync(pedidoId, corpo, aprovar: true, ct);

    public Task<PontoPedidoAjuste> RecusarAsync(int pedidoId, PontoPedidoDecisao? corpo, CancellationToken ct) => DecidirAsync(pedidoId, corpo, aprovar: false, ct);

    private async Task<PontoPedidoAjuste> DecidirAsync(int pedidoId, PontoPedidoDecisao? corpo, bool aprovar, CancellationToken ct)
    {
        var observacao = ObservacaoDaDecisao.Ler(corpo);
        var gestorIdDoRh = await repositorio.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct);
        var doRh = new EscopoDePedidos(PapelNaConsulta.Rh, escopo.FuncionarioId, null, gestorIdDoRh);
        var lido = (await consulta.ObterAsync(pedidoId, doRh, ct)).OuNaoEncontrado();
        ConferirAEtapa(lido, aprovar);
        ConferirQuemDecide(lido, gestorIdDoRh);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                // A pessoa do pedido fica travada: nenhuma batida nem outro pedido dela se mistura com a aplicação dos itens.
                _ = await ponto.TravarPessoaAsync(lido.Funcionario.Id, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;

                // O pedido é lido DE NOVO já com a trava: a decisão do gestor, a do RH e a correção direta do dia podem ter acontecido entre a leitura e a trava.
                var pedido = (await consulta.ObterAsync(pedidoId, doRh, ct)).OuNaoEncontrado();
                ConferirAEtapa(pedido, aprovar);

                if (aprovar)
                {
                    var itens = pedido.Itens.Select(i => new ItemParaAplicar(i.MarcacaoId, i.InstanteUtc, i.Tipo)).ToList();
                    var aplicado = AplicacaoDosItens.Aplicar(await ponto.BatidasDoDiaAsync(pedido.Funcionario.Id, pedido.Dia, ct), itens);
                    if (aplicado.Erro is { } erro)
                    {
                        throw erro == ErroDeAplicacao.BatidaDuplicada ? MensagensDoPonto.BatidaDuplicadaNaAprovacao.ComoProblema() : MensagensDoPonto.SequenciaInvalidaNaAprovacao.ComoProblema();
                    }

                    if (!await repositorio.DecidirComoRhAsync(pedidoId, true, escopo.FuncionarioId, gestorIdDoRh, agora, observacao, ct))
                    {
                        throw MensagensDoPonto.EtapaIncorreta.ComoProblema();
                    }

                    await repositorio.AplicarBatidasAsync(pedido.Funcionario.Id, pedido.Dia, aplicado.Dia, agora, ct);
                    await recalculo.RecalcularDiaAsync(pedido.Funcionario.Id, pedido.Dia, ct);
                }
                else
                {
                    if (!await repositorio.DecidirComoRhAsync(pedidoId, false, escopo.FuncionarioId, gestorIdDoRh, agora, observacao, ct))
                    {
                        throw MensagensDoPonto.EtapaIncorreta.ComoProblema();
                    }

                    // Enquanto o pedido esperava, a consolidação do dia deixou a linha antiga como estava; agora que ele saiu do estado pendente o dia é recalculado (a falta ou a hora extra que ficou esperando).
                    await recalculo.RecalcularDiaAsync(pedido.Funcionario.Id, pedido.Dia, ct);
                }

                var descricao = $"{(aprovar ? "Aprovou" : "Recusou")} o pedido de ajuste de ponto do dia {pedido.Dia:dd/MM/yyyy} (etapa do RH).";
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ponto, aprovar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou, descricao, $"Ponto de {pedido.Dia:dd/MM/yyyy}", "PONTOS_MANUTENCOES", pedidoId));
                await unidade.SalvarAsync(ct);
                await ResultadoPorEmail.EnfileirarAsync(fila, repositorio, pedido, aprovar, "RH", escopo.Nome, observacao, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var decidido = (await consulta.ObterAsync(pedidoId, doRh, ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(decidido, new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, gestorIdDoRh));
    }

    /// <summary>Concluído = 409 <c>PEDIDO_JA_DECIDIDO</c>; ainda na etapa do gestor = 409 <c>PEDIDO_ETAPA_INCORRETA</c> (ao aprovar, "ainda está na etapa do gestor"; ao recusar, o texto genérico).</summary>
    private static void ConferirAEtapa(PedidoLido pedido, bool aprovar)
    {
        switch (EtapaDoPedidoDeAjuste.De(pedido.Status, pedido.GestorId, pedido.ResultadoDoGestor))
        {
            case EtapaDoPedido.Concluida:
                throw MensagensDoPonto.PedidoJaDecidido.ComoProblema();
            case EtapaDoPedido.Gestor:
                throw (aprovar ? MensagensDoPonto.EtapaIncorretaDoGestor : MensagensDoPonto.EtapaIncorreta).ComoProblema();
        }
    }

    /// <summary>Pedido que não tem etapa do gestor: o banco grava em <c>ID_GESTOR</c> a linha de quem decide, então a própria pessoa não decide (vence) e quem não está em <c>GESTORES</c> também não.</summary>
    private void ConferirQuemDecide(PedidoLido pedido, int? gestorIdDoRh)
    {
        if (pedido.GestorId is not null)
        {
            return;
        }

        if (escopo.FuncionarioId == pedido.Funcionario.Id)
        {
            throw MensagensDoPonto.PedidoProprio.ComoProblema(RegrasDoBancoDoPonto.PedidoProprio);
        }

        if (gestorIdDoRh is null)
        {
            throw MensagensDoPonto.PedidoRhForaDeGestores.ComoProblema(RegrasDoBancoDoPonto.PedidoSemGestor);
        }
    }
}

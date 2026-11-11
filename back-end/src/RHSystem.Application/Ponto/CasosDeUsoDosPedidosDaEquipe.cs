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

/// <summary>Os escopos de quem consulta ou decide pelo gestor: a equipe dele e o <c>ID_GESTOR</c> dele (nulo para quem não gerencia setor).</summary>
internal static class EscoposDeGestor
{
    public static EscopoDePedidos Do(IEscopoDoUsuario escopo, EquipeDoGestor? equipe) => new(PapelNaConsulta.Gestor, escopo.FuncionarioId, equipe, equipe?.GestorId);

    public static QuemConsultaOPedido Quem(IEscopoDoUsuario escopo, EquipeDoGestor? equipe) => new(PapelNaConsulta.Gestor, escopo.FuncionarioId, equipe?.GestorId);
}

/// <summary>
/// <c>GET /api/gestor/ponto/pedidos-de-ajuste</c>: os pedidos da EQUIPE do gestor que têm o <c>ID_GESTOR</c> dele (o pedido do próprio gestor, o de outro gestor e o de outro setor não aparecem). O <c>resumo</c> conta as etapas dos pedidos
/// <c>PENDENTE</c> do escopo e independe da página, do status e da etapa. Gestor sem setor: lista vazia.
/// </summary>
public sealed class ListarPedidosDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta)
{
    public async Task<PontoPedidoAjusteFilaPagina> ExecutarAsync(int pagina, int tamanhoPagina, string? status, string? etapa, int? funcionarioId, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var (parametros, filtro) = LeitorDeFiltrosDePedidos.Ler(pagina, tamanhoPagina, status, etapa, ordenarPor, direcao, funcionarioId);
        var equipe = await escopo.ObterEquipeAsync(ct);
        var resultado = await consulta.ListarAsync(EscoposDeGestor.Do(escopo, equipe), filtro, parametros, ct);
        var quem = EscoposDeGestor.Quem(escopo, equipe);
        var total = resultado.Total;
        return new PontoPedidoAjusteFilaPagina(
            [.. resultado.Itens.Select(p => MapeadorDoPedidoDeAjuste.Montar(p, quem))],
            parametros.Pagina,
            parametros.TamanhoPagina,
            total,
            (int)(((long)total + parametros.TamanhoPagina - 1) / parametros.TamanhoPagina),
            resultado.Resumo);
    }
}

/// <summary><c>GET /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}</c>: o pedido de uma pessoa da equipe; outra equipe, outro gestor, o próprio gestor e o que não existe dão o mesmo 404.</summary>
public sealed class ObterPedidoDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta)
{
    public async Task<PontoPedidoAjuste> ExecutarAsync(int pedidoId, CancellationToken ct)
    {
        var equipe = await escopo.ObterEquipeAsync(ct);
        var pedido = (await consulta.ObterAsync(pedidoId, EscoposDeGestor.Do(escopo, equipe), ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(pedido, EscoposDeGestor.Quem(escopo, equipe));
    }
}

/// <summary>
/// <c>POST .../aprovar</c> e <c>.../recusar</c> da etapa do gestor. Pedido fora do escopo = 404. Pelo estado lido: já decidido = 409 <c>PEDIDO_JA_DECIDIDO</c>, na etapa do RH = 409 <c>PEDIDO_ETAPA_INCORRETA</c>. A decisão é um UPDATE condicional (a corrida com o
/// gestor ausente, com o RH e com o segundo clique devolve o mesmo 409 que a leitura teria dado). Aprovar NÃO muda nenhuma batida (o pedido segue <c>PENDENTE</c>, para o RH); recusar encerra. O evento e o e-mail do resultado saem na MESMA transação: se ela
/// desfaz, nada sai; a falha do envio (feito depois, pela fila) nunca desfaz a decisão.
/// </summary>
public sealed class DecidirComoGestorCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDePedidosDeAjuste consulta,
    IPedidosDeAjusteRepositorio repositorio,
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
        var equipe = await escopo.ObterEquipeAsync(ct);
        var doGestor = EscoposDeGestor.Do(escopo, equipe);
        var pedido = (await consulta.ObterAsync(pedidoId, doGestor, ct)).OuNaoEncontrado();
        ErrosDeEtapa.ConferirNaEtapaDoGestor(pedido);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var agora = relogio.AgoraUtc;
                if (!await repositorio.DecidirComoGestorAsync(pedidoId, equipe!.GestorId, aprovar, agora, observacao, ct))
                {
                    // Outra decisão chegou antes (o gestor ausente, o RH ou o mesmo clique): o estado de agora diz qual dos dois 409.
                    var atual = (await consulta.ObterAsync(pedidoId, doGestor, ct)).OuNaoEncontrado();
                    ErrosDeEtapa.ConferirNaEtapaDoGestor(atual);
                    throw MensagensDoPonto.EtapaIncorreta.ComoProblema();
                }

                var descricao = $"{(aprovar ? "Aprovou" : "Recusou")} o pedido de ajuste de ponto do dia {pedido.Dia:dd/MM/yyyy} (etapa do gestor).";
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ponto, aprovar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou, descricao, $"Ponto de {pedido.Dia:dd/MM/yyyy}", "PONTOS_MANUTENCOES", pedidoId));
                await unidade.SalvarAsync(ct);
                await ResultadoPorEmail.EnfileirarAsync(fila, repositorio, pedido, aprovar, "GESTOR", escopo.Nome, observacao, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var decidido = (await consulta.ObterAsync(pedidoId, doGestor, ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(decidido, EscoposDeGestor.Quem(escopo, equipe));
    }
}

/// <summary>A observação opcional das decisões: até 500 caracteres, aparada; vazia vira <c>null</c>.</summary>
internal static class ObservacaoDaDecisao
{
    public const int TamanhoMaximo = 500;

    public static string? Ler(PontoPedidoDecisao? corpo)
    {
        var observacao = string.IsNullOrWhiteSpace(corpo?.Observacao) ? null : corpo.Observacao.Trim();
        if (observacao is { Length: > TamanhoMaximo })
        {
            throw new ErroDeValidacaoException("observacao", $"A observação tem no máximo {TamanhoMaximo} caracteres.");
        }

        return observacao;
    }
}

/// <summary>Os dois 409 de quem decide fora da vez, pelo estado lido do pedido.</summary>
internal static class ErrosDeEtapa
{
    /// <summary>Concluído = <c>PEDIDO_JA_DECIDIDO</c>; na etapa do RH = <c>PEDIDO_ETAPA_INCORRETA</c> (genérico: "não está mais na etapa em que você pode decidir").</summary>
    public static void ConferirNaEtapaDoGestor(PedidoLido pedido)
    {
        switch (EtapaDoPedidoDeAjuste.De(pedido.Status, pedido.GestorId, pedido.ResultadoDoGestor))
        {
            case EtapaDoPedido.Concluida:
                throw MensagensDoPonto.PedidoJaDecidido.ComoProblema();
            case EtapaDoPedido.Rh:
                throw MensagensDoPonto.EtapaIncorreta.ComoProblema();
        }
    }
}

/// <summary>
/// O aviso do resultado (<c>RESULTADO_AJUSTE_PONTO</c>): a cada decisão (do gestor, do RH), para o e-mail corporativo da pessoa do pedido. Quem não tem e-mail corporativo é pulado (a fila recusa destinatário nulo) e o evento de auditoria já conta o que foi
/// decidido. A chave (<c>pedido</c> e <c>etapa</c>) impede o aviso de sair duas vezes. A carga leva o nome e a observação para o texto; o renderizador conta o resto.
/// </summary>
internal static class ResultadoPorEmail
{
    public const string Tipo = "RESULTADO_AJUSTE_PONTO";

    public static async Task EnfileirarAsync(IFilaDeEmails fila, IPedidosDeAjusteRepositorio repositorio, PedidoLido pedido, bool aprovou, string etapa, string decididoPor, string? observacao, CancellationToken ct)
    {
        var destinatario = await repositorio.EmailCorporativoAsync(pedido.Funcionario.Id, ct);
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            return;
        }

        await fila.EnfileirarAsync(
            new EmailParaEnfileirar(
                Tipo,
                ChaveDeIdempotencia.Montar(Tipo, ("pedido", pedido.Id), ("etapa", etapa)),
                destinatario,
                new
                {
                    nome = pedido.Funcionario.Nome,
                    data = pedido.Dia.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    resultado = aprovou ? "APROVADA" : "REJEITADA",
                    etapa,
                    decididoPor,
                    observacao,
                },
                pedido.Funcionario.Id,
                pedido.Id),
            ct);
    }
}

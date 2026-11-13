using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Emails;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Ferias;

/// <summary>
/// <c>GET /api/rh/ferias</c>: os pedidos de toda a empresa, por padrão os que esperam o RH (<c>COM_O_RH</c>), do mais antigo ao mais novo, cada um com os conflitos e a equipe disponível do setor de quem pede. <c>podeDecidir</c> vale para QUEM consulta: pedido sem etapa do gestor, sendo
/// a própria pessoa ou quem do RH não está em <c>GESTORES</c>, não pode ser decidido por ela (o banco exige o gestor gravado).
/// </summary>
public sealed class ListarFeriasParaORhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta)
{
    public async Task<RespostaPaginada<FeriasPedidoAnalise>> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? situacao, int? setorId, CancellationToken ct)
    {
        var filtro = EnumsDeConsulta.Ler<SituacaoDasFerias>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca, null, null).Validado();
        var (itens, total) = await consulta.ListarParaRhAsync(filtro ?? SituacaoDasFerias.ComORh, setorId, parametros, ct);
        var contexto = await consulta.ContextoDaEquipeAsync(itens, ct);
        var quem = new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, await consulta.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct));
        return RespostaPaginada<FeriasPedidoAnalise>.De([.. itens.Select(i => FeriasPedidoAnalise.De(MontadorDoPedidoDeFerias.Montar(i, quem), contexto[i.Id].Conflitos, contexto[i.Id].EquipeDisponivel))], total, parametros);
    }
}

/// <summary>
/// <c>POST /api/rh/ferias/{feriasId}/confirmar</c> e <c>.../recusar</c>: a decisão FINAL do RH, numa transação só, com a pessoa do pedido travada ANTES (a mesma ordem de travas de toda escrita de férias). Não existe "o RH aprova direto". Erros, nesta ordem: 404 (pedido que não existe); 409
/// <c>PEDIDO_JA_DECIDIDO</c> (já terminou) ou <c>PEDIDO_ETAPA_INCORRETA</c> (ainda com o gestor ou esperando a resposta da pessoa); depois, só no pedido SEM etapa do gestor, 422 <c>PEDIDO_PROPRIO</c> (quem decide é a dona; vence) ou <c>PEDIDO_RH_FORA_DE_GESTORES</c> (quem decide não está em
/// <c>GESTORES</c>): o banco grava em <c>ID_GESTOR</c> a linha de quem decide. O RH pode confirmar o próprio pedido quando a etapa do gestor foi de outra pessoa, e recusar até o que o gestor aprovou. O motivo da recusa (5 a 500) vai em <c>decisaoRh.observacao</c> e no e-mail; a mensagem da pessoa
/// nunca é tocada.
/// </summary>
public sealed class DecidirFeriasComoRhCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeFerias consulta,
    IFeriasRepositorio repositorio,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IFilaDeEmails fila,
    IRelogio relogio)
{
    public Task<FeriasPedidoAnalise> ConfirmarAsync(int feriasId, CancellationToken ct) => DecidirAsync(feriasId, confirmar: true, motivo: null, ct);

    public Task<FeriasPedidoAnalise> RecusarAsync(int feriasId, FeriasRecusarComando? corpo, CancellationToken ct) => DecidirAsync(feriasId, confirmar: false, MotivoDaRecusaDeFerias.Ler(corpo), ct);

    private async Task<FeriasPedidoAnalise> DecidirAsync(int feriasId, bool confirmar, string? motivo, CancellationToken ct)
    {
        var gestorIdDoRh = await consulta.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct);
        var lido = await consulta.ObterAsync(feriasId, ct) ?? throw new NaoEncontradoException();
        ConferirAEtapa(lido);
        ConferirQuemDecide(lido, gestorIdDoRh);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                // A pessoa do pedido é travada ANTES do UPDATE (a mesma ordem de toda escrita de férias) e o pedido é relido já com a trava.
                _ = await repositorio.TravarPessoaAsync(lido.Funcionario.Id, ct) ?? throw new NaoEncontradoException();
                var pedido = await consulta.ObterAsync(feriasId, ct) ?? throw new NaoEncontradoException();
                ConferirAEtapa(pedido);
                ConferirQuemDecide(pedido, gestorIdDoRh);

                var agora = relogio.AgoraUtc;
                var decidiu = confirmar
                    ? await repositorio.ConfirmarComoRhAsync(feriasId, escopo.FuncionarioId, gestorIdDoRh, agora, ct)
                    : await repositorio.RecusarComoRhAsync(feriasId, escopo.FuncionarioId, gestorIdDoRh, agora, motivo!, ct);
                if (!decidiu)
                {
                    ConferirAEtapa(await consulta.ObterAsync(feriasId, ct) ?? throw new NaoEncontradoException());
                    throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
                }

                var descricao = $"{(confirmar ? "Confirmou" : "Recusou")} o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy} (etapa do RH).";
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, confirmar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou, descricao, $"Férias de {pedido.Funcionario.Nome}", "FERIAS", feriasId));
                await unidade.SalvarAsync(ct);
                await DecisaoDeFeriasPorEmail.EnfileirarAsync(fila, consulta, pedido, confirmar ? DecisaoDeFeriasPorEmail.RhConfirmou : DecisaoDeFeriasPorEmail.RhRecusou, escopo.Nome, motivo, null, null, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var decidido = await consulta.ObterAsync(feriasId, ct) ?? throw new NaoEncontradoException();
        var contexto = (await consulta.ContextoDaEquipeAsync([decidido], ct))[decidido.Id];
        return FeriasPedidoAnalise.De(MontadorDoPedidoDeFerias.Montar(decidido, new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, gestorIdDoRh)), contexto.Conflitos, contexto.EquipeDisponivel);
    }

    /// <summary>Só o pedido <c>COM_O_RH</c> se decide aqui: terminado = <c>PEDIDO_JA_DECIDIDO</c>; ainda com o gestor ou esperando a resposta da pessoa = <c>PEDIDO_ETAPA_INCORRETA</c>.</summary>
    private static void ConferirAEtapa(FeriasLida pedido)
    {
        switch (SituacaoDoPedidoDeFerias.Situacao(pedido.ComoLinha()))
        {
            case SituacaoDasFerias.ComORh:
                return;
            case SituacaoDasFerias.ComOGestor or SituacaoDasFerias.SugestaoDeData:
                throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
            default:
                throw MensagensDaGestao.PedidoJaDecidido.ComoProblema();
        }
    }

    /// <summary>Pedido sem etapa do gestor: o banco grava em <c>ID_GESTOR</c> a linha de quem decide, então a própria pessoa não decide (vence) e quem não está em <c>GESTORES</c> também não.</summary>
    private void ConferirQuemDecide(FeriasLida pedido, int? gestorIdDoRh)
    {
        if (pedido.GestorId is not null)
        {
            return;
        }

        if (escopo.FuncionarioId == pedido.Funcionario.Id)
        {
            throw MensagensDaGestao.PedidoProprio();
        }

        if (gestorIdDoRh is null)
        {
            throw MensagensDaGestao.PedidoRhForaDeGestores();
        }
    }
}

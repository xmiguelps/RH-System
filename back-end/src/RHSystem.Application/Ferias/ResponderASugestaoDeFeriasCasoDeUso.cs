using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Ferias;

/// <summary>
/// <c>POST /api/eu/ferias/{feriasId}/sugestao/aceitar</c> e <c>.../recusar</c>: a resposta da PESSOA à sugestão de outra data do gestor. Pedido de outra pessoa ou que não existe: 404; fora de <c>SUGESTAO_DE_DATA</c> (ainda com o gestor ou já com o RH): 409 <c>PEDIDO_ETAPA_INCORRETA</c>; terminado: 409
/// <c>PEDIDO_JA_DECIDIDO</c>. A pessoa é travada ANTES de tudo (a mesma ordem de travas de toda escrita de férias) e o estado é relido com a trava. <b>Aceitar</b> reconfere as regras com as datas sugeridas, TODAS menos a antecedência (a data foi escolhida pelo gestor), inclusive <c>CABE_NO_SALDO</c>
/// (só agora esses dias passam a ser reservados): 422 com a lista; troca as datas do pedido (as datas de antes só ficam no log de auditoria) e o pedido vai para o RH. <b>Recusar</b> cancela o pedido. <b>Nenhum e-mail</b> sai (a decisão é da própria pessoa). Se o prazo da sugestão
/// (<c>expiraEm</c>) já passou e a tarefa ainda não rodou, a resposta chega tarde: o pedido é expirado aqui (a mesma conta da tarefa) e a resposta é 409 <c>PEDIDO_JA_DECIDIDO</c>.
/// </summary>
public sealed class ResponderASugestaoDeFeriasCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeFerias consulta,
    IFeriasRepositorio repositorio,
    ConferidorDeFerias conferidor,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private enum Resultado
    {
        Respondeu,
        Expirou,
    }

    public Task<FeriasPedido> AceitarAsync(int feriasId, CancellationToken ct) => ResponderAsync(feriasId, aceitar: true, ct);

    public Task<FeriasPedido> RecusarAsync(int feriasId, CancellationToken ct) => ResponderAsync(feriasId, aceitar: false, ct);

    private async Task<FeriasPedido> ResponderAsync(int feriasId, bool aceitar, CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        ConferirEsperandoARespostaDaPessoa(await consulta.ObterDaPessoaAsync(feriasId, eu, ct) ?? throw new NaoEncontradoException());

        var resultado = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(eu, ct) ?? throw new NaoEncontradoException();
                var pedido = await consulta.ObterDaPessoaAsync(feriasId, eu, ct) ?? throw new NaoEncontradoException();
                ConferirEsperandoARespostaDaPessoa(pedido);
                var agora = relogio.AgoraUtc;
                var hoje = FusoBrasil.Hoje(agora);
                var sugestao = MapeadorDoPedidoDeFerias.Sugestao(pedido.ComoLinha()) ?? throw new NaoEncontradoException();
                var registroAfetado = $"Férias de {pessoa.Nome}";

                if (hoje > sugestao.ExpiraEm)
                {
                    // O prazo passou e a tarefa ainda não rodou: o pedido expira aqui, pela mesma regra, e a resposta chega tarde.
                    await ConferirQueMudouAsync(await repositorio.ExpirarSugestaoAsync(feriasId, eu, agora, ct), feriasId, eu, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, AuditoriaAcao.Alterou, $"A sugestão de outras datas para o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy} expirou sem resposta.", registroAfetado, "FERIAS", feriasId));
                    await unidade.SalvarAsync(ct);
                    return Resultado.Expirou;
                }

                if (aceitar)
                {
                    var outras = (await consulta.DaPessoaAsync(eu, ct)).Where(f => f.Id != feriasId).ToList();
                    var conferencia = await conferidor.ConferirAsync(new ComandoDeFerias(sugestao.DataInicio, sugestao.DataFim, pedido.AbonoPecuniario, pedido.DiasAbono), ModoDaConferencia.AceiteDaSugestao, pessoa, outras, hoje, pedido.PeriodoInicio, ct);
                    if (!conferencia.PodeEnviar)
                    {
                        throw MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.PedirOuAceitar, conferencia.Regras);
                    }

                    await ConferirQueMudouAsync(await repositorio.AceitarSugestaoAsync(feriasId, eu, agora, ct), feriasId, eu, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(
                        AuditoriaArea.Ferias,
                        AuditoriaAcao.Alterou,
                        $"Aceitou a sugestão de outras datas: o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy} passou para {sugestao.DataInicio:dd/MM/yyyy} a {sugestao.DataFim:dd/MM/yyyy}.",
                        registroAfetado,
                        "FERIAS",
                        feriasId));
                }
                else
                {
                    await ConferirQueMudouAsync(await repositorio.RecusarSugestaoAsync(feriasId, eu, agora, ct), feriasId, eu, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, AuditoriaAcao.Alterou, $"Recusou a sugestão de outras datas: o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy} foi cancelado.", registroAfetado, "FERIAS", feriasId));
                }

                await unidade.SalvarAsync(ct);
                return Resultado.Respondeu;
            },
            IsolationLevel.ReadCommitted,
            ct);

        if (resultado == Resultado.Expirou)
        {
            throw MensagensDaGestao.PedidoJaDecidido.ComoProblema();
        }

        var depois = await consulta.ObterDaPessoaAsync(feriasId, eu, ct) ?? throw new NaoEncontradoException();
        return MontadorDoPedidoDeFerias.Montar(depois, new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, eu, null));
    }

    /// <summary>O UPDATE condicional não pegou nenhuma linha: outra resposta (ou a tarefa) chegou antes; o estado de agora diz qual dos dois 409.</summary>
    private async Task ConferirQueMudouAsync(bool atualizou, int feriasId, int funcionarioId, CancellationToken ct)
    {
        if (atualizou)
        {
            return;
        }

        ConferirEsperandoARespostaDaPessoa(await consulta.ObterDaPessoaAsync(feriasId, funcionarioId, ct) ?? throw new NaoEncontradoException());
        throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
    }

    /// <summary>Só o pedido <c>SUGESTAO_DE_DATA</c> se responde: terminado = <c>PEDIDO_JA_DECIDIDO</c>; ainda com o gestor ou já com o RH = <c>PEDIDO_ETAPA_INCORRETA</c>.</summary>
    private static void ConferirEsperandoARespostaDaPessoa(FeriasLida pedido)
    {
        switch (SituacaoDoPedidoDeFerias.Situacao(pedido.ComoLinha()))
        {
            case SituacaoDasFerias.SugestaoDeData:
                return;
            case SituacaoDasFerias.ComOGestor or SituacaoDasFerias.ComORh:
                throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
            default:
                throw MensagensDaGestao.PedidoJaDecidido.ComoProblema();
        }
    }
}

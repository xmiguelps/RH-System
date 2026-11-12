using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/encerrar?confirmar=</c>: encerra o formulário PUBLICADO (os outros: 422 <c>FORMULARIO_NAO_PUBLICADO</c>). Grava <c>ATIVO</c> falso e a data de encerramento do relógio da aplicação no mesmo UPDATE e <b>cancela as próximas reaberturas</b> (a recorrência continua descrita;
/// não há "reabrir"). Sem <c>confirmar=true</c>, se ainda há quem possa responder (<c>pendentes</c>: pessoas que faltam; nas avaliações, avaliações que faltam), 409 <c>CONFIRMACAO_NECESSARIA</c> com o número. <b>No anônimo a confirmação é pedida SEMPRE</b> (mesmo com todos já tendo participado: encerrar
/// direto diria que não falta ninguém) e <c>pendentes</c> sai nulo; o caso de uso nem olha respostas. Formulário por link e Sugestão não têm ninguém a esperar. Evento <c>ALTEROU</c> só com o título, sem contagem. A gravação é um <c>UPDATE</c> condicionado ao estado lido: duas abas encerrando
/// dão um 200 e um 422.
/// </summary>
public sealed class EncerrarFormularioCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IDestinatariosDoFormulario destinatarios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private const int Tentativas = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, bool confirmar, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            if (MapeadorDoFormulario.SituacaoDe(lido, agora) != SituacaoDoFormulario.Publicado)
            {
                throw MensagensDosFormularios.FormularioNaoPublicadoParaEncerrar.ComoProblema();
            }

            if (!confirmar)
            {
                var pendentes = await PendentesDoFormulario.ContarAsync(consulta, destinatarios, lido, FusoBrasil.Hoje(agora), ct);
                if (pendentes is not 0)
                {
                    throw MensagensDosFormularios.ConfirmacaoNecessaria(pendentes);
                }
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await formularios.EncerrarAsync(lido, InstanteAoSegundo.De(agora), cancelarReaberturas: true, ct))
                    {
                        return false;
                    }

                    if (lido.Prazo is { } prazo)
                    {
                        await agendamento.CancelarEncerramentoAsync(formularioId, InstantesDoFormulario.EncerramentoNominal(prazo), ct);
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Encerrou o formulário \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                    await unidade.SalvarAsync(ct);
                    return true;
                },
                ct);
            if (gravou)
            {
                return MapeadorDoFormulario.Detalhe((await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado(), relogio.AgoraUtc);
            }
        }

        throw MensagensDosFormularios.FormularioMudouEmOutraTela;
    }
}

using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/publicar</c>: publica agora (<c>agendarPara</c> vazio ou de hoje: <c>ATIVO</c> e a data de publicação do relógio da aplicação) ou agenda (um dia futuro: <c>AGENDADO</c>, a tarefa publica no começo do dia -03:00, uma vez). As mesmas conferências do
/// "guardar" (só o rascunho publica), e depois, nesta ordem: 422 <c>FORMULARIO_ANONIMO_PUBLICO_PEQUENO</c> (anônimo para menos de 5 PESSOAS que respondem, ANTES da verificação) e 422 <c>VERIFICACAO_COM_BLOQUEIO</c> (a verificação com o público do CORPO). Por link gera o código mesmo
/// agendando. O anônimo publicado agora tem o público fotografado (uma linha por pessoa); agendado, a fotografia é tirada pela tarefa no dia. Um formulário recorrente guarda a próxima reabertura. Agenda o encerramento pelo prazo (no instante nominal: o começo do dia seguinte
/// ao prazo). <b>Nenhum e-mail</b> (a lista de e-mails do sistema é fechada). A gravação é um <c>UPDATE</c> condicionado ao estado lido: se outra aba publicou no meio, relê e dá o erro certo. Evento <c>PUBLICOU</c> só com o título, sem número de destinatários.
/// </summary>
public sealed class PublicarFormularioCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IDestinatariosDoFormulario destinatarios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private const int Tentativas = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, FormularioPublicacaoComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var hoje = FusoBrasil.Hoje(agora);
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var situacao = MapeadorDoFormulario.SituacaoDe(lido, agora);
            var campos = await ConferenciaDaPublicacao.ConferirAsync(consulta, lido, situacao, corpo, agora, paraPublicar: true, ct);

            var quem = await destinatarios.CalcularAsync(new PublicoDoFormulario(campos.Alvo, lido.Tipo, lido.Sentido, campos.SetorIds, campos.FuncionarioIds), hoje, ct);
            if (lido.Anonimo && quem.PessoasQueRespondem < MensagensDosFormularios.MinimoDePessoasNoAnonimo)
            {
                throw MensagensDosFormularios.AnonimoPequeno(quem.PessoasQueRespondem).ComoProblema();
            }

            if (!RegrasDaVerificacao.Montar(new EntradaDaVerificacao(lido.Titulo, lido.Descricao, lido.Tipo, lido.Anonimo, lido.Perguntas, quem)).PodePublicar)
            {
                throw MensagensDosFormularios.VerificacaoComBloqueio.ComoProblema();
            }

            var agendar = campos.AgendarPara is { } dia && dia > hoje;
            var dataDePublicacao = agendar ? InstantesDoFormulario.InicioDoDiaAgendado(campos.AgendarPara!.Value) : InstanteAoSegundo.De(agora);
            DateTime? proxima = !agendar && campos.Recorrencia is { } recorrencia ? InstantesDoFormulario.ProximaReaberturaDentroDoLimite(dataDePublicacao, recorrencia.Intervalo, recorrencia.Unidade, recorrencia.Ate) : null;
            var dados = new PublicacaoParaGravar(campos.Alvo, campos.SetorIds, campos.FuncionarioIds, campos.Prazo, campos.Recorrencia, campos.LembreteAutomatico, dataDePublicacao);

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await formularios.PublicarAsync(lido, dados, new EfeitoDaPublicacao(Ativar: !agendar, GerarToken: campos.Alvo == AlvoDoFormulario.Link, proxima), ct))
                    {
                        return false;
                    }

                    if (lido.Anonimo && !agendar)
                    {
                        await destinatarios.FotografarAsync(formularioId, hoje, ct);
                    }

                    if (campos.Prazo is { } prazo)
                    {
                        await agendamento.AgendarEncerramentoAsync(formularioId, InstantesDoFormulario.EncerramentoNominal(prazo), ct);
                        if (campos.LembreteAutomatico && campos.Alvo != AlvoDoFormulario.Link && lido.Tipo != TipoDeFormulario.Outro && RegrasDoLembrete.DiaDoAutomatico(prazo, FusoBrasil.DiaLocalDe(dataDePublicacao)) is { } diaDoLembrete)
                        {
                            await agendamento.AgendarLembreteAutomaticoAsync(formularioId, diaDoLembrete, ct);
                        }
                    }

                    if (agendar)
                    {
                        await agendamento.AgendarPublicacaoAsync(formularioId, dataDePublicacao, ct);
                    }

                    if (proxima is { } reabertura)
                    {
                        await agendamento.AgendarReaberturaAsync(formularioId, reabertura, ct);
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Publicou, Descricao(lido.Titulo, agendar, campos.AgendarPara), $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
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

    internal static string Descricao(string titulo, bool agendou, DateOnly? dia) =>
        agendou ? $"Agendou o formulário \"{titulo}\" para {dia!.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}." : $"Publicou o formulário \"{titulo}\".";
}

using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// <c>PUT /api/rh/formularios/{formularioId}/publicacao</c>: só GUARDA as escolhas da etapa Publicação; o formulário continua rascunho (ou agendado). Não roda a verificação, não confere o mínimo de 5 do anônimo, não manda e-mail e não gera o link. Ordem das recusas:
/// 404, 400 (todos os campos juntos; pessoa ou setor que não existe também), 422 publicado ou encerrado, 422 público que o tipo ou o anonimato não aceita (anônimo por link; só entrevista por link), 422 pessoa inativa (e ninguém entra). O <c>agendarPara</c> de um RASCUNHO é aceito e
/// NÃO é guardado (o banco só tem o dia enquanto o formulário está agendado); o de um AGENDADO muda o dia e reagenda a tarefa (cancela a chave antiga e agenda a nova); sem ele o dia guardado fica. A gravação é um <c>UPDATE</c> condicionado ao estado lido: se outra aba publicou
/// no meio, relê e dá o erro certo. Evento <c>ALTEROU</c> só com o título: nem as pessoas nem o público vão para a auditoria.
/// </summary>
public sealed class SalvarPublicacaoCasoDeUso(IConsultaDeFormularios consulta, IFormulariosRepositorio formularios, IAgendamentoDoFormulario agendamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const int Tentativas = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, FormularioPublicacaoComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var situacao = MapeadorDoFormulario.SituacaoDe(lido, agora);
            var erros = new ColetorDeErros();
            var campos = ValidacaoDaPublicacao.Validar(erros, corpo, FusoBrasil.Hoje(agora), lido.Tipo);
            ConferirPrazoContraODiaAgendado(erros, corpo, lido, situacao);
            if (campos is not null)
            {
                await ConferirExistenciaAsync(erros, campos, ct);
            }

            erros.LancarSeHouver();
            ConferirSituacao(situacao);
            if (RegrasDaPublicacao.Recusa(lido.Tipo, lido.Anonimo, campos!.Alvo) is { } recusa)
            {
                throw (recusa == RecusaDePublicacao.AnonimoSemLink ? MensagensDosFormularios.AnonimoSemLink : MensagensDosFormularios.LinkSoParaEntrevista).ComoProblema();
            }

            await ConferirPessoasAtivasAsync(campos, ct);
            var novoDia = situacao == SituacaoDoFormulario.Agendado && campos.AgendarPara is { } dia ? InstantesDoFormulario.InicioDoDiaAgendado(dia) : (DateTime?)null;
            if (novoDia == lido.PublicadoEmUtc)
            {
                novoDia = null;
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var mudou = await formularios.AtualizarPublicacaoAsync(lido, new PublicacaoParaGravar(campos.Alvo, campos.SetorIds, campos.FuncionarioIds, campos.Prazo, campos.Recorrencia, campos.LembreteAutomatico, novoDia), ct);
                    if (!mudou)
                    {
                        return false;
                    }

                    if (novoDia is { } novo)
                    {
                        if (lido.PublicadoEmUtc is { } antigo)
                        {
                            await agendamento.CancelarPublicacaoAsync(formularioId, antigo, ct);
                        }

                        await agendamento.AgendarPublicacaoAsync(formularioId, novo, ct);
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Guardou a publicação do formulário \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
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

    /// <summary>Num agendado sem <c>agendarPara</c> no corpo o dia guardado continua valendo, e o prazo não pode ser anterior a ele.</summary>
    private static void ConferirPrazoContraODiaAgendado(ColetorDeErros erros, FormularioPublicacaoComando corpo, FormularioLido lido, SituacaoDoFormulario situacao)
    {
        if (situacao == SituacaoDoFormulario.Agendado && corpo.AgendarPara is null && corpo.Prazo is { } prazo && lido.PublicadoEmUtc is { } publicacao && prazo < FusoBrasil.DiaLocalDe(publicacao))
        {
            erros.Adicionar("prazo", "O prazo não pode ser anterior ao dia agendado.");
        }
    }

    private async Task ConferirExistenciaAsync(ColetorDeErros erros, PublicacaoValidada campos, CancellationToken ct)
    {
        if (campos.FuncionarioIds.Count > 0 && (await consulta.PessoasAsync(campos.FuncionarioIds, ct)).Count != campos.FuncionarioIds.Count)
        {
            erros.Adicionar("funcionarioIds", "Uma das pessoas escolhidas não existe.");
        }

        if (campos.SetorIds.Count > 0 && await consulta.ContarSetoresExistentesAsync(campos.SetorIds, ct) != campos.SetorIds.Count)
        {
            erros.Adicionar("setorIds", "Um dos setores escolhidos não existe.");
        }
    }

    private static void ConferirSituacao(SituacaoDoFormulario situacao)
    {
        if (situacao == SituacaoDoFormulario.Publicado)
        {
            throw MensagensDosFormularios.FormularioJaPublicado.ComoProblema();
        }

        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaEditar.ComoProblema();
        }
    }

    private async Task ConferirPessoasAtivasAsync(PublicacaoValidada campos, CancellationToken ct)
    {
        if (campos.FuncionarioIds.Count > 0 && (await consulta.PessoasAsync(campos.FuncionarioIds, ct)).Any(p => !p.Ativo))
        {
            throw MensagensDosFormularios.PessoaInativaNoPublico.ComoProblema();
        }
    }
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/verificacao</c>: os itens da etapa Verificação (<see cref="RegrasDaVerificacao"/>) com o público JÁ GUARDADO (o "publicar" a repete com o público do corpo). Vale em qualquer situação; só lê.
/// </summary>
public sealed class VerificarFormularioCasoDeUso(IConsultaDeFormularios consulta, IDestinatariosDoFormulario destinatarios, IRelogio relogio)
{
    public async Task<FormularioVerificacao> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var quem = (await destinatarios.ObterAsync(formularioId, FusoBrasil.Hoje(relogio.AgoraUtc), ct)).OuNaoEncontrado();
        return RegrasDaVerificacao.Montar(new EntradaDaVerificacao(lido.Titulo, lido.Descricao, lido.Tipo, lido.Anonimo, lido.Perguntas, quem));
    }
}

using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// <c>PUT /api/rh/formularios/{formularioId}/prazo</c> (decisão do dono, 2026-11-17): muda o prazo de um formulário PUBLICADO. Ordem: 404, 400 (o prazo é obrigatório, não é anterior a hoje; a caixa de sugestões não tem prazo), 422 <c>FORMULARIO_ENCERRADO</c> (inclusive o prazo vencido que a tarefa ainda não encerrou)
/// e 422 <c>FORMULARIO_NAO_PUBLICADO</c> (rascunho e agendado guardam o prazo na etapa Publicação). O mesmo prazo é 200 sem efeito. Numa transação: troca o prazo (um <c>UPDATE</c> condicionado ao estado lido), cancela a tarefa de encerrar e o lembrete automático do prazo antigo e agenda os do novo (a chave leva o
/// instante e uma chave cancelada volta, então ir e voltar não cria tarefa a mais). O lembrete do prazo novo só é agendado se o dia dele (7 dias antes) ainda não passou. A próxima reabertura de um formulário recorrente não muda; a rodada seguinte mantém a duração da rodada atual. Evento <c>ALTEROU</c> só com o título e os dias.
/// </summary>
public sealed class AlterarPrazoDoFormularioCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private const int Tentativas = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, FormularioPrazoComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var hoje = FusoBrasil.Hoje(agora);
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var erros = new ColetorDeErros();
            ValidacaoDoPrazoDoFormulario.Validar(erros, corpo.Prazo, hoje, lido.Tipo);
            erros.LancarSeHouver();
            var novo = corpo.Prazo;

            var situacao = MapeadorDoFormulario.SituacaoDe(lido, agora);
            if (situacao == SituacaoDoFormulario.Encerrado)
            {
                throw MensagensDosFormularios.FormularioEncerradoParaEditar.ComoProblema();
            }

            if (situacao != SituacaoDoFormulario.Publicado)
            {
                throw MensagensDosFormularios.FormularioNaoPublicadoParaPrazo.ComoProblema();
            }

            if (lido.Prazo == novo)
            {
                return MapeadorDoFormulario.Detalhe(lido, agora);
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await formularios.AtualizarPrazoAsync(lido, novo, ct))
                    {
                        return false;
                    }

                    await TrocarAsTarefasAsync(lido, novo, hoje, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, Descricao(lido.Titulo, lido.Prazo, novo), $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
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

    private async Task TrocarAsTarefasAsync(FormularioLido lido, DateOnly novo, DateOnly hoje, CancellationToken ct)
    {
        // O lembrete automático existe só para formulário de pessoas ou setores (nunca por link nem a caixa de sugestões) com o lembrete ligado.
        var comLembrete = lido.LembreteAutomatico && lido.Alvo != AlvoDoFormulario.Link && lido.Tipo != TipoDeFormulario.Outro;
        if (lido.Prazo is { } antigo)
        {
            await agendamento.CancelarEncerramentoAsync(lido.Id, InstantesDoFormulario.EncerramentoNominal(antigo), ct);
            if (comLembrete && lido.PublicadoEmUtc is { } publicadoEmUtc && RegrasDoLembrete.DiaDoAutomatico(antigo, FusoBrasil.DiaLocalDe(publicadoEmUtc)) is { } diaAntigo)
            {
                await agendamento.CancelarLembreteAutomaticoAsync(lido.Id, diaAntigo, ct);
            }
        }

        await agendamento.AgendarEncerramentoAsync(lido.Id, InstantesDoFormulario.EncerramentoNominal(novo), ct);
        if (comLembrete && RegrasDoLembrete.DiaDoAutomatico(novo, hoje) is { } diaNovo)
        {
            await agendamento.AgendarLembreteAutomaticoAsync(lido.Id, diaNovo, ct);
        }
    }

    private static string Descricao(string titulo, DateOnly? antigo, DateOnly novo) =>
        antigo is { } dia
            ? string.Create(CultureInfo.InvariantCulture, $"Mudou o prazo do formulário \"{titulo}\" de {dia:dd/MM/yyyy} para {novo:dd/MM/yyyy}.")
            : string.Create(CultureInfo.InvariantCulture, $"Definiu o prazo do formulário \"{titulo}\" para {novo:dd/MM/yyyy}.");
}

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/rascunho</c> (decisão do dono, 2026-11-17): devolve um formulário AGENDADO a rascunho. Só o agendado (os outros: 422 <c>FORMULARIO_NAO_ESTA_AGENDADO</c>). Numa transação: apaga a data de publicação e o código do link (um <c>UPDATE</c> condicionado ao estado lido; o link agendado deixa de valer e publicar de novo gera outro), cancela
/// todas as tarefas pendentes do formulário (publicar, encerrar, lembrete) e registra o evento <c>ALTEROU</c>. As demais escolhas da publicação (alvo, pessoas, setores, prazo, recorrência, lembrete) continuam guardadas; o dia agendado não, porque o rascunho não o guarda. Duas abas dão um 200 e um 422.
/// </summary>
public sealed class VoltarFormularioParaRascunhoCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private const int Tentativas = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            if (MapeadorDoFormulario.SituacaoDe(lido, relogio.AgoraUtc) != SituacaoDoFormulario.Agendado)
            {
                throw MensagensDosFormularios.FormularioNaoEstaAgendado.ComoProblema();
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await formularios.VoltarParaRascunhoAsync(lido, ct))
                    {
                        return false;
                    }

                    await agendamento.CancelarTudoAsync(formularioId, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Voltou o formulário \"{lido.Titulo}\" para rascunho.", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
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

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// <c>REABRIR_FORMULARIO:formulario={id}:em={instante}</c>: a reabertura do formulário RECORRENTE. <b>Relê o formulário</b> e só age se a data da chave ainda é a próxima reabertura guardada (encerrado à mão cancela a recorrência: a tarefa antiga não faz nada; rodar duas vezes reabre uma vez só, porque a primeira
/// avança a data). <b>Só reabre o que está ENCERRADO</b> (pela mesma função de situação, então o prazo vencido com a tarefa de encerrar parada conta): se ainda está publicado, NADA é apagado e a data avança para a próxima (próxima reabertura mais o intervalo). Reabrir <b>apaga as respostas, as participações (e os arquivos delas)
/// da rodada anterior</b>, liga o <c>ATIVO</c> e limpa o encerramento juntos, grava a nova data de publicação (a da chave), avança o prazo o MESMO número de dias da rodada anterior, recalcula a próxima reabertura (até <c>recorrencia.ate</c>) e agenda o encerramento, o lembrete automático e a reabertura seguinte. O anônimo tira a
/// fotografia de novo e só reabre com 5 ou mais pessoas ativas; com menos, fica ENCERRADO e a recorrência é cancelada. Evento <c>ALTEROU</c> do Sistema, sem contagem. A recuperação agenda a reabertura de todo formulário que tem a próxima data e ficou sem tarefa.
/// </summary>
public sealed class ReabrirFormularioTarefa(
    AppDbContext db,
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IDestinatariosDoFormulario destinatarios,
    IAgendamentoDoFormulario agendamento,
    IArmazenamentoDeArquivos armazenamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio,
    ILogger<ReabrirFormularioTarefa> log) : ITarefaAgendada
{
    public string Tipo => AgendamentoDoFormulario.TipoDeReabrir;

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var formularioId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o formulário (ReferenciaId).");
        var instante = AgendamentoDoFormulario.InstanteDaChave(tarefa.ChaveDeIdempotencia) ?? throw new InvalidOperationException("A chave da tarefa não traz o instante da reabertura.");
        var lido = await consulta.ObterAsync(formularioId, ct);
        if (lido is null || lido.ProximaReaberturaEmUtc != instante || lido.RecorrenciaIntervalo is not { } intervalo || lido.RecorrenciaUnidade is not { } unidadeDaRecorrencia || lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores))
        {
            return;
        }

        var agora = relogio.AgoraUtc;
        DateTime? seguinte = InstantesDoFormulario.ProximaReaberturaDentroDoLimite(instante, intervalo, unidadeDaRecorrencia, lido.RecorrenciaAte);
        if (RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agora) != SituacaoDoFormulario.Encerrado)
        {
            await AvancarSemApagarAsync(formularioId, instante, seguinte, ct); // a rodada ainda está aberta: nada se apaga
            return;
        }

        var novaPublicacao = instante;
        DateOnly? novoPrazo = lido.Prazo is { } prazo && lido.PublicadoEmUtc is { } publicada ? InstantesDoFormulario.NovoPrazo(prazo, publicada, novaPublicacao) : null;
        var hoje = FusoBrasil.Hoje(agora);
        IReadOnlyList<string>? arquivos;
        try
        {
            arquivos = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var apagados = await formularios.ReabrirAsync(lido, instante, novaPublicacao, novoPrazo, seguinte, ct);
                    if (apagados is null)
                    {
                        return null; // outra execução já avançou a data
                    }

                    if (lido.Anonimo && (await destinatarios.FotografarAsync(formularioId, hoje, ct)).PessoasQueRespondem < MensagensDosFormularios.MinimoDePessoasNoAnonimo)
                    {
                        throw new PublicoAbaixoDoMinimoException(); // desfaz a reabertura inteira (inclusive o apagar das respostas)
                    }

                    await AgendarRodadaAsync(lido, novoPrazo, novaPublicacao, seguinte, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Reabriu o formulário \"{lido.Titulo}\" para uma nova rodada.", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                    await unidade.SalvarAsync(ct);
                    return apagados;
                },
                ct);
        }
        catch (PublicoAbaixoDoMinimoException)
        {
            await CancelarRecorrenciaAsync(lido, instante, ct);
            return;
        }

        foreach (var caminho in arquivos ?? [])
        {
            await ApagarArquivoAsync(caminho);
        }
    }

    /// <summary>O formulário que tem a próxima data e ficou sem a tarefa (a tarefa só existia na memória de um processo que caiu, ou a data foi gravada sem agendar).</summary>
    public async Task RecuperarAsync(CancellationToken ct)
    {
        var comProxima = await db.Formularios.AsNoTracking().Where(f => f.ProximaReaberturaEmUtc != null && f.RecorrenciaIntervalo != null).Select(f => new { f.Id, f.ProximaReaberturaEmUtc }).ToListAsync(ct);
        foreach (var formulario in comProxima)
        {
            await agendamento.AgendarReaberturaAsync(formulario.Id, formulario.ProximaReaberturaEmUtc!.Value, ct);
        }
    }

    private async Task AvancarSemApagarAsync(int formularioId, DateTime instante, DateTime? seguinte, CancellationToken ct) =>
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await formularios.AvancarReaberturaAsync(formularioId, instante, seguinte, ct))
                {
                    return false;
                }

                if (seguinte is { } proxima)
                {
                    await agendamento.AgendarReaberturaAsync(formularioId, proxima, ct);
                }

                return true;
            },
            ct);

    /// <summary>Agenda o que a rodada nova precisa: o encerramento pelo prazo novo, o lembrete automático dela (7 dias antes do prazo) e a reabertura seguinte.</summary>
    private async Task AgendarRodadaAsync(FormularioLido lido, DateOnly? novoPrazo, DateTime novaPublicacao, DateTime? seguinte, CancellationToken ct)
    {
        if (novoPrazo is { } prazo)
        {
            await agendamento.AgendarEncerramentoAsync(lido.Id, InstantesDoFormulario.EncerramentoNominal(prazo), ct);
            if (lido.LembreteAutomatico && RegrasDoLembrete.DiaDoAutomatico(prazo, FusoBrasil.DiaLocalDe(novaPublicacao)) is { } dia)
            {
                await agendamento.AgendarLembreteAutomaticoAsync(lido.Id, dia, ct);
            }
        }

        if (seguinte is { } proxima)
        {
            await agendamento.AgendarReaberturaAsync(lido.Id, proxima, ct);
        }
    }

    /// <summary>O anônimo que não chegou a 5 pessoas fica ENCERRADO e a recorrência acaba (a data da próxima reabertura sai); o evento diz o motivo sem número.</summary>
    private async Task CancelarRecorrenciaAsync(FormularioLido lido, DateTime instante, CancellationToken ct) =>
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await formularios.AvancarReaberturaAsync(lido.Id, instante, null, ct))
                {
                    return false;
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Não reabriu o formulário \"{lido.Titulo}\": o público ficou abaixo do mínimo e a recorrência foi cancelada.", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);

    private async Task ApagarArquivoAsync(string caminho)
    {
        try
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
        }
        catch (Exception erro)
        {
            // A reabertura já valeu: a sobra fica para a LIMPAR_ARQUIVOS_ORFAOS (a fonte de referências dos formulários não a tem mais). Só o tipo da exceção vai para o log.
            log.LogWarning("Não foi possível apagar um arquivo de resposta da rodada anterior ({Tipo}); a limpeza de sobras o apaga depois.", erro.GetType().Name);
        }
    }

    private sealed class PublicoAbaixoDoMinimoException : Exception;
}

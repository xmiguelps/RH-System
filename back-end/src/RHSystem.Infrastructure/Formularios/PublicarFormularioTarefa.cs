using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// <c>PUBLICAR_FORMULARIO:formulario={id}:em={instante}</c>: publica o formulário AGENDADO no começo do dia escolhido, UMA vez. <b>Relê o formulário</b> e só age se ele ainda existe, continua agendado (inativo, sem encerramento) e a data guardada é exatamente a da chave (reagendado ou publicado à mão
/// nesse meio tempo, a tarefa antiga não faz nada, e rodar duas vezes também não). No anônimo confere de novo o público mínimo na hora: com menos de 5 pessoas NÃO publica e o formulário continua <c>AGENDADO</c> (a verificação mostra o bloqueio; quem corrige o público escolhe outro dia, que cria outra tarefa),
/// e ao publicar tira a fotografia (o público do DIA da publicação). Um formulário recorrente ganha a próxima reabertura. O evento <c>PUBLICOU</c> sai como Sistema. <b>Sem e-mail.</b> A recuperação (na subida e a cada hora) agenda a tarefa de todo agendado que ficou sem ela.
/// </summary>
public sealed class PublicarFormularioTarefa(
    AppDbContext db,
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IDestinatariosDoFormulario destinatarios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio) : ITarefaAgendada
{
    public string Tipo => AgendamentoDoFormulario.TipoDePublicar;

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var formularioId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o formulário (ReferenciaId).");
        var instante = AgendamentoDoFormulario.InstanteDaChave(tarefa.ChaveDeIdempotencia) ?? throw new InvalidOperationException("A chave da tarefa não traz o instante da publicação.");
        var lido = await consulta.ObterAsync(formularioId, ct);
        if (lido is null || lido.Ativo || lido.EncerradoEmUtc is not null || lido.PublicadoEmUtc != instante)
        {
            return;
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        if (lido.Anonimo && (await destinatarios.ObterAsync(formularioId, hoje, ct))!.PessoasQueRespondem < MensagensDosFormularios.MinimoDePessoasNoAnonimo)
        {
            return;
        }

        DateTime? proxima = lido.RecorrenciaIntervalo is { } intervalo && lido.RecorrenciaUnidade is { } unidadeDaRecorrencia
            ? InstantesDoFormulario.ProximaReaberturaDentroDoLimite(instante, intervalo, unidadeDaRecorrencia, lido.RecorrenciaAte)
            : null;
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await formularios.AtivarAgendadoAsync(formularioId, instante, proxima, ct))
                {
                    return false;
                }

                if (lido.Anonimo)
                {
                    await destinatarios.FotografarAsync(formularioId, hoje, ct);
                }

                if (proxima is { } reabertura)
                {
                    await agendamento.AgendarReaberturaAsync(formularioId, reabertura, ct);
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Publicou, $"Publicou o formulário \"{lido.Titulo}\" (agendado).", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agendados = await db.Formularios.AsNoTracking().Where(f => !f.Ativo && f.EncerradoEmUtc == null && f.PublicadoEmUtc != null).Select(f => new { f.Id, f.PublicadoEmUtc }).ToListAsync(ct);
        foreach (var agendado in agendados)
        {
            await agendamento.AgendarPublicacaoAsync(agendado.Id, agendado.PublicadoEmUtc!.Value, ct);
        }
    }
}

/// <summary>
/// <c>ENCERRAR_FORMULARIO:formulario={id}:em={instante nominal}</c>: encerra o formulário publicado quando o prazo passa. O instante da chave é o encerramento NOMINAL (o começo do dia seguinte ao prazo, -03:00) e é ele que fica gravado em <c>DATA_ENCERRAMENTO</c>, não a hora em que a
/// tarefa rodou. <b>Relê o formulário</b> e só age se ele ainda está publicado (ativo, sem encerramento) e o prazo guardado ainda é o da chave (prazo tirado ou mudado, encerrado à mão ou apagado: a tarefa antiga não faz nada). O encerramento pelo prazo NÃO cancela a próxima reabertura (só o encerramento
/// à mão cancela). Evento <c>ALTEROU</c> como Sistema, sem contagem. A recuperação agenda a tarefa de todo publicado com prazo que ficou sem ela.
/// </summary>
public sealed class EncerrarFormularioTarefa(
    AppDbContext db,
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IAgendamentoDoFormulario agendamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor) : ITarefaAgendada
{
    public string Tipo => AgendamentoDoFormulario.TipoDeEncerrar;

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var formularioId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o formulário (ReferenciaId).");
        var instante = AgendamentoDoFormulario.InstanteDaChave(tarefa.ChaveDeIdempotencia) ?? throw new InvalidOperationException("A chave da tarefa não traz o instante do encerramento.");
        var lido = await consulta.ObterAsync(formularioId, ct);
        if (lido is null || !lido.Ativo || lido.EncerradoEmUtc is not null || lido.Prazo is not { } prazo || InstantesDoFormulario.EncerramentoNominal(prazo) != instante)
        {
            return;
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await formularios.EncerrarAsync(lido, instante, cancelarReaberturas: false, ct))
                {
                    return false;
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Encerrou o formulário \"{lido.Titulo}\" pelo prazo.", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var abertos = await db.Formularios.AsNoTracking().Where(f => f.Ativo && f.EncerradoEmUtc == null && f.Prazo != null).Select(f => new { f.Id, f.Prazo }).ToListAsync(ct);
        foreach (var aberto in abertos)
        {
            await agendamento.AgendarEncerramentoAsync(aberto.Id, InstantesDoFormulario.EncerramentoNominal(aberto.Prazo!.Value), ct);
        }
    }
}

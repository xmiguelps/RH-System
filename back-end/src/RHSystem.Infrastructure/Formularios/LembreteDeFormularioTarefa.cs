using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A tarefa <c>LEMBRETE_FORMULARIO</c>: o lembrete AUTOMÁTICO (7 dias antes do prazo; a chave é a marca do dia, <c>PENDENTE</c> no começo do dia) e o ENVIO do lembrete manual do anônimo (a rota só marca e agenda; chave com <c>:envio=1</c>). Relê o formulário antes de agir e não faz nada se ele sumiu, não está mais
/// PUBLICADO (encerrou), é por link, é Sugestão, ou (no automático) a caixa foi desligada ou o prazo já não é o do dia da chave. Com nome: um e-mail por pessoa que falta (a chave formulário + pessoa + dia o repete no máximo uma vez por dia); anônimo: e-mails sigilosos. O evento do automático sai como Sistema
/// (no anônimo SEMPRE e sem número; com nome só se alguém recebeu); o envio do manual do anônimo não grava evento, porque a rota já gravou o dela.
/// </summary>
public sealed class LembreteDeFormularioTarefa(
    AppDbContext db,
    IConsultaDeFormularios consulta,
    LembretesDoFormulario lembretes,
    IAgendamentoDoFormulario agendamento,
    IAuditor auditor,
    IRelogio relogio) : ITarefaAgendada
{
    private const string MarcaDoEnvio = ":envio=1";

    public string Tipo => RegrasDoLembrete.TipoDaTarefa;

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var formularioId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o formulário (ReferenciaId).");
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var dia = tarefa.DiaReferencia ?? hoje;
        var envioDoManual = tarefa.ChaveDeIdempotencia.EndsWith(MarcaDoEnvio, StringComparison.Ordinal);

        var lido = await consulta.ObterAsync(formularioId, ct);
        if (lido is null || RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agora) != SituacaoDoFormulario.Publicado
            || lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores) || lido.Tipo == TipoDeFormulario.Outro)
        {
            return;
        }

        if (!envioDoManual && (!lido.LembreteAutomatico || lido.Prazo is not { } prazo || prazo.AddDays(-RegrasDoLembrete.DiasAntesDoPrazo) != dia))
        {
            return;
        }

        if (lido.Anonimo)
        {
            _ = await lembretes.EnviarSigilosoAsync(lido, hoje, ct);
            if (!envioDoManual)
            {
                await auditor.RegistrarEGravarAsync(LembrarQuemFaltaFormularioCasoDeUso.Evento(lido, $"Mandou o lembrete automático do formulário \"{lido.Titulo}\"."), ct);
            }

            return;
        }

        var (enviados, _) = await lembretes.LembrarComNomeAsync(lido, [], hoje, ct);
        if (enviados > 0)
        {
            await auditor.RegistrarEGravarAsync(LembrarQuemFaltaFormularioCasoDeUso.Evento(lido, $"Mandou o lembrete automático do formulário \"{lido.Titulo}\" para {enviados} pessoa(s) que ainda não responderam."), ct);
        }
    }

    /// <summary>Agenda o automático que ficou para trás (formulário publicado com prazo e a caixa ligada que não tem a tarefa): só de hoje em diante, nunca um lembrete atrasado.</summary>
    public async Task RecuperarAsync(CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var abertos = await db.Formularios.AsNoTracking()
            .Where(f => f.Ativo && f.EncerradoEmUtc == null && f.Prazo != null && f.LembreteAutomatico && f.PublicadoEmUtc != null && f.Tipo != TipoDeFormulario.Outro && (f.Alvo == AlvoDoFormulario.Pessoas || f.Alvo == AlvoDoFormulario.Setores))
            .Select(f => new { f.Id, f.Prazo, f.PublicadoEmUtc })
            .ToListAsync(ct);
        foreach (var aberto in abertos)
        {
            if (RegrasDoLembrete.DiaDoAutomatico(aberto.Prazo!.Value, FusoBrasil.DiaLocalDe(aberto.PublicadoEmUtc!.Value)) is { } dia && dia >= hoje)
            {
                await agendamento.AgendarLembreteAutomaticoAsync(aberto.Id, dia, ct);
            }
        }
    }
}

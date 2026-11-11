using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Agendamento.Tarefas;

/// <summary>
/// <c>LIMPAR_EMAILS</c>: apaga da fila o que já terminou há mais de <c>Retencao:EmailsDias</c> (contados da criação): <c>ENVIADO</c>, <c>FALHOU</c>, <c>DEVOLVIDO</c> e
/// <c>CANCELADO</c>. O endereço do destinatário é dado pessoal, e depois do prazo não há motivo para guardá-lo. <b>Nunca</b> apaga <c>PENDENTE</c> nem <c>EM_ENVIO</c>. A
/// recuperação agenda uma por dia (<c>LIMPAR_EMAILS:dia=AAAA-MM-DD</c>).
/// </summary>
public sealed class LimparEmailsAntigosTarefa(AppDbContext db, IAgendador agendador, IRelogio relogio, OpcoesDeRetencao retencao) : ITarefaAgendada
{
    public const string TipoDaTarefa = "LIMPAR_EMAILS";

    private static readonly SituacaoDoEmail[] Terminadas = [SituacaoDoEmail.Enviado, SituacaoDoEmail.Falhou, SituacaoDoEmail.Devolvido, SituacaoDoEmail.Cancelado];

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var corte = relogio.AgoraUtc.AddDays(-retencao.EmailsDias);

        // AUDITORIA-OK: tabela técnica, sem registro por decisão (limpeza de retenção: o endereço do destinatário é dado pessoal)
        await db.EmailsNaFila.Where(e => Terminadas.Contains(e.Situacao) && e.CriadoEm < corte).ExecuteDeleteAsync(ct);
    }
}

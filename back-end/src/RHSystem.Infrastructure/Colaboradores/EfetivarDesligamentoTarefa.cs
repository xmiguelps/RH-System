using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// <c>EFETIVAR_DESLIGAMENTO</c>: no dia seguinte ao último dia de trabalho (00:00 de -03:00) grava o desligamento (<see cref="IDesligamentoDeFuncionarios.EfetivarAsync"/>). A chave é
/// <c>EFETIVAR_DESLIGAMENTO:desligamento={id}</c>, <c>ReferenciaId</c> é o id do desligamento. Idempotente: <c>JaEstavaEfetivado</c> e <c>NaoEstavaAgendado</c> (cancelado) voltam normalmente e a tarefa
/// termina; <c>AindaNaoPode</c> LANÇA, e o agendador tenta de novo com espera (a mensagem não leva dado pessoal: vai para <c>ULTIMO_ERRO</c>).
/// <para>
/// <b>Recuperação</b> (na subida e a cada hora): todo <c>AGENDADO</c> VENCIDO é efetivado direto (uma tarefa <c>FALHOU</c> ou perdida não deixa ninguém para trás) e todo <c>AGENDADO</c> que ainda não venceu
/// ganha a tarefa (o agendamento é idempotente pela chave). Uma pessoa que falha não impede as outras: o primeiro erro sobe no fim.
/// </para>
/// </summary>
public sealed class EfetivarDesligamentoTarefa(AppDbContext db, IDesligamentoDeFuncionarios desligamentos, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "EFETIVAR_DESLIGAMENTO";

    public string Tipo => TipoDaTarefa;

    public static string Chave(int desligamentoId) => ChaveDeIdempotencia.Montar(TipoDaTarefa, ("desligamento", desligamentoId));

    /// <summary>O INÍCIO do dia seguinte ao último dia de trabalho, em UTC: a partir daí (inclusive) o desligamento pode ser gravado.</summary>
    public static DateTime ExecutarApos(DateOnly ultimoDiaDeTrabalho) => FusoBrasil.InicioDoDiaEmUtc(ultimoDiaDeTrabalho.AddDays(1));

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var desligamentoId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o desligamento (ReferenciaId).");
        if (await desligamentos.EfetivarAsync(desligamentoId, ct) == ResultadoDaEfetivacao.AindaNaoPode)
        {
            throw new InvalidOperationException("Desligamento ainda não pode ser efetivado.");
        }
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var agendados = await db.DesligamentosAgendados.AsNoTracking()
            .Where(d => d.Situacao == SituacaoDoDesligamento.Agendado)
            .OrderBy(d => d.Id)
            .Select(d => new { d.Id, d.UltimoDiaTrabalho })
            .ToListAsync(ct);

        Exception? primeiroErro = null;
        foreach (var agendado in agendados)
        {
            try
            {
                if (agora >= ExecutarApos(agendado.UltimoDiaTrabalho))
                {
                    await desligamentos.EfetivarAsync(agendado.Id, ct); // vencido: direto (AindaNaoPode fica para a próxima hora)
                }
                else
                {
                    await agendador.AgendarAsync(new NovaTarefa(Tipo, Chave(agendado.Id), ExecutarApos(agendado.UltimoDiaTrabalho), ReferenciaId: agendado.Id), ct);
                }
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                primeiroErro ??= erro;
            }
        }

        if (primeiroErro is not null)
        {
            ExceptionDispatchInfo.Capture(primeiroErro).Throw();
        }
    }
}

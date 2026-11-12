using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// <c>CONSOLIDAR_BANCO_HORAS:dia=AAAA-MM-DD</c>: lança o banco de horas de todo mundo que trabalhava naquele dia (da admissão ao desligamento, inativo inclusive: ele só atualiza a linha que já tem). Roda a partir das <b>12:00 (-03:00) do dia seguinte</b>: a jornada que atravessa
/// a meia-noite (22:00 às 06:00) só fecha de manhã, e a janela dela é de 12 horas. O dia vem do <c>DiaReferencia</c>. <b>Idempotente</b> (rodar duas vezes não duplica) e uma pessoa que falha não impede as outras: o primeiro erro sobe no fim e o agendador tenta de novo.
/// <para>
/// <b>Recuperação</b> (na subida e a cada hora): cria a tarefa de cada dia, do seguinte ao último consolidado até hoje, no máximo 60 dias para trás; assim uma parada de vários dias se recupera sozinha e a tarefa de hoje já fica agendada para amanhã ao meio-dia.
/// </para>
/// </summary>
public sealed class ConsolidarBancoDeHorasTarefa(AppDbContext db, IRecalculoDoBancoDeHoras recalculo, IUltimoDiaConsolidado ultimoConsolidado, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "CONSOLIDAR_BANCO_HORAS";

    /// <summary>Mais longe que isto a recuperação não volta (o que ficou para trás de mais tem de ser refeito à mão).</summary>
    public const int HorizonteDaRecuperacaoEmDias = 60;

    private const int HoraDeInicioNoDiaSeguinte = 12;

    public string Tipo => TipoDaTarefa;

    public static string Chave(DateOnly dia) => ChaveDeIdempotencia.Montar(TipoDaTarefa, ("dia", dia));

    /// <summary>12:00 (-03:00) do dia seguinte, em UTC.</summary>
    public static DateTime ExecutarApos(DateOnly dia) => FusoBrasil.InicioDoDiaEmUtc(dia.AddDays(1)).AddHours(HoraDeInicioNoDiaSeguinte);

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var dia = tarefa.DiaReferencia ?? throw new InvalidOperationException("A tarefa não traz o dia (DiaReferencia).");
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(f => f.DataAdmissao <= dia && (f.DataDesligamento == null || f.DataDesligamento >= dia))
            .OrderBy(f => f.Id)
            .Select(f => f.Id)
            .ToListAsync(ct);

        Exception? primeiroErro = null;
        foreach (var pessoa in pessoas)
        {
            try
            {
                await recalculo.RecalcularDiaAsync(pessoa, dia, ct);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                db.ChangeTracker.Clear(); // a gravação que falhou não pode ficar pendente para a pessoa seguinte
                primeiroErro ??= erro;
            }
        }

        if (primeiroErro is not null)
        {
            ExceptionDispatchInfo.Capture(primeiroErro).Throw();
        }
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var primeiro = hoje.AddDays(-HorizonteDaRecuperacaoEmDias);
        if (await ultimoConsolidado.ObterAsync(ct) is { } ultimo && ultimo.AddDays(1) > primeiro)
        {
            primeiro = ultimo.AddDays(1);
        }

        for (var dia = primeiro; dia <= hoje; dia = dia.AddDays(1))
        {
            await agendador.AgendarAsync(new NovaTarefa(Tipo, Chave(dia), ExecutarApos(dia), DiaReferencia: dia), ct);
        }
    }
}

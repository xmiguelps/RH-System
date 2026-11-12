using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Agendamento;

/// <summary>
/// Agenda no <see cref="AppDbContext"/> da requisição, na mesma transação do fato quando o chamador está em <c>ExecutarEmTransacaoAsync</c>. A mesma técnica da fila de
/// e-mails: salva o que estiver pendente ANTES, insere a tarefa sozinha e trata a chave repetida (<c>1062</c>) como <c>false</c>, sem perder o resto. <c>CriadaEm</c> vem do
/// <see cref="IRelogio"/>, nunca do padrão do banco; <c>ExecutarApos</c> é o que o chamador informou.
/// </summary>
public sealed class Agendador(AppDbContext db, IRelogio relogio) : IAgendador
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public Task<bool> AgendarAsync(NovaTarefa tarefa, CancellationToken ct) =>
        InserirAsync(
            TarefaAgendada.Criar(
                tarefa.Tipo,
                tarefa.ChaveDeIdempotencia,
                relogio.AgoraUtc,
                tarefa.ReferenciaId,
                tarefa.DiaReferencia,
                tarefa.Parametros is null ? null : JsonSerializer.Serialize(tarefa.Parametros, Json),
                tarefa.ExecutarApos),
            ct);

    public Task<bool> RegistrarComoConcluidaAsync(string tipo, string chaveDeIdempotencia, DateOnly? diaReferencia, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var tarefa = TarefaAgendada.Criar(tipo, chaveDeIdempotencia, agora, diaReferencia: diaReferencia);
        tarefa.MarcarComoConcluida(agora); // nasce CONCLUIDA, com a hora (o CHECK exige as duas coisas juntas)
        return InserirAsync(tarefa, ct);
    }

    public async Task<int> CancelarAsync(string chaveDeIdempotencia, CancellationToken ct) =>
        // AUDITORIA-OK: tabela técnica, sem registro por decisão (e a condição PENDENTE no WHERE faz de cancelar e reservar uma disputa que só um ganha)
        await db.TarefasAgendadas.Where(t => t.ChaveDeIdempotencia == chaveDeIdempotencia && t.Situacao == SituacaoDaTarefa.Pendente).ExecuteUpdateAsync(s => s.SetProperty(t => t.Situacao, SituacaoDaTarefa.Cancelada), ct);

    public async Task<int> ReativarAsync(string chaveDeIdempotencia, CancellationToken ct) =>
        // AUDITORIA-OK: tabela técnica, sem registro por decisão (e a condição CANCELADA no WHERE só reativa o que ninguém rodou)
        await db.TarefasAgendadas.Where(t => t.ChaveDeIdempotencia == chaveDeIdempotencia && t.Situacao == SituacaoDaTarefa.Cancelada).ExecuteUpdateAsync(s => s.SetProperty(t => t.Situacao, SituacaoDaTarefa.Pendente), ct);

    private async Task<bool> InserirAsync(TarefaAgendada tarefa, CancellationToken ct)
    {
        // Salva o que já estava pendente ANTES: um 1062 desta tarefa não pode levar junto o resto do que o chamador tinha no contexto.
        await db.SaveChangesAsync(ct);
        db.TarefasAgendadas.Add(tarefa);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException erro) when (erro.InnerException is MySqlException { Number: 1062 })
        {
            db.Entry(tarefa).State = EntityState.Detached; // a chave já existe: vale como sucesso
            return false;
        }
    }
}

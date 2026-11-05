using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Agendamento;

/// <summary>
/// Um ciclo do agendador. <b>Reserva</b> até <c>LoteMaximoPorCiclo</c> tarefas com <c>SELECT ... FOR UPDATE SKIP LOCKED</c> pelo <c>FromSql</c> (a mesma técnica da fila de
/// e-mails, medida pela sonda <c>SkipLockedPeloFromSqlTests</c>), numa transação curta: elegíveis são <c>PENDENTE</c> com <c>ExecutarApos &lt;= agora</c> ou <c>EM_EXECUCAO</c> com a
/// reserva vencida. Marca <c>EM_EXECUCAO</c> (<c>ReservadaAte = agora + ReservaMinutos</c>, <c>UltimaTentativaEm</c>, <c>Tentativas + 1</c>) e executa cada uma FORA da transação, em
/// <b>escopo próprio</b> (sem <c>HttpContext</c>, logo o ator é o <c>Sistema</c>). Sucesso: <c>CONCLUIDA</c>. Exceção: volta a <c>PENDENTE</c> com espera de 1, 5, 30 e 60 minutos
/// (reserva limpa na mesma instrução); ao chegar em <c>MaximoDeTentativas</c>, <c>FALHOU</c>. Tipo sem <see cref="ITarefaAgendada"/>: <c>FALHOU</c> com erro claro. Uma tarefa
/// que falha nunca impede as outras do ciclo. Todo instante vem do <see cref="IRelogio"/>, nunca do <c>NOW()</c> do SQL.
/// </summary>
public sealed class ExecutorDeTarefas(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IServiceScopeFactory escopos,
    IRelogio relogio,
    OpcoesDoAgendador opcoes,
    ILogger<ExecutorDeTarefas> log) : IExecutorDeTarefas
{
    /// <summary>A espera depois da 1ª, 2ª, 3ª e 4ª falha (a partir da 5ª, vale a última, se o máximo for maior).</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(60)];

    public async Task<int> RecuperarAsync(CancellationToken ct)
    {
        string[] tipos;
        await using (var escopoDaLista = escopos.CreateAsyncScope())
        {
            tipos = [.. escopoDaLista.ServiceProvider.GetServices<ITarefaAgendada>().Select(t => t.Tipo)];
        }

        var recuperados = 0;
        foreach (var tipo in tipos)
        {
            try
            {
                // Um escopo por tipo: se a recuperação de um deixar mudança pendente no contexto, ela não vai junto na do próximo.
                await using var escopo = escopos.CreateAsyncScope();
                await escopo.ServiceProvider.GetServices<ITarefaAgendada>().First(t => t.Tipo == tipo).RecuperarAsync(ct);
                recuperados++;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                log.LogError("A recuperação do tipo {Tipo} falhou: {Erro}", tipo, erro.GetType().Name);
            }
        }

        return recuperados;
    }

    public async Task<int> ExecutarCicloAsync(CancellationToken ct)
    {
        var reservadas = await ReservarAsync(ct);
        foreach (var tarefa in reservadas)
        {
            try
            {
                await ExecutarUmaAsync(tarefa, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // o serviço está parando: a tarefa fica EM_EXECUCAO e a reserva vence
            }
            catch (Exception erro)
            {
                // Uma tarefa com problema não impede as outras do ciclo. Ela fica EM_EXECUCAO até a reserva vencer e ser retomada.
                log.LogError("Falha ao tratar a tarefa {Id} ({Tipo}): {Erro}", tarefa.Id, tarefa.Tipo, erro.GetType().Name);
                db.Entry(tarefa).State = EntityState.Detached; // se foi o SaveChanges que falhou, a mudança dela não pode travar a próxima
            }
        }

        return reservadas.Count;
    }

    private async Task<List<TarefaAgendada>> ReservarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var reservaAte = agora.AddMinutes(opcoes.ReservaMinutos);
        var lote = opcoes.LoteMaximoPorCiclo;
        return await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var elegiveis = await db.TarefasAgendadas
                    .FromSql($"SELECT * FROM TAREFAS_AGENDADAS WHERE (SITUACAO = 'PENDENTE' AND EXECUTAR_APOS <= {agora}) OR (SITUACAO = 'EM_EXECUCAO' AND RESERVADA_ATE < {agora}) ORDER BY EXECUTAR_APOS, ID_TAREFA LIMIT {lote} FOR UPDATE SKIP LOCKED")
                    .ToListAsync(ct);
                foreach (var tarefa in elegiveis)
                {
                    tarefa.Reservar(reservaAte, agora);
                }

                await db.SaveChangesAsync(ct);
                return elegiveis;
            },
            System.Data.IsolationLevel.ReadCommitted, // no padrão (REPEATABLE READ) dois executores dão deadlock aqui (medido: sonda DeadlockNaReservaDeTarefasTests)
            ct);
    }

    private async Task ExecutarUmaAsync(TarefaAgendada tarefa, CancellationToken ct)
    {
        await using var escopo = escopos.CreateAsyncScope();
        var executor = escopo.ServiceProvider.GetServices<ITarefaAgendada>().FirstOrDefault(t => t.Tipo == tarefa.Tipo);
        if (executor is null)
        {
            await DesistirAsync(tarefa, $"SemExecutor:{tarefa.Tipo}", ct); // ninguém sabe fazer este tipo: repetir não adianta
            return;
        }

        try
        {
            using var parametros = tarefa.Parametros is null ? null : JsonDocument.Parse(tarefa.Parametros);
            await executor.ExecutarAsync(
                new TarefaParaExecutar(tarefa.Id, tarefa.ChaveDeIdempotencia, tarefa.ReferenciaId, tarefa.DiaReferencia, parametros?.RootElement.Clone(), tarefa.Tentativas),
                ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception erro)
        {
            await RegistrarFalhaAsync(tarefa, erro, ct);
            return;
        }

        tarefa.MarcarComoConcluida(relogio.AgoraUtc);
        await db.SaveChangesAsync(ct);
    }

    private async Task DesistirAsync(TarefaAgendada tarefa, string motivo, CancellationToken ct)
    {
        log.LogWarning("A tarefa {Id} ({Tipo}) foi dada como FALHOU: {Motivo}", tarefa.Id, tarefa.Tipo, motivo);
        tarefa.MarcarComoFalhou(motivo);
        await db.SaveChangesAsync(ct);
    }

    private async Task RegistrarFalhaAsync(TarefaAgendada tarefa, Exception erro, CancellationToken ct)
    {
        // UltimoErro = o nome da exceção e a mensagem (cortada em 500 pela entidade). Os executores NÃO põem dado pessoal em mensagem de exceção; o log leva só o nome.
        var texto = $"{erro.GetType().Name}: {erro.Message}";
        var desistir = tarefa.Tentativas >= opcoes.MaximoDeTentativas;
        log.LogWarning("A tarefa {Id} ({Tipo}) falhou na tentativa {Tentativa}: {Erro}{Fim}", tarefa.Id, tarefa.Tipo, tarefa.Tentativas, erro.GetType().Name, desistir ? " (desistiu)" : string.Empty);
        if (desistir)
        {
            tarefa.MarcarComoFalhou(texto);
        }
        else
        {
            tarefa.AgendarNovaTentativa(relogio.AgoraUtc + Esperas[Math.Min(tarefa.Tentativas - 1, Esperas.Length - 1)], texto);
        }

        await db.SaveChangesAsync(ct);
    }
}

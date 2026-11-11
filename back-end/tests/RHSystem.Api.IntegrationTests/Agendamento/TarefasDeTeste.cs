using System.Collections.Concurrent;
using RHSystem.Api.IntegrationTests.Emails;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.IntegrationTests.Agendamento;

/// <summary>O que as tarefas de teste anotam e como se comportam. Um só para a API inteira (singleton), seguro para vários executores ao mesmo tempo.</summary>
public sealed class RegistroDeTarefasDeTeste
{
    private readonly ConcurrentQueue<TarefaParaExecutar> _execucoes = new();
    private readonly ConcurrentQueue<string> _recuperacoes = new();

    /// <summary>O que cada execução faz DEPOIS de anotada (lançar = a tarefa falha).</summary>
    public Func<TarefaParaExecutar, Task> Comportamento { get; set; } = _ => Task.CompletedTask;

    /// <summary>Uma pausa por execução, para dois executores de verdade se sobreporem no tempo.</summary>
    public TimeSpan Pausa { get; set; } = TimeSpan.Zero;

    /// <summary>O primeiro dia que a tarefa recuperável de teste cria (uma tarefa por dia, até ontem).</summary>
    public DateOnly PrimeiroDiaDaRecuperacao { get; set; } = DateOnly.FromDateTime(ElencoDeTeste.Hoje);

    /// <summary>Cada tentativa de execução (também as que lançaram), na ordem em que começaram.</summary>
    public IReadOnlyList<TarefaParaExecutar> Execucoes => [.. _execucoes];

    /// <summary>Os tipos cuja <c>RecuperarAsync</c> foi chamada.</summary>
    public IReadOnlyList<string> Recuperacoes => [.. _recuperacoes];

    public void Limpar()
    {
        _execucoes.Clear();
        _recuperacoes.Clear();
        Comportamento = _ => Task.CompletedTask;
        Pausa = TimeSpan.Zero;
        PrimeiroDiaDaRecuperacao = DateOnly.FromDateTime(ElencoDeTeste.Hoje);
    }

    internal async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        _execucoes.Enqueue(tarefa);
        if (Pausa > TimeSpan.Zero)
        {
            await Task.Delay(Pausa, ct);
        }

        await Comportamento(tarefa);
    }

    internal void AnotarRecuperacao(string tipo) => _recuperacoes.Enqueue(tipo);
}

/// <summary>O tipo comum de teste: anota e faz o que o <see cref="RegistroDeTarefasDeTeste.Comportamento"/> mandar.</summary>
public sealed class TarefaDeTeste(RegistroDeTarefasDeTeste registro) : ITarefaAgendada
{
    public const string TipoDaTarefa = "TESTE_TAREFA";

    public string Tipo => TipoDaTarefa;

    public Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct) => registro.ExecutarAsync(tarefa, ct);

    public Task RecuperarAsync(CancellationToken ct)
    {
        registro.AnotarRecuperacao(Tipo);
        return Task.CompletedTask;
    }
}

/// <summary>Recupera criando UMA tarefa por dia, do <see cref="RegistroDeTarefasDeTeste.PrimeiroDiaDaRecuperacao"/> até ONTEM (Brasília): o jeito de provar que uma parada longa é refeita sem duplicar.</summary>
public sealed class TarefaRecuperavelDeTeste(RegistroDeTarefasDeTeste registro, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "TESTE_RECUPERAVEL";

    public string Tipo => TipoDaTarefa;

    public Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct) => registro.ExecutarAsync(tarefa, ct);

    public async Task RecuperarAsync(CancellationToken ct)
    {
        registro.AnotarRecuperacao(Tipo);
        var ontem = FusoBrasil.Hoje(relogio.AgoraUtc).AddDays(-1);
        for (var dia = registro.PrimeiroDiaDaRecuperacao; dia <= ontem; dia = dia.AddDays(1))
        {
            await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), relogio.AgoraUtc, DiaReferencia: dia), ct);
        }
    }
}

/// <summary>Um tipo cuja recuperação sempre lança: não pode impedir a dos outros.</summary>
public sealed class TarefaQueNaoRecuperaDeTeste(RegistroDeTarefasDeTeste registro) : ITarefaAgendada
{
    public const string TipoDaTarefa = "TESTE_QUEBRADA";

    public string Tipo => TipoDaTarefa;

    public Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct) => registro.ExecutarAsync(tarefa, ct);

    public Task RecuperarAsync(CancellationToken ct)
    {
        registro.AnotarRecuperacao(Tipo);
        throw new InvalidOperationException("a recuperação deste tipo quebrou (de propósito)");
    }
}

/// <summary>Grava um evento de auditoria ao executar: sem <c>HttpContext</c> o ator tem de sair como Sistema, sem IP.</summary>
public sealed class TarefaAuditadaDeTeste(RegistroDeTarefasDeTeste registro, IAuditor auditor) : ITarefaAgendada
{
    public const string TipoDaTarefa = "TESTE_AUDITADA";
    public const string Descricao = "Tarefa de teste auditada";

    public string Tipo => TipoDaTarefa;

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        await registro.ExecutarAsync(tarefa, ct);
        await auditor.RegistrarEGravarAsync(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Criou, Descricao, "Tarefa de teste"), ct);
    }
}

/// <summary>Os testes do agendador usam a mesma API (e o mesmo banco) dos de e-mail; cada teste começa com a fila de e-mails e as tarefas vazias e o registro limpo.</summary>
internal static class ApiDeAgendamento
{
    public static async Task<ApiFactory> ComecarAsync(MySqlFixture mysql)
    {
        var api = await ApiDeEmails.ComecarAsync(mysql); // fila vazia, relógio em 07/10/2026 12:42 UTC, enviador limpo
        await Auditoria.AuditoriaDeTeste.ExecutarAsync(api.Conexao, "DELETE FROM TAREFAS_AGENDADAS");
        api.Tarefas.Limpar();
        return api;
    }
}

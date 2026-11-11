using System.Text.Json;

namespace RHSystem.Application.Agendamento;

/// <summary>A tarefa que o executor entrega ao <see cref="ITarefaAgendada"/>. <c>Parametros</c> é um clone do JSON gravado (nunca com dado pessoal).</summary>
public sealed record TarefaParaExecutar(long Id, string ChaveDeIdempotencia, int? ReferenciaId, DateOnly? DiaReferencia, JsonElement? Parametros, int Tentativas);

/// <summary>
/// Um por tipo de trabalho, registrado como escopo (cada execução tem escopo próprio, sem <c>HttpContext</c>, logo o ator é o <c>Sistema</c> e os eventos de auditoria saem
/// como <c>SISTEMA</c>). <b>Idempotente</b>: o processo pode cair entre agir e marcar <c>CONCLUIDA</c>, então a tarefa confere o estado real antes de agir.
/// </summary>
public interface ITarefaAgendada
{
    /// <summary><c>"EFETIVAR_DESLIGAMENTO"</c>: <c>MAIÚSCULAS_COM_SUBLINHADO</c>, de 3 a 50 caracteres.</summary>
    string Tipo { get; }

    /// <summary>Lançar = tentar de novo (com espera); voltar normalmente = concluída. <b>Não ponha dado pessoal na mensagem da exceção</b>: ela vai (cortada em 500) para <c>ULTIMO_ERRO</c>.</summary>
    Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct);

    /// <summary>Cria (pelo <see cref="IAgendador"/>) o que ficou para trás. Idempotente. Roda na subida e a cada hora. O padrão do tipo é não ter nada a recuperar.</summary>
    Task RecuperarAsync(CancellationToken ct) => Task.CompletedTask;
}

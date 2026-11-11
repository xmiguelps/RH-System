namespace RHSystem.Application.Agendamento;

/// <summary>
/// Uma tarefa a agendar. <c>Tipo</c> (3 a 50, <c>MAIÚSCULAS_COM_SUBLINHADO</c>) e <c>ChaveDeIdempotencia</c> vêm de
/// <c>ChaveDeIdempotencia.Montar</c>. <c>Parametros</c> vai em JSON camelCase e <b>nunca leva dado pessoal</b> (<c>V001</c>): só ids e datas.
/// </summary>
public sealed record NovaTarefa(
    string Tipo,
    string ChaveDeIdempotencia,
    DateTime ExecutarApos,
    int? ReferenciaId = null,
    DateOnly? DiaReferencia = null,
    object? Parametros = null);

/// <summary>
/// O que os casos de uso chamam para agendar. Agendar usa a mesma técnica da fila de e-mails: salva o que estiver pendente no contexto, insere a tarefa sozinha
/// e trata a chave repetida (<c>1062</c>) como <c>false</c>, sem perder o resto.
/// </summary>
public interface IAgendador
{
    /// <summary><c>false</c> = a chave já existe (agendada, rodada ou cancelada); vale como sucesso e nunca lança por duplicidade.</summary>
    Task<bool> AgendarAsync(NovaTarefa tarefa, CancellationToken ct);

    /// <summary>
    /// Insere a tarefa já <c>CONCLUIDA</c>: a marca idempotente de "isto já foi feito" (por exemplo "lembrete do formulário já enviado hoje"). <c>false</c> = a chave já existia
    /// (a marca já estava lá, e o chamador não repete o trabalho).
    /// </summary>
    Task<bool> RegistrarComoConcluidaAsync(string tipo, string chaveDeIdempotencia, DateOnly? diaReferencia, CancellationToken ct);

    /// <summary><c>PENDENTE</c> vira <c>CANCELADA</c>. Devolve 1, ou 0 se não achou a chave ou se a tarefa já rodou (ou está rodando): cancelar nunca tira uma tarefa de dentro de uma execução.</summary>
    Task<int> CancelarAsync(string chaveDeIdempotencia, CancellationToken ct);
}

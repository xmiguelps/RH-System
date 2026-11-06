namespace RHSystem.Application.Colaboradores;

/// <summary>O que a efetivação de um desligamento encontrou. <c>AindaNaoPode</c> é "o dia seguinte ao último dia ainda não começou" ou "uma condição de fatia ainda recusa": a tarefa lança para o agendador tentar de novo.</summary>
public enum ResultadoDaEfetivacao
{
    Efetivado,
    JaEstavaEfetivado,
    NaoEstavaAgendado,
    AindaNaoPode,
}

/// <summary><paramref name="EfetivadoNaHora"/>: o último dia já tinha passado e nenhuma condição recusou, então a pessoa foi desligada na mesma transação (a ficha mostra <c>EFETIVADO</c>); senão ficou <c>AGENDADO</c>, com a tarefa.</summary>
public sealed record ResultadoDoAgendamento(int DesligamentoId, bool EfetivadoNaHora);

/// <summary>
/// O desligamento do colaborador: o RH informa o ÚLTIMO DIA de trabalho, que pode ser amanhã; o banco inativa a pessoa assim que <c>DATA_DESLIGAMENTO</c> tem valor, então a aplicação guarda o agendamento e só grava
/// a data no dia seguinte ao último dia (00:00 de -03:00), na tarefa; se esse momento já passou, grava na hora. <b>Cada método abre a própria transação</b> e trava, nesta ordem, <c>FUNCIONARIOS</c> e
/// <c>DESLIGAMENTOS_AGENDADOS</c> (<c>FOR UPDATE</c>), o que serializa o clique duplo e a corrida entre cancelar e efetivar.
/// </summary>
public interface IDesligamentoDeFuncionarios
{
    /// <summary>
    /// Cria o desligamento <c>AGENDADO</c> e, se o INÍCIO do dia seguinte ao último dia já passou E nenhuma condição recusa, efetiva na mesma transação; senão agenda a tarefa.
    /// 404 (a pessoa não existe); 400 em <c>ultimoDiaDeTrabalho</c> (anterior à admissão, ou mais de 10 anos à frente); 409 <c>DESLIGAMENTO_JA_REGISTRADO</c> (a pessoa está inativa ou já tem desligamento vigente).
    /// </summary>
    Task<ResultadoDoAgendamento> AgendarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct);

    /// <summary>404 (a pessoa não existe); 422 <c>SEM_DESLIGAMENTO_AGENDADO</c> se não há <c>AGENDADO</c> vigente (inclusive se já está <c>EFETIVADO</c>). Cancela a tarefa pela chave.</summary>
    Task CancelarAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Idempotente: a tarefa e a recuperação chamam. Relê o estado depois de travar, então a corrida com o cancelamento termina coerente (nunca <c>CANCELADO</c> com a pessoa inativa).</summary>
    Task<ResultadoDaEfetivacao> EfetivarAsync(int desligamentoId, CancellationToken ct);
}

/// <summary>
/// Uma condição a mais para efetivar. A fatia 3 acrescenta "depois de a consolidação do banco de horas daquele dia fechar" (roadmap) registrando uma implementação com <c>AddScoped</c>; a 1b não registra nenhuma, e
/// sem nenhuma a efetivação vale. Todas as registradas precisam liberar.
/// </summary>
public interface ICondicaoParaEfetivarDesligamento
{
    Task<bool> PodeEfetivarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct);
}

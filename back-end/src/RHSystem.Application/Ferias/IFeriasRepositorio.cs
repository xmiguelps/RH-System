using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

/// <summary>
/// As gravações de férias. Toda mudança de estado é UM <c>UPDATE</c> condicional ao estado de partida (corrida entre duas pessoas), gravando juntas as colunas que os <c>CHECK</c> em cadeia exigem. O pedido nasce na transação em que a pessoa já foi travada (<see cref="TravarPessoaAsync"/>, <c>FOR UPDATE</c> em
/// <c>FUNCIONARIOS</c>): é o que serializa dois pedidos, o pedido e o registro do RH, o aceite e o pedido, para o saldo nunca estourar.
/// </summary>
public interface IFeriasRepositorio
{
    /// <summary>Trava a linha da pessoa (só vale dentro de transação). <c>null</c>: a pessoa não existe.</summary>
    Task<PessoaParaFerias?> TravarPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Grava o pedido novo e devolve o id. A recusa de trigger do banco (pessoa inativa) sobe já traduzida em 422.</summary>
    Task<int> CriarAsync(PedidoDeFerias pedido, CancellationToken ct);

    /// <summary>
    /// As três decisões da etapa do gestor: cada uma é UM <c>UPDATE</c> condicional ao pedido continuar <c>SOLICITADA</c>, com o <c>ID_GESTOR</c> dele e sem resultado dele ainda. <c>false</c>: outra decisão chegou antes (o mesmo clique, o gestor ausente, o RH, a inativação); o chamador relê o estado e responde o 409 certo.
    /// Nenhuma toca na <c>OBSERVACAO</c> (a mensagem da pessoa).
    /// </summary>
    Task<bool> AprovarComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, CancellationToken ct);

    Task<bool> RecusarComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, string motivo, CancellationToken ct);

    Task<bool> SugerirComoGestorAsync(int feriasId, int gestorId, DateTime agoraUtc, DateOnly inicio, DateOnly fim, CancellationToken ct);

    /// <summary>
    /// A resposta da PESSOA à sugestão do gestor, cada uma UM <c>UPDATE</c> condicional ao pedido do dono continuar esperando a resposta (<c>SOLICITADA</c>, <c>SUGERIU_DATA</c> e sem resposta). Aceitar copia as datas sugeridas para as do pedido (as sugeridas ficam: o <c>CHECK</c> exige) e grava <c>ACEITOU</c>;
    /// recusar e expirar viram <c>CANCELADA</c> com <c>RECUSOU</c> ou <c>EXPIROU</c> no mesmo <c>UPDATE</c>. <c>false</c>: outra resposta (ou a tarefa) chegou antes.
    /// </summary>
    /// <summary>
    /// As duas decisões do RH, a final: cada uma UM <c>UPDATE</c> condicional ao pedido estar na etapa do RH (<c>COM_O_RH</c>). O <c>ID_GESTOR</c> fica o que já era e, num pedido sem etapa do gestor, passa a ser a linha de <c>GESTORES</c> de quem decide (o trigger do banco exige o gestor e que ele não seja a
    /// pessoa do pedido). A recusa de trigger sobe traduzida (422 <c>PEDIDO_PROPRIO</c> ou <c>PEDIDO_RH_FORA_DE_GESTORES</c>). Não tocam na <c>OBSERVACAO</c>.
    /// </summary>
    Task<bool> ConfirmarComoRhAsync(int feriasId, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, CancellationToken ct);

    Task<bool> RecusarComoRhAsync(int feriasId, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, string motivo, CancellationToken ct);

    Task<bool> AceitarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct);

    Task<bool> RecusarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct);

    Task<bool> ExpirarSugestaoAsync(int feriasId, int funcionarioId, DateTime agoraUtc, CancellationToken ct);
}

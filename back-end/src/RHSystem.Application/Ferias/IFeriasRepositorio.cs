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
}

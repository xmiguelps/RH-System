namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A implementação <b>padrão</b> da porta (escopo da requisição): chama cada <see cref="IPedidosParadosNoGestorDaFatia"/> registrada, uma de cada vez e na ordem do registro
/// (o contexto do banco da requisição não é paralelo), e soma o que as fatias aprovaram (<see cref="PedidosAprovados.Somar"/>; <see cref="PedidosAprovados.Zero"/> sem
/// nenhuma). Sem nenhuma fatia, não faz nada: é a dívida registrada em <c>contrato/STATUS.md</c> até as fatias 3 e 7 implementarem as delas. Exceção de uma fatia
/// <b>sobe como veio</b> (não é engolida nem embrulhada): quem chamou está numa transação e a desfaz; as ações são idempotentes, então tentar de novo é seguro.
/// </summary>
public sealed class PedidosParadosNoGestorComposto(IEnumerable<IPedidosParadosNoGestorDaFatia> fatias) : IPedidosParadosNoGestor
{
    public async Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
    {
        var total = PedidosAprovados.Zero;
        foreach (var fatia in fatias)
        {
            total = total.Somar(await fatia.AprovarEtapaDoGestorAsync(gestorId, ct));
        }

        return total;
    }

    public async Task PassarParaORhAsync(int gestorId, CancellationToken ct)
    {
        foreach (var fatia in fatias)
        {
            await fatia.PassarParaORhAsync(gestorId, ct);
        }
    }
}

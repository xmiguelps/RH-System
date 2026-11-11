namespace RHSystem.Application.Colaboradores;

/// <summary>
/// O que "Gestor ausente" aprovou agora, por tipo de pedido (vira <c>pedidosAprovadosAgora { ajustesDePonto, ferias }</c> na resposta da fatia 1c).
/// <see cref="Somar"/> é como o composto junta o que cada fatia dona de pedidos devolveu.
/// </summary>
public sealed record PedidosAprovados(int AjustesDePonto, int Ferias)
{
    /// <summary>Nenhum pedido aprovado: 0 e 0. É o que o composto devolve quando nenhuma fatia registrou a sua implementação.</summary>
    public static PedidosAprovados Zero { get; } = new(0, 0);

    /// <summary>Soma campo a campo.</summary>
    public PedidosAprovados Somar(PedidosAprovados outro) => new(AjustesDePonto + outro.AjustesDePonto, Ferias + outro.Ferias);
}

using RHSystem.Application.Comum;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>
/// A etapa de um pedido de ajuste, deduzida das colunas (não há coluna de etapa): <c>PENDENTE</c> com <c>ID_GESTOR</c> e sem resultado do gestor é <see cref="EtapaDoPedido.Gestor"/>; <c>PENDENTE</c> sem <c>ID_GESTOR</c> (o pedido não tem etapa do gestor) ou com o
/// gestor já aprovado é <see cref="EtapaDoPedido.Rh"/>; <c>APROVADA</c> e <c>REJEITADA</c> são <see cref="EtapaDoPedido.Concluida"/>. A etapa nasce em Gestor ou Rh e só avança.
/// </summary>
public static class EtapaDoPedidoDeAjuste
{
    public static EtapaDoPedido De(StatusDoPedidoDeAjuste status, int? gestorId, ResultadoDaEtapaDoGestor? resultadoDoGestor)
    {
        if (status != StatusDoPedidoDeAjuste.Pendente)
        {
            return EtapaDoPedido.Concluida;
        }

        return gestorId is not null && resultadoDoGestor is null ? EtapaDoPedido.Gestor : EtapaDoPedido.Rh;
    }
}

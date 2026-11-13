using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>
/// Se quem consulta pode decidir o pedido de férias agora e o porquê quando não: as regras da fatia 3 (os cinco textos, <see cref="RegrasDeDecisaoDoPedido"/>, reaproveitadas no lugar, sem copiar) mais o sexto texto, só das férias: em <c>SUGESTAO_DE_DATA</c> ninguém decide, a resposta é da própria pessoa,
/// por rotas próprias, e o motivo diz quem se espera.
/// </summary>
public static class RegrasDeDecisaoDeFerias
{
    public static string EsperandoARespostaDe(string nomeDeQuemPediu) => $"Esperando a resposta de {nomeDeQuemPediu} à sugestão de outra data.";

    public static (bool Pode, string? Motivo) PodeDecidir(SituacaoDasFerias situacao, PedidoParaDecisao pedido, QuemConsultaOPedido quem, string nomeDeQuemPediu) =>
        situacao == SituacaoDasFerias.SugestaoDeData ? (false, EsperandoARespostaDe(nomeDeQuemPediu)) : RegrasDeDecisaoDoPedido.PodeDecidir(pedido, quem);
}

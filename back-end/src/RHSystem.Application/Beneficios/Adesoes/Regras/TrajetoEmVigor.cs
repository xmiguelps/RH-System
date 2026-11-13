using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>Os campos de um trajeto que decidem qual está em vigor.</summary>
public sealed record TrajetoParaEscolher(int Id, StatusDoPedidoDeBeneficio Status, DateTime? RespondidoEmUtc);

/// <summary>
/// O trajeto em vigor (<c>trajetoAtual</c>): entre os <c>APROVADO</c>, o que o RH respondeu por último (em empate, o de id maior). Um pedido novo não tira o em vigor: ele só vale quando o RH aprova. Sem nenhum aprovado, <c>null</c>.
/// </summary>
public static class TrajetoEmVigor
{
    public static int? IdDe(IEnumerable<TrajetoParaEscolher> trajetos) =>
        trajetos
            .Where(t => t.Status == StatusDoPedidoDeBeneficio.Aprovado)
            .OrderByDescending(t => t.RespondidoEmUtc)
            .ThenByDescending(t => t.Id)
            .Select(t => (int?)t.Id)
            .FirstOrDefault();
}

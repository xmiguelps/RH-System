using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>Um item do catálogo de benefícios (fatia 1) com as três marcas que a tela do funcionário usa.</summary>
public sealed record ItemDoCatalogo(int Id, string Nome, string? Descricao, bool Opcional, bool AceitaDependentes, bool AceitaTrajeto);

/// <summary>A linha de <c>BENEFICIOS_FUNCIONARIOS</c> da pessoa para um benefício (no máximo uma por benefício: a chave única).</summary>
public sealed record AdesaoDaPessoa(
    int AdesaoId,
    int BeneficioId,
    StatusDoPedidoDeBeneficio Status,
    bool Ativo,
    DateOnly? InicioEm,
    bool AutorizaDescontoEmFolha,
    string? MotivoDaRecusa,
    DateTime? RespondidoEmUtc);

/// <summary>Um cartão de "Seus benefícios": o item do catálogo e a adesão ativa e aprovada.</summary>
public sealed record CartaoDeBeneficio(ItemDoCatalogo Item, AdesaoDaPessoa Adesao);

/// <summary>O pedido anterior, recusado ou cancelado, do mesmo benefício.</summary>
public sealed record UltimoPedidoDoBeneficio(StatusDoPedidoDeBeneficio Situacao, string? MotivoDaRecusa, DateTime? RespondidoEmUtc);

/// <summary>Um item de "Disponíveis para você". <see cref="UltimoPedido"/> é <c>null</c> quando nunca houve pedido, ou quando a linha é de um benefício aprovado que o RH desativou.</summary>
public sealed record BeneficioDisponivel(ItemDoCatalogo Item, UltimoPedidoDoBeneficio? UltimoPedido);

/// <summary>
/// As duas listas de "Meus benefícios" a partir do catálogo e das linhas da pessoa. <c>Cartoes</c>: adesões ATIVAS e aprovadas, qualquer que seja a marca <c>opcional</c> do item (o RH dá direto os que não são opcionais). <c>Disponiveis</c>: itens OPCIONAIS que a pessoa não tem e
/// não está pedindo; uma linha pendente tira o item da lista, uma recusada ou cancelada o deixa com o motivo em <c>UltimoPedido</c>, e uma aprovada mas inativa o deixa sem <c>UltimoPedido</c> (a pessoa pode pedir de novo). Os dois em ordem de id do catálogo.
/// </summary>
public sealed record ListasDoBeneficio(IReadOnlyList<CartaoDeBeneficio> Cartoes, IReadOnlyList<BeneficioDisponivel> Disponiveis)
{
    public static ListasDoBeneficio Montar(IReadOnlyCollection<ItemDoCatalogo> catalogo, IReadOnlyCollection<AdesaoDaPessoa> adesoes)
    {
        var porBeneficio = adesoes.ToDictionary(a => a.BeneficioId);
        var itens = catalogo.OrderBy(i => i.Id).ToList();

        var cartoes = new List<CartaoDeBeneficio>();
        var disponiveis = new List<BeneficioDisponivel>();
        foreach (var item in itens)
        {
            if (!porBeneficio.TryGetValue(item.Id, out var adesao))
            {
                if (item.Opcional)
                {
                    disponiveis.Add(new BeneficioDisponivel(item, null));
                }

                continue;
            }

            if (adesao.Status == StatusDoPedidoDeBeneficio.Aprovado && adesao.Ativo)
            {
                cartoes.Add(new CartaoDeBeneficio(item, adesao));
                continue;
            }

            if (!item.Opcional || adesao.Status == StatusDoPedidoDeBeneficio.Pendente)
            {
                continue;
            }

            var ultimo = adesao.Status is StatusDoPedidoDeBeneficio.Recusado or StatusDoPedidoDeBeneficio.Cancelado
                ? new UltimoPedidoDoBeneficio(adesao.Status, adesao.MotivoDaRecusa, adesao.RespondidoEmUtc)
                : null;
            disponiveis.Add(new BeneficioDisponivel(item, ultimo));
        }

        return new ListasDoBeneficio(cartoes, disponiveis);
    }
}

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>O que a pessoa pediu: a adesão ao benefício, a inclusão de um dependente nela ou a atualização do trajeto do vale-transporte.</summary>
public enum TipoDoPedidoDeBeneficio
{
    Adesao,
    Dependente,
    Trajeto,
}

/// <summary>
/// O texto pronto da linha de "Seus pedidos" (contrato, <c>BeneficioAdesaoPedidoResumo.titulo</c>): "Adesão ao plano odontológico", "Inclusão de dependente no plano de saúde", "Atualização do trajeto do vale-transporte". As três preposições
/// (ao, no, do) são fixas, qualquer que seja o gênero do nome do benefício; o nome entra com a primeira letra em minúscula e o resto como o RH escreveu.
/// </summary>
public static class TituloDoPedido
{
    public static string De(TipoDoPedidoDeBeneficio tipo, string nomeDoBeneficio)
    {
        var nome = NoMeioDaFrase(nomeDoBeneficio);
        return tipo switch
        {
            TipoDoPedidoDeBeneficio.Adesao => $"Adesão ao {nome}",
            TipoDoPedidoDeBeneficio.Dependente => $"Inclusão de dependente no {nome}",
            TipoDoPedidoDeBeneficio.Trajeto => $"Atualização do trajeto do {nome}",
            _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de pedido de benefício desconhecido."),
        };
    }

    /// <summary>Só a primeira letra vira minúscula ("Plano de saúde" → "plano de saúde"); uma sigla (as duas primeiras letras em maiúscula, como "VR") fica como está.</summary>
    private static string NoMeioDaFrase(string nome)
    {
        var limpo = nome.Trim();
        if (limpo.Length == 0 || (limpo.Length > 1 && char.IsUpper(limpo[1])))
        {
            return limpo;
        }

        return char.ToLowerInvariant(limpo[0]) + limpo[1..];
    }
}

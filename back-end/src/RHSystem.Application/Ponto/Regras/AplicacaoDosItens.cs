using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Por que os itens não se aplicam ao dia (nada é gravado).</summary>
public enum ErroDeAplicacao
{
    /// <summary>O item corrige uma batida que não é deste dia (ou não existe).</summary>
    MarcacaoInexistenteNoDia,

    /// <summary>Dois itens corrigem a mesma batida.</summary>
    MarcacaoRepetida,

    /// <summary>Duas batidas ficariam no mesmo segundo (a chave única do banco recusaria).</summary>
    BatidaDuplicada,

    /// <summary>Com os itens aplicados o dia deixa de alternar ENTRADA e SAÍDA terminando em SAÍDA.</summary>
    SequenciaInvalida,
}

/// <summary>Uma batida do dia depois de aplicar os itens: <paramref name="Id"/> nulo é batida nova; <paramref name="Mudou"/> diz se tem de ser gravada (nova, ou existente com outro horário ou tipo).</summary>
public sealed record BatidaDepoisDeAplicar(int? Id, DateTime InstanteUtc, TipoDeMarcacao Tipo, bool Mudou);

public sealed record ResultadoDaAplicacao(IReadOnlyList<BatidaDepoisDeAplicar> Dia, ErroDeAplicacao? Erro);

/// <summary>
/// Aplica os itens de um pedido às batidas do dia, <b>em memória</b>: o RH aprova tudo ou nada, então a conferência (batida de outro dia, repetida, no mesmo segundo, sequência que não fecha) vem ANTES de gravar qualquer coisa. Os horários vão cortados ao
/// segundo. O dia pode ter mudado desde o pedido (outra correção do RH): quem chama revalida na hora de decidir.
/// </summary>
public static class AplicacaoDosItens
{
    public static ResultadoDaAplicacao Aplicar(IReadOnlyList<BatidaLida> batidasDoDia, IReadOnlyList<ItemParaAplicar> itens)
    {
        var dia = batidasDoDia.Select(b => new BatidaDepoisDeAplicar(b.Id, b.InstanteUtc, b.Tipo, false)).ToList();
        var jaCorrigidas = new HashSet<int>();

        foreach (var item in itens)
        {
            var instante = MarcacaoDePonto.AoSegundo(item.InstanteUtc);
            if (item.MarcacaoId is null)
            {
                dia.Add(new BatidaDepoisDeAplicar(null, instante, item.Tipo, true));
                continue;
            }

            var posicao = dia.FindIndex(b => b.Id == item.MarcacaoId);
            if (posicao < 0)
            {
                return Falhou(dia, ErroDeAplicacao.MarcacaoInexistenteNoDia);
            }

            if (!jaCorrigidas.Add(item.MarcacaoId.Value))
            {
                return Falhou(dia, ErroDeAplicacao.MarcacaoRepetida);
            }

            var atual = dia[posicao];
            dia[posicao] = atual with { InstanteUtc = instante, Tipo = item.Tipo, Mudou = atual.InstanteUtc != instante || atual.Tipo != item.Tipo };
        }

        if (dia.GroupBy(b => b.InstanteUtc).Any(g => g.Count() > 1))
        {
            return Falhou(dia, ErroDeAplicacao.BatidaDuplicada);
        }

        // O Id negativo só serve para a conferência: a batida nova ainda não tem id.
        var marcacoes = dia.Select((b, i) => new MarcacaoDoDia(b.Id ?? -(i + 1), b.InstanteUtc, b.Tipo)).ToList();
        return SequenciaDoDia.EhConsistente(marcacoes) ? new ResultadoDaAplicacao(Ordenar(dia), null) : Falhou(dia, ErroDeAplicacao.SequenciaInvalida);
    }

    private static ResultadoDaAplicacao Falhou(List<BatidaDepoisDeAplicar> dia, ErroDeAplicacao erro) => new(Ordenar(dia), erro);

    private static IReadOnlyList<BatidaDepoisDeAplicar> Ordenar(List<BatidaDepoisDeAplicar> dia) => [.. dia.OrderBy(b => b.InstanteUtc)];
}

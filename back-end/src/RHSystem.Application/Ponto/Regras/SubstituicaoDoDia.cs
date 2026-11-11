using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Uma batida da lista que o RH manda para o dia: com <paramref name="Id"/> é a existente (corrigida ou igual), sem ele é nova.</summary>
public sealed record BatidaNaLista(int? Id, DateTime InstanteUtc, TipoDeMarcacao Tipo);

/// <summary>O dia depois de substituir a lista: as batidas que ficam (<c>Mudou</c> diz o que tem de ser gravado) e os ids das que saem.</summary>
public sealed record ResultadoDaSubstituicao(IReadOnlyList<BatidaDepoisDeAplicar> Dia, IReadOnlyList<int> Removidas, ErroDeAplicacao? Erro);

/// <summary>
/// A correção direta do RH: a lista de batidas que o dia DEVE ter depois da correção substitui a atual. Batida com <c>id</c> que muda de horário ou de tipo é corrigida; sem <c>id</c> é nova; a existente que não vem na lista é apagada. Conferido em memória
/// ANTES de gravar: <c>id</c> de outro dia (ou repetido), duas batidas no mesmo segundo e, se o dia não fica vazio, a sequência (alternar ENTRADA e SAÍDA terminando em SAÍDA). Uma lista vazia limpa o dia.
/// </summary>
public static class SubstituicaoDoDia
{
    public static ResultadoDaSubstituicao Substituir(IReadOnlyList<BatidaLida> atuais, IReadOnlyList<BatidaNaLista> lista)
    {
        var dia = new List<BatidaDepoisDeAplicar>();
        var mantidas = new HashSet<int>();
        foreach (var item in lista)
        {
            var instante = MarcacaoDePonto.AoSegundo(item.InstanteUtc);
            if (item.Id is not { } id)
            {
                dia.Add(new BatidaDepoisDeAplicar(null, instante, item.Tipo, true));
                continue;
            }

            var atual = atuais.FirstOrDefault(b => b.Id == id);
            if (atual is null)
            {
                return Falhou(dia, atuais, mantidas, ErroDeAplicacao.MarcacaoInexistenteNoDia);
            }

            if (!mantidas.Add(id))
            {
                return Falhou(dia, atuais, mantidas, ErroDeAplicacao.MarcacaoRepetida);
            }

            dia.Add(new BatidaDepoisDeAplicar(id, instante, item.Tipo, atual.InstanteUtc != instante || atual.Tipo != item.Tipo));
        }

        if (dia.GroupBy(b => b.InstanteUtc).Any(g => g.Count() > 1))
        {
            return Falhou(dia, atuais, mantidas, ErroDeAplicacao.BatidaDuplicada);
        }

        var marcacoes = dia.Select((b, i) => new MarcacaoDoDia(b.Id ?? -(i + 1), b.InstanteUtc, b.Tipo)).ToList();
        if (marcacoes.Count > 0 && !SequenciaDoDia.EhConsistente(marcacoes))
        {
            return Falhou(dia, atuais, mantidas, ErroDeAplicacao.SequenciaInvalida);
        }

        return new ResultadoDaSubstituicao(Ordenar(dia), Removidas(atuais, mantidas), null);
    }

    private static ResultadoDaSubstituicao Falhou(List<BatidaDepoisDeAplicar> dia, IReadOnlyList<BatidaLida> atuais, HashSet<int> mantidas, ErroDeAplicacao erro) => new(Ordenar(dia), Removidas(atuais, mantidas), erro);

    private static IReadOnlyList<int> Removidas(IReadOnlyList<BatidaLida> atuais, HashSet<int> mantidas) => [.. atuais.Where(b => !mantidas.Contains(b.Id)).Select(b => b.Id)];

    private static IReadOnlyList<BatidaDepoisDeAplicar> Ordenar(List<BatidaDepoisDeAplicar> dia) => [.. dia.OrderBy(b => b.InstanteUtc)];
}

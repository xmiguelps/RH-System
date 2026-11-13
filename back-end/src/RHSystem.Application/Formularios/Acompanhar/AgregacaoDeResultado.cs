using System.Globalization;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>
/// As contas do resultado, puras (nada de banco): por pergunta, a múltipla escolha com TODAS as alternativas (total e percentual de uma casa), a escala com a média e os cinco valores, os textos em ordem alfabética (sem diferenciar maiúscula nem acento, em português; o desempate é o próprio texto, nunca a ordem em que
/// foram gravados) e o arquivo só com a quantidade. Com <paramref name="minimo"/> (3 no anônimo) a pergunta com menos respostas SOME da lista e entra na conta de <c>perguntasOcultas</c>; sem mínimo (formulário com nome) aparecem todas.
/// </summary>
public static class AgregacaoDeResultado
{
    /// <summary>O mínimo de respostas por pergunta e por avaliado no formulário anônimo (regras 3 e 4 de "Formulários").</summary>
    public const int MinimoNoAnonimo = 3;

    private static readonly CultureInfo Portugues = CultureInfo.GetCultureInfo("pt-BR");

    private static readonly StringComparer Alfabetica = StringComparer.Create(Portugues, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace);

    public static (IReadOnlyList<FormularioResultadoPergunta> Perguntas, int Ocultas) Agregar(IReadOnlyList<PerguntaLida> perguntas, IReadOnlyList<RespostaParaResultado> respostas, int? minimo)
    {
        var porPergunta = respostas.ToLookup(r => r.PerguntaId);
        var visiveis = new List<FormularioResultadoPergunta>();
        var ocultas = 0;
        foreach (var pergunta in perguntas.OrderBy(p => p.Ordem).ThenBy(p => p.Id))
        {
            var dela = porPergunta[pergunta.Id].ToList();
            if (minimo is { } piso && dela.Count < piso)
            {
                ocultas++;
                continue;
            }

            visiveis.Add(Uma(pergunta, dela));
        }

        return (visiveis, ocultas);
    }

    private static FormularioResultadoPergunta Uma(PerguntaLida pergunta, List<RespostaParaResultado> dela)
    {
        var total = dela.Count;
        return pergunta.Tipo switch
        {
            TipoDePergunta.MultiplaEscolha => new FormularioResultadoPergunta(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, total, Alternativas(pergunta, dela), null, []),
            TipoDePergunta.Escala => new FormularioResultadoPergunta(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, total, [], Escala(dela), []),
            TipoDePergunta.RespostaCurta or TipoDePergunta.RespostaLonga => new FormularioResultadoPergunta(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, total, [], null, Textos(dela)),
            _ => new FormularioResultadoPergunta(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, total, [], null, []),
        };
    }

    private static List<FormularioResultadoAlternativa> Alternativas(PerguntaLida pergunta, List<RespostaParaResultado> dela) =>
        [.. pergunta.Alternativas.OrderBy(a => a.Ordem).ThenBy(a => a.Id).Select(a =>
        {
            var quantas = dela.Count(r => r.AlternativaId == a.Id);
            return new FormularioResultadoAlternativa(a.Id, a.Texto, quantas, Percentual(quantas, dela.Count));
        })];

    private static FormularioResultadoEscala? Escala(List<RespostaParaResultado> dela)
    {
        var valores = dela.Select(r => int.TryParse(r.Resposta, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) ? valor : 0).Where(v => v is >= 1 and <= 5).ToList();
        if (valores.Count == 0)
        {
            return null;
        }

        var media = Math.Round((decimal)valores.Sum() / valores.Count, 1, MidpointRounding.AwayFromZero);
        return new FormularioResultadoEscala(media, [.. Enumerable.Range(1, 5).Select(v => new FormularioResultadoEscalaValor(v, valores.Count(x => x == v)))]);
    }

    private static List<string> Textos(List<RespostaParaResultado> dela) =>
        [.. dela.Select(r => r.Resposta ?? string.Empty).Order(Alfabetica.ThenOrdinal())];

    /// <summary>O percentual com uma casa, sobre as respostas da pergunta (0 se não houve nenhuma).</summary>
    public static decimal Percentual(int parte, int total) => total == 0 ? 0 : Math.Round(100m * parte / total, 1, MidpointRounding.AwayFromZero);

    private static IComparer<string> ThenOrdinal(this StringComparer principal) => Comparer<string>.Create((a, b) =>
    {
        var comparacao = principal.Compare(a, b);
        return comparacao != 0 ? comparacao : StringComparer.Ordinal.Compare(a, b);
    });
}

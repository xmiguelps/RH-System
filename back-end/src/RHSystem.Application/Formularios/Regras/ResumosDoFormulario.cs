using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>
/// O tempo estimado para responder, em minutos, que a lista "meus formulários" mostra: o peso de cada tipo de pergunta somado, vezes o número de pessoas a avaliar (uma avaliação por pessoa; sem avaliados vale 1), arredondado para cima, no mínimo 1.
/// Pesos (hipótese do contrato, que só fala em "tempo estimado"): resposta curta 1, longa 1,5, múltipla escolha 0,5, escala 0,5, arquivo 1.
/// </summary>
public static class MinutosEstimados
{
    public static int De(IEnumerable<TipoDePergunta> tipos, int avaliados)
    {
        var porRodada = tipos.Sum(Peso);
        var total = porRodada * Math.Max(1, avaliados);
        return Math.Max(1, (int)Math.Ceiling(total));
    }

    private static decimal Peso(TipoDePergunta tipo) => tipo switch
    {
        TipoDePergunta.RespostaCurta => 1m,
        TipoDePergunta.RespostaLonga => 1.5m,
        TipoDePergunta.MultiplaEscolha => 0.5m,
        TipoDePergunta.Escala => 0.5m,
        _ => 1m, // arquivo
    };
}

/// <summary>As contagens que o resumo "quem responde" lê: o alvo, o sentido (só na avaliação), se é avaliação e as pessoas, setores, gestores e avaliações a fazer.</summary>
public sealed record ResumoDoPublico(AlvoDoFormulario? Alvo, SentidoDaAvaliacao? Sentido, bool EhAvaliacao, int Pessoas, int Setores, bool TodosOsSetores, int Gestores, int Avaliacoes);

/// <summary>O texto curto de "quem responde" da lista do RH: "3 pessoas escolhidas", "2 setores (12 pessoas)", "3 gestores (24 avaliações)", "Candidatos, por link".</summary>
public static class QuemRespondeResumo
{
    public static string Texto(ResumoDoPublico resumo)
    {
        if (resumo.Alvo is null)
        {
            return "Ainda não definido";
        }

        if (resumo.Alvo == AlvoDoFormulario.Link)
        {
            return "Candidatos, por link";
        }

        var avaliacoes = Contar(resumo.Avaliacoes, "avaliação", "avaliações");
        if (resumo.EhAvaliacao && resumo.Sentido == SentidoDaAvaliacao.GestorAvaliaEquipe)
        {
            return $"{Contar(resumo.Gestores, "gestor", "gestores")} ({avaliacoes})";
        }

        if (resumo.Alvo == AlvoDoFormulario.Pessoas)
        {
            var escolhidas = Contar(resumo.Pessoas, "pessoa escolhida", "pessoas escolhidas");
            return resumo.EhAvaliacao ? $"{escolhidas} ({avaliacoes})" : escolhidas;
        }

        var setores = resumo.TodosOsSetores ? "Todos os setores" : Contar(resumo.Setores, "setor", "setores");
        return $"{setores} ({(resumo.EhAvaliacao ? avaliacoes : Contar(resumo.Pessoas, "pessoa", "pessoas"))})";
    }

    private static string Contar(int quantas, string singular, string plural) => $"{quantas} {(quantas == 1 ? singular : plural)}";
}

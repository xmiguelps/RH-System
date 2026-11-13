using RHSystem.Application.Formularios.Destinatarios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>Uma linha da lista antes de ganhar os dados das pessoas: quem, o avaliado, e o instante em que respondeu (<c>null</c> = não respondeu).</summary>
public sealed record LinhaDeRespondente(int PessoaId, int? AvaliadoId, DateTime? RespondidoEmUtc);

/// <summary>
/// As contas da tela "Quem respondeu", puras. <b>Com nome</b> (fora a Sugestão): uma linha por par esperado (quem responde e o avaliado, nas avaliações) mais quem respondeu e já saiu do público, com o instante da resposta; <c>respondidos</c> e <c>faltam</c> contam só os pares ESPERADOS (a taxa nunca passa de 100).
/// <b>Sugestão</b>: uma linha por envio e só <c>respondidos</c> (os envios). <b>Anônimo</b>: sem linhas; a contagem só existe depois de encerrado (as participações), antes só os esperados (a fotografia inteira). Sem público ainda (<c>Alvo</c> vazio): nada de esperados.
/// </summary>
public static class RegrasDosRespondentes
{
    public static IReadOnlyList<LinhaDeRespondente> Linhas(bool sugestao, QuemDeveResponder quem, IReadOnlyList<EnvioLido> envios)
    {
        if (sugestao)
        {
            return [.. envios.Select(e => new LinhaDeRespondente(e.PessoaId, null, e.RespondidoEmUtc))];
        }

        var instantes = envios.GroupBy(e => new ParDeResposta(e.PessoaId, e.AvaliadoId)).ToDictionary(g => g.Key, g => g.Max(e => e.RespondidoEmUtc));
        var todos = quem.Pares.Concat(instantes.Keys).Distinct();
        return [.. todos.Select(p => new LinhaDeRespondente(p.PessoaId, p.AvaliadoId, instantes.TryGetValue(p, out var instante) ? instante : null))];
    }

    public static FormularioParticipacaoResumo Resumo(bool sugestao, bool anonimo, bool encerrado, QuemDeveResponder quem, IReadOnlyList<EnvioLido> envios, int participacoes)
    {
        if (sugestao)
        {
            return new FormularioParticipacaoResumo(envios.Count, null, null, null);
        }

        int? esperados = quem.Alvo is null ? null : quem.Esperados;
        if (anonimo)
        {
            return encerrado ? Exato(participacoes, esperados) : new FormularioParticipacaoResumo(null, null, esperados, null);
        }

        var responderam = envios.Select(e => new ParDeResposta(e.PessoaId, e.AvaliadoId)).ToHashSet();
        return Exato(quem.Alvo is null ? responderam.Count : quem.Pares.Count(responderam.Contains), esperados);
    }

    private static FormularioParticipacaoResumo Exato(int respondidos, int? esperados) =>
        esperados is not { } total
            ? new FormularioParticipacaoResumo(respondidos, null, null, null)
            : new FormularioParticipacaoResumo(respondidos, Math.Max(total - respondidos, 0), total, total == 0 ? 0 : Math.Min(100, (int)Math.Round(100m * respondidos / total, MidpointRounding.AwayFromZero)));
}

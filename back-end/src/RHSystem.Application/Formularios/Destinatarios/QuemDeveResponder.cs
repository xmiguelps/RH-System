using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Destinatarios;

/// <summary>
/// Um par "quem responde" e "quem é avaliado". Fora das avaliações o <c>AvaliadoId</c> é <c>null</c> (uma resposta por pessoa); nas avaliações cada par é uma avaliação (o gestor avalia cada pessoa da equipe; cada pessoa avalia o gestor do setor).
/// </summary>
public sealed record ParDeResposta(int PessoaId, int? AvaliadoId);

/// <summary>
/// Quem deve responder o formulário: os pares e os números que o contrato mostra (esperados, resumo da lista). Calculado <b>na hora</b> nos formulários com nome (quem entra no setor depois também responde); congelado numa fotografia (pessoa × avaliado) só no
/// anônimo publicado. <c>PessoasEscolhidas</c> é o que o RH escolheu (pessoas ou, nas avaliações do gestor, gestores), <c>SetoresEscolhidos</c> e <c>TodosOsSetores</c> descrevem a escolha por setor e <c>Gestores</c> conta os gestores que avaliam.
/// </summary>
public sealed record QuemDeveResponder(
    AlvoDoFormulario? Alvo,
    SentidoDaAvaliacao? Sentido,
    bool EhAvaliacao,
    IReadOnlyList<ParDeResposta> Pares,
    int PessoasEscolhidas,
    int SetoresEscolhidos,
    bool TodosOsSetores,
    int Gestores)
{
    /// <summary>Quanto se espera de respostas: avaliações (pares) nas avaliações, pessoas nos outros formulários. Por link e sem público ainda: 0 (a rota mostra <c>null</c>).</summary>
    public int Esperados => EhAvaliacao ? Pares.Count : PessoasQueRespondem;

    /// <summary>As pessoas distintas que respondem (numa avaliação, quem avalia).</summary>
    public int PessoasQueRespondem => Pares.Select(p => p.PessoaId).Distinct().Count();

    /// <summary>O número de "pessoas" do resumo: as escolhidas, quando o RH escolheu pessoas; as que respondem, quando escolheu setores.</summary>
    public int Pessoas => Alvo == AlvoDoFormulario.Pessoas ? PessoasEscolhidas : PessoasQueRespondem;

    public ResumoDoPublico Resumo => new(Alvo, Sentido, EhAvaliacao, Pessoas, SetoresEscolhidos, TodosOsSetores, Gestores, Pares.Count);

    /// <summary>Os pares que ainda não responderam, na mesma ordem. Fora das avaliações o par é só a pessoa (<c>AvaliadoId</c> nulo).</summary>
    public IReadOnlyList<ParDeResposta> QuemFalta(IEnumerable<ParDeResposta> jaResponderam)
    {
        var feitos = jaResponderam.ToHashSet();
        return [.. Pares.Where(p => !feitos.Contains(p))];
    }
}

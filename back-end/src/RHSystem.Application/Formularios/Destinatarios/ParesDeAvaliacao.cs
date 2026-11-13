namespace RHSystem.Application.Formularios.Destinatarios;

/// <summary>
/// As duas formas de avaliação do contrato, em pares. <b>GESTOR_AVALIA_EQUIPE</b>: cada gestor escolhido avalia cada pessoa da equipe dele, menos ele mesmo. <b>EQUIPE_AVALIA_GESTOR</b>: cada pessoa escolhida (ou do setor escolhido) avalia o gestor do setor dela; quem é de
/// setor sem gestor fica fora, e o gestor não se avalia. Sem par repetido e em ordem estável (por quem responde e depois por quem é avaliado): a ordem da lista não muda de uma leitura para outra.
/// </summary>
public static class ParesDeAvaliacao
{
    /// <param name="gestores">Cada gestor (o id da pessoa dele) e a equipe dele (os ids das pessoas); o próprio gestor, se aparecer na equipe, é descartado.</param>
    public static IReadOnlyList<ParDeResposta> GestorAvaliaEquipe(IEnumerable<(int GestorFuncionarioId, IReadOnlyList<int> Equipe)> gestores) =>
        [.. gestores
            .SelectMany(g => g.Equipe.Where(membro => membro != g.GestorFuncionarioId).Select(membro => new ParDeResposta(g.GestorFuncionarioId, membro)))
            .Distinct()
            .OrderBy(p => p.PessoaId)
            .ThenBy(p => p.AvaliadoId)];

    /// <param name="pessoas">Cada pessoa que avalia e o gestor do setor dela (<c>null</c> se o setor não tem gestor).</param>
    public static IReadOnlyList<ParDeResposta> EquipeAvaliaGestor(IEnumerable<(int PessoaId, int? GestorFuncionarioId)> pessoas) =>
        [.. pessoas
            .Where(p => p.GestorFuncionarioId is not null && p.GestorFuncionarioId != p.PessoaId)
            .Select(p => new ParDeResposta(p.PessoaId, p.GestorFuncionarioId))
            .Distinct()
            .OrderBy(p => p.PessoaId)
            .ThenBy(p => p.AvaliadoId)];
}

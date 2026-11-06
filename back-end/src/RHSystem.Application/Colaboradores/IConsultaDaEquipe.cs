using RHSystem.Application.Escopo;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A leitura da equipe pelo gestor. A equipe é a definição única da 1a (<c>DaEquipe</c>: ativos, com a admissão já começada, do setor dele, sem ele) e <b>a consulta já exclui o que está fora</b>: nunca "buscar e
/// depois conferir". <c>equipe</c> nula é o gestor sem setor.
/// </summary>
public interface IConsultaDaEquipe
{
    /// <summary>Equipe nula: setor nulo, total 0, contagens zeradas, itens vazios (não é erro).</summary>
    Task<FuncionarioEquipeResposta> ListarAsync(EquipeDoGestor? equipe, CancellationToken ct);

    /// <summary><c>null</c> para quem não é da equipe: de outro setor, o próprio gestor, em admissão, inativo ou que não existe (a mesma resposta para todos, e sem equipe tudo é <c>null</c>).</summary>
    Task<FuncionarioFichaGestor?> ObterFichaAsync(EquipeDoGestor? equipe, int funcionarioId, CancellationToken ct);
}

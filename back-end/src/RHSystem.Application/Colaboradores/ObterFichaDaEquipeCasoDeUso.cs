using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;

namespace RHSystem.Application.Colaboradores;

/// <summary><c>obterFichaDaEquipe</c>: a ficha de uma pessoa da equipe. Fora do escopo (ou inexistente) é 404, nunca 403 nem 200 vazio.</summary>
public sealed class ObterFichaDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe consulta)
{
    public async Task<FuncionarioFichaGestor> ExecutarAsync(int funcionarioId, CancellationToken ct) =>
        (await consulta.ObterFichaAsync(await escopo.ObterEquipeAsync(ct), funcionarioId, ct)).OuNaoEncontrado();
}

using RHSystem.Application.Escopo;

namespace RHSystem.Application.Colaboradores;

/// <summary><c>listarEquipeDoGestor</c>: a equipe do gestor logado. O gestor sem setor recebe a lista vazia, não um erro.</summary>
public sealed class ListarEquipeDoGestorCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe consulta)
{
    public async Task<FuncionarioEquipeResposta> ExecutarAsync(CancellationToken ct) => await consulta.ListarAsync(await escopo.ObterEquipeAsync(ct), ct);
}

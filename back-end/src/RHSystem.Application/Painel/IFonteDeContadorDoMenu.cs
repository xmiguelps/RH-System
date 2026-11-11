using RHSystem.Application.Escopo;

namespace RHSystem.Application.Painel;

/// <summary>
/// Quem sabe contar um dos números do menu. Cada fatia dona registra a sua por DI (<c>AddScoped&lt;IFonteDeContadorDoMenu, ...&gt;</c>); várias fontes do
/// mesmo <see cref="Contador"/> somam, e sem nenhuma o contador vale 0. A rota é chamada a cada navegação do front: a fonte faz <b>uma consulta de contagem</b>
/// (<c>COUNT</c>), nunca carrega a lista para contar.
/// </summary>
public interface IFonteDeContadorDoMenu
{
    ContadorDoMenu Contador { get; }

    /// <summary>A contagem para a pessoa logada (use o <paramref name="escopo"/>: o funcionário vê só o dele e o gestor, só a equipe dele).</summary>
    Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct);
}

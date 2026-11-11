using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Escopo;

/// <summary>
/// A equipe de um gestor: o setor de que ele é o gestor (<c>SETORES.ID_GESTOR</c>), com o dia de hoje em -03:00. Quem é a equipe é definição única
/// (contrato, fatia 1): as pessoas ativas, com a admissão já começada, do setor, sem o próprio gestor.
/// </summary>
/// <param name="GestorId"><c>GESTORES.ID_GESTOR</c> do gestor logado.</param>
/// <param name="SetorId">O setor de que ele é o gestor.</param>
/// <param name="SetorNome">O nome do setor.</param>
/// <param name="GestorFuncionarioId"><c>FUNCIONARIOS.ID_FUNCIONARIO</c> do gestor (ele não é membro da própria equipe).</param>
/// <param name="Hoje">O dia de calendário em -03:00: quem entra hoje já está na equipe.</param>
public sealed record EquipeDoGestor(int GestorId, int SetorId, string SetorNome, int GestorFuncionarioId, DateOnly Hoje);

/// <summary>
/// "Um filtro de escopo único": quem está logado e o que ele enxerga. O caso de uso do gestor recebe a <see cref="EquipeDoGestor"/> (<c>null</c> é
/// lista vazia, ou 404 ao pedir uma pessoa), monta a consulta sobre <c>DaEquipe(equipe)</c> e termina em <c>OuNaoEncontrado()</c>: a consulta já
/// exclui o que está fora do escopo, e "não existe" e "é de outro setor" dão a mesma resposta. Só o RH vê todas as linhas; o funcionário lê só as
/// dele (<see cref="FuncionarioId"/>), nunca um id que veio da requisição nas rotas <c>/api/eu</c>.
/// </summary>
public interface IEscopoDoUsuario
{
    /// <exception cref="InvalidOperationException">Fora de requisição autenticada (sem login ou em segundo plano): não há pessoa para ter escopo.</exception>
    int UsuarioId { get; }

    /// <exception cref="InvalidOperationException">Fora de requisição autenticada.</exception>
    int FuncionarioId { get; }

    /// <exception cref="InvalidOperationException">Fora de requisição autenticada.</exception>
    Perfil Perfil { get; }

    /// <exception cref="InvalidOperationException">Fora de requisição autenticada.</exception>
    string Nome { get; }

    /// <summary>
    /// A equipe do gestor logado, lida do banco uma vez por requisição; <c>null</c> se a pessoa não é Gestor de um setor (perfil Funcionario ou RH,
    /// ou gestor sem setor). Se o gestor tem mais de um setor (o banco permite), vale o de menor id, com aviso no log.
    /// </summary>
    /// <exception cref="InvalidOperationException">Fora de requisição autenticada.</exception>
    Task<EquipeDoGestor?> ObterEquipeAsync(CancellationToken ct);
}

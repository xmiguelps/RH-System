using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Escopo;

/// <summary>Quem está agindo agora: uma pessoa logada, um visitante sem login ou o próprio sistema (tarefa em segundo plano).</summary>
public enum TipoDeAtorAtual
{
    Pessoa,
    Sistema,
    Anonimo,
}

/// <summary>
/// Quem está agindo agora, no escopo da requisição ou da tarefa em segundo plano. É o que a auditoria grava e o que o escopo por perfil lê;
/// nenhum caso de uso lê o <c>HttpContext</c> diretamente.
/// </summary>
public interface IAtorAtual
{
    TipoDeAtorAtual Tipo { get; }

    /// <summary><c>USUARIOS.ID_USUARIO</c> (o claim <c>sub</c>); nulo se não for uma pessoa.</summary>
    int? UsuarioId { get; }

    /// <summary><c>FUNCIONARIOS.ID_FUNCIONARIO</c> (o claim <c>fid</c>); nulo se não for uma pessoa.</summary>
    int? FuncionarioId { get; }

    /// <summary>O perfil do token; nulo se não for uma pessoa ou se o claim não for um dos três perfis.</summary>
    Perfil? Perfil { get; }

    /// <summary>O nome da pessoa, ou <c>Sistema</c> ou <c>Anônimo</c> quando não é uma pessoa.</summary>
    string Nome { get; }

    /// <summary>O IP de quem chamou; só existe em requisição.</summary>
    string? Ip { get; }

    /// <summary>Portal (requisição) ou Sistema (segundo plano).</summary>
    CanalDeOrigem Canal { get; }
}

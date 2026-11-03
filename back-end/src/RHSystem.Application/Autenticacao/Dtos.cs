using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Autenticacao;

public sealed record EntrarComando(string Email, string Senha);

public sealed record RenovarComando(string RefreshToken);

/// <summary>O usuário como a API o devolve. <paramref name="Cargo"/> e <paramref name="Setor"/> são os nomes, ou nulos se a pessoa não tem.</summary>
public sealed record UsuarioResposta(int Id, int FuncionarioId, string Nome, string Email, Perfil Perfil, string? Cargo, string? Setor);

public sealed record SessaoResposta(string TokenDeAcesso, DateTime AcessoExpiraEmUtc, string RefreshToken, UsuarioResposta Usuario);

public enum FalhaDeEntrada
{
    CredenciaisInvalidas,
    UsuarioInativo,

    /// <summary>Senha certa e conta ativa, mas o funcionário não tem nível de acesso, ou o nível é desconhecido.</summary>
    PerfilNaoConfigurado,
}

public sealed record ResultadoDeEntrada(SessaoResposta? Sessao, FalhaDeEntrada? Falha);

internal static class Mapeamento
{
    public static UsuarioResposta Usuario(Usuario usuario, Perfil perfil) =>
        new(
            usuario.Id,
            usuario.FuncionarioId,
            usuario.Funcionario.Nome,
            usuario.Funcionario.EmailCorporativo ?? string.Empty,
            perfil,
            usuario.Funcionario.Cargo?.Nome,
            usuario.Funcionario.Setor?.Nome);

    public static SessaoResposta Sessao(Usuario usuario, Perfil perfil, AcessoEmitido acesso, RefreshGerado refresh) =>
        new(acesso.Token, acesso.ExpiraEmUtc, refresh.Token, Usuario(usuario, perfil));
}

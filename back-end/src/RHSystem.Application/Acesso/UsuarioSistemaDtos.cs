using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Acesso;

/// <summary>
/// A situação do login de um colaborador, CALCULADA (nunca gravada): sem <c>USUARIOS</c> é <c>SEM_CONVITE</c>; com senha definida, <c>SENHA_DEFINIDA</c>; provisória com convite em aberto e dentro do
/// prazo, <c>CONVITE_ENVIADO</c>; provisória sem ele, <c>CONVITE_EXPIRADO</c>. No JSON: <c>SEM_CONVITE</c>, <c>CONVITE_ENVIADO</c>, <c>CONVITE_EXPIRADO</c>, <c>SENHA_DEFINIDA</c>.
/// </summary>
public enum UsuarioSistemaSituacao
{
    SemConvite,
    ConviteEnviado,
    ConviteExpirado,
    SenhaDefinida,
}

/// <summary>Uma linha de "Usuários do sistema" (tela 29). A linha é o colaborador; o login pode ainda não existir (<c>Ativo</c> é falso em <c>SEM_CONVITE</c>).</summary>
public sealed record UsuarioSistemaItem(
    int FuncionarioId,
    string Nome,
    string? EmailCorporativo,
    Perfil? Perfil,
    UsuarioSistemaSituacao Situacao,
    bool Ativo,
    DateTime? UltimoLoginEmUtc,
    DateTime? ConviteEnviadoEmUtc);

public interface IConsultaDeUsuariosDoSistema
{
    /// <summary>
    /// As linhas são os colaboradores ATIVOS (inclui os em admissão; inativos não aparecem). Ordem: nome, e o id desempata. A busca procura no nome e no e-mail corporativo. <c>situacao</c> e
    /// <c>ativo</c> filtram pelo mesmo valor que a linha mostra (o filtro e a projeção usam a mesma expressão). <paramref name="p"/> chega sem <c>OrdenarPor</c> e <c>Direcao</c>.
    /// </summary>
    Task<RespostaPaginada<UsuarioSistemaItem>> ListarAsync(ParametrosDeLista p, UsuarioSistemaSituacao? situacao, bool? ativo, CancellationToken ct);

    /// <summary>Qualquer colaborador que exista, ativo ou não (as respostas de convite, ativar e desativar usam).</summary>
    Task<UsuarioSistemaItem?> ObterAsync(int funcionarioId, CancellationToken ct);
}

using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Autenticacao;

/// <summary>
/// Uma linha de <c>NIVEIS_DE_ACESSO</c>. Implementa <see cref="IAuditavel"/> só para que a chave estrangeira <c>Funcionario.NivelDeAcessoId</c> apareça
/// no log pelo NOME ("Colaborador", "Gestor", "RH"); o nível em si não é <c>[Auditado]</c> (ninguém o cria nem altera pela API).
/// </summary>
public class NivelDeAcesso : IAuditavel
{
    private NivelDeAcesso()
    {
    }

    public NivelDeAcesso(string nome)
    {
        Nome = nome;
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    /// <summary>O nome como a tela mostra: <c>RH</c>, <c>Colaborador</c> (nível <c>Funcionario</c> no banco) ou <c>Gestor</c> (<c>Funcionario Gestor</c>).</summary>
    public string NomeParaAuditoria => Perfis.PerfilPorNome(Nome) switch
    {
        Perfil.RH => "RH",
        Perfil.Gestor => "Gestor",
        Perfil.Funcionario => "Colaborador",
        _ => Nome,
    };

    public string RegistroAfetado => $"Nível de acesso {NomeParaAuditoria}";
}

namespace RHSystem.Domain.Autenticacao;

/// <summary>O nome do nível de acesso no banco para cada perfil: o inverso de <see cref="Perfis.PerfilPorNome"/>.</summary>
public static class NomesDeNivel
{
    public static string DoPerfil(Perfil perfil) => perfil switch
    {
        Perfil.RH => "RH",
        Perfil.Funcionario => "Funcionario",
        Perfil.Gestor => "Funcionario Gestor",
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil desconhecido."),
    };
}

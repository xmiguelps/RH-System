namespace RHSystem.Domain.Autenticacao;

/// <summary>O perfil que a API enxerga. Vem do nome do nível de acesso do funcionário.</summary>
public enum Perfil
{
    Funcionario = 1,
    Gestor = 2,
    RH = 3,
}

public static class Perfis
{
    /// <summary>
    /// "RH" vira <see cref="Perfil.RH"/>, "Funcionario" vira <see cref="Perfil.Funcionario"/> e "Funcionario Gestor" vira
    /// <see cref="Perfil.Gestor"/> (nomes confirmados pelo dono em 2026-10-28). Nulo ou qualquer outro nome devolve nulo.
    /// </summary>
    public static Perfil? PerfilPorNome(string? nomeDoNivel) => nomeDoNivel switch
    {
        "RH" => Perfil.RH,
        "Funcionario" => Perfil.Funcionario,
        "Funcionario Gestor" => Perfil.Gestor,
        _ => null,
    };
}

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>Os dados de um dependente (nome, parentesco em texto livre e nascimento): o corpo dos pedidos e o que volta em <c>dependentesCadastrados</c>. Dado de menor de idade: nunca vai para log nem para rota de gestor.</summary>
public sealed record DadosDoDependente(string? Nome, string? Parentesco, DateOnly DataNascimento)
{
    /// <summary>Os textos sem espaços nas pontas, como são gravados.</summary>
    public DadosDoDependente Aparado() => this with { Nome = Nome?.Trim(), Parentesco = Parentesco?.Trim() };
}

/// <summary>O trajeto do vale-transporte. A origem costuma ser o endereço de casa: dado pessoal, fora do log e fora de rota de gestor.</summary>
public sealed record DadosDoTrajeto(string? Origem, string? Destino, string? LinhasMeios)
{
    public DadosDoTrajeto Aparado() => this with { Origem = Origem?.Trim(), Destino = Destino?.Trim(), LinhasMeios = LinhasMeios?.Trim() };
}

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>Os dados de um dependente (nome, parentesco em texto livre e nascimento): o corpo dos pedidos e o que volta em <c>dependentesCadastrados</c>. Dado de menor de idade: nunca vai para log nem para rota de gestor.</summary>
public sealed record DadosDoDependente(string Nome, string Parentesco, DateOnly DataNascimento)
{
    /// <summary>Os textos sem espaços nas pontas, como são gravados; um texto nulo (o corpo pode trazer <c>null</c>) vira vazio e a validação o recusa.</summary>
    public DadosDoDependente Aparado() => this with { Nome = (Nome ?? string.Empty).Trim(), Parentesco = (Parentesco ?? string.Empty).Trim() };
}

/// <summary>O trajeto do vale-transporte. A origem costuma ser o endereço de casa: dado pessoal, fora do log e fora de rota de gestor.</summary>
public sealed record DadosDoTrajeto(string Origem, string Destino, string LinhasMeios)
{
    /// <summary>Como <see cref="DadosDoDependente.Aparado"/>: sem espaços nas pontas e nulo vira vazio.</summary>
    public DadosDoTrajeto Aparado() => this with { Origem = (Origem ?? string.Empty).Trim(), Destino = (Destino ?? string.Empty).Trim(), LinhasMeios = (LinhasMeios ?? string.Empty).Trim() };
}

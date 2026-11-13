namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>
/// A lista "Quem vai entrar no plano" (<c>dependentesCadastrados</c>): os dependentes já aprovados em qualquer benefício da pessoa, uma vez só. Dois são o mesmo dependente quando nome, parentesco e nascimento são iguais, sem diferenciar maiúsculas nem minúsculas
/// (e sem espaços nas pontas); vale o primeiro da fila de entrada. A saída vem em ordem de nome, depois de nascimento.
/// </summary>
public static class DependentesCadastrados
{
    public static IReadOnlyList<DadosDoDependente> SemRepetir(IEnumerable<DadosDoDependente> dependentes)
    {
        var vistos = new HashSet<(string Nome, string Parentesco, DateOnly Nascimento)>();
        var unicos = new List<DadosDoDependente>();
        foreach (var dependente in dependentes.Select(d => d.Aparado()))
        {
            var chave = (
                (dependente.Nome ?? string.Empty).ToUpperInvariant(),
                (dependente.Parentesco ?? string.Empty).ToUpperInvariant(),
                dependente.DataNascimento);
            if (vistos.Add(chave))
            {
                unicos.Add(dependente);
            }
        }

        return unicos
            .OrderBy(d => d.Nome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(d => d.DataNascimento)
            .ToList();
    }
}

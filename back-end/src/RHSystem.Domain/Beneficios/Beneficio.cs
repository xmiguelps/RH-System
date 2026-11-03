namespace RHSystem.Domain.Beneficios;

/// <summary>Uma linha de <c>BENEFICIOS</c>: o catálogo de benefícios. As três marcas têm padrão <c>FALSE</c> no banco, mas o EF sempre as envia.</summary>
public class Beneficio
{
    private Beneficio()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public bool Opcional { get; private set; }

    public bool AceitaDependentes { get; private set; }

    public bool AceitaTrajeto { get; private set; }

    public static Beneficio Criar(string nome, string? descricao, bool opcional, bool aceitaDependentes, bool aceitaTrajeto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nome);
        return new Beneficio
        {
            Nome = nome,
            Descricao = descricao,
            Opcional = opcional,
            AceitaDependentes = aceitaDependentes,
            AceitaTrajeto = aceitaTrajeto,
        };
    }
}

using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Beneficios;

/// <summary>Uma linha de <c>BENEFICIOS</c>: o catálogo de benefícios. As três marcas têm padrão <c>FALSE</c> no banco, mas o EF sempre as envia.</summary>
[Auditado(AuditoriaArea.Cadastros, "Benefício")]
public class Beneficio : IAuditavel
{
    private Beneficio()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    [Auditar("Descrição")]
    public string? Descricao { get; private set; }

    [Auditar("Opcional")]
    public bool Opcional { get; private set; }

    [Auditar("Aceita dependentes")]
    public bool AceitaDependentes { get; private set; }

    [Auditar("Aceita trajeto")]
    public bool AceitaTrajeto { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Benefício {Nome}";

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

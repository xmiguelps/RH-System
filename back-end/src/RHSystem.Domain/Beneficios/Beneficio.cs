using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Beneficios;

/// <summary>
/// Uma linha de <c>BENEFICIOS</c>: o catálogo de benefícios (nome único sem diferenciar maiúsculas nem acentos). As três marcas têm padrão <c>FALSE</c> no banco, mas o EF sempre as envia. <see cref="Criar"/> e
/// <see cref="Atualizar"/> recusam com <see cref="ArgumentException"/> nome vazio ou acima de 100 (depois do <c>Trim</c>) e descrição acima de 255.
/// </summary>
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
        var beneficio = new Beneficio();
        beneficio.Atualizar(nome, descricao, opcional, aceitaDependentes, aceitaTrajeto);
        return beneficio;
    }

    /// <summary>Valida TUDO primeiro e só então grava: o que for recusado não deixa o benefício pela metade.</summary>
    public void Atualizar(string nome, string? descricao, bool opcional, bool aceitaDependentes, bool aceitaTrajeto)
    {
        var nomeValido = CamposDeTexto.Obrigatorio(nome, nameof(nome), 100);
        var descricaoValida = CamposDeTexto.Opcional(descricao, nameof(descricao), 255);
        Nome = nomeValido;
        Descricao = descricaoValida;
        Opcional = opcional;
        AceitaDependentes = aceitaDependentes;
        AceitaTrajeto = aceitaTrajeto;
    }
}

namespace RHSystem.Application.Arquivos;

/// <summary>
/// Cada fatia que guarda caminho de arquivo no banco registra UMA (<c>AddScoped</c>), com o prefixo dela (<c>"documentos/"</c>, <c>"holerites/"</c>...): a limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) só toca
/// prefixo que tem fonte, porque só a fonte sabe o que está referenciado. Sem a fonte, as sobras do prefixo ficam (seguro, mas ficam).
/// </summary>
public interface IFonteDeReferenciasDeArquivo
{
    /// <summary>Termina em <c>/</c>: <c>"documentos/"</c>.</summary>
    string Prefixo { get; }

    /// <summary>Quais destes caminhos (no máximo 100 por chamada) têm linha no banco.</summary>
    Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct);
}

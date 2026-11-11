using RHSystem.Application.Arquivos;
using RHSystem.Application.Ponto;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Diz à limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) que <b>nenhum</b> caminho de <c>espelhos/</c> é referenciado pelo banco: o PDF do espelho é de uso único (o link vale minutos), então a limpeza o apaga depois do mínimo de horas.
/// </summary>
public sealed class FonteDeReferenciasDosEspelhos : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => BaixarEspelhoEmPdfCasoDeUso.PastaNoArmazenamento + "/";

    public Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct) =>
        Task.FromResult<IReadOnlySet<string>>(new HashSet<string>(StringComparer.Ordinal));
}

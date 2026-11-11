using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Documentos;

/// <summary>
/// Diz à limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) quais caminhos de <c>documentos/</c> têm linha em <c>FUNCIONARIOS_DOCUMENTOS</c>. Sem esta fonte a limpeza nunca tocaria o prefixo; com ela, o que o banco não
/// referencia (a sobra de um envio que caiu entre o blob e a linha) é apagado depois do mínimo de horas.
/// </summary>
public sealed class FonteDeReferenciasDosDocumentos(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDeDocumento.PastaNoArmazenamento + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var encontrados = await db.Documentos.AsNoTracking()
            .Where(d => d.CaminhoArquivo != null && lista.Contains(d.CaminhoArquivo))
            .Select(d => d.CaminhoArquivo!)
            .ToListAsync(ct);
        return encontrados.ToHashSet(StringComparer.Ordinal);
    }
}

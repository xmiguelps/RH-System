using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Importacoes;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>Diz à limpeza de sobras quais caminhos de <c>importacoes/</c> têm linha no banco: o arquivo de uma importação ainda guardada nunca é tratado como sobra.</summary>
public sealed class FonteDeReferenciasDasImportacoes(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDeImportacao.PastaNoArmazenamento + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var encontrados = await db.Importacoes.AsNoTracking()
            .Where(i => i.CaminhoArquivo != null && lista.Contains(i.CaminhoArquivo))
            .Select(i => i.CaminhoArquivo!)
            .ToListAsync(ct);
        return encontrados.ToHashSet(StringComparer.Ordinal);
    }
}

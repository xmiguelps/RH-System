using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// Diz à limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) quais caminhos de <c>holerites/</c> têm linha em <c>HOLERITES</c>. O que o banco não referencia (a sobra de um envio que caiu entre o arquivo e a linha) é apagado depois de 24 horas. Sem esta fonte
/// as sobras ficariam para sempre; e o banco nunca apaga holerite, então nenhum caminho referenciado deixa de sê-lo.
/// </summary>
public sealed class FonteDeReferenciasDosHolerites(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDeHolerite.PastaNoArmazenamento + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var encontrados = await db.Holerites.AsNoTracking()
            .Where(h => h.CaminhoArquivo != null && lista.Contains(h.CaminhoArquivo))
            .Select(h => h.CaminhoArquivo!)
            .ToListAsync(ct);
        return encontrados.ToHashSet(StringComparer.Ordinal);
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Ponto.Atestados;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Diz à limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) quais caminhos de <c>atestados/</c> têm linha em <c>ATESTADOS</c>. O que o banco não referencia (a sobra de um envio que caiu entre o blob e a linha) é apagado depois do mínimo de horas.
/// </summary>
public sealed class FonteDeReferenciasDosAtestados(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDeAtestado.PastaNoArmazenamento + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var encontrados = await db.Atestados.AsNoTracking()
            .Where(a => lista.Contains(a.CaminhoArquivo))
            .Select(a => a.CaminhoArquivo)
            .ToListAsync(ct);
        return encontrados.ToHashSet(StringComparer.Ordinal);
    }
}

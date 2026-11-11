using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Importacoes;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>Os cargos, setores e benefícios de hoje, cada lista em ordem de nome (a ordem do banco: <c>utf8mb4_unicode_ci</c>, sem diferenciar maiúsculas nem acentos).</summary>
public sealed class ConsultaDoCatalogoDaImportacao(AppDbContext db) : IConsultaDoCatalogoDaImportacao
{
    public async Task<CatalogoDaImportacao> ObterAsync(CancellationToken ct)
    {
        var cargos = await db.Cargos.AsNoTracking().OrderBy(c => c.Nome).Select(c => new ItemDoCatalogo(c.Id, c.Nome)).ToListAsync(ct);
        var setores = await db.Setores.AsNoTracking().OrderBy(s => s.Nome).Select(s => new ItemDoCatalogo(s.Id, s.Nome)).ToListAsync(ct);
        var beneficios = await db.Beneficios.AsNoTracking().OrderBy(b => b.Nome).Select(b => new ItemDoCatalogo(b.Id, b.Nome)).ToListAsync(ct);
        return new CatalogoDaImportacao(cargos, setores, beneficios);
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Importacoes;
using RHSystem.Domain.Importacoes;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>As importações no banco. Quem chama abre a transação.</summary>
public sealed class ImportacoesRepositorio(AppDbContext db) : IImportacoesRepositorio
{
    public async Task AdicionarAsync(Importacao importacao, CancellationToken ct)
    {
        db.Importacoes.Add(importacao);
        await db.SaveChangesAsync(ct);
    }

    public async Task<Importacao?> ObterAsync(int id, CancellationToken ct) =>
        await db.Importacoes.AsNoTracking().FirstOrDefaultAsync(i => i.Id == id, ct);

    public async Task<bool> ReivindicarAsync(int id, DateTime agoraUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: a importação não é auditada por linha; o evento Importou é registrado pelo caso de uso, na mesma transação
        var linhas = await db.Importacoes
            .Where(i => i.Id == id && i.Situacao == SituacaoDaImportacao.Pronta && i.ExpiraEm > agoraUtc)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.Situacao, SituacaoDaImportacao.Confirmada)
                    .SetProperty(i => i.ConfirmadaEm, (DateTime?)agoraUtc)
                    .SetProperty(i => i.Previa, (string?)null)
                    .SetProperty(i => i.CaminhoArquivo, (string?)null)
                    .SetProperty(i => i.TotalCriados, i => (int?)i.TotalLinhas)
                    .SetProperty(i => i.ConvitesEnviados, (int?)0)
                    .SetProperty(i => i.IncluidosNoOnboarding, (int?)0)
                    .SetProperty(i => i.IncluidosNoFormularioAdmissao, (int?)0),
                ct);
        return linhas == 1;
    }

    public async Task RegistrarResultadoAsync(int id, int convites, int onboarding, int formulario, CancellationToken ct) =>
        // AUDITORIA-OK: a importação não é auditada por linha; o evento Importou é registrado pelo caso de uso, na mesma transação
        await db.Importacoes
            .Where(i => i.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(i => i.ConvitesEnviados, (int?)convites)
                    .SetProperty(i => i.IncluidosNoOnboarding, (int?)onboarding)
                    .SetProperty(i => i.IncluidosNoFormularioAdmissao, (int?)formulario),
                ct);

    public async Task<IReadOnlyList<FuncionarioResumo>> ResumirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct)
    {
        var ids = funcionarioIds.ToList();
        var porId = (await db.Funcionarios.AsNoTracking()
                .Where(f => ids.Contains(f.Id))
                .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo == null ? null : f.Cargo.Nome, f.Setor == null ? null : f.Setor.Nome))
                .ToListAsync(ct))
            .ToDictionary(r => r.Id);
        return [.. ids.Select(id => porId[id])];
    }

    public async Task<IReadOnlyList<ImportacaoVencida>> ListarVencidasAsync(DateTime agoraUtc, int maximo, CancellationToken ct) =>
        await db.Importacoes.AsNoTracking()
            .Where(i => i.ExpiraEm <= agoraUtc && (i.Previa != null || i.CaminhoArquivo != null))
            .OrderBy(i => i.Id)
            .Take(maximo)
            .Select(i => new ImportacaoVencida(i.Id, i.CaminhoArquivo))
            .ToListAsync(ct);

    public async Task LimparVencidaAsync(int id, bool tambemOCaminho, CancellationToken ct)
    {
        var vencida = db.Importacoes.Where(i => i.Id == id);
        if (tambemOCaminho)
        {
            // AUDITORIA-OK: limpeza de retenção (a prévia tem dado pessoal), sem evento por linha
            await vencida.ExecuteUpdateAsync(s => s.SetProperty(i => i.Previa, (string?)null).SetProperty(i => i.CaminhoArquivo, (string?)null), ct);
        }
        else
        {
            // AUDITORIA-OK: limpeza de retenção (a prévia tem dado pessoal), sem evento por linha
            await vencida.ExecuteUpdateAsync(s => s.SetProperty(i => i.Previa, (string?)null), ct);
        }
    }
}

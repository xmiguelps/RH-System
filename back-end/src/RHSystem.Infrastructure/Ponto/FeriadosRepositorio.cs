using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto.Feriados;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// A leitura e a gravação dos feriados. A lista usa a mesma regra de "quais feriados valem para a empresa" do calendário do ponto. A chave única <c>UQ_FERIADOS_DATA_NOME</c> (que não diferencia caixa nem acento) vira o 409 <c>FERIADO_DUPLICADO</c> ao gravar. O
/// evento de auditoria sai do interceptor do EF.
/// </summary>
public sealed class FeriadosRepositorio(AppDbContext db) : IFeriadosRepositorio
{
    public async Task<IReadOnlyList<Feriado>> ListarAsync(FiltroDeFeriados filtro, ConfiguracaoDaEmpresaLida? empresa, CancellationToken ct)
    {
        var inicio = new DateOnly(filtro.Ano, filtro.Mes ?? 1, 1);
        var fim = filtro.Mes is null ? new DateOnly(filtro.Ano, 12, 31) : inicio.AddMonths(1).AddDays(-1);
        var feriados = db.Feriados.AsNoTracking().Where(f => f.Data >= inicio && f.Data <= fim).QueValemPara(empresa);
        if (!filtro.IncluirInativos)
        {
            feriados = feriados.Where(f => f.Ativo);
        }

        var linhas = await feriados.ToListAsync(ct);
        return [.. linhas.OrderBy(f => f.Data).ThenBy(f => f.Tipo).ThenBy(f => f.Nome, StringComparer.Ordinal)];
    }

    public async Task<Feriado?> ObterParaEditarAsync(int feriadoId, CancellationToken ct) => await db.Feriados.FirstOrDefaultAsync(f => f.Id == feriadoId, ct);

    public async Task<int> AdicionarAsync(Feriado feriado, CancellationToken ct)
    {
        db.Feriados.Add(feriado);
        await SalvarAsync(ct);
        return feriado.Id;
    }

    public Task SalvarEdicaoAsync(CancellationToken ct) => SalvarAsync(ct);

    private async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.Feriado) is { } problema)
        {
            throw problema;
        }
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O saldo do banco de horas: <b>cada linha</b> (<c>QUANTIDADE</c>, em horas com 2 casas) vira minutos inteiros, com arredondamento, e só então os créditos somam e os débitos subtraem. Somar as horas e converter no fim erra até alguns minutos, e
/// <c>FN_CALCULAR_SALDO_BANCO_HORAS</c> soma horas: por isso a conta é esta. O último dia consolidado (<c>AtualizadoAte</c>) vem da tarefa de consolidação (<c>null</c> enquanto ela nunca rodou).
/// </summary>
public sealed class SaldoDoBancoDeHoras(AppDbContext db, IUltimoDiaConsolidado ultimoConsolidado) : ISaldoDoBancoDeHoras
{
    public async Task<SaldoDoBanco> ObterAsync(int funcionarioId, CancellationToken ct) =>
        new((await ObterSaldosAsync([funcionarioId], ct))[funcionarioId], await ultimoConsolidado.ObterAsync(ct));

    public async Task<IReadOnlyDictionary<int, int>> ObterSaldosAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct)
    {
        var ids = funcionarioIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var linhas = await db.LancamentosDoBancoDeHoras.AsNoTracking()
            .Where(l => ids.Contains(l.FuncionarioId))
            .Select(l => new { l.FuncionarioId, l.Tipo, l.Quantidade })
            .ToListAsync(ct);

        var porPessoa = linhas
            .GroupBy(l => l.FuncionarioId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantidade is { } horas ? (l.Tipo == TipoDeLancamentoDoBanco.Debito ? -1 : 1) * LancamentoDoBancoDeHoras.HorasParaMinutos(horas) : 0));
        return ids.ToDictionary(id => id, id => porPessoa.GetValueOrDefault(id));
    }
}

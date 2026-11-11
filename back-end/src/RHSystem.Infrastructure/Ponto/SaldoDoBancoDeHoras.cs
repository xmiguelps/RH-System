using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O saldo do banco de horas: <b>cada linha</b> (<c>QUANTIDADE</c>, em horas com 2 casas) vira minutos inteiros, com arredondamento, e só então os créditos somam e os débitos subtraem. Somar as horas e converter no fim erra até alguns minutos, e
/// <c>FN_CALCULAR_SALDO_BANCO_HORAS</c> soma horas: por isso a conta é esta. O último dia consolidado vem da tarefa de consolidação (<c>null</c> enquanto ela não existe ou nunca rodou).
/// </summary>
public sealed class SaldoDoBancoDeHoras(AppDbContext db) : ISaldoDoBancoDeHoras
{
    public async Task<SaldoDoBanco> ObterAsync(int funcionarioId, CancellationToken ct)
    {
        var linhas = await db.LancamentosDoBancoDeHoras.AsNoTracking()
            .Where(l => l.FuncionarioId == funcionarioId)
            .Select(l => new { l.Tipo, l.Quantidade })
            .ToListAsync(ct);

        var saldo = linhas.Sum(l => l.Quantidade is { } horas ? (l.Tipo == TipoDeLancamentoDoBanco.Debito ? -1 : 1) * LancamentoDoBancoDeHoras.HorasParaMinutos(horas) : 0);
        return new SaldoDoBanco(saldo, null);
    }
}

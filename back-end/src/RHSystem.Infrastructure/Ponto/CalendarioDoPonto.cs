using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O calendário do ponto (<see cref="ICalendarioDoPonto"/>). A UF e o município do feriado se comparam no banco (collation sem distinção de maiúscula nem de acento: "sao paulo" é "São Paulo"). A ordem dos tipos é a do enum e
/// se aplica na memória (no banco o tipo é texto e ordenaria pela letra).
/// </summary>
public sealed class CalendarioDoPonto(AppDbContext db, IConfiguracaoDaEmpresa empresa) : ICalendarioDoPonto
{
    public async Task<DiaDoCalendario> DiaAsync(DateOnly dia, CancellationToken ct)
    {
        var feriados = await FeriadosQueValemAsync(dia, dia, ct);
        var fimDeSemana = dia.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
        return new DiaDoCalendario(!fimDeSemana && feriados.Count == 0, feriados.Count > 0 ? feriados[0].Nome : null);
    }

    public async Task<IReadOnlyList<FeriadoQueVale>> FeriadosQueValemAsync(DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var configuracao = await empresa.ObterAsync(ct);
        var linhas = await db.Feriados.AsNoTracking()
            .Where(f => f.Ativo && f.Data >= de && f.Data <= ate)
            .QueValemPara(configuracao)
            .Select(f => new FeriadoQueVale(f.Data, f.Nome, f.Tipo))
            .ToListAsync(ct);
        return [.. linhas.OrderBy(f => f.Data).ThenBy(f => f.Tipo).ThenBy(f => f.Nome, StringComparer.Ordinal)];
    }
}

/// <summary>Quais feriados valem para a empresa (a mesma regra do calendário e da lista do RH).</summary>
internal static class EscopoDeFeriados
{
    public static IQueryable<Feriado> QueValemPara(this IQueryable<Feriado> feriados, ConfiguracaoDaEmpresaLida? empresa)
    {
        if (empresa is null)
        {
            return feriados.Where(f => f.Tipo == TipoDeFeriado.Nacional || f.Tipo == TipoDeFeriado.Empresa);
        }

        var uf = empresa.Uf;
        var municipio = empresa.Municipio;
        return feriados.Where(f =>
            f.Tipo == TipoDeFeriado.Nacional
            || f.Tipo == TipoDeFeriado.Empresa
            || (f.Tipo == TipoDeFeriado.Estadual && f.Uf == uf)
            || (f.Tipo == TipoDeFeriado.Municipal && f.Uf == uf && f.Municipio == municipio));
    }
}

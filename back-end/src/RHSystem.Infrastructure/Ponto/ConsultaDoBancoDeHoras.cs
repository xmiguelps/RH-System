using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O extrato do banco de horas de uma pessoa: as linhas com tipo, quantidade e dia de referência (as que o banco aceita sem esses campos não entram), filtradas por <c>de</c> e <c>ate</c> (inclusive) e pelo tipo, do dia mais recente para o mais antigo e, no mesmo dia, pelo id
/// maior. O total é o dos filtros.
/// </summary>
public sealed class ConsultaDoBancoDeHoras(AppDbContext db) : IConsultaDoBancoDeHoras
{
    public async Task<PaginaDoExtrato> ListarAsync(int funcionarioId, FiltroDoExtrato filtro, ParametrosDeLista parametros, CancellationToken ct)
    {
        var linhas = db.LancamentosDoBancoDeHoras.AsNoTracking()
            .Where(l => l.FuncionarioId == funcionarioId && l.Tipo != null && l.Quantidade != null && l.DiaDeReferencia != null);
        if (filtro.De is { } de)
        {
            linhas = linhas.Where(l => l.DiaDeReferencia >= de);
        }

        if (filtro.Ate is { } ate)
        {
            linhas = linhas.Where(l => l.DiaDeReferencia <= ate);
        }

        if (filtro.Tipo is { } tipo)
        {
            linhas = linhas.Where(l => l.Tipo == tipo);
        }

        var total = await linhas.CountAsync(ct);
        var pagina = await linhas.OrderByDescending(l => l.DiaDeReferencia).ThenByDescending(l => l.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).ToListAsync(ct);
        return new PaginaDoExtrato([.. pagina.Select(l => new LinhaDoExtrato(l.Id, l.DiaDeReferencia!.Value, l.Tipo!.Value, l.Quantidade!.Value, l.Observacao))], total);
    }
}

/// <summary>O indicador "banco de horas" da ficha do colaborador: o MESMO saldo do extrato (a soma dos minutos de cada linha), 0 para quem não tem linha.</summary>
public sealed class FonteDoBancoDeHorasNaFicha(ISaldoDoBancoDeHoras saldo) : IFonteDeIndicadorDaFicha
{
    public IndicadorDaFicha Indicador => IndicadorDaFicha.BancoDeHorasMinutos;

    public async Task<int?> ObterAsync(int funcionarioId, CancellationToken ct) => (await saldo.ObterSaldosAsync([funcionarioId], ct))[funcionarioId];
}

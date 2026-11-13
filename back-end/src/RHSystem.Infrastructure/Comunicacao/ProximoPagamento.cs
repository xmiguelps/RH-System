using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comunicacao;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// O próximo pagamento com o que o banco sabe: o dia da empresa, as exceções por mês (<c>PAGAMENTOS_EXCECOES</c>) e os feriados do calendário do ponto. Os feriados vêm de uma janela só
/// (do primeiro dia do mês de hoje até 37 meses à frente; o que vale é sempre de hoje em diante, então feriado de antes do mês não muda nenhum resultado): uma consulta, e não uma por data.
/// </summary>
public sealed class ProximoPagamento(AppDbContext db, IConfiguracaoDaEmpresa empresa, ICalendarioDoPonto calendario) : IProximoPagamento
{
    public async Task<PagamentoDoMes?> ObterAsync(DateOnly hoje, CancellationToken ct)
    {
        var dia = (await empresa.ObterAsync(ct))?.DiaPagamento;
        var inicioDoMes = new DateOnly(hoje.Year, hoje.Month, 1);

        var excecoes = (await db.ExcecoesDePagamento.AsNoTracking().Where(e => e.DataDoPagamento >= inicioDoMes).Select(e => new { e.Ano, e.Mes, e.DataDoPagamento }).ToListAsync(ct))
            .ToDictionary(e => (e.Ano, e.Mes), e => e.DataDoPagamento);

        var feriados = (await calendario.FeriadosQueValemAsync(inicioDoMes, inicioDoMes.AddMonths(37), ct)).Select(f => f.Data).ToHashSet();
        return RegraDoPagamento.Proximo(hoje, dia, (ano, mes) => excecoes.TryGetValue((ano, mes), out var data) ? data : null, feriados.Contains);
    }
}

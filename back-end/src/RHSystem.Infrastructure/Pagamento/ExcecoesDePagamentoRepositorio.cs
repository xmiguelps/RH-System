using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Pagamento;
using RHSystem.Domain.Pagamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Pagamento;

/// <summary>
/// As exceções de pagamento no banco. As gravações são entidades rastreadas (criar, trocar a data e excluir), para o interceptor registrar o evento de Cadastros; o PUT que perde a corrida na chave única <c>(ANO, MES)</c> (1062) relê a linha do outro e troca a data dela, em vez de falhar.
/// </summary>
public sealed class ExcecoesDePagamentoRepositorio(AppDbContext db) : IExcecoesDePagamentoRepositorio
{
    public async Task<IReadOnlyList<ExcecaoDePagamentoGravada>> ListarAsync(int? ano, (int Ano, int Mes)? aPartirDoMes, CancellationToken ct)
    {
        var consulta = db.ExcecoesDePagamento.AsNoTracking().AsQueryable();
        if (ano is { } daqueleAno)
        {
            consulta = consulta.Where(e => e.Ano == daqueleAno);
        }
        else if (aPartirDoMes is { } desde)
        {
            var anoInicial = desde.Ano;
            var mesInicial = desde.Mes;
            consulta = consulta.Where(e => e.Ano > anoInicial || (e.Ano == anoInicial && e.Mes >= mesInicial));
        }

        return await consulta.OrderBy(e => e.DataDoPagamento).ThenBy(e => e.Id).Select(e => new ExcecaoDePagamentoGravada(e.Ano, e.Mes, e.DataDoPagamento)).ToListAsync(ct);
    }

    public async Task<ExcecaoDePagamentoGravada> DefinirAsync(int ano, int mes, DateOnly data, CancellationToken ct)
    {
        var existente = await db.ExcecoesDePagamento.FirstOrDefaultAsync(e => e.Ano == ano && e.Mes == mes, ct);
        if (existente is null)
        {
            var nova = ExcecaoDePagamento.Criar(ano, mes, data);
            db.ExcecoesDePagamento.Add(nova);
            try
            {
                await db.SaveChangesAsync(ct);
                return new ExcecaoDePagamentoGravada(nova.Ano, nova.Mes, nova.DataDoPagamento);
            }
            catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveRepetida(erro))
            {
                // Outro PUT criou o mês entre a leitura e a gravação: o que perdeu a corrida troca a data da linha dele.
                db.ChangeTracker.Clear();
                existente = await db.ExcecoesDePagamento.FirstAsync(e => e.Ano == ano && e.Mes == mes, ct);
            }
        }

        existente.Substituir(data);
        await db.SaveChangesAsync(ct);
        return new ExcecaoDePagamentoGravada(existente.Ano, existente.Mes, existente.DataDoPagamento);
    }

    public async Task<bool> RemoverAsync(int ano, int mes, CancellationToken ct)
    {
        var existente = await db.ExcecoesDePagamento.FirstOrDefaultAsync(e => e.Ano == ano && e.Mes == mes, ct);
        if (existente is null)
        {
            return false;
        }

        db.ExcecoesDePagamento.Remove(existente);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateConcurrencyException)
        {
            // Outra chamada removeu a mesma exceção entre a leitura e a gravação.
            db.ChangeTracker.Clear();
            return false;
        }
    }
}

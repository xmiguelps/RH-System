using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Lê as férias sem entidade (a fatia 7 é a dona de <c>FERIAS</c>): só quem está aprovada, em gozo ou concluída tira a pessoa do expediente. Uma consulta pelo período, de todas as pessoas, e o chamador filtra na memória.
/// </summary>
public sealed class FeriasDoPonto(AppDbContext db) : IFeriasDoPonto
{
    public async Task<IReadOnlyList<FeriasDoPeriodo>> NoPeriodoAsync(DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var linhas = await db.Database.SqlQuery<LinhaDeFerias>(
            $"SELECT ID_FUNCIONARIO AS FuncionarioId, DATA_INICIO AS Inicio, DATA_FIM AS Fim FROM FERIAS WHERE STATUS_FERIAS IN ('APROVADA', 'EM_GOZO', 'CONCLUIDA') AND DATA_INICIO <= {ate} AND DATA_FIM >= {de}")
            .ToListAsync(ct);
        return [.. linhas.Select(l => new FeriasDoPeriodo(l.FuncionarioId, l.Inicio, l.Fim))];
    }

    /// <summary>A forma da linha lida (as colunas voltam com estes nomes).</summary>
    private sealed class LinhaDeFerias
    {
        public int FuncionarioId { get; set; }

        public DateOnly Inicio { get; set; }

        public DateOnly Fim { get; set; }
    }
}

using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// Matrícula pelo contador <c>SEQUENCIAS</c> (<c>NOME = 'MATRICULA'</c>). Dois comandos na MESMA conexão e transação do
/// <see cref="AppDbContext"/>: o <c>UPDATE</c> soma e guarda o valor no <c>LAST_INSERT_ID</c> da conexão, e o <c>SELECT</c> o lê. Duas
/// conexões nunca leem o mesmo valor (a linha fica travada até o <c>COMMIT</c>), e um <c>ROLLBACK</c> devolve o número.
/// </summary>
public sealed class GeradorDeMatriculas(AppDbContext db) : IGeradorDeMatriculas
{
    private const int MaximoPorChamada = 500;
    private const long UltimaMatricula = 99999;

    public async Task<IReadOnlyList<string>> ReservarAsync(int quantidade, CancellationToken ct)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(quantidade, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(quantidade, MaximoPorChamada);

        // Sem transação o UPDATE confirmaria sozinho e o LAST_INSERT_ID poderia vir de outra conexão do pool.
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Reservar matrículas exige uma transação aberta (o contador só é seguro dentro dela).");
        }

        // AUDITORIA-OK: contador técnico, sem registro por decisão
        var linhas = await db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + {quantidade}) WHERE NOME = 'MATRICULA'",
            ct);
        if (linhas == 0)
        {
            throw new InvalidOperationException("A linha MATRICULA não existe na tabela SEQUENCIAS (o script V001 a cria com valor 0).");
        }

        var ultimo = (await db.Database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS `Value`").ToListAsync(ct)).Single();
        if (ultimo > UltimaMatricula)
        {
            // A transação do chamador é desfeita pela exceção e o contador volta; a matrícula tem exatamente 5 dígitos (CHECK do banco).
            throw new InvalidOperationException($"As matrículas de 5 dígitos acabaram: o contador passaria de {UltimaMatricula}.");
        }

        return Enumerable.Range(0, quantidade)
            .Select(i => (ultimo - quantidade + 1 + i).ToString("D5", CultureInfo.InvariantCulture))
            .ToList();
    }
}

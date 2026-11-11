using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Uma chamada a <see cref="IGestorDoFuncionario.RefazerDosMembrosAsync"/>, vista no instante em que ela chegou: se havia transação aberta no contexto de quem chamou e qual era o <c>SETORES.ID_GESTOR</c>
/// do setor lido pela MESMA conexão (dentro da transação, o que já foi salvo e ainda não confirmado aparece).
/// </summary>
public sealed record ChamadaDeRefazerOsMembros(int SetorId, bool EmTransacao, int? GestorDoSetorNaMesmaTransacao);

/// <summary>O que os espiões do <see cref="IGestorDoFuncionario"/> anotam, de todas as requisições da API em que foram registrados (<see cref="ApiFactory.ComGestorDoFuncionarioEspiao"/>).</summary>
public sealed class ChamadasDoGestorDoFuncionario
{
    private readonly object _trava = new();
    private readonly List<ChamadaDeRefazerOsMembros> _chamadas = [];

    public IReadOnlyList<ChamadaDeRefazerOsMembros> Chamadas
    {
        get
        {
            lock (_trava)
            {
                return [.. _chamadas];
            }
        }
    }

    internal void Registrar(ChamadaDeRefazerOsMembros chamada)
    {
        lock (_trava)
        {
            _chamadas.Add(chamada);
        }
    }
}

/// <summary>
/// O decorador de teste de <see cref="IGestorDoFuncionario"/>: delega à implementação REAL da 1b (o efeito sobre o <c>ID_GESTOR</c> dos membros é o de verdade) e anota cada chamada a
/// <see cref="RefazerDosMembrosAsync"/> em <see cref="ChamadasDoGestorDoFuncionario"/>. Não reimplementa regra nenhuma.
/// </summary>
public sealed class GestorDoFuncionarioEspiao(GestorDoFuncionario real, AppDbContext db, ChamadasDoGestorDoFuncionario chamadas) : IGestorDoFuncionario
{
    public Task<ResolucaoDoGestor> ResolverAsync(int setorId, int? funcionarioId, CancellationToken ct) => real.ResolverAsync(setorId, funcionarioId, ct);

    public async Task<int> RefazerDosMembrosAsync(int setorId, CancellationToken ct)
    {
        var gestorDoSetor = await db.Setores.AsNoTracking().Where(s => s.Id == setorId).Select(s => s.GestorId).FirstOrDefaultAsync(ct);
        chamadas.Registrar(new ChamadaDeRefazerOsMembros(setorId, db.Database.CurrentTransaction is not null, gestorDoSetor));
        return await real.RefazerDosMembrosAsync(setorId, ct);
    }
}

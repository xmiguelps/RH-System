using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A porta do público (<see cref="IPublicoDoTreinamento"/>): quem é do público do treinamento agora. Só pessoas ATIVAS com a admissão já ocorrida (<c>hoje</c> inclusive); o setor da pessoa é o de hoje, e as linhas de <c>TREINAMENTOS_PUBLICO</c> dizem quais setores ou pessoas foram escolhidos (<c>TODOS</c> não guarda linha).
/// </summary>
public sealed class AlcanceDoPublicoDoTreinamento(AppDbContext db) : IPublicoDoTreinamento
{
    public async Task<IReadOnlyList<int>> PessoasAsync(int treinamentoId, DateOnly hoje, CancellationToken ct)
    {
        var id = treinamentoId;
        PublicoDoTreinamento? publico = await db.Treinamentos.AsNoTracking().Where(t => t.Id == id).Select(t => t.Publico).SingleOrDefaultAsync(ct);
        var alcancaveis = db.Funcionarios.AsNoTracking().Where(f => f.Ativo && f.DataAdmissao <= hoje);
        var escolhidas = publico switch
        {
            PublicoDoTreinamento.Todos => alcancaveis,
            PublicoDoTreinamento.Setores => alcancaveis.Where(f => f.SetorId != null && db.TreinamentosPublico.Any(p => p.TreinamentoId == id && p.SetorId == f.SetorId)),
            PublicoDoTreinamento.Pessoas => alcancaveis.Where(f => db.TreinamentosPublico.Any(p => p.TreinamentoId == id && p.FuncionarioId == f.Id)),
            _ => alcancaveis.Where(f => false),
        };
        return await escolhidas.OrderBy(f => f.Id).Select(f => f.Id).ToListAsync(ct);
    }
}

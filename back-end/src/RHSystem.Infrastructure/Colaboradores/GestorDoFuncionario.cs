using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// O gestor do colaborador é DERIVADO do setor (<c>FUNCIONARIOS.ID_GESTOR</c> = <c>SETORES.ID_GESTOR</c>), salvo para o próprio gestor, que fica sem gestor (o trigger do banco não o deixa ser gestor
/// de si mesmo). Lê o gestor do setor NO BANCO.
/// </summary>
public sealed class GestorDoFuncionario(AppDbContext db) : IGestorDoFuncionario
{
    private const string GestorInativo = "O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar.";

    public async Task<ResolucaoDoGestor> ResolverAsync(int setorId, int? funcionarioId, CancellationToken ct)
    {
        var gestor = await (
            from s in db.Setores.AsNoTracking()
            where s.Id == setorId && s.GestorId != null
            join g in db.Gestores.AsNoTracking() on s.GestorId equals g.Id
            join f in db.Funcionarios.AsNoTracking() on g.FuncionarioId equals f.Id
            select new { GestorId = g.Id, FuncionarioId = f.Id, f.Nome, f.Ativo }).FirstOrDefaultAsync(ct);

        if (gestor is null)
        {
            return new ResolucaoDoGestor(null, false, null); // setor inexistente ou sem gestor
        }

        if (funcionarioId is { } id && gestor.FuncionarioId == id)
        {
            return new ResolucaoDoGestor(null, false, gestor.Nome); // o gestor do setor não tem gestor
        }

        return new ResolucaoDoGestor(gestor.GestorId, !gestor.Ativo, gestor.Nome);
    }

    public async Task<int> RefazerDosMembrosAsync(int setorId, CancellationToken ct)
    {
        var resolucao = await ResolverAsync(setorId, null, ct);
        if (resolucao.GestorInativo)
        {
            throw new ErroDeNegocioException("GESTOR_DO_SETOR_INATIVO", GestorInativo);
        }

        var novoGestor = resolucao.GestorId;
        var funcionarioDoGestor = novoGestor is null
            ? null
            : await db.Gestores.AsNoTracking().Where(g => g.Id == novoGestor).Select(g => (int?)g.FuncionarioId).FirstOrDefaultAsync(ct);

        // AUDITORIA-OK: o gestor do colaborador é derivado do setor; o evento é o da troca de gestor do setor (fatia 1c)
        var mudaram = await db.Funcionarios
            .Where(f => f.SetorId == setorId && f.Ativo && f.Id != funcionarioDoGestor && f.GestorId != novoGestor)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.GestorId, novoGestor), ct);

        if (funcionarioDoGestor is { } proprio)
        {
            // O próprio gestor do setor fica sem gestor.
            // AUDITORIA-OK: o gestor do colaborador é derivado do setor; o evento é o da troca de gestor do setor (fatia 1c)
            mudaram += await db.Funcionarios
                .Where(f => f.Id == proprio && f.Ativo && f.GestorId != null)
                .ExecuteUpdateAsync(s => s.SetProperty(f => f.GestorId, (int?)null), ct);
        }

        return mudaram;
    }
}

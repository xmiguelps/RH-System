using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Equipe;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A leitura do gestor sobre os formulários da equipe. A equipe é a definição única do contrato: pessoas ativas, com a admissão já começada (quem entra hoje já está), do setor, sem o próprio gestor. Nunca lê o conteúdo de resposta nem devolve quem participou do anônimo: só CONTA as participações de quem
/// está na lista dada.
/// </summary>
public sealed class ConsultaDeEquipeDoFormulario(AppDbContext db) : IConsultaDeEquipeDoFormulario
{
    public async Task<IReadOnlyList<MembroDaEquipe>> MembrosAsync(int setorId, DateOnly hoje, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => f.SetorId == setorId && f.Ativo && f.DataAdmissao <= hoje && !db.Setores.Any(s => s.Id == setorId && s.Gestor != null && s.Gestor.FuncionarioId == f.Id))
            .OrderBy(f => f.Nome)
            .ThenBy(f => f.Id)
            .Select(f => new MembroDaEquipe(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<FormularioParaAEquipe>> AbertosAsync(CancellationToken ct) =>
        await db.Formularios.AsNoTracking()
            .Where(f => f.Ativo && f.EncerradoEmUtc == null && (f.Alvo == AlvoDoFormulario.Pessoas || f.Alvo == AlvoDoFormulario.Setores) && f.Tipo != TipoDeFormulario.Outro && f.Tipo != TipoDeFormulario.Avaliacao)
            .Select(f => new FormularioParaAEquipe(f.Id, f.Titulo, f.Tipo ?? TipoDeFormulario.Pesquisa, f.Anonimo, f.Ativo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo))
            .ToListAsync(ct);

    public Task<int> ParticipacoesDaEquipeAsync(int formularioId, IReadOnlyCollection<int> membros, CancellationToken ct) =>
        db.FormulariosParticipacoes.AsNoTracking().CountAsync(p => p.FormularioId == formularioId && membros.Contains(p.FuncionarioId), ct);
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// O que o lembrete lê do banco, só para montar e enviar (nenhuma rota devolve isto): quem já respondeu (com nome) ou participou (anônimo), como pares, e quem pode receber e-mail (ativo, com e-mail corporativo). A lista de quem participou do anônimo NÃO sai daqui para tela nenhuma: só a tarefa a usa,
/// para escolher a quem mandar o e-mail sigiloso.
/// </summary>
public sealed class ConsultaDeLembretes(AppDbContext db) : IConsultaDeLembretes
{
    public async Task<IReadOnlyList<ParDeResposta>> JaResponderamAsync(int formularioId, bool anonimo, CancellationToken ct)
    {
        if (anonimo)
        {
            var participacoes = await db.FormulariosParticipacoes.AsNoTracking().Where(p => p.FormularioId == formularioId).Select(p => new { p.FuncionarioId, p.AvaliadoId }).ToListAsync(ct);
            return [.. participacoes.Select(p => new ParDeResposta(p.FuncionarioId, p.AvaliadoId)).Distinct()];
        }

        var respostas = await db.FormulariosRespostas.AsNoTracking().Where(r => r.FormularioId == formularioId && r.FuncionarioId != null).Select(r => new { r.FuncionarioId, r.AvaliadoId }).Distinct().ToListAsync(ct);
        return [.. respostas.Select(r => new ParDeResposta(r.FuncionarioId!.Value, r.AvaliadoId))];
    }

    public async Task<IReadOnlyList<PessoaParaLembrar>> PodemReceberAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => ids.Contains(f.Id) && f.Ativo && f.EmailCorporativo != null)
            .OrderBy(f => f.Id)
            .Select(f => new PessoaParaLembrar(f.Id, f.Nome, f.EmailCorporativo!, f.SetorId))
            .ToListAsync(ct);
}

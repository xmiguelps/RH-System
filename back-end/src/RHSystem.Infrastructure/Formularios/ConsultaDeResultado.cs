using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A leitura do resultado e da exportação. A resposta ANÔNIMA sai só como <c>(pergunta, resposta, alternativa, avaliado)</c>: sem id, sem data (que de qualquer jeito é o dia da rodada) e sem autor (que ela nem tem). Quem usa soma e ordena por texto, então a ordem em que o banco devolve não aparece em lugar nenhum. As participações só são CONTADAS.
/// </summary>
public sealed class ConsultaDeResultado(AppDbContext db) : IConsultaDeResultado
{
    public async Task<IReadOnlyList<RespostaParaResultado>> RespostasAsync(int formularioId, int? avaliadoId, CancellationToken ct) =>
        await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && (avaliadoId == null || r.AvaliadoId == avaliadoId))
            .Select(r => new RespostaParaResultado(r.PerguntaId, r.Resposta, r.AlternativaId, r.AvaliadoId))
            .ToListAsync(ct);

    public async Task<int> TotalAsync(int formularioId, bool anonimo, int? avaliadoId, CancellationToken ct)
    {
        if (anonimo)
        {
            return await db.FormulariosParticipacoes.AsNoTracking().CountAsync(p => p.FormularioId == formularioId && (avaliadoId == null || p.AvaliadoId == avaliadoId), ct);
        }

        return await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && (avaliadoId == null || r.AvaliadoId == avaliadoId))
            .Select(r => new { r.FuncionarioId, r.CandidatoId, r.AvaliadoId, r.RespondidaEmUtc })
            .Distinct()
            .CountAsync(ct);
    }

    public async Task<IReadOnlyDictionary<int, int>> ParticipacoesPorAvaliadoAsync(int formularioId, CancellationToken ct) =>
        await db.FormulariosParticipacoes.AsNoTracking()
            .Where(p => p.FormularioId == formularioId && p.AvaliadoId != null)
            .GroupBy(p => p.AvaliadoId!.Value)
            .Select(g => new { Avaliado = g.Key, Total = g.Count() })
            .ToDictionaryAsync(g => g.Avaliado, g => g.Total, ct);

    public async Task<IReadOnlyList<LinhaDeExportacaoDeResposta>> LinhasParaExportarAsync(int formularioId, CancellationToken ct)
    {
        var linhas = await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && !r.Anonimo)
            .Select(r => new { r.PerguntaId, r.FuncionarioId, r.CandidatoId, r.AvaliadoId, r.Resposta, r.RespondidaEmUtc })
            .ToListAsync(ct);
        var perguntas = await db.FormulariosPerguntas.AsNoTracking().Where(p => p.FormularioId == formularioId).Select(p => new { p.Id, p.Ordem, p.Texto }).ToDictionaryAsync(p => p.Id, ct);
        var idsDePessoas = linhas.SelectMany(l => new[] { l.FuncionarioId, l.AvaliadoId }).Where(i => i is not null).Select(i => i!.Value).Distinct().ToList();
        var pessoas = await db.Funcionarios.AsNoTracking().Where(f => idsDePessoas.Contains(f.Id)).Select(f => new { f.Id, f.Nome, f.EmailCorporativo, Setor = f.Setor != null ? f.Setor.Nome : null }).ToDictionaryAsync(f => f.Id, ct);
        var candidatos = await db.FormulariosCandidatos.AsNoTracking().Where(c => c.FormularioId == formularioId).Select(c => new { c.Id, c.Nome, c.Email }).ToDictionaryAsync(c => c.Id, ct);

        return [.. linhas
            .Where(l => perguntas.ContainsKey(l.PerguntaId))
            .Select(l =>
            {
                var pergunta = perguntas[l.PerguntaId];
                var funcionario = l.FuncionarioId is { } f && pessoas.TryGetValue(f, out var achada) ? achada : null;
                var candidato = l.CandidatoId is { } c && candidatos.TryGetValue(c, out var candidata) ? candidata : null;
                var avaliado = l.AvaliadoId is { } a && pessoas.TryGetValue(a, out var avaliada) ? avaliada.Nome : null;
                return new LinhaDeExportacaoDeResposta(funcionario?.Nome ?? candidato?.Nome ?? string.Empty, funcionario?.EmailCorporativo ?? candidato?.Email, funcionario?.Setor, avaliado, pergunta.Ordem ?? int.MaxValue, pergunta.Texto ?? string.Empty, l.Resposta, l.RespondidaEmUtc);
            })
            .OrderBy(l => l.Respondente, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.Avaliado ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(l => l.RespondidoEmUtc)
            .ThenBy(l => l.PerguntaOrdem)];
    }
}

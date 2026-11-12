using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A leitura do RH sobre as respostas de um formulário. Respostas COM NOME saem pela pessoa, pelo avaliado e pelo instante do envio; a anônima nunca é lida por aqui (só a contagem de participações, que não tem data nem ligação com as respostas) e nenhuma consulta devolve o id nem a ordem de gravação
/// de uma resposta anônima. O caminho do arquivo só sai para <see cref="ArquivoAsync"/>, que o entrega ao caso de uso para gerar o link temporário.
/// </summary>
public sealed class ConsultaDeAcompanhamento(AppDbContext db) : IConsultaDeAcompanhamento
{
    public async Task<IReadOnlyList<EnvioLido>> EnviosAsync(int formularioId, CancellationToken ct)
    {
        var linhas = await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && r.FuncionarioId != null && r.RespondidaEmUtc != null)
            .Select(r => new { r.FuncionarioId, r.AvaliadoId, r.RespondidaEmUtc })
            .Distinct()
            .ToListAsync(ct);
        return [.. linhas.Select(l => new EnvioLido(l.FuncionarioId!.Value, l.AvaliadoId, l.RespondidaEmUtc!.Value))];
    }

    public Task<int> ParticipacoesAsync(int formularioId, CancellationToken ct) => db.FormulariosParticipacoes.AsNoTracking().CountAsync(p => p.FormularioId == formularioId, ct);

    public async Task<IReadOnlyList<PessoaDaLista>> PessoasAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new PessoaDaLista(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null, f.SetorId)).ToListAsync(ct);

    public async Task<IReadOnlyList<RespostaLida>> DoEnvioAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime? instanteUtc, CancellationToken ct) =>
        await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && r.FuncionarioId == funcionarioId && r.AvaliadoId == avaliadoId && (instanteUtc == null || r.RespondidaEmUtc == instanteUtc))
            .OrderBy(r => r.PerguntaId)
            .Select(r => new RespostaLida(r.Id, r.PerguntaId, r.Resposta, r.AlternativaId, r.NomeArquivoOriginal, r.RespondidaEmUtc))
            .ToListAsync(ct);

    public async Task<ArquivoDeRespostaLido?> ArquivoAsync(int formularioId, int respostaId, CancellationToken ct) =>
        await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.Id == respostaId && r.FormularioId == formularioId && !r.Anonimo && r.CaminhoArquivo != null && r.NomeArquivoOriginal != null)
            .Select(r => new ArquivoDeRespostaLido(r.CaminhoArquivo!, r.NomeArquivoOriginal!))
            .FirstOrDefaultAsync(ct);
}

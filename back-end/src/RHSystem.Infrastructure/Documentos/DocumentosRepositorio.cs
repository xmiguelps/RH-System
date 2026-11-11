using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Documentos;

/// <summary>
/// Os documentos da ficha no banco. A lista vai dos mais recentes para os mais antigos (data de cadastro, que o banco preenche, e depois o id) e traz só os campos do contrato: o caminho do arquivo nunca sai daqui como dado
/// de resposta. O documento só é devolvido se for da pessoa dada: de outra pessoa ou inexistente dá o mesmo <c>null</c>.
/// </summary>
public sealed class DocumentosRepositorio(AppDbContext db) : IDocumentosRepositorio
{
    public async Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.FirstOrDefaultAsync(f => f.Id == funcionarioId, ct);

    public async Task<IReadOnlyList<DocumentoColaborador>> ListarAsync(int funcionarioId, CancellationToken ct) =>
        await db.Documentos.AsNoTracking()
            .Where(d => d.FuncionarioId == funcionarioId)
            .OrderByDescending(d => d.CadastradoEm)
            .ThenByDescending(d => d.Id)
            .Select(d => new DocumentoColaborador(d.Id, d.Tipo, d.Descricao, d.DataValidade, d.Situacao, d.NomeArquivoOriginal, d.PedidoEmUtc, d.EnviadoEmUtc))
            .ToListAsync(ct);

    public async Task<DocumentoDoColaborador?> ObterDaPessoaAsync(int funcionarioId, int documentoId, CancellationToken ct) =>
        await db.Documentos.AsNoTracking()
            .Include(d => d.Funcionario)
            .FirstOrDefaultAsync(d => d.Id == documentoId && d.FuncionarioId == funcionarioId, ct);

    public async Task AdicionarAsync(DocumentoDoColaborador documento, CancellationToken ct)
    {
        db.Documentos.Add(documento);
        await db.SaveChangesAsync(ct);
    }
}

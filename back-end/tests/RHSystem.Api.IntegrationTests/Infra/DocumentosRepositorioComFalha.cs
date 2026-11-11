using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// O <see cref="IDocumentosRepositorio"/> de verdade com UMA falha forçada: <see cref="AdicionarAsync"/> grava a linha e o evento (dentro da transação do caso de uso) e DEPOIS lança, como um banco que cai no
/// meio. O teste prova que a transação desfaz a linha e o evento e que o caso de uso apaga o blob (<see cref="ApiFactory.ComFalhaNoBancoAoSalvarDocumento"/>).
/// </summary>
public sealed class DocumentosRepositorioComFalha(IDocumentosRepositorio real) : IDocumentosRepositorio
{
    public const string TextoDaFalha = "falha-simulada-do-banco-ao-salvar-documento-4242";

    public Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) => real.ObterPessoaAsync(funcionarioId, ct);

    public Task<IReadOnlyList<DocumentoColaborador>> ListarAsync(int funcionarioId, CancellationToken ct) => real.ListarAsync(funcionarioId, ct);

    public Task<DocumentoDoColaborador?> ObterDaPessoaAsync(int funcionarioId, int documentoId, CancellationToken ct) => real.ObterDaPessoaAsync(funcionarioId, documentoId, ct);

    public async Task AdicionarAsync(DocumentoDoColaborador documento, CancellationToken ct)
    {
        await real.AdicionarAsync(documento, ct);
        throw new DbUpdateException(TextoDaFalha);
    }
}

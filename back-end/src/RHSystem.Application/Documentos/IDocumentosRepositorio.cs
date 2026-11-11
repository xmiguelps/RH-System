using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Documentos;

/// <summary>Os documentos no banco. A gravação passa pelo <c>SaveChanges</c> do contexto, onde o interceptor grava o evento <c>Criou</c> na mesma transação.</summary>
public interface IDocumentosRepositorio
{
    /// <summary>A pessoa RASTREADA (o documento novo aponta para ela e o log mostra o nome); <c>null</c> se não existe. Vale também para inativos.</summary>
    Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Os documentos da pessoa, dos mais recentes para os mais antigos (data de cadastro e depois id, decrescentes).</summary>
    Task<IReadOnlyList<DocumentoColaborador>> ListarAsync(int funcionarioId, CancellationToken ct);

    /// <summary>O documento SÓ se for da pessoa dada (com ela, para o nome no log); de outra pessoa ou inexistente dá o mesmo <c>null</c>.</summary>
    Task<DocumentoDoColaborador?> ObterDaPessoaAsync(int funcionarioId, int documentoId, CancellationToken ct);

    /// <summary>Grava o documento novo (a linha e o evento).</summary>
    Task AdicionarAsync(DocumentoDoColaborador documento, CancellationToken ct);
}

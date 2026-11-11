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

    /// <summary>O documento RASTREADO (com a pessoa, para o log), só se for da pessoa dada, para receber o arquivo; de outra pessoa ou inexistente dá o mesmo <c>null</c>.</summary>
    Task<DocumentoDoColaborador?> ObterParaEnvioAsync(int funcionarioId, int documentoId, CancellationToken ct);

    /// <summary>
    /// Grava o envio do arquivo no documento rastreado (a linha e o evento <c>Alterou</c>, juntos). Devolve <c>false</c>, sem gravar nada, se a situação já não é a que foi lida (outro envio chegou antes: a <c>Situacao</c> é
    /// o token de concorrência); qualquer outra falha do banco sobe como exceção.
    /// </summary>
    Task<bool> SalvarEnvioAsync(CancellationToken ct);

    /// <summary>Quantos documentos <c>Pendente</c> a pessoa tem: UMA consulta de contagem (a bolinha do menu, chamada a cada navegação).</summary>
    Task<int> ContarPendentesAsync(int funcionarioId, CancellationToken ct);
}

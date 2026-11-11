namespace RHSystem.Application.Arquivos;

/// <summary>Um link para baixar um arquivo (o schema <c>LinkTemporario</c> do contrato): vale por pouco tempo, só para leitura, e baixa como anexo.</summary>
public sealed record LinkTemporario(string Url, DateTime ExpiraEmUtc);

/// <summary>Um arquivo que está no armazenamento: o caminho, o tamanho em bytes e quando foi criado (UTC).</summary>
public sealed record ArquivoGuardado(string Caminho, long Tamanho, DateTime CriadoEmUtc);

/// <summary>
/// Onde ficam os arquivos (Blob Storage; nos testes, memória). <b>Reaproveitável por holerites, atestados, currículos, respostas de formulário e módulos; nomes e assinaturas não mudam depois.</b> O caminho SEMPRE vem de
/// <see cref="CaminhosDeArquivo.Novo"/> (um GUID, nunca o nome do usuário). A receita do envio (decisão 11 do plano): validar tudo, gravar aqui SEM transação aberta, uma transação curta que insere a linha e, se qualquer
/// passo falhar, <see cref="ApagarAsync"/> do caminho.
/// </summary>
public interface IArmazenamentoDeArquivos
{
    /// <summary>Grava o arquivo, com o <c>Content-Type</c> dado. <b>Não sobrescreve</b>: caminho que já existe lança. Caminho que não é de <see cref="CaminhosDeArquivo.EhValido"/> lança <see cref="ArgumentException"/>, sem falar com o serviço.</summary>
    Task GravarAsync(string caminho, Stream conteudo, string tipoDeConteudo, CancellationToken ct);

    /// <summary>
    /// Um link de leitura que vale <c>Armazenamento:LinkValidoMinutos</c> a partir do relógio do sistema e baixa o arquivo como ANEXO com <paramref name="nomeParaDownload"/> (passado por <see cref="NomeParaDownload.Limpar"/>:
    /// aspas e quebras de linha não chegam ao cabeçalho). O link nunca contém a chave da conta nem a connection string.
    /// </summary>
    Task<LinkTemporario> CriarLinkTemporarioAsync(string caminho, string nomeParaDownload, CancellationToken ct);

    /// <summary>Apaga o arquivo. <b>Idempotente</b>: apagar o que não existe não lança.</summary>
    Task ApagarAsync(string caminho, CancellationToken ct);
}

/// <summary>Só a limpeza de sobras usa: lista o que há sob um prefixo (<c>"documentos/"</c>).</summary>
public interface IListagemDeArquivos
{
    IAsyncEnumerable<ArquivoGuardado> ListarAsync(string prefixo, CancellationToken ct);
}

namespace RHSystem.Application.Comum.Exportacao;

/// <summary>Um arquivo que o caso de uso gerou para baixar: o conteúdo e o nome (o <c>filename</c> do <c>Content-Disposition</c>).</summary>
public sealed record ArquivoExportado(byte[] Conteudo, string NomeDoArquivo);

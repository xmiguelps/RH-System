namespace RHSystem.Application.Importacoes;

/// <summary><c>baixarModeloDeImportacao</c>: o arquivo é gerado na hora do pedido, com os cadastros de apoio de agora (contrato: "Valores válidos" traz o que existe hoje).</summary>
public sealed class ModeloDeImportacaoCasoDeUso(IConsultaDoCatalogoDaImportacao catalogo, IGeradorDoModelo gerador)
{
    public const string NomeDoArquivo = "modelo-importacao-colaboradores.xlsx";

    public const string TipoDeConteudo = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    public async Task<byte[]> GerarAsync(CancellationToken ct) => gerador.Gerar((await catalogo.ObterAsync(ct)).ValoresValidos());
}

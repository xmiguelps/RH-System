namespace RHSystem.Application.Documentos;

/// <summary>Os limites do envio de documento (<c>CONVENCOES.md</c>, seção 6; decisão 8 do plano da 1c: 10 MB são 10 MiB).</summary>
public static class LimitesDeDocumento
{
    /// <summary>O arquivo tem de ter no máximo isto: 10 MiB. Acima, 400 em <c>arquivo</c>.</summary>
    public const long TamanhoMaximoBytes = 10L * 1024 * 1024;

    /// <summary>
    /// O teto da requisição inteira (arquivo mais campos do formulário): 16 MiB. Acima disto o próprio servidor corta com 413, que o contrato não lista; entre 10 e 16 MiB a API chega a ler o arquivo
    /// e devolve o 400 em <c>arquivo</c> que o contrato manda.
    /// </summary>
    public const long TetoDaRequisicaoBytes = 16L * 1024 * 1024;

    /// <summary>A pasta dos documentos no armazenamento (e, com a barra, o prefixo da <c>IFonteDeReferenciasDeArquivo</c> deles).</summary>
    public const string PastaNoArmazenamento = "documentos";
}

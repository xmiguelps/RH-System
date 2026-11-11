namespace RHSystem.Application.Importacoes;

/// <summary>
/// Os limites da importação por planilha. Tamanho e linhas são do contrato (<c>25-fatia-2-importacao.yaml</c>; decisão 5 do plano da fatia 2: 5 MB são 5 MiB); os do zip o contrato deixa para a implementação
/// ("o servidor também limita o tamanho descompactado, o número de planilhas e o de células").
/// </summary>
public static class LimitesDeImportacao
{
    /// <summary>O arquivo tem de ter no máximo isto: 5 MiB. Acima, 422 <c>ARQUIVO_MUITO_GRANDE</c>.</summary>
    public const long TamanhoMaximoBytes = 5L * 1024 * 1024;

    /// <summary>O teto da requisição inteira (arquivo mais campos do formulário): 6 MiB. Entre 5 e 6 MiB a API chega a ler o arquivo e devolve o 422 do contrato; acima disto o servidor corta com 413.</summary>
    public const long TetoDaRequisicaoBytes = 6L * 1024 * 1024;

    /// <summary>Linhas de dados (sem o cabeçalho) que uma importação aceita.</summary>
    public const int LinhasMaximas = 500;

    /// <summary>Quanto tempo a prévia vale para a confirmação.</summary>
    public static readonly TimeSpan Validade = TimeSpan.FromHours(1);

    /// <summary>A pasta das importações no armazenamento (e, com a barra, o prefixo da <c>IFonteDeReferenciasDeArquivo</c> delas).</summary>
    public const string PastaNoArmazenamento = "importacoes";

    /// <summary>Soma do que as entradas do zip realmente descompactam (nunca o tamanho que o cabeçalho diz).</summary>
    public const long DescompactadoMaximoBytes = 50L * 1024 * 1024;

    public const int EntradasMaximas = 100;

    public const int PlanilhasMaximas = 10;

    public const int CelulasMaximas = 200_000;
}

namespace RHSystem.Domain.Importacoes;

/// <summary>Texto no banco: <c>COM_ERROS</c>, <c>PRONTA</c>, <c>CONFIRMADA</c>.</summary>
public enum SituacaoDaImportacao
{
    ComErros,
    Pronta,
    Confirmada,
}

/// <summary>
/// Uma linha de <c>IMPORTACOES</c>: a prévia de uma planilha de colaboradores (tudo ou nada, validade de 1 hora). A prévia tem dado pessoal:
/// o banco recusa <c>CONFIRMADA</c> com prévia. A confirmação e o resultado são da fatia 2; <see cref="Criar"/> aplica os <c>CHECK</c> da criação.
/// </summary>
public class Importacao
{
    private Importacao()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioRhId { get; private set; }

    public string NomeArquivo { get; private set; } = string.Empty;

    public SituacaoDaImportacao Situacao { get; private set; }

    public int TotalLinhas { get; private set; }

    public int LinhasValidas { get; private set; }

    public int LinhasComErro { get; private set; }

    public DateTime CriadaEm { get; private set; }

    public DateTime ExpiraEm { get; private set; }

    public string? CaminhoArquivo { get; private set; }

    /// <summary>JSON da prévia (com dado pessoal); some ao confirmar.</summary>
    public string? Previa { get; private set; }

    public DateTime? ConfirmadaEm { get; private set; }

    public int? TotalCriados { get; private set; }

    public int? ConvitesEnviados { get; private set; }

    public int? IncluidosNoOnboarding { get; private set; }

    public int? IncluidosNoFormularioAdmissao { get; private set; }

    public static Importacao Criar(
        int funcionarioRhId,
        string nomeArquivo,
        int totalLinhas,
        int linhasValidas,
        int linhasComErro,
        DateTime agoraUtc,
        TimeSpan validade,
        string? caminhoArquivo = null,
        string? previa = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nomeArquivo);

        // CHK_IMPORTACOES_TOTAL_LINHAS e _LINHAS_SOMAM
        if (totalLinhas is < 1 or > 500)
        {
            throw new ArgumentException("A planilha tem de 1 a 500 linhas.", nameof(totalLinhas));
        }

        if (linhasValidas < 0 || linhasComErro < 0 || linhasValidas + linhasComErro != totalLinhas)
        {
            throw new ArgumentException("As linhas válidas e as com erro somam o total de linhas.", nameof(linhasValidas));
        }

        // CHK_IMPORTACOES_VALIDADE
        if (validade <= TimeSpan.Zero)
        {
            throw new ArgumentException("A validade da prévia tem de ser maior que zero.", nameof(validade));
        }

        return new Importacao
        {
            FuncionarioRhId = funcionarioRhId,
            NomeArquivo = nomeArquivo,
            Situacao = linhasComErro > 0 ? SituacaoDaImportacao.ComErros : SituacaoDaImportacao.Pronta,
            TotalLinhas = totalLinhas,
            LinhasValidas = linhasValidas,
            LinhasComErro = linhasComErro,
            CriadaEm = agoraUtc,
            ExpiraEm = agoraUtc + validade,
            CaminhoArquivo = caminhoArquivo,
            Previa = previa,
        };
    }
}

using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Documentos;

/// <summary>
/// Um documento do colaborador (<c>FUNCIONARIOS_DOCUMENTOS</c>). Nasce de dois jeitos: <see cref="Anexar"/> (o RH envia o arquivo na hora: já <c>Enviado</c>) ou <see cref="Pedir"/> (o RH pede e a pessoa envia
/// depois: <c>Pendente</c>, sem arquivo, com a data do pedido). <see cref="ReceberArquivo"/> só vale para <c>Pendente</c> e preserva a data do pedido. O caminho do arquivo é segredo de armazenamento e NUNCA é auditado;
/// o nome do arquivo que a pessoa enviou aparece no log como valor não registrado (pode revelar conteúdo). Os instantes vão cortados ao segundo (a coluna não tem fração) e a data de cadastro é do banco.
/// </summary>
[Auditado(AuditoriaArea.Documentos, "Documento")]
public class DocumentoDoColaborador : IAuditavel
{
    private const int TamanhoMaximo = 255;

    private DocumentoDoColaborador()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public Funcionario Funcionario { get; private set; } = null!;

    [Auditar("Tipo")]
    public TipoDeDocumento Tipo { get; private set; }

    [Auditar("Descrição")]
    public string? Descricao { get; private set; }

    [Auditar("Validade")]
    public DateOnly? DataValidade { get; private set; }

    [Auditar("Situação")]
    public SituacaoDoDocumento Situacao { get; private set; }

    /// <summary>Onde o arquivo está no armazenamento. Nunca vai para a auditoria, o log nem a resposta da API.</summary>
    public string? CaminhoArquivo { get; private set; }

    [Auditar("Arquivo", Sensivel = true)]
    public string? NomeArquivoOriginal { get; private set; }

    public DateTime? PedidoEmUtc { get; private set; }

    public DateTime? EnviadoEmUtc { get; private set; }

    /// <summary>A data do cadastro da linha, que o banco preenche (<c>DEFAULT CURRENT_TIMESTAMP</c>); só vale depois de salvar.</summary>
    public DateTime CadastradoEm { get; private set; }

    /// <summary>O rótulo do tipo, o da tela (copiado do contrato, <c>DocumentoColaboradorTipo</c>).</summary>
    public string NomeParaAuditoria => Tipo switch
    {
        TipoDeDocumento.Identidade => "Documento de identidade (RG ou CNH)",
        TipoDeDocumento.Cpf => "CPF",
        TipoDeDocumento.CarteiraDeTrabalho => "Carteira de trabalho digital",
        TipoDeDocumento.ComprovanteDeResidencia => "Comprovante de residência",
        TipoDeDocumento.ContratoAssinado => "Contrato assinado",
        TipoDeDocumento.ExameAdmissional => "Exame admissional (ASO)",
        _ => "Outro",
    };

    public string RegistroAfetado => $"Documento {NomeParaAuditoria} de {Funcionario?.Nome ?? $"colaborador {FuncionarioId}"}";

    /// <summary>O RH envia o arquivo na hora: nasce <c>Enviado</c>, com o caminho, o nome original e a data do envio.</summary>
    public static DocumentoDoColaborador Anexar(Funcionario funcionario, TipoDeDocumento tipo, string? descricao, DateOnly? dataValidade, string caminho, string nomeOriginal, DateTime agoraUtc)
    {
        ArgumentNullException.ThrowIfNull(funcionario);
        var documento = new DocumentoDoColaborador
        {
            FuncionarioId = funcionario.Id,
            Funcionario = funcionario,
            Tipo = tipo,
            Descricao = CamposDeTexto.Opcional(descricao, nameof(descricao), TamanhoMaximo),
            DataValidade = dataValidade,
        };
        documento.GravarArquivo(caminho, nomeOriginal, agoraUtc);
        return documento;
    }

    /// <summary>O RH pede o documento: nasce <c>Pendente</c>, sem arquivo, com a data do pedido.</summary>
    public static DocumentoDoColaborador Pedir(Funcionario funcionario, TipoDeDocumento tipo, string? descricao, DateOnly? dataValidade, DateTime agoraUtc)
    {
        ArgumentNullException.ThrowIfNull(funcionario);
        return new DocumentoDoColaborador
        {
            FuncionarioId = funcionario.Id,
            Funcionario = funcionario,
            Tipo = tipo,
            Descricao = CamposDeTexto.Opcional(descricao, nameof(descricao), TamanhoMaximo),
            DataValidade = dataValidade,
            Situacao = SituacaoDoDocumento.Pendente,
            PedidoEmUtc = CamposDeTexto.AoSegundo(agoraUtc),
        };
    }

    /// <summary>A pessoa enviou o arquivo do pedido. Só um documento <c>Pendente</c> o recebe; a data do pedido fica.</summary>
    /// <exception cref="InvalidOperationException">O documento não está <c>Pendente</c>.</exception>
    public void ReceberArquivo(string caminho, string nomeOriginal, DateTime agoraUtc)
    {
        if (Situacao != SituacaoDoDocumento.Pendente)
        {
            throw new InvalidOperationException("Só um documento pendente recebe o arquivo.");
        }

        GravarArquivo(caminho, nomeOriginal, agoraUtc);
    }

    private void GravarArquivo(string caminho, string nomeOriginal, DateTime agoraUtc)
    {
        var caminhoValido = CamposDeTexto.Obrigatorio(caminho, nameof(caminho), TamanhoMaximo);
        var nomeValido = CamposDeTexto.Obrigatorio(nomeOriginal, nameof(nomeOriginal), TamanhoMaximo);
        CaminhoArquivo = caminhoValido;
        NomeArquivoOriginal = nomeValido;
        EnviadoEmUtc = CamposDeTexto.AoSegundo(agoraUtc);
        Situacao = SituacaoDoDocumento.Enviado;
    }
}

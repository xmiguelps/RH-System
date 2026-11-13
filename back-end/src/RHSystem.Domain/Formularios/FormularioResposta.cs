namespace RHSystem.Domain.Formularios;

/// <summary>
/// Uma linha de resposta (<c>FORMULARIOS_RESPOSTAS</c>), de UMA pergunta. Há três donos, e cada fábrica monta um só (os <c>CHECK</c>s do banco recusam o resto): o funcionário, o candidato de fora e <b>ninguém</b> (a resposta anônima, sem autor, sem arquivo e com a data
/// grossa: o dia da publicação da rodada). <c>Anonimo</c> sempre igual ao do formulário (a chave estrangeira composta o confere; o padrão do banco é <c>FALSE</c> e nunca pode ser omitido). Esta entidade <b>não é auditada</b> pelo interceptor: ele gravaria autor e hora de todo
/// INSERT e desfaria o anonimato. Pergunta opcional em branco não gera linha. <c>Resposta</c> é sempre preenchida: o texto, o texto da alternativa, o número da escala ou o nome do arquivo (o trigger recusa obrigatória vazia).
/// </summary>
public class FormularioResposta
{
    private FormularioResposta()
    {
    }

    public int Id { get; private set; }

    public int PerguntaId { get; private set; }

    public int FormularioId { get; private set; }

    public int? FuncionarioId { get; private set; }

    public string? Resposta { get; private set; }

    /// <summary>Com nome: o mesmo instante em todas as linhas do envio. Anônima: o dia da publicação da rodada (00:00 -03:00).</summary>
    public DateTime? RespondidaEmUtc { get; private set; }

    /// <summary>Quem foi avaliado, nas avaliações (inclusive na anônima).</summary>
    public int? AvaliadoId { get; private set; }

    public int? CandidatoId { get; private set; }

    public int? AlternativaId { get; private set; }

    public bool Anonimo { get; private set; }

    public string? CaminhoArquivo { get; private set; }

    public string? NomeArquivoOriginal { get; private set; }

    public static FormularioResposta DeFuncionario(int formularioId, int perguntaId, int funcionarioId, string resposta, DateTime respondidaEmUtc, int? avaliadoId, int? alternativaId, string? caminhoArquivo, string? nomeArquivoOriginal) =>
        new()
        {
            FormularioId = formularioId,
            PerguntaId = perguntaId,
            FuncionarioId = funcionarioId,
            Resposta = resposta,
            RespondidaEmUtc = CamposDeTexto.AoSegundo(respondidaEmUtc),
            AvaliadoId = avaliadoId,
            AlternativaId = alternativaId,
            Anonimo = false,
            CaminhoArquivo = caminhoArquivo,
            NomeArquivoOriginal = nomeArquivoOriginal,
        };

    public static FormularioResposta DeCandidato(int formularioId, int perguntaId, int candidatoId, string resposta, DateTime respondidaEmUtc, int? alternativaId, string? caminhoArquivo, string? nomeArquivoOriginal) =>
        new()
        {
            FormularioId = formularioId,
            PerguntaId = perguntaId,
            CandidatoId = candidatoId,
            Resposta = resposta,
            RespondidaEmUtc = CamposDeTexto.AoSegundo(respondidaEmUtc),
            AlternativaId = alternativaId,
            Anonimo = false,
            CaminhoArquivo = caminhoArquivo,
            NomeArquivoOriginal = nomeArquivoOriginal,
        };

    /// <summary>Sem funcionário, sem candidato, sem arquivo: só o que foi respondido e (nas avaliações) quem foi avaliado.</summary>
    public static FormularioResposta Anonima(int formularioId, int perguntaId, string resposta, DateTime diaDaRodadaEmUtc, int? avaliadoId, int? alternativaId) =>
        new()
        {
            FormularioId = formularioId,
            PerguntaId = perguntaId,
            Resposta = resposta,
            RespondidaEmUtc = CamposDeTexto.AoSegundo(diaDaRodadaEmUtc),
            AvaliadoId = avaliadoId,
            AlternativaId = alternativaId,
            Anonimo = true,
        };
}

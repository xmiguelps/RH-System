namespace RHSystem.Application.Formularios.Responder;

/// <summary>Uma linha de resposta a gravar: a resposta SEMPRE preenchida (o texto, o texto da alternativa, o número da escala ou o nome do arquivo), a alternativa escolhida e, na pergunta de arquivo, o caminho guardado e o nome que a pessoa deu.</summary>
public sealed record LinhaParaGravar(int PerguntaId, string Resposta, int? AlternativaId, string? CaminhoArquivo, string? NomeArquivoOriginal);

/// <summary>Uma linha de resposta como o banco a guarda, só o que o "ver minhas respostas" mostra (o caminho do arquivo nunca sai do repositório).</summary>
public sealed record RespostaLida(int Id, int PerguntaId, string? Resposta, int? AlternativaId, string? NomeArquivoOriginal, DateTime? RespondidaEmUtc);

/// <summary>
/// A gravação e a leitura das respostas do funcionário. A receita (decisão 11): o chamador grava os arquivos, abre UMA transação curta e nela segura a linha do formulário (<see cref="SegurarAbertoAsync"/>), confere se a pessoa já respondeu e grava todas as linhas de uma vez, com o mesmo instante.
/// </summary>
public interface IFormulariosRespostasRepositorio
{
    /// <summary>
    /// Segura a linha do formulário até o fim da transação (um <c>UPDATE</c> sem mudança) <b>se ele continua publicado</b> (<c>ATIVO</c> e sem <c>ENCERRADO_EM</c>) e com o mesmo anonimato: dois envios da mesma pessoa esperam um pelo outro e o encerrar e o publicar também. <c>false</c> = o
    /// formulário mudou depois da leitura (foi encerrado em outra aba): o chamador relê e dá o erro certo.
    /// </summary>
    Task<bool> SegurarAbertoAsync(int formularioId, bool anonimo, CancellationToken ct);

    /// <summary>A pessoa já tem resposta neste formulário (para este avaliado, nas avaliações; <paramref name="avaliadoId"/> nulo nos outros).</summary>
    Task<bool> JaRespondeuAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct);

    /// <summary>No formulário ANÔNIMO: a pessoa já tem participação registrada (para este avaliado, nas avaliações). A chave única da participação é a segunda barreira (409 também numa corrida).</summary>
    Task<bool> JaParticipouAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct);

    /// <summary>O instante do envio mais recente da pessoa neste formulário (na Sugestão, que aceita vários envios), ou <c>null</c>.</summary>
    Task<DateTime?> UltimoEnvioAsync(int formularioId, int funcionarioId, CancellationToken ct);

    /// <summary>Grava todas as linhas do envio, com o MESMO <paramref name="instanteUtc"/> (cortado ao segundo). Sem evento de auditoria: responder com nome não audita nada.</summary>
    Task GravarDeFuncionarioAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime instanteUtc, IReadOnlyList<LinhaParaGravar> linhas, CancellationToken ct);

    /// <summary>
    /// Grava a resposta ANÔNIMA: as linhas SEM funcionário, sem candidato e sem arquivo, todas com a data grossa <paramref name="diaDaRodadaUtc"/> (o dia da publicação da rodada, 00:00 -03:00), e, à parte, a participação (a pessoa e o avaliado, sem data e sem ligação com as linhas) com um UUID v4 da
    /// aplicação, que é devolvido. Sem evento de auditoria. Participação repetida = 409 <c>JA_RESPONDIDO</c> pela chave única.
    /// </summary>
    Task<Guid> GravarAnonimaAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime diaDaRodadaUtc, IReadOnlyList<LinhaParaGravar> linhas, CancellationToken ct);

    /// <summary>As linhas que a pessoa gravou neste formulário (para este avaliado, nas avaliações), em ordem de pergunta.</summary>
    Task<IReadOnlyList<RespostaLida>> DaPessoaAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct);
}

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

    /// <summary>O instante do envio mais recente da pessoa neste formulário (na Sugestão, que aceita vários envios), ou <c>null</c>.</summary>
    Task<DateTime?> UltimoEnvioAsync(int formularioId, int funcionarioId, CancellationToken ct);

    /// <summary>Grava todas as linhas do envio, com o MESMO <paramref name="instanteUtc"/> (cortado ao segundo). Sem evento de auditoria: responder com nome não audita nada.</summary>
    Task GravarDeFuncionarioAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime instanteUtc, IReadOnlyList<LinhaParaGravar> linhas, CancellationToken ct);

    /// <summary>As linhas que a pessoa gravou neste formulário (para este avaliado, nas avaliações), em ordem de pergunta.</summary>
    Task<IReadOnlyList<RespostaLida>> DaPessoaAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct);
}

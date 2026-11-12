using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Destinatarios;

/// <summary>
/// A descrição do público de um formulário, antes de gravar ou publicar: o alvo, as listas escolhidas e o que o formulário é (tipo, sentido da avaliação, anônimo). É a entrada de <see cref="IDestinatariosDoFormulario.CalcularAsync"/>: o
/// "publicar" a usa com o público do CORPO, antes de gravar, e o resto com o público já guardado.
/// </summary>
public sealed record PublicoDoFormulario(AlvoDoFormulario? Alvo, TipoDeFormulario Tipo, SentidoDaAvaliacao? Sentido, IReadOnlyList<int> SetorIds, IReadOnlyList<int> FuncionarioIds);

/// <summary>
/// A porta única de "quem deve responder" (contrato, <c>publicarFormulario</c>, <c>listarRespondentesFormulario</c>, os lembretes, a lista do funcionário). Quem entra no setor depois também responde; quem está em admissão (admissão ainda não começada) fica fora de
/// <c>SETORES</c> e dentro de <c>PESSOAS</c> (o formulário de admissão); pessoa inativa nunca responde. Os pares das avaliações seguem <see cref="ParesDeAvaliacao"/>.
/// </summary>
public interface IDestinatariosDoFormulario
{
    /// <summary>Calcula quem responderia a um formulário com este público, <b>na hora</b> (o dia de hoje, em -03:00, decide quem já tem admissão começada).</summary>
    Task<QuemDeveResponder> CalcularAsync(PublicoDoFormulario publico, DateOnly hoje, CancellationToken ct);

    /// <summary>
    /// Quem deve responder o formulário já gravado. Formulário com nome: calculado na hora a partir do público guardado. Anônimo já publicado: a fotografia guardada (os pares, pessoa por pessoa, tirados no dia da publicação, que não mudam). <c>null</c> se o formulário não existe.
    /// </summary>
    Task<QuemDeveResponder?> ObterAsync(int formularioId, DateOnly hoje, CancellationToken ct);

    /// <summary>
    /// Tira a fotografia do anônimo: apaga as linhas de destinatário por pessoa e grava uma linha por par (pessoa e avaliado), mantendo as linhas de setor (para a publicação continuar mostrando os setores escolhidos). Roda na transação de quem publica. Devolve o que
    /// guardou.
    /// </summary>
    Task<QuemDeveResponder> FotografarAsync(int formularioId, DateOnly hoje, CancellationToken ct);
}

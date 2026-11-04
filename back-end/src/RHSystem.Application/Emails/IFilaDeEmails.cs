namespace RHSystem.Application.Emails;

/// <summary>
/// Uma mensagem a enfileirar. <c>Sigiloso</c> (lembrete de formulário anônimo) exige <c>FuncionarioId</c> nulo; <c>Tipo</c> tem de 3 a 40 caracteres
/// (<c>FILA_EMAILS.TIPO</c>); os dois erros são <see cref="ArgumentException"/> e aparecem no teste, não no <c>CHECK</c> do banco. <c>Carga</c> vai em JSON camelCase.
/// </summary>
public sealed record EmailParaEnfileirar(
    string Tipo,
    string ChaveDeIdempotencia,
    string DestinatarioEmail,
    object? Carga = null,
    int? FuncionarioId = null,
    int? ReferenciaId = null,
    bool Sigiloso = false,
    DateTime? EnviarApos = null);

/// <summary>
/// A fila de e-mails (<c>FILA_EMAILS</c>). Enfileirar grava com o contexto da requisição, na MESMA transação do fato quando o chamador está em
/// <c>ExecutarEmTransacaoAsync</c>: se a transação desfaz, a mensagem some junto.
/// </summary>
public interface IFilaDeEmails
{
    /// <summary><c>false</c> = a chave já existe (na fila ou já enviada); vale como sucesso e nunca lança por duplicidade.</summary>
    Task<bool> EnfileirarAsync(EmailParaEnfileirar email, CancellationToken ct);

    /// <summary>Quantos entraram (os já existentes não contam). O lote com mensagem sigilosa é embaralhado antes de gravar, para a ordem não ligar a pessoa à linha.</summary>
    Task<int> EnfileirarLoteAsync(IReadOnlyList<EmailParaEnfileirar> emails, CancellationToken ct);

    /// <summary>
    /// As <c>PENDENTE</c> do tipo e da referência: as não sigilosas viram <c>CANCELADO</c> com a carga apagada; as SIGILOSAS são APAGADAS (o <c>CHECK</c> do
    /// <c>V001</c> proíbe sigilosa em situação final). Devolve quantas, das duas.
    /// </summary>
    Task<int> CancelarPendentesAsync(string tipo, int referenciaId, CancellationToken ct);
}

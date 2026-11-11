namespace RHSystem.Domain.Documentos;

/// <summary>A situação do documento. Texto no banco (com <c>CHECK</c>): <c>PENDENTE</c> (o RH pediu e a pessoa ainda não enviou) e <c>ENVIADO</c> (há arquivo e data de envio).</summary>
public enum SituacaoDoDocumento
{
    Pendente,
    Enviado,
}

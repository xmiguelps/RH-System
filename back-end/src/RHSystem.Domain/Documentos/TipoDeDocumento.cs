namespace RHSystem.Domain.Documentos;

/// <summary>O tipo do documento do colaborador. Texto no banco (livre, sem <c>CHECK</c>): <c>IDENTIDADE</c>, <c>CPF</c>, <c>CARTEIRA_DE_TRABALHO</c>, <c>COMPROVANTE_DE_RESIDENCIA</c>, <c>CONTRATO_ASSINADO</c>, <c>EXAME_ADMISSIONAL</c>, <c>OUTRO</c>. A lista é provisória no contrato.</summary>
public enum TipoDeDocumento
{
    Identidade,
    Cpf,
    CarteiraDeTrabalho,
    ComprovanteDeResidencia,
    ContratoAssinado,
    ExameAdmissional,
    Outro,
}

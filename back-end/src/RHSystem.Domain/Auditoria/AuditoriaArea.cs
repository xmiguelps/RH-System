namespace RHSystem.Domain.Auditoria;

/// <summary>A área do sistema de um evento de auditoria. No banco e no contrato, em maiúsculas (<c>ACESSO</c>, <c>COLABORADORES</c>...).</summary>
public enum AuditoriaArea
{
    Acesso,
    Colaboradores,
    Documentos,
    Cadastros,
    Importacao,
    Ponto,
    Feriados,
    Ferias,
    Holerites,
    Advertencias,
    Avisos,
    Formularios,
    Treinamentos,
    Beneficios,
    Atestados,
}

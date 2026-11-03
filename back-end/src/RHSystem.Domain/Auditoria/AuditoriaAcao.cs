namespace RHSystem.Domain.Auditoria;

/// <summary>O que foi feito num evento de auditoria. No banco e no contrato, em maiúsculas com sublinhado (<c>ACESSO_NEGADO</c>, <c>PEDIU_AJUSTE</c>...).</summary>
public enum AuditoriaAcao
{
    Entrou,
    AcessoNegado,
    Criou,
    Alterou,
    Excluiu,
    Inativou,
    Registrou,
    PediuAjuste,
    Aprovou,
    Recusou,
    Publicou,
    EnviouEmail,
    Importou,
    Baixou,
}

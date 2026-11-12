namespace RHSystem.Domain.Ponto;

/// <summary>Texto no banco: <c>ENTRADA</c> e <c>SAIDA</c>.</summary>
public enum TipoDeMarcacao
{
    Entrada,
    Saida,
}

/// <summary>Texto no banco: <c>SISTEMA</c> (a pessoa bateu) e <c>CORRIGIDA_RH</c> (o RH corrigiu ou incluiu, direto ou pela aprovação de um pedido).</summary>
public enum OrigemDaMarcacao
{
    Sistema,
    CorrigidaRh,
}

/// <summary>Texto no banco: <c>PENDENTE</c>, <c>APROVADA</c>, <c>REJEITADA</c>. Só muda na decisão FINAL (a etapa do gestor fica em <see cref="ResultadoDaEtapaDoGestor"/>).</summary>
public enum StatusDoPedidoDeAjuste
{
    Pendente,
    Aprovada,
    Rejeitada,
}

/// <summary>Texto no banco: <c>APROVOU</c> e <c>RECUSOU</c> (o que o gestor decidiu na primeira etapa).</summary>
public enum ResultadoDaEtapaDoGestor
{
    Aprovou,
    Recusou,
}

/// <summary>Texto no banco: <c>CREDITO</c> e <c>DEBITO</c>. O banco de horas só tem lançamento automático.</summary>
public enum TipoDeLancamentoDoBanco
{
    Credito,
    Debito,
}

/// <summary>Texto no banco: <c>NACIONAL</c>, <c>ESTADUAL</c>, <c>MUNICIPAL</c> e <c>EMPRESA</c>.</summary>
public enum TipoDeFeriado
{
    Nacional,
    Estadual,
    Municipal,
    Empresa,
}

/// <summary>Texto no banco: <c>ENVIADO</c>, <c>VALIDADO</c> e <c>RECUSADO</c>.</summary>
public enum SituacaoDoAtestado
{
    Enviado,
    Validado,
    Recusado,
}

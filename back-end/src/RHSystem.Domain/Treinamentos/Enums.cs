namespace RHSystem.Domain.Treinamentos;

/// <summary>Para quem o treinamento vale (<c>TREINAMENTOS.PUBLICO</c>); <c>NULL</c> no banco até o RH escolher. <c>Todos</c> não guarda linhas de público; <c>Setores</c> e <c>Pessoas</c> guardam uma por destino.</summary>
public enum PublicoDoTreinamento
{
    Todos,
    Setores,
    Pessoas,
}

/// <summary>O tipo do módulo (<c>TREINAMENTOS_MODULOS.TIPO_MODULO</c>). Só o <c>Pdf</c> tem arquivo; o vídeo é sempre link externo (o banco aceitaria arquivo, o contrato não).</summary>
public enum TipoDeModulo
{
    Video,
    Texto,
    Link,
    Pdf,
}

/// <summary>
/// A situação da pessoa no treinamento (<c>TREINAMENTOS_FUNCIONARIOS.STATUS_TREINAMENTO</c>). O banco ainda aceita <c>REPROVADO</c> e <c>CANCELADO</c> (nota e quiz foram cortados): a aplicação nunca os grava e as consultas só olham estes três.
/// </summary>
public enum SituacaoNoTreinamento
{
    Inscrito,
    EmAndamento,
    Concluido,
}

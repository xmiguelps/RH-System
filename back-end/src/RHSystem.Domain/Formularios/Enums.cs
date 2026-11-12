namespace RHSystem.Domain.Formularios;

/// <summary>O tipo do formulário (<c>FORMULARIOS.TIPO_FORMULARIO</c>, <c>CHK_FORMULARIOS_TIPO</c>). <c>OUTRO</c> é a Sugestão: caixa sempre aberta que aceita várias respostas da mesma pessoa.</summary>
public enum TipoDeFormulario
{
    Entrevista,
    Admissao,
    Avaliacao,
    Pesquisa,
    Outro,
}

/// <summary>Quem avalia quem numa avaliação (<c>FORMULARIOS.SENTIDO_AVALIACAO</c>): o gestor avalia cada pessoa da equipe, ou a equipe avalia o gestor.</summary>
public enum SentidoDaAvaliacao
{
    GestorAvaliaEquipe,
    EquipeAvaliaGestor,
}

/// <summary>Para quem o formulário vale (<c>FORMULARIOS.PUBLICO</c>): candidatos por link público, setores ou pessoas escolhidas.</summary>
public enum AlvoDoFormulario
{
    Link,
    Setores,
    Pessoas,
}

/// <summary>A unidade do intervalo de reabertura do formulário recorrente (<c>FORMULARIOS.RECORRENCIA_UNIDADE</c>).</summary>
public enum UnidadeDeRecorrencia
{
    Dia,
    Semana,
    Mes,
}

/// <summary>O tipo da pergunta (<c>FORMULARIOS_PERGUNTAS.TIPO_RESPOSTA</c>, texto livre no banco).</summary>
public enum TipoDePergunta
{
    RespostaCurta,
    RespostaLonga,
    MultiplaEscolha,
    Escala,
    Arquivo,
}

/// <summary>
/// A situação do formulário, <b>calculada</b> das colunas e do relógio (nunca gravada): rascunho (inativo, sem datas), agendado (inativo com a data de publicação), publicado (ativo e dentro do prazo) e encerrado (a data de encerramento, ou o prazo já vencido
/// mesmo que a tarefa de encerrar ainda não tenha rodado).
/// </summary>
public enum SituacaoDoFormulario
{
    Rascunho,
    Agendado,
    Publicado,
    Encerrado,
}

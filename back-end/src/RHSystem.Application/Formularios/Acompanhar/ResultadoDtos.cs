using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>Uma alternativa da múltipla escolha no resultado: SEMPRE todas as alternativas, com o total e o percentual sobre as respostas da pergunta (uma casa decimal).</summary>
public sealed record FormularioResultadoAlternativa(int AlternativaId, string Texto, int Total, decimal Percentual);

/// <summary>Quantas respostas tiveram este valor da escala (1 a 5).</summary>
public sealed record FormularioResultadoEscalaValor(int Valor, int Total);

/// <summary>A escala: a média (uma casa decimal) e SEMPRE os cinco valores, na ordem.</summary>
public sealed record FormularioResultadoEscala(decimal Media, IReadOnlyList<FormularioResultadoEscalaValor> Distribuicao);

/// <summary>
/// O resultado de uma pergunta. Só o campo do tipo traz dados: <c>Alternativas</c> (múltipla escolha), <c>Escala</c> (nulo se ninguém respondeu), <c>Textos</c> (curta e longa, em ordem alfabética, NUNCA na ordem em que foram gravados) ou nada além de <c>TotalRespostas</c> (arquivo). Não há id nem data de resposta individual.
/// </summary>
public sealed record FormularioResultadoPergunta(int PerguntaId, int Ordem, string Texto, TipoDePergunta Tipo, int TotalRespostas, IReadOnlyList<FormularioResultadoAlternativa> Alternativas, FormularioResultadoEscala? Escala, IReadOnlyList<string> Textos);

/// <summary>
/// O resultado geral do formulário (<c>FormularioResultado</c>). <c>TotalRespostas</c> são as participações (anônimo) ou os envios de pessoas e candidatos (com nome); nas avaliações, só os do <c>Avaliado</c>. No anônimo só existe depois de encerrado, com o mínimo de 3 respostas POR PERGUNTA
/// (<c>PerguntasOcultas</c> diz quantas ficaram de fora); com nome não há mínimo (<c>MinimoPorPergunta</c> nulo e <c>PerguntasOcultas</c> 0).
/// </summary>
public sealed record FormularioResultado(int FormularioId, string Titulo, bool Anonimo, SituacaoDoFormulario Situacao, int TotalRespostas, int? MinimoPorPergunta, int PerguntasOcultas, FuncionarioResumo? Avaliado, IReadOnlyList<FormularioResultadoPergunta> Perguntas);

/// <summary>Uma resposta como o resultado a lê: só o que conta. Nunca o autor, a data nem o id.</summary>
public sealed record RespostaParaResultado(int PerguntaId, string? Resposta, int? AlternativaId, int? AvaliadoId);

/// <summary>Uma linha do CSV de um formulário com nome ou por link: uma resposta, com quem respondeu (funcionário ou candidato), o avaliado e a pergunta.</summary>
public sealed record LinhaDeExportacaoDeResposta(string Respondente, string? Email, string? Setor, string? Avaliado, int PerguntaOrdem, string PerguntaTexto, string? Resposta, DateTime? RespondidoEmUtc);

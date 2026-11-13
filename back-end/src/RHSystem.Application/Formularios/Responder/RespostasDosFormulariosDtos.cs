using RHSystem.Application.Colaboradores;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// O envio de <c>responderFormulario</c> como chegou, em texto cru (<c>multipart/form-data</c>): o caso de uso valida tudo e devolve os erros juntos num 400 só. <c>Respostas</c> é o texto JSON da lista de <c>RespostaFormularioItem</c>; <c>Arquivos</c> são as partes de arquivo
/// (<c>arquivos</c>), na ordem de <c>arquivoIndice</c>; <c>ArquivosRecebidos</c> conta as partes de arquivo de QUALQUER nome de campo.
/// </summary>
public sealed record NovaRespostaDeFormulario(string? AvaliadoId, string? Respostas, IReadOnlyList<ArquivoRecebido> Arquivos, int ArquivosRecebidos);

/// <summary>O recibo: <c>ParticipacaoId</c> só no formulário anônimo (UUID v4 da aplicação) e <c>Restantes</c> são as avaliações que ainda faltam à pessoa (0 fora das avaliações).</summary>
public sealed record RespostaFormularioRecibo(int FormularioId, string? ParticipacaoId, int Restantes);

/// <summary>O arquivo que a pessoa enviou numa resposta: só o id da linha e o nome que ela deu (o caminho nunca sai da API).</summary>
public sealed record RespostaFormularioArquivo(int RespostaId, string Nome);

/// <summary>
/// A resposta a UMA pergunta, como as telas de "ver respostas" a mostram: só o campo do tipo da pergunta vem preenchido (<c>texto</c>, <c>alternativas</c> com a escolhida, <c>valorEscala</c> ou <c>arquivo</c>); pergunta opcional deixada em branco vem com tudo vazio.
/// </summary>
public sealed record RespostaFormularioDada(int PerguntaId, int Ordem, string PerguntaTexto, TipoDePergunta Tipo, string? Texto, IReadOnlyList<PerguntaAlternativa> Alternativas, int? ValorEscala, RespostaFormularioArquivo? Arquivo);

/// <summary>O que a pessoa logada respondeu (formulário com nome): <c>Avaliado</c> só nas avaliações e <c>RespondidoEmUtc</c> é o instante do envio.</summary>
public sealed record RespostaFormularioMinhas(int FormularioId, string Titulo, FuncionarioResumo? Avaliado, DateTime RespondidoEmUtc, IReadOnlyList<RespostaFormularioDada> Respostas);

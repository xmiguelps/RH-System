using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>Uma alternativa como o banco a guarda.</summary>
public sealed record AlternativaLida(int Id, int Ordem, string Texto);

/// <summary>Uma pergunta como o banco a guarda, com as alternativas em ordem.</summary>
public sealed record PerguntaLida(int Id, int Ordem, string Texto, TipoDePergunta Tipo, bool Obrigatoria, IReadOnlyList<AlternativaLida> Alternativas);

/// <summary>
/// O formulário como o banco o guarda, para o RH: as colunas, o autor, as perguntas e a publicação já escolhida (setores e pessoas das linhas de destinatário). <c>TemRespostas</c> conta resposta de pessoa, de candidato e anônima (o RH nunca recebe o
/// número: o mapeador o esconde no anônimo). A situação NÃO vem pronta: quem lê a calcula com <c>RegraDaSituacaoDoFormulario</c> e o <c>agora</c> da aplicação (o banco não fecha no prazo).
/// </summary>
public sealed record FormularioLido(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? Sentido,
    bool Anonimo,
    bool Ativo,
    DateTime CriadoEmUtc,
    FuncionarioResumo? Autor,
    AlvoDoFormulario? Alvo,
    DateTime? PublicadoEmUtc,
    DateTime? EncerradoEmUtc,
    DateOnly? Prazo,
    int? RecorrenciaIntervalo,
    UnidadeDeRecorrencia? RecorrenciaUnidade,
    DateOnly? RecorrenciaAte,
    DateTime? ProximaReaberturaEmUtc,
    bool LembreteAutomatico,
    string? TokenDoLink,
    bool TemRespostas,
    IReadOnlyList<PerguntaLida> Perguntas,
    IReadOnlyList<FormularioSetor> Setores,
    IReadOnlyList<FuncionarioResumo> Pessoas);

/// <summary>A leitura dos formulários para o RH.</summary>
public interface IConsultaDeFormularios
{
    /// <summary><c>null</c> se o formulário não existe.</summary>
    Task<FormularioLido?> ObterAsync(int formularioId, CancellationToken ct);
}

/// <summary>Os campos que a edição dos detalhes grava. <c>LimparPrazoERecorrencia</c> vale ao virar Sugestão (sempre aberta); <c>ExigirSemResposta</c>, ao mudar tipo ou sentido de um formulário com nome publicado.</summary>
public sealed record DetalhesParaGravar(string Titulo, string? Descricao, TipoDeFormulario Tipo, SentidoDaAvaliacao? Sentido, bool Anonimo, bool LimparPrazoERecorrencia, bool ExigirSemResposta);

/// <summary>
/// A gravação dos formulários. Toda mudança de estado é um <c>UPDATE</c> condicional ao estado lido (o banco decide a corrida): devolve <c>false</c> quando outra decisão chegou antes e o chamador relê para saber qual erro dar. Quem chama registra o evento de
/// auditoria na mesma transação (as entidades dos formulários não são auditadas pelo interceptor: a resposta nunca pode deixar rastro, e o resto tem eventos à mão).
/// </summary>
public interface IFormulariosRepositorio
{
    /// <summary>Grava o formulário (rascunho: <c>ATIVO</c> falso explícito) e devolve o id.</summary>
    Task<int> AdicionarAsync(Formulario formulario, CancellationToken ct);

    /// <summary>
    /// Muda os detalhes se o formulário ainda está como foi lido (<c>ATIVO</c>, anonimato e encerramento iguais) e, com <c>ExigirSemResposta</c>, se continua sem nenhuma resposta: <c>false</c> se outra aba publicou, encerrou ou recebeu resposta nesse meio tempo.
    /// </summary>
    Task<bool> AtualizarDetalhesAsync(FormularioLido lido, DetalhesParaGravar novos, CancellationToken ct);
}

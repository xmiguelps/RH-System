using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>A aba do cartão (<c>FormularioMinhaSituacao</c>): para responder (admissão e avaliação), disponível (pesquisa e Sugestão) ou respondido.</summary>
public enum SituacaoMinhaDoFormulario
{
    ParaResponder,
    Disponivel,
    Respondido,
}

/// <summary>
/// O cartão de "meus formulários" (<c>FormularioMeuItem</c>). <c>Respondidos</c> de <c>Total</c> é a barra: <c>Total</c> é 1, exceto na avaliação da equipe, em que é o número de pessoas que o gestor deve avaliar. <c>RespondidoEmUtc</c> é nulo no anônimo e na Sugestão;
/// <c>PodeVerMinhasRespostas</c> só vale com nome, fora da Sugestão e depois de ter respondido.
/// </summary>
public sealed record FormularioMeuItem(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? SentidoAvaliacao,
    bool Anonimo,
    SituacaoMinhaDoFormulario Situacao,
    int TotalPerguntas,
    int MinutosEstimados,
    DateOnly? Prazo,
    int Respondidos,
    int Total,
    DateTime? RespondidoEmUtc,
    bool PodeVerMinhasRespostas);

/// <summary>Os números das abas (<c>todos = paraResponder + disponiveis</c>): respeitam a busca e o tipo e IGNORAM a situação.</summary>
public sealed record FormularioMeusContagens(int Todos, int ParaResponder, int Disponiveis, int Respondidos);

/// <summary>A página de cartões e as contagens das abas (<c>FormularioMeusLista</c>).</summary>
public sealed record FormularioMeusLista(IReadOnlyList<FormularioMeuItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, FormularioMeusContagens Contagens)
    : RespostaPaginada<FormularioMeuItem>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

/// <summary>Uma pessoa que a pessoa logada deve avaliar e se já avaliou (<c>avaliados[]</c> de <c>FormularioParaResponder</c>).</summary>
public sealed record AvaliadoParaResponder(FuncionarioResumo Funcionario, bool Respondido);

/// <summary>O formulário aberto para responder (<c>FormularioParaResponder</c>): as perguntas em ordem e, nas avaliações, quem avaliar. Nada do que é do RH (público, código do link, contagem de respostas).</summary>
public sealed record FormularioParaResponder(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? SentidoAvaliacao,
    bool Anonimo,
    DateOnly? Prazo,
    int MinutosEstimados,
    IReadOnlyList<PerguntaFormulario> Perguntas,
    IReadOnlyList<AvaliadoParaResponder> Avaliados);

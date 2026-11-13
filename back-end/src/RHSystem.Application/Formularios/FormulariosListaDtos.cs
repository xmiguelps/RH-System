using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios;

/// <summary>
/// Uma linha da lista do RH (<c>FormularioRhItem</c>). <c>Respondidos</c>, <c>Esperados</c> e <c>TaxaDeRespostaPercentual</c> dependem do alvo e do anonimato: por link e na Sugestão <c>Esperados</c> e a taxa são <c>null</c> (<c>Respondidos</c> conta candidatos ou envios); o anônimo que ainda não
/// foi encerrado só mostra <c>Esperados</c>. Nas avaliações a unidade é a avaliação (pessoa avaliando pessoa).
/// </summary>
public sealed record FormularioRhItem(
    int Id,
    string Titulo,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? SentidoAvaliacao,
    bool Anonimo,
    SituacaoDoFormulario Situacao,
    AlvoDoFormulario? Alvo,
    string QuemRespondeResumo,
    int? Respondidos,
    int? Esperados,
    int? TaxaDeRespostaPercentual,
    DateOnly? Prazo,
    DateTime? EncerradoEmUtc,
    DateTime CriadoEmUtc);

/// <summary>Os números das abas da lista (<c>FormularioContagemPorTipo</c>): "avaliações" são os dois sentidos juntos e "sugestões" é o tipo <c>OUTRO</c>.</summary>
public sealed record FormularioContagemPorTipo(int Todos, int Entrevistas, int Admissoes, int Avaliacoes, int Pesquisas, int Sugestoes);

/// <summary>A página da lista do RH (<c>FormularioRhLista</c>): a paginação, as linhas e a contagem por tipo (que respeita a busca e a situação e IGNORA o tipo).</summary>
public sealed record FormularioRhLista(IReadOnlyList<FormularioRhItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, FormularioContagemPorTipo ContagemPorTipo)
    : RespostaPaginada<FormularioRhItem>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

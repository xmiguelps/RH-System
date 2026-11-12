using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Formularios.Responder;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>A aba da tela "Quem respondeu" (<c>FormularioRespondenteSituacao</c>): Respondeu ou Não respondeu.</summary>
public enum SituacaoDoRespondente
{
    Respondido,
    NaoRespondeu,
}

/// <summary>
/// Os três cartões da tela "Quem respondeu" (<c>FormularioParticipacaoResumo</c>); não mudam com os filtros. No formulário anônimo que ainda não foi encerrado só <c>Esperados</c> vem preenchido; na Sugestão só <c>Respondidos</c> (os envios); sem público ainda ou sem
/// pessoas esperadas nada de "faltam" nem taxa.
/// </summary>
public sealed record FormularioParticipacaoResumo(int? Respondidos, int? Faltam, int? Esperados, int? TaxaDeRespostaPercentual);

/// <summary>Uma linha da tela: quem respondeu (ou deveria), o avaliado (só nas avaliações), a situação e quando. <c>PodeVerRespostas</c> só para quem respondeu.</summary>
public sealed record FormularioRespondenteRh(FuncionarioResumo Funcionario, FuncionarioResumo? Avaliado, SituacaoDoRespondente Situacao, DateTime? RespondidoEmUtc, bool PodeVerRespostas);

/// <summary>A tela "Quem respondeu" (<c>FormularioRespondentesRh</c>): em formulário anônimo <c>Itens</c> vem sempre vazio e <c>Total</c> é 0.</summary>
public sealed record FormularioRespondentesRh(IReadOnlyList<FormularioRespondenteRh> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, bool Anonimo, FormularioParticipacaoResumo Resumo)
    : RespostaPaginada<FormularioRespondenteRh>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

/// <summary>O que uma pessoa respondeu, para o RH (<c>RespostaFormularioFuncionario</c>): <c>Avaliado</c> só nas avaliações e <c>RespondidoEmUtc</c> é o instante do envio.</summary>
public sealed record RespostaFormularioFuncionario(int FormularioId, string Titulo, FuncionarioResumo Funcionario, FuncionarioResumo? Avaliado, DateTime RespondidoEmUtc, IReadOnlyList<RespostaFormularioDada> Respostas);

/// <summary>Os filtros da lista de respondentes já lidos: aba, setor e busca (só no nome de quem responde).</summary>
public sealed record FiltroDeRespondentes(SituacaoDoRespondente? Situacao, int? SetorId, string? Busca);

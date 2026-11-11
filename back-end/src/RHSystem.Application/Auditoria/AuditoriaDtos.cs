using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Auditoria;

// Os tipos de resposta levam os nomes dos schemas do contrato (contrato/fontes/20-fatia-1-colaboradores.yaml, "auditoria"): o OpenAPI que a API gera
// os herda do nome do tipo, e o teste de divergência confere.

/// <summary>Quem fez. <c>FuncionarioId</c> nulo, <c>Nome</c> "Sistema" e <c>Perfil</c> nulo quando foi o sistema; <c>Perfil</c> é o que a pessoa tinha NA HORA do evento; <c>Desligado</c> vem da ficha de hoje.</summary>
public sealed record AuditoriaAtor(int? FuncionarioId, string Nome, TipoDeAtor Tipo, Perfil? Perfil, bool Desligado);

/// <summary>Uma linha do log.</summary>
public record AuditoriaEvento(long Id, DateTime QuandoUtc, AuditoriaAtor Ator, AuditoriaArea Area, AuditoriaAcao Acao, string Descricao);

/// <summary>Página de eventos de auditoria.</summary>
public sealed record AuditoriaListaResposta(IReadOnlyList<AuditoriaEvento> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas)
    : RespostaPaginada<AuditoriaEvento>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

/// <summary>De onde veio a ação. <c>Ip</c> nulo para tarefas do sistema.</summary>
public sealed record AuditoriaOrigem(CanalDeOrigem Canal, string? Ip);

/// <summary>Uma linha da tabela "O que mudou". Campo sensível: <c>ValorOculto</c> verdadeiro e <c>Antes</c> e <c>Depois</c> nulos.</summary>
public sealed record AuditoriaMudanca(string Campo, string? Antes, string? Depois, bool ValorOculto);

/// <summary>Um evento com o que a linha expandida mostra.</summary>
public sealed record AuditoriaEventoDetalhe(
    long Id,
    DateTime QuandoUtc,
    AuditoriaAtor Ator,
    AuditoriaArea Area,
    AuditoriaAcao Acao,
    string Descricao,
    string RegistroAfetado,
    AuditoriaOrigem Origem,
    IReadOnlyList<AuditoriaMudanca> Mudancas)
    : AuditoriaEvento(Id, QuandoUtc, Ator, Area, Acao, Descricao);

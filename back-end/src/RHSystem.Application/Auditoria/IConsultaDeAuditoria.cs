using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Auditoria;

/// <summary>
/// O que o usuário pediu, como veio da rota (<c>area</c> e <c>acao</c> ainda em texto, para o caso de uso validar os dois juntos). <c>De</c> e <c>Ate</c> são
/// dias de calendário em -03:00, inclusivos.
/// </summary>
public sealed record PedidoDeAuditoria(DateOnly? De, DateOnly? Ate, string? Busca, string? Area, string? Acao, bool ApenasAlteracoes = false);

/// <summary>O pedido já validado: o intervalo em instantes UTC (<c>[DeUtc, AteUtcExclusivo)</c>), a busca aparada e os enums lidos.</summary>
public sealed record FiltroDeAuditoria(DateTime DeUtc, DateTime AteUtcExclusivo, string? Busca, AuditoriaArea? Area, AuditoriaAcao? Acao, bool ApenasAlteracoes);

/// <summary>Uma linha do CSV, ainda com os códigos (os rótulos da tela entram na hora de escrever o arquivo).</summary>
public sealed record LinhaDeExportacaoDeAuditoria(
    DateTime QuandoUtc,
    string Quem,
    TipoDeAtor Tipo,
    Perfil? Perfil,
    AuditoriaArea Area,
    AuditoriaAcao Acao,
    string Descricao,
    string RegistroAfetado,
    CanalDeOrigem Canal,
    string? Ip);

/// <summary>Só leitura do log. A ordem é fixa: do mais recente ao mais antigo (<c>Quando</c>, depois <c>Id</c>, ambos decrescentes).</summary>
public interface IConsultaDeAuditoria
{
    Task<RespostaPaginada<AuditoriaEvento>> ListarAsync(FiltroDeAuditoria filtro, ParametrosDeLista paginacao, CancellationToken ct);

    /// <summary>O evento com as mudanças em <c>Ordem</c>, ou <c>null</c> se não existe.</summary>
    Task<AuditoriaEventoDetalhe?> ObterAsync(long id, CancellationToken ct);

    /// <summary>Todos os eventos do filtro, sem paginar, na mesma ordem da lista.</summary>
    Task<IReadOnlyList<LinhaDeExportacaoDeAuditoria>> ListarParaExportacaoAsync(FiltroDeAuditoria filtro, CancellationToken ct);
}

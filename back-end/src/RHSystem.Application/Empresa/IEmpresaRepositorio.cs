using RHSystem.Domain.Empresa;

namespace RHSystem.Application.Empresa;

/// <summary>O acesso à linha única da configuração da empresa. As gravações passam pelo <c>SaveChanges</c> do contexto, onde o interceptor grava o evento de auditoria na mesma transação.</summary>
public interface IEmpresaRepositorio
{
    /// <summary>A configuração, rastreada, para editar; <c>null</c> se a empresa ainda não foi configurada.</summary>
    Task<ConfiguracaoDaEmpresa?> ObterAsync(CancellationToken ct);

    /// <summary>
    /// Grava a linha nova. <c>false</c> = outra chamada criou a linha antes (a chave única do banco recusou, <c>1062</c>): o contexto fica limpo e quem chamou refaz como atualização. Nunca lança por isso.
    /// </summary>
    Task<bool> TentarAdicionarAsync(ConfiguracaoDaEmpresa nova, CancellationToken ct);

    /// <summary>Grava a configuração editada.</summary>
    Task SalvarAsync(CancellationToken ct);
}

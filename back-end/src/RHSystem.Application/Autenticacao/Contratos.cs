using System.Data;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Autenticacao;

/// <summary>Quem guarda e busca os usuários. Devolve o <see cref="Usuario"/> com Funcionario, NivelDeAcesso, Cargo e Setor carregados (Cargo e Setor podem ser nulos).</summary>
public interface IUsuarioRepositorio
{
    Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct);

    Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct);
}

public interface IRefreshTokenRepositorio
{
    Task AdicionarAsync(RefreshToken token, CancellationToken ct);

    Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct);

    /// <summary>Revoga o token de forma atômica e devolve <c>true</c> só para quem de fato o revogou (duas renovações simultâneas: só uma ganha).</summary>
    Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct);

    Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct);
}

public interface IUnidadeDeTrabalho
{
    Task SalvarAsync(CancellationToken ct);

    Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct);

    /// <summary>
    /// A mesma transação, com o nível de isolamento escolhido para ELA (vale só para esta transação; a conexão volta ao padrão). A reserva das filas (e-mails e agendador)
    /// usa <see cref="IsolationLevel.ReadCommitted"/>: no padrão (REPEATABLE READ) o <c>SELECT ... FOR UPDATE SKIP LOCKED</c> segura bloqueios de intervalo no índice e o
    /// <c>UPDATE</c> seguinte, que move a linha de <c>PENDENTE</c> para <c>EM_ENVIO</c> no mesmo índice, dá deadlock entre dois processadores (medido; ver README).
    /// </summary>
    Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, IsolationLevel nivel, CancellationToken ct);
}

public interface ISenhaHasher
{
    string Gerar(string senha);

    bool Verificar(string hash, string senha);
}

public interface IEmissorDeTokens
{
    /// <summary>Quanto tempo um refresh token vale (7 dias por padrão).</summary>
    TimeSpan ValidadeDoRefresh { get; }

    AcessoEmitido EmitirAcesso(Usuario usuario);

    RefreshGerado NovoRefresh();

    string HashDoRefresh(string token);
}

public sealed record AcessoEmitido(string Token, DateTime ExpiraEmUtc);

public sealed record RefreshGerado(string Token, string Hash);

/// <summary>O relógio da aplicação (UTC). Os testes trocam por um falso.</summary>
public interface IRelogio
{
    DateTime AgoraUtc { get; }
}

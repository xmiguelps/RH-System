namespace RHSystem.Application.Autenticacao;

/// <summary>Sair deste aparelho: revoga o refresh token informado. Desconhecido ou já revogado não é erro (sair é idempotente).</summary>
public sealed class SairCasoDeUso(IRefreshTokenRepositorio tokens, IEmissorDeTokens emissor, IRelogio relogio)
{
    public async Task ExecutarAsync(RenovarComando comando, CancellationToken ct)
    {
        await tokens.TentarRevogarAsync(emissor.HashDoRefresh(comando.RefreshToken), relogio.AgoraUtc, ct);
    }
}

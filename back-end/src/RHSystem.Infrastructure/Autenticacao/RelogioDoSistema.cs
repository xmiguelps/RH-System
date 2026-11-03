using RHSystem.Application.Autenticacao;

namespace RHSystem.Infrastructure.Autenticacao;

public sealed class RelogioDoSistema : IRelogio
{
    public DateTime AgoraUtc => DateTime.UtcNow;
}

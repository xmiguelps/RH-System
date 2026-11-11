using System.Collections.Concurrent;
using RHSystem.Application.Acesso;
using RHSystem.Application.Autenticacao;

namespace RHSystem.Infrastructure.Acesso;

/// <summary>
/// O limite de erros de senha atual de <c>trocarMinhaSenha</c>: <c>limite</c> erros da pessoa nos últimos 60 s bloqueiam a próxima tentativa. Singleton, em memória e por instância (com mais de
/// uma instância o limite vale por instância: a mesma limitação do limitador por IP da fatia 0). Conta pelo <see cref="IRelogio"/>; um erro sai da janela quando completa 60 s.
/// </summary>
public sealed class LimiteDeSenhaAtual(IRelogio relogio, int limite) : ILimiteDeSenhaAtual
{
    private static readonly TimeSpan Janela = TimeSpan.FromSeconds(60);

    private readonly ConcurrentDictionary<int, List<DateTime>> _erros = new();

    public TimeSpan? Bloqueio(int usuarioId)
    {
        if (!_erros.TryGetValue(usuarioId, out var erros))
        {
            return null;
        }

        var agora = relogio.AgoraUtc;
        lock (erros)
        {
            erros.RemoveAll(quando => quando <= agora - Janela);
            return erros.Count < limite ? null : erros.Min() + Janela - agora;
        }
    }

    public void RegistrarErro(int usuarioId)
    {
        var erros = _erros.GetOrAdd(usuarioId, _ => []);
        lock (erros)
        {
            erros.Add(relogio.AgoraUtc);
        }
    }
}

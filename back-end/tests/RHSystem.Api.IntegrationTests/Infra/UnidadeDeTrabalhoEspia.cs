using System.Data;
using RHSystem.Application.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Decora a <see cref="IUnidadeDeTrabalho"/> de verdade (o <c>ApiFactory</c> a registra por cima) e marca, num <see cref="AsyncLocal{T}"/>, quando um <c>ExecutarEmTransacaoAsync</c> está em curso. O
/// <see cref="ArmazenamentoEmMemoria"/> lê a marca para contar as gravações feitas com transação aberta: o envio de arquivo grava no Blob FORA de qualquer transação, e os testes provam que a contagem é 0.
/// </summary>
public sealed class UnidadeDeTrabalhoEspia(IUnidadeDeTrabalho real) : IUnidadeDeTrabalho
{
    private static readonly AsyncLocal<bool> Dentro = new();

    /// <summary>Há um <c>ExecutarEmTransacaoAsync</c> em curso no fluxo de quem pergunta.</summary>
    public static bool EmTransacao => Dentro.Value;

    public Task SalvarAsync(CancellationToken ct) => real.SalvarAsync(ct);

    public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct)
    {
        var antes = Dentro.Value;
        Dentro.Value = true;
        try
        {
            return await real.ExecutarEmTransacaoAsync(acao, ct);
        }
        finally
        {
            Dentro.Value = antes;
        }
    }

    public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, IsolationLevel nivel, CancellationToken ct)
    {
        var antes = Dentro.Value;
        Dentro.Value = true;
        try
        {
            return await real.ExecutarEmTransacaoAsync(acao, nivel, ct);
        }
        finally
        {
            Dentro.Value = antes;
        }
    }
}

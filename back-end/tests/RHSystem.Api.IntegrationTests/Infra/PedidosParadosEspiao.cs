using RHSystem.Application.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// A porta <see cref="IPedidosParadosNoGestor"/> da 1a no lugar do composto padrão (vazio), só nos testes (<see cref="ApiFactory.ComPedidosParados"/>): registra <c>(método, gestorId)</c> de cada chamada, devolve o
/// <see cref="PedidosAprovados"/> que o teste configurar (padrão <see cref="PedidosAprovados.Zero"/>), pode ser mandada lançar e deixa o teste olhar o banco no instante da chamada
/// (<see cref="AoChamar"/>). <b>Não</b> é a implementação das fatias 3 e 7: não mexe em pedido nenhum.
/// </summary>
public sealed class PedidosParadosEspiao : IPedidosParadosNoGestor
{
    public const string Aprovar = nameof(IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync);
    public const string PassarParaORh = nameof(IPedidosParadosNoGestor.PassarParaORhAsync);
    public const string PassarOsDaPessoa = nameof(IPedidosParadosNoGestor.PassarParaORhOsDaPessoaAsync);

    private readonly object _trava = new();
    private readonly List<(string Metodo, int GestorId)> _chamadas = [];

    /// <summary>O que <see cref="AprovarEtapaDoGestorAsync"/> devolve.</summary>
    public PedidosAprovados Devolve { get; set; } = PedidosAprovados.Zero;

    /// <summary>Se preenchida, as duas ações lançam esta exceção (depois de registrar a chamada).</summary>
    public Exception? Lanca { get; set; }

    /// <summary>Roda no instante da chamada, antes de devolver: o teste lê o banco por outra conexão para saber o que já estava confirmado.</summary>
    public Func<string, int, Task>? AoChamar { get; set; }

    public IReadOnlyList<(string Metodo, int GestorId)> Chamadas
    {
        get
        {
            lock (_trava)
            {
                return [.. _chamadas];
            }
        }
    }

    public async Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
    {
        await RegistrarAsync(Aprovar, gestorId);
        return Devolve;
    }

    public async Task PassarParaORhAsync(int gestorId, CancellationToken ct) => await RegistrarAsync(PassarParaORh, gestorId);

    /// <summary>A troca de setor da pessoa: registra a chamada com o id da PESSOA no lugar do gestor.</summary>
    public async Task PassarParaORhOsDaPessoaAsync(int funcionarioId, CancellationToken ct) => await RegistrarAsync(PassarOsDaPessoa, funcionarioId);

    private async Task RegistrarAsync(string metodo, int gestorId)
    {
        lock (_trava)
        {
            _chamadas.Add((metodo, gestorId));
        }

        if (AoChamar is { } aoChamar)
        {
            await aoChamar(metodo, gestorId);
        }

        if (Lanca is { } erro)
        {
            throw erro;
        }
    }
}

using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ponto;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// A condição do ponto para efetivar um desligamento: o banco de horas do ÚLTIMO dia de trabalho da pessoa já foi fechado (a consolidação daquele dia, ou de um dia depois, concluiu). Antes disso a pessoa ainda tem dia por lançar e o desligamento espera: a tarefa do
/// desligamento tenta de novo e a consolidação das 12:00 do dia seguinte libera.
/// </summary>
public sealed class CondicaoDoBancoDeHorasParaDesligamento(IUltimoDiaConsolidado ultimoConsolidado) : ICondicaoParaEfetivarDesligamento
{
    public async Task<bool> PodeEfetivarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct) =>
        await ultimoConsolidado.ObterAsync(ct) is { } ultimo && ultimo >= ultimoDiaDeTrabalho;
}

using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Holerites;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// A tarefa que manda o e-mail do holerite na hora em que ele fica visível: <c>PUBLICAR_HOLERITE:holerite={id}</c>, com <c>ExecutarApos</c> = a publicação (agora, ou <c>00:00 -03:00</c> do dia agendado). A data de um holerite nunca muda (corrigir é uma versão
/// nova, com outro id), então a chave não leva o instante. Os parâmetros ficam vazios: a tarefa relê o holerite.
/// </summary>
public sealed class AgendamentoDoHolerite(IAgendador agendador) : IAgendamentoDoHolerite
{
    public const string Tipo = "PUBLICAR_HOLERITE";

    public static string Chave(int holeriteId) => ChaveDeIdempotencia.Montar(Tipo, ("holerite", holeriteId));

    public async Task AgendarAsync(int holeriteId, DateTime publicacaoEmUtc, CancellationToken ct) =>
        _ = await agendador.AgendarAsync(new NovaTarefa(Tipo, Chave(holeriteId), publicacaoEmUtc, ReferenciaId: holeriteId), ct);
}

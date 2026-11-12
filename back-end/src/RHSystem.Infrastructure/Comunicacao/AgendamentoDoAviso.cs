using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Avisos;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// A tarefa que manda o e-mail do aviso na hora da publicação: <c>PUBLICAR_AVISO:aviso={id}:em={instante}</c>, com <c>ExecutarApos</c> = a publicação (também para "agora"). A chave leva o instante porque uma chave cancelada nunca volta
/// (<c>IAgendador</c>): mudar a data de um agendado cancela a chave antiga e agenda outra. Os parâmetros da tarefa ficam vazios (nunca levam dado pessoal; a tarefa relê o aviso).
/// </summary>
public sealed class AgendamentoDoAviso(IAgendador agendador) : IAgendamentoDoAviso
{
    public const string Tipo = "PUBLICAR_AVISO";

    public static string Chave(int avisoId, DateTime publicacaoEmUtc) => ChaveDeIdempotencia.Montar(Tipo, ("aviso", avisoId), ("em", publicacaoEmUtc));

    /// <summary>O instante da publicação que a chave guarda (<c>em=2026-10-07T12:42:00Z</c>), ou <c>null</c> se a chave não tem o formato.</summary>
    public static DateTime? InstanteDaChave(string chave)
    {
        const string marca = ":em=";
        var inicio = chave.LastIndexOf(marca, StringComparison.Ordinal);
        return inicio >= 0 && DateTime.TryParseExact(chave[(inicio + marca.Length)..], "yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var instante)
            ? instante
            : null;
    }

    public async Task AgendarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct) =>
        _ = await agendador.AgendarAsync(new NovaTarefa(Tipo, Chave(avisoId, publicacaoEmUtc), publicacaoEmUtc, ReferenciaId: avisoId), ct);

    public async Task CancelarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct) => _ = await agendador.CancelarAsync(Chave(avisoId, publicacaoEmUtc), ct);
}

using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Construtor;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A tarefa que publica o formulário agendado: <c>PUBLICAR_FORMULARIO:formulario={id}:em={instante}</c>, com <c>ExecutarApos</c> = o começo do dia escolhido (00:00 -03:00). A chave leva o instante porque uma chave cancelada nunca volta (<c>IAgendador</c>):
/// mudar o dia de um agendado cancela a chave antiga e agenda outra. Os parâmetros da tarefa ficam vazios (nunca levam dado pessoal; a tarefa relê o formulário).
/// </summary>
public sealed class AgendamentoDoFormulario(IAgendador agendador) : IAgendamentoDoFormulario
{
    public const string TipoDePublicar = "PUBLICAR_FORMULARIO";
    public const string TipoDeEncerrar = "ENCERRAR_FORMULARIO";

    public static string ChaveDePublicar(int formularioId, DateTime publicacaoEmUtc) => ChaveDeIdempotencia.Montar(TipoDePublicar, ("formulario", formularioId), ("em", publicacaoEmUtc));

    public static string ChaveDeEncerrar(int formularioId, DateTime encerramentoNominalEmUtc) => ChaveDeIdempotencia.Montar(TipoDeEncerrar, ("formulario", formularioId), ("em", encerramentoNominalEmUtc));

    /// <summary>O instante que a chave guarda (<c>em=2026-10-07T12:42:00Z</c>), ou <c>null</c> se a chave não tem o formato.</summary>
    public static DateTime? InstanteDaChave(string chave)
    {
        const string marca = ":em=";
        var inicio = chave.LastIndexOf(marca, StringComparison.Ordinal);
        return inicio >= 0 && DateTime.TryParseExact(chave[(inicio + marca.Length)..], "yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal, out var instante)
            ? instante
            : null;
    }

    /// <summary>Se a chave já existe é porque este formulário já teve uma tarefa para este instante: cancelada (o dia foi e voltou), e então ela é REATIVADA; pendente ou concluída, nada a fazer.</summary>
    public async Task AgendarPublicacaoAsync(int formularioId, DateTime publicacaoEmUtc, CancellationToken ct)
    {
        var chave = ChaveDePublicar(formularioId, publicacaoEmUtc);
        if (!await agendador.AgendarAsync(new NovaTarefa(TipoDePublicar, chave, publicacaoEmUtc, ReferenciaId: formularioId), ct))
        {
            _ = await agendador.ReativarAsync(chave, ct);
        }
    }

    public async Task CancelarPublicacaoAsync(int formularioId, DateTime publicacaoEmUtc, CancellationToken ct) => _ = await agendador.CancelarAsync(ChaveDePublicar(formularioId, publicacaoEmUtc), ct);

    public async Task AgendarEncerramentoAsync(int formularioId, DateTime encerramentoNominalEmUtc, CancellationToken ct)
    {
        var chave = ChaveDeEncerrar(formularioId, encerramentoNominalEmUtc);
        if (!await agendador.AgendarAsync(new NovaTarefa(TipoDeEncerrar, chave, encerramentoNominalEmUtc, ReferenciaId: formularioId), ct))
        {
            _ = await agendador.ReativarAsync(chave, ct);
        }
    }

    public async Task CancelarEncerramentoAsync(int formularioId, DateTime encerramentoNominalEmUtc, CancellationToken ct) => _ = await agendador.CancelarAsync(ChaveDeEncerrar(formularioId, encerramentoNominalEmUtc), ct);
}

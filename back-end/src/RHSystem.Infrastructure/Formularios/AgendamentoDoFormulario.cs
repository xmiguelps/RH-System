using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Emails;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A tarefa que publica o formulário agendado: <c>PUBLICAR_FORMULARIO:formulario={id}:em={instante}</c>, com <c>ExecutarApos</c> = o começo do dia escolhido (00:00 -03:00). A chave leva o instante porque uma chave cancelada nunca volta (<c>IAgendador</c>):
/// mudar o dia de um agendado cancela a chave antiga e agenda outra. Os parâmetros da tarefa ficam vazios (nunca levam dado pessoal; a tarefa relê o formulário).
/// </summary>
public sealed class AgendamentoDoFormulario(IAgendador agendador, IFilaDeEmails fila, AppDbContext db) : IAgendamentoDoFormulario
{
    public const string TipoDePublicar = "PUBLICAR_FORMULARIO";
    public const string TipoDeEncerrar = "ENCERRAR_FORMULARIO";
    public const string TipoDeReabrir = "REABRIR_FORMULARIO";
    public const string TipoDoLembrete = RegrasDoLembrete.TipoDaTarefa;
    private static readonly string[] TiposDeTarefa = [TipoDePublicar, TipoDeEncerrar, TipoDeReabrir, TipoDoLembrete];

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

    public async Task AgendarLembreteAutomaticoAsync(int formularioId, DateOnly dia, CancellationToken ct) =>
        _ = await agendador.AgendarAsync(new NovaTarefa(TipoDoLembrete, RegrasDoLembrete.ChaveDaMarca(formularioId, dia, RegrasDoLembrete.AlcanceTodos), FusoBrasil.InicioDoDiaEmUtc(dia), formularioId, dia), ct);

    public async Task CancelarTudoAsync(int formularioId, CancellationToken ct)
    {
        // AUDITORIA-OK: cancelar as tarefas e os lembretes do formulário que vai ser excluído é efeito de sistema da exclusão, que o caso de uso audita (evento Excluiu).
        await db.TarefasAgendadas.Where(t => t.ReferenciaId == formularioId && TiposDeTarefa.Contains(t.Tipo) && t.Situacao == SituacaoDaTarefa.Pendente).ExecuteUpdateAsync(s => s.SetProperty(t => t.Situacao, SituacaoDaTarefa.Cancelada), ct);

        // o lembrete pendente de um formulário excluído não pode mais sair: a fila cancela o com nome e APAGA o sigiloso (o CHECK do banco não deixa sigiloso em situação final)
        _ = await fila.CancelarPendentesAsync(RegrasDoLembrete.TipoDoEmail, formularioId, ct);
    }

    public async Task CancelarEncerramentoAsync(int formularioId, DateTime encerramentoNominalEmUtc, CancellationToken ct) => _ = await agendador.CancelarAsync(ChaveDeEncerrar(formularioId, encerramentoNominalEmUtc), ct);
}

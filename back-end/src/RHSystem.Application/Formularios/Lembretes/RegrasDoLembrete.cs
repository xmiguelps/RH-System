using RHSystem.Application.Comum;

namespace RHSystem.Application.Formularios.Lembretes;

/// <summary>
/// As regras do lembrete de formulário, puras. <b>Automático:</b> um e-mail 7 dias antes do <c>prazo</c>, só se o prazo está a MAIS de 7 dias do dia da publicação (a 7 dias ou menos, não há lembrete). <b>Chaves:</b> o e-mail com nome é um por pessoa, por formulário e por dia
/// (<c>FORMULARIO_LEMBRETE:form=9:func=21:dia=2026-10-24</c>, o mesmo limite do manual e do automático); a MARCA "já saiu lembrete hoje" é por formulário, dia e alcance (<c>LEMBRETE_FORMULARIO:form=9:dia=2026-10-24:alcance=TODOS</c>, na tabela de tarefas, nunca no formulário): o automático é essa mesma chave
/// <c>PENDENTE</c> no começo do dia, e o manual a insere já <c>CONCLUIDA</c>, então um dos dois por dia.
/// </summary>
public static class RegrasDoLembrete
{
    /// <summary>O tipo do e-mail (<c>FILA_EMAILS.TIPO</c>) e do renderizador.</summary>
    public const string TipoDoEmail = "FORMULARIO_LEMBRETE";

    /// <summary>O tipo da tarefa (<c>TAREFAS_AGENDADAS.TIPO</c>): o automático, a marca do dia e o envio do lembrete manual do anônimo.</summary>
    public const string TipoDaTarefa = "LEMBRETE_FORMULARIO";

    /// <summary>Quantos dias antes do prazo sai o lembrete automático.</summary>
    public const int DiasAntesDoPrazo = 7;

    public const string AlcanceTodos = "TODOS";

    /// <summary>O dia em que sai o automático, ou <c>null</c> se não há (o prazo está a 7 dias ou menos do dia da publicação).</summary>
    public static DateOnly? DiaDoAutomatico(DateOnly prazo, DateOnly diaDaPublicacao) =>
        prazo.DayNumber - diaDaPublicacao.DayNumber > DiasAntesDoPrazo ? prazo.AddDays(-DiasAntesDoPrazo) : null;

    /// <summary>O alcance de um lembrete de setor (o gestor): <c>SETOR_3</c>.</summary>
    public static string AlcanceDoSetor(int setorId) => $"SETOR_{setorId.ToString(System.Globalization.CultureInfo.InvariantCulture)}";

    public static string ChaveDoEmail(int formularioId, int funcionarioId, DateOnly dia) =>
        ChaveDeIdempotencia.Montar(TipoDoEmail, ("form", formularioId), ("func", funcionarioId), ("dia", dia));

    /// <summary>A chave de um e-mail SIGILOSO: sem a pessoa (um sorteio no lugar), então a chave nunca liga a mensagem a quem a recebe.</summary>
    public static string ChaveDoEmailSigiloso(int formularioId, DateOnly dia, Guid sorteio) =>
        ChaveDeIdempotencia.Montar(TipoDoEmail, ("form", formularioId), ("dia", dia), ("r", sorteio.ToString("N")));

    /// <summary>A marca do dia: o automático (pendente) e o manual do anônimo (concluída) usam a MESMA chave.</summary>
    public static string ChaveDaMarca(int formularioId, DateOnly dia, string alcance) =>
        ChaveDeIdempotencia.Montar(TipoDaTarefa, ("form", formularioId), ("dia", dia), ("alcance", alcance));

    /// <summary>A chave da tarefa que RESOLVE os destinatários e enfileira os e-mails do manual do anônimo (a rota só marca e agenda: o tempo da resposta nunca depende de quantos faltam).</summary>
    public static string ChaveDoEnvio(int formularioId, DateOnly dia, string alcance) =>
        ChaveDeIdempotencia.Montar(TipoDaTarefa, ("form", formularioId), ("dia", dia), ("alcance", alcance), ("envio", 1));
}

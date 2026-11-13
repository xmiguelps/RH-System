namespace RHSystem.Application.Ferias.Regras;

/// <summary>A cor do item no quadro (<c>FeriasCalendarioItem.situacao</c>; NÃO é a <see cref="SituacaoDasFerias"/>): confirmada, com o RH, ou esperando o gestor que consulta.</summary>
public enum SituacaoDoItemDoCalendario
{
    Aprovada,
    ComORh,
    AguardandoVoce,
}

/// <summary>A janela do quadro: a segunda-feira da primeira semana, o domingo da última e a segunda de cada coluna.</summary>
public sealed record JanelaDoCalendario(DateOnly Inicio, DateOnly Fim, IReadOnlyList<DateOnly> Semanas)
{
    /// <summary>O período encosta na janela, nem que seja por um dia.</summary>
    public bool Encosta(DateOnly inicio, DateOnly fim) => inicio <= Fim && fim >= Inicio;
}

/// <summary>
/// O calendário de férias da equipe. A janela começa na segunda-feira da semana do dia informado (sem dia, a de hoje) e vai por <c>semanas</c> semanas. Entram os pedidos <c>APROVADA</c> (inclui em gozo e concluída), <c>COM_O_RH</c> e <c>AGUARDANDO_VOCE</c> (esperam o gestor que consulta); a
/// sugestão pendente espera a pessoa e fica de fora, como os recusados e cancelados.
/// </summary>
public static class CalendarioDeFerias
{
    public const int SemanasPadrao = 13;

    public const int SemanasMaximo = 26;

    public static JanelaDoCalendario Janela(DateOnly? inicio, int semanas, DateOnly hoje)
    {
        var dia = inicio ?? hoje;
        var segunda = dia.AddDays(-(((int)dia.DayOfWeek + 6) % 7));
        var colunas = Enumerable.Range(0, semanas).Select(i => segunda.AddDays(7 * i)).ToList();
        return new JanelaDoCalendario(segunda, segunda.AddDays((7 * semanas) - 1), colunas);
    }

    /// <summary>A cor do pedido, ou <c>null</c> se ele não entra. <paramref name="gestorIdDeQuemConsulta"/> é o <c>ID_GESTOR</c> dele: o pedido que espera OUTRO gestor não é "aguardando você".</summary>
    public static SituacaoDoItemDoCalendario? Situacao(LinhaDoPedidoDeFerias linha, int gestorIdDeQuemConsulta) => SituacaoDoPedidoDeFerias.Situacao(linha) switch
    {
        SituacaoDasFerias.Aprovada or SituacaoDasFerias.EmGozo or SituacaoDasFerias.Concluida => SituacaoDoItemDoCalendario.Aprovada,
        SituacaoDasFerias.ComORh => SituacaoDoItemDoCalendario.ComORh,
        SituacaoDasFerias.ComOGestor when linha.GestorId == gestorIdDeQuemConsulta => SituacaoDoItemDoCalendario.AguardandoVoce,
        _ => null,
    };
}

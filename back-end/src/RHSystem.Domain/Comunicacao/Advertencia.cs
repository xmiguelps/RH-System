namespace RHSystem.Domain.Comunicacao;

/// <summary>
/// Uma advertência (<c>ADVERTENCIAS</c>). Quase toda coluna aceita <c>NULL</c> no banco (o mapeamento é conferido contra ele), mas a aplicação sempre preenche tipo, motivo, descrição, situação e data do ocorrido: o trigger
/// só preencheria a situação e a data com o <c>CURDATE()</c> real, então os dois vêm da aplicação. <c>GestorId</c> nulo é "aplicada pelo RH". O banco nunca apaga advertência. A resposta do RH à contestação <b>não tem coluna</b>
/// (só vai por e-mail e pela auditoria) e o desfecho é deduzido da situação e do texto da contestação.
/// </summary>
public class Advertencia
{
    private Advertencia()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    /// <summary>A linha de <c>GESTORES</c> de quem aplicou; <c>null</c> quando foi o RH.</summary>
    public int? GestorId { get; private set; }

    /// <summary>O dia do ocorrido.</summary>
    public DateOnly? Data { get; private set; }

    public TipoDeAdvertencia? Tipo { get; private set; }

    public string? Motivo { get; private set; }

    public string? Descricao { get; private set; }

    public SituacaoDaAdvertencia? Situacao { get; private set; }

    public DateTime? CienciaEmUtc { get; private set; }

    public string? TextoContestacao { get; private set; }

    public DateTime? ContestacaoEmUtc { get; private set; }

    /// <summary>Nasce <c>PENDENTE</c>.</summary>
    public static Advertencia Registrar(int funcionarioId, int? gestorId, DateOnly data, TipoDeAdvertencia tipo, string motivo, string descricao) =>
        new()
        {
            FuncionarioId = funcionarioId,
            GestorId = gestorId,
            Data = data,
            Tipo = tipo,
            Motivo = motivo,
            Descricao = descricao,
            Situacao = SituacaoDaAdvertencia.Pendente,
        };
}

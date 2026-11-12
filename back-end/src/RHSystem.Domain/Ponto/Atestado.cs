namespace RHSystem.Domain.Ponto;

/// <summary>
/// Um atestado (<c>ATESTADOS</c>), dado de SAÚDE: o arquivo e o nome dele nunca vão para log nem auditoria. Dia inteiro (sem horas) ou um intervalo de horas num dia só. Nasce <see cref="SituacaoDoAtestado.Enviado"/>; só o RH valida ou recusa
/// (com motivo), e a decisão é final. <see cref="EnviadoEmUtc"/> vem do relógio da aplicação (o padrão do banco é o relógio do servidor, que os testes não controlam).
/// </summary>
public class Atestado
{
    private Atestado()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly DataInicio { get; private set; }

    public DateOnly DataFim { get; private set; }

    public TimeOnly? HoraInicio { get; private set; }

    public TimeOnly? HoraFim { get; private set; }

    public string CaminhoArquivo { get; private set; } = string.Empty;

    public string? NomeArquivoOriginal { get; private set; }

    public SituacaoDoAtestado Situacao { get; private set; }

    public int? FuncionarioRhId { get; private set; }

    public DateTime? DecididoEmUtc { get; private set; }

    public string? MotivoRecusa { get; private set; }

    public DateTime EnviadoEmUtc { get; private set; }

    public static Atestado Enviar(int funcionarioId, DateOnly dataInicio, DateOnly dataFim, TimeOnly? horaInicio, TimeOnly? horaFim, string caminhoArquivo, string? nomeArquivoOriginal, DateTime agoraUtc) =>
        new()
        {
            FuncionarioId = funcionarioId,
            DataInicio = dataInicio,
            DataFim = dataFim,
            HoraInicio = horaInicio,
            HoraFim = horaFim,
            CaminhoArquivo = caminhoArquivo,
            NomeArquivoOriginal = nomeArquivoOriginal,
            Situacao = SituacaoDoAtestado.Enviado,
            EnviadoEmUtc = MarcacaoDePonto.AoSegundo(agoraUtc),
        };
}

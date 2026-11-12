namespace RHSystem.Domain.Comunicacao;

/// <summary>
/// A leitura de um aviso por uma pessoa (<c>AVISOS_VISUALIZACOES</c>, chave única por aviso e pessoa). <c>Visualizado</c> com data é "viu"; linha não lida ou ausente é "não viu". A data de leitura vem do relógio da aplicação
/// (o trigger usaria o <c>NOW()</c> real), cortada ao segundo; o <c>CHECK</c> exige a data quando a linha é de lido.
/// </summary>
public class LeituraDeAviso
{
    private LeituraDeAviso()
    {
    }

    public int Id { get; private set; }

    public int AvisoId { get; private set; }

    public int FuncionarioId { get; private set; }

    public bool Visualizado { get; private set; }

    public DateTime? VisualizadoEmUtc { get; private set; }

    public static LeituraDeAviso Criar(int avisoId, int funcionarioId, bool visualizado, DateTime? visualizadoEmUtc) =>
        new()
        {
            AvisoId = avisoId,
            FuncionarioId = funcionarioId,
            Visualizado = visualizado,
            VisualizadoEmUtc = visualizadoEmUtc is { } instante ? CamposDeTexto.AoSegundo(instante) : null,
        };
}

namespace RHSystem.Domain.Treinamentos;

/// <summary>Um módulo que a pessoa concluiu (<c>TREINAMENTOS_MODULOS_PROGRESSO</c>): só existe linha de módulo concluído, uma por (módulo, pessoa). O instante vem SEMPRE do relógio da aplicação (o <c>CURRENT_TIMESTAMP</c> do banco usa o relógio real) e é cortado ao segundo.</summary>
public class TreinamentoModuloProgresso
{
    private TreinamentoModuloProgresso()
    {
    }

    public int Id { get; private set; }

    public int ModuloId { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateTime ConcluidoEmUtc { get; private set; }

    public static TreinamentoModuloProgresso Criar(int moduloId, int funcionarioId, DateTime concluidoEmUtc) =>
        new() { ModuloId = moduloId, FuncionarioId = funcionarioId, ConcluidoEmUtc = new DateTime(concluidoEmUtc.Ticks - (concluidoEmUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc) };
}

namespace RHSystem.Domain.Ponto;

/// <summary>
/// O ponto de uma pessoa num dia (<c>PONTOS</c>, um por pessoa e dia). A <see cref="CargaHoraria"/> (horas do dia) é o cargo ÷ 5 e o TRIGGER a preenche quando vem nula: ela fica congelada na criação do dia. <c>HORAS_TRABALHADAS</c> não é mais
/// preenchida por ninguém (as horas são calculadas pela aplicação a partir das batidas).
/// </summary>
public class PontoDoDia
{
    private PontoDoDia()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly Dia { get; private set; }

    public decimal? HorasTrabalhadas { get; private set; }

    public decimal? CargaHoraria { get; private set; }

    public static PontoDoDia Novo(int funcionarioId, DateOnly dia) => new() { FuncionarioId = funcionarioId, Dia = dia };
}

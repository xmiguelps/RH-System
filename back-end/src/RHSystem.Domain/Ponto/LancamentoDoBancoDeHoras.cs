namespace RHSystem.Domain.Ponto;

/// <summary>
/// Um lançamento do banco de horas (<c>BANCOS_HORAS</c>): só o automático (a consolidação do dia), só crédito ou débito. A <see cref="Quantidade"/> está em HORAS com 2 casas no banco; a aplicação raciocina em MINUTOS inteiros e converte por
/// linha (o saldo é a soma dos minutos de cada linha, nunca <c>FN_CALCULAR_SALDO_BANCO_HORAS</c>). Uma linha por pessoa e <see cref="DiaDeReferencia"/>.
/// </summary>
public class LancamentoDoBancoDeHoras
{
    private LancamentoDoBancoDeHoras()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly? DataDoLancamento { get; private set; }

    public TipoDeLancamentoDoBanco? Tipo { get; private set; }

    public decimal? Quantidade { get; private set; }

    public string? Observacao { get; private set; }

    public DateOnly? DiaDeReferencia { get; private set; }

    public static LancamentoDoBancoDeHoras Criar(int funcionarioId, DateOnly diaDeReferencia, TipoDeLancamentoDoBanco tipo, int minutos, string observacao, DateOnly hoje) =>
        new()
        {
            FuncionarioId = funcionarioId,
            DiaDeReferencia = diaDeReferencia,
            DataDoLancamento = hoje,
            Tipo = tipo,
            Quantidade = MinutosParaHoras(minutos),
            Observacao = observacao,
        };

    /// <summary>Reescreve o lançamento do dia (a consolidação recalcula).</summary>
    public void Refazer(TipoDeLancamentoDoBanco tipo, int minutos, string observacao)
    {
        Tipo = tipo;
        Quantidade = MinutosParaHoras(minutos);
        Observacao = observacao;
    }

    /// <summary>Minutos inteiros para horas com 2 casas: o erro máximo é 0,3 minuto; truncar perderia mais.</summary>
    public static decimal MinutosParaHoras(int minutos) => Math.Round(minutos / 60m, 2, MidpointRounding.AwayFromZero);

    /// <summary>Horas com 2 casas para minutos inteiros (arredonda; nunca trunca).</summary>
    public static int HorasParaMinutos(decimal horas) => (int)Math.Round(horas * 60m, 0, MidpointRounding.AwayFromZero);
}

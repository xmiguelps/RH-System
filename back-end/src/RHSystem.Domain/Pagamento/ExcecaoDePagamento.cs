using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Pagamento;

/// <summary>
/// Uma linha de <c>PAGAMENTOS_EXCECOES</c>: a data do pagamento de UM mês que foge da regra do dia da empresa (o RH decide). Um mês só tem uma exceção (<c>UQ (ANO, MES)</c>) e a data TEM de cair no próprio mês (<c>CHECK</c>): fim de semana ou feriado vale como está, quem escolhe é o RH. O <c>ANO</c> do banco é do tipo
/// <c>YEAR</c> (1901 a 2155); o contrato só aceita de 2020 a 2100. Auditada pelo interceptor, na área Cadastros (criar, trocar a data e excluir).
/// </summary>
[Auditado(AuditoriaArea.Cadastros, "Exceção de pagamento")]
public class ExcecaoDePagamento : IAuditavel
{
    public const int AnoMinimo = 2020;

    public const int AnoMaximo = 2100;

    private ExcecaoDePagamento()
    {
    }

    public int Id { get; private set; }

    public int Ano { get; private set; }

    public int Mes { get; private set; }

    [Auditar("Data do pagamento")]
    public DateOnly DataDoPagamento { get; private set; }

    public string NomeParaAuditoria => $"{Mes:00}/{Ano}";

    public string RegistroAfetado => $"Exceção de pagamento de {Mes:00}/{Ano}";

    /// <exception cref="ArgumentOutOfRangeException">Ano fora de 2020 a 2100, mês fora de 1 a 12 ou data fora do mês: o banco recusaria.</exception>
    public static ExcecaoDePagamento Criar(int ano, int mes, DateOnly data)
    {
        if (ano is < AnoMinimo or > AnoMaximo)
        {
            throw new ArgumentOutOfRangeException(nameof(ano), ano, "O ano vai de 2020 a 2100.");
        }

        if (mes is < 1 or > 12)
        {
            throw new ArgumentOutOfRangeException(nameof(mes), mes, "O mês vai de 1 a 12.");
        }

        var excecao = new ExcecaoDePagamento { Ano = ano, Mes = mes };
        excecao.Substituir(data);
        return excecao;
    }

    /// <summary>Troca a data do mês: precisa cair no mesmo mês e ano da exceção (o <c>CHECK</c> do banco).</summary>
    public void Substituir(DateOnly data)
    {
        if (data.Year != Ano || data.Month != Mes)
        {
            throw new ArgumentOutOfRangeException(nameof(data), data, "A data do pagamento precisa cair no mês da exceção.");
        }

        DataDoPagamento = data;
    }
}

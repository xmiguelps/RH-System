namespace RHSystem.Application.Pagamento;

/// <summary>Uma exceção gravada: o mês e a data do pagamento.</summary>
public sealed record ExcecaoDePagamentoGravada(int Ano, int Mes, DateOnly Data);

/// <summary>
/// As exceções de pagamento no banco (<c>PAGAMENTOS_EXCECOES</c>, a data de um mês que foge da regra do dia). A gravação é auditada pelo interceptor do EF (área Cadastros: criar, trocar a data e excluir).
/// </summary>
public interface IExcecoesDePagamentoRepositorio
{
    /// <summary>Em ordem de data. Com <paramref name="ano"/>, as daquele ano; sem ele, as do mês <paramref name="aPartirDoMes"/> em diante (por MÊS: a exceção do mês de hoje conta mesmo com a data já passada).</summary>
    Task<IReadOnlyList<ExcecaoDePagamentoGravada>> ListarAsync(int? ano, (int Ano, int Mes)? aPartirDoMes, CancellationToken ct);

    /// <summary>
    /// Cria a do mês ou, se já existe, troca a data (um PUT que substitui). Dois PUT do mesmo mês ao mesmo tempo: o que perde a corrida na chave única (1062) vira "já existe, atualizar" e termina com a data dele, nunca com erro.
    /// </summary>
    Task<ExcecaoDePagamentoGravada> DefinirAsync(int ano, int mes, DateOnly data, CancellationToken ct);

    /// <summary><c>false</c>: não existia (ou outra chamada a removeu antes).</summary>
    Task<bool> RemoverAsync(int ano, int mes, CancellationToken ct);
}

namespace RHSystem.Application.Empresa;

/// <summary>A configuração da empresa como as outras fatias a leem: horários como <see cref="TimeOnly"/>, sem fuso (o horário padrão da empresa, em hora local).</summary>
public sealed record ConfiguracaoDaEmpresaLida(string NomeEmpresa, string Uf, string Municipio, TimeOnly HorarioEntrada, TimeOnly HorarioSaida, int ToleranciaAtrasoMinutos, int DiaPagamento);

/// <summary>
/// A porta que as fatias 3 (ponto) e 7 (férias) usam para ler a empresa. <b>Não há cache</b>: cada chamada lê o banco, então a mudança salva agora é a que a chamada seguinte vê (é uma linha só).
/// </summary>
public interface IConfiguracaoDaEmpresa
{
    /// <summary><c>null</c> = a empresa ainda não foi configurada. Quem pode seguir sem ela usa este.</summary>
    Task<ConfiguracaoDaEmpresaLida?> ObterAsync(CancellationToken ct);

    /// <summary>
    /// Para a operação que PRECISA da empresa (CONVENCOES, seção 11).
    /// </summary>
    /// <exception cref="Comum.ErroDeNegocioException">Sem configuração: <c>EMPRESA_NAO_CONFIGURADA</c>, que a Api responde como 422.</exception>
    Task<ConfiguracaoDaEmpresaLida> ObterOuFalharAsync(CancellationToken ct);
}

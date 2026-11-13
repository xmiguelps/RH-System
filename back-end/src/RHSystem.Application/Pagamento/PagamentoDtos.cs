using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Pagamento;

// Os formatos do contrato (fatia 7, pagamento). A regra do dia mora em RegraDoPagamento; aqui só o que sai e o que entra.

/// <summary>A data efetiva de pagamento de um mês (<c>PagamentoData</c>). <c>Ajustes</c> só vem na <c>REGRA</c> (vazio na <c>EXCECAO</c>) e pode trazer dois.</summary>
public sealed record PagamentoDataDto(int Ano, int Mes, DateOnly Data, OrigemDoPagamento Origem, IReadOnlyList<AjusteDoPagamento> Ajustes)
{
    public static PagamentoDataDto De(PagamentoDoMes pagamento) => new(pagamento.Ano, pagamento.Mes, pagamento.Data, pagamento.Origem, pagamento.Ajustes);
}

/// <summary>O mês do holerite (<c>PagamentoCompetencia</c>): sempre o mês anterior ao do pagamento.</summary>
public sealed record PagamentoCompetenciaDto(int Ano, int Mes);

/// <summary>O próximo pagamento (<c>PagamentoProximo</c>): os dois campos são nulos quando a empresa não tem dia de pagamento nem exceção.</summary>
public sealed record PagamentoProximoDto(PagamentoDataDto? Pagamento, PagamentoCompetenciaDto? Competencia);

public sealed record PagamentoExcecaoDto(int Ano, int Mes, DateOnly DataPagamento);

public sealed record PagamentoExcecaoListaDto(IReadOnlyList<PagamentoExcecaoDto> Itens);

public sealed record PagamentoCalendarioDto(int Ano, IReadOnlyList<PagamentoDataDto> Meses);

/// <summary>O corpo de <c>definirExcecaoDePagamento</c>: a data precisa cair dentro do ano e do mês da rota.</summary>
public sealed record PagamentoExcecaoComando
{
    public required DateOnly DataPagamento { get; init; }
}

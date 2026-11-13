using RHSystem.Application.Pagamento;

namespace RHSystem.Application.Painel;

/// <summary>O cartão "Férias disponíveis" da tela inicial (<c>PainelFeriasResumo</c>): o mesmo número da rota de férias; <c>prazoParaTirarSaldo</c> é nulo sem saldo.</summary>
public sealed record PainelFeriasResumoDto(int DiasDisponiveis, DateOnly? PrazoParaTirarSaldo);

/// <summary>Uma linha do cartão "Seus benefícios" (<c>PainelBeneficioResumo</c>). <c>Detalhe</c> é o texto curto montado pela API ("Você e 1 dependente", as linhas do trajeto), ou nulo.</summary>
public sealed record PainelBeneficioResumoDto(int AdesaoId, string Nome, string? Detalhe);

/// <summary>Os cartões da tela inicial do funcionário e do gestor que são da gestão (<c>PainelFuncionario</c>).</summary>
public sealed record PainelFuncionarioDto(PainelFeriasResumoDto Ferias, PagamentoProximoDto ProximoPagamento, IReadOnlyList<PainelBeneficioResumoDto> Beneficios);

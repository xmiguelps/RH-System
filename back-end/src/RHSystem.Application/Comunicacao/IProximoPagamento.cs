using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Comunicacao;

/// <summary>
/// O próximo pagamento da empresa, de <paramref name="hoje"/> em diante (hoje inclusive): o dia da regra (<c>CONFIGURACOES_EMPRESA.DIA_PAGAMENTO</c>), a exceção do mês (<c>PAGAMENTOS_EXCECOES</c>) e os feriados que valem para a empresa (os mesmos do ponto).
/// <c>null</c> quando a empresa não tem dia de pagamento configurado e não há exceção. Os holerites agendados "no dia do pagamento" (fatia 4) usam esta porta; a fatia 7 a reaproveita no cartão "Próximo pagamento".
/// </summary>
public interface IProximoPagamento
{
    Task<PagamentoDoMes?> ObterAsync(DateOnly hoje, CancellationToken ct);
}

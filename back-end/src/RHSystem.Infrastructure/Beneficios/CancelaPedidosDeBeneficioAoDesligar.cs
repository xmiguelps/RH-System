using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Beneficios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Beneficios;

/// <summary>
/// Quando a efetivação inativa a pessoa, os pedidos de benefício dela que ainda esperam o RH (a adesão, os dependentes e os trajetos <c>PENDENTE</c>) viram <c>CANCELADO</c>, sem resposta (nem RH nem data): saem da lista do RH e a pessoa vê o item de volta em "disponíveis", como quem teve o pedido cancelado. O trigger
/// do grupo só desativa as adesões ATIVAS; nada cancela pedido. Sem evento próprio: a inativação já tem o dela. Roda dentro da transação da efetivação, com a pessoa travada.
/// </summary>
public sealed class CancelaPedidosDeBeneficioAoDesligar(AppDbContext db) : IAoEfetivarDesligamento
{
    public async Task AposEfetivarAsync(int funcionarioId, CancellationToken ct)
    {
        // AUDITORIA-OK: o cancelamento dos pedidos pendentes de quem foi desligado não tem evento próprio (a inativação já tem o dela, escrito pelo interceptor) e o ExecuteUpdate não passa pelo interceptor do EF.
        await db.Dependentes
            .Where(d => d.Status == StatusDoPedidoDeBeneficio.Pendente && db.BeneficiosDosFuncionarios.Any(l => l.Id == d.AdesaoId && l.FuncionarioId == funcionarioId))
            .ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, StatusDoPedidoDeBeneficio.Cancelado), ct);

        // AUDITORIA-OK: idem para os trajetos pendentes: sem evento próprio e o ExecuteUpdate não passa pelo interceptor do EF.
        await db.TrajetosDeValeTransporte
            .Where(t => t.Status == StatusDoPedidoDeBeneficio.Pendente && db.BeneficiosDosFuncionarios.Any(l => l.Id == t.AdesaoId && l.FuncionarioId == funcionarioId))
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusDoPedidoDeBeneficio.Cancelado), ct);

        // AUDITORIA-OK: idem para a adesão pendente: CANCELADO sem resposta (o pedido continua com a data dele); sem evento próprio e o ExecuteUpdate não passa pelo interceptor do EF.
        await db.BeneficiosDosFuncionarios
            .Where(l => l.FuncionarioId == funcionarioId && l.Status == StatusDoPedidoDeBeneficio.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Status, StatusDoPedidoDeBeneficio.Cancelado), ct);
    }
}

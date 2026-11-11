using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Escopo;
using RHSystem.Application.Painel;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Os pedidos de ajuste de ponto que aguardam o GESTOR (a bolinha do menu e a parte de ajustes das pendências da equipe): <c>PENDENTE</c>, na etapa do gestor (<c>ID_GESTOR</c> é o dele e sem resultado dele), de gente da equipe dele. Sem setor: 0. O contador é
/// um parâmetro porque a mesma conta serve a duas bolinhas.
/// </summary>
public sealed class FonteDePedidosParaOGestor(AppDbContext db, ContadorDoMenu contador) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => contador;

    public async Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct)
    {
        if (await escopo.ObterEquipeAsync(ct) is not { } equipe)
        {
            return 0;
        }

        var gestorId = equipe.GestorId;
        return await db.PedidosDeAjuste.AsNoTracking()
            .Where(p => p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId == gestorId && p.ResultadoDoGestor == null && db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(p.FuncionarioId))
            .CountAsync(ct);
    }
}

/// <summary>Os pedidos de ajuste de ponto que o RH já pode decidir (a bolinha do menu do RH e <c>resumo.comRh</c> da fila): <c>PENDENTE</c> na etapa do RH, com o gestor já aprovado ou sem etapa do gestor.</summary>
public sealed class FonteDePedidosParaORh(AppDbContext db) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => ContadorDoMenu.PedidosDeAjusteDePontoDoRh;

    public async Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct) =>
        await db.PedidosDeAjuste.AsNoTracking().CountAsync(p => p.Status == StatusDoPedidoDeAjuste.Pendente && (p.GestorId == null || p.ResultadoDoGestor != null), ct);
}

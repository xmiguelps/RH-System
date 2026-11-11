using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// As leituras do ponto do espelho e da equipe, sem rastreamento. O que é da equipe parte SEMPRE de <c>DaEquipe(equipe)</c>: quem está fora do escopo nunca é lido, então "não existe" e "é de outro setor" não se distinguem.
/// </summary>
public sealed class ConsultaDoPonto(AppDbContext db) : IConsultaDoPonto
{
    private static readonly Expression<Func<Funcionario, PessoaDoEspelho>> Projecao = f => new PessoaDoEspelho(
        f.Id,
        f.Nome,
        f.Cargo != null ? f.Cargo.Nome : null,
        f.Setor != null ? f.Setor.Nome : null,
        f.Ativo,
        f.DataAdmissao,
        f.DataDesligamento,
        f.Cargo != null ? f.Cargo.CargaSemanalHoras : (decimal?)null);

    public async Task<PessoaDoEspelho?> PessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(Projecao).FirstOrDefaultAsync(ct);

    public async Task<PessoaDoEspelho?> PessoaDaEquipeAsync(EquipeDoGestor equipe, int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().DaEquipe(equipe).Where(f => f.Id == funcionarioId).Select(Projecao).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<PessoaDoEspelho>> PessoasDaEquipeAsync(EquipeDoGestor equipe, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().DaEquipe(equipe).OrderBy(f => f.Nome).ThenBy(f => f.Id).Select(Projecao).ToListAsync(ct);

    public async Task<IReadOnlyList<BatidaDoDia>> BatidasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var ids = funcionarioIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await (
            from m in db.Marcacoes.AsNoTracking()
            join p in db.PontosDosDias.AsNoTracking() on m.PontoId equals p.Id
            where ids.Contains(p.FuncionarioId) && p.Dia >= de && p.Dia <= ate
            orderby m.InstanteUtc, m.Id
            select new BatidaDoDia(p.FuncionarioId, p.Dia, new BatidaLida(m.Id, m.InstanteUtc, m.Tipo, m.Origem, m.CorrigidaEmUtc)))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AtestadoValidadoDoPonto>> AtestadosValidadosAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly de, DateOnly ate, CancellationToken ct)
    {
        var ids = funcionarioIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await db.Atestados.AsNoTracking()
            .Where(a => a.Situacao == SituacaoDoAtestado.Validado && ids.Contains(a.FuncionarioId) && a.DataInicio <= ate && a.DataFim >= de)
            .Select(a => new AtestadoValidadoDoPonto(a.FuncionarioId, a.DataInicio, a.DataFim, a.HoraInicio, a.HoraFim))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<PedidoPendenteDoDia>> PedidosPendentesAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct)
    {
        var ids = funcionarioIds.ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        return await (
            from pedido in db.PedidosDeAjuste.AsNoTracking()
            join ponto in db.PontosDosDias.AsNoTracking() on pedido.PontoId equals ponto.Id
            where pedido.Status == StatusDoPedidoDeAjuste.Pendente && ids.Contains(pedido.FuncionarioId)
            select new PedidoPendenteDoDia(pedido.FuncionarioId, ponto.Dia, pedido.Id, pedido.GestorId != null && pedido.ResultadoDoGestor == null, pedido.GestorId))
            .ToListAsync(ct);
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// As leituras de férias. Cada pedido sai com as pessoas já identificadas (a do pedido, a do gestor da etapa e a do RH que decidiu) em UMA consulta de pessoas por lote, nunca uma por pedido. Só lê: as gravações são do <see cref="FeriasRepositorio"/>.
/// </summary>
public sealed class ConsultaDeFerias(AppDbContext db) : IConsultaDeFerias
{
    public async Task<PessoaParaFerias?> PessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => f.Id == funcionarioId)
            .Select(f => new PessoaParaFerias(f.Id, f.Nome, f.Ativo, f.DataAdmissao, f.DataDesligamento))
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<FeriasLida>> DaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await CarregarAsync(db.PedidosDeFerias.AsNoTracking().Where(f => f.FuncionarioId == funcionarioId), ct);

    public async Task<FeriasLida?> ObterDaPessoaAsync(int feriasId, int funcionarioId, CancellationToken ct) =>
        (await CarregarAsync(db.PedidosDeFerias.AsNoTracking().Where(f => f.Id == feriasId && f.FuncionarioId == funcionarioId), ct)).FirstOrDefault();

    public async Task<FuncionarioResumo?> ResumoDoGestorAsync(int gestorId, CancellationToken ct) =>
        await (
            from g in db.Gestores.AsNoTracking()
            join f in db.Funcionarios.AsNoTracking() on g.FuncionarioId equals f.Id
            where g.Id == gestorId
            select new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .FirstOrDefaultAsync(ct);

    private async Task<IReadOnlyList<FeriasLida>> CarregarAsync(IQueryable<PedidoDeFerias> consulta, CancellationToken ct)
    {
        var pedidos = await consulta.ToListAsync(ct);
        if (pedidos.Count == 0)
        {
            return [];
        }

        var gestorIds = pedidos.Where(p => p.GestorId != null).Select(p => p.GestorId!.Value).Distinct().ToList();
        var pessoaDoGestor = await db.Gestores.AsNoTracking().Where(g => gestorIds.Contains(g.Id)).ToDictionaryAsync(g => g.Id, g => g.FuncionarioId, ct);
        var pessoaIds = pedidos.Select(p => p.FuncionarioId)
            .Concat(pedidos.Where(p => p.FuncionarioRhId != null).Select(p => p.FuncionarioRhId!.Value))
            .Concat(pessoaDoGestor.Values)
            .Distinct()
            .ToList();
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(f => pessoaIds.Contains(f.Id))
            .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .ToDictionaryAsync(r => r.Id, ct);

        FuncionarioResumo? Pessoa(int? id) => id is { } valor ? pessoas.GetValueOrDefault(valor) : null;

        return [.. pedidos.Select(p => new FeriasLida(
            p.Id,
            pessoas[p.FuncionarioId],
            p.InicioDoPeriodoAquisitivo,
            p.FimDoPeriodoAquisitivo,
            p.DataInicio,
            p.DataFim,
            p.AbonoPecuniario,
            p.DiasAbono,
            p.Adiantar13Salario,
            p.Status,
            p.SolicitadaEmUtc,
            p.Observacao,
            p.GestorId,
            p.GestorId is { } g && pessoaDoGestor.TryGetValue(g, out var pessoaId) ? pessoas.GetValueOrDefault(pessoaId) : null,
            p.ResultadoDoGestor,
            p.EtapaDoGestorEmUtc,
            p.EtapaDoGestorAutomatica,
            Pessoa(p.FuncionarioRhId),
            p.ConfirmadaPeloRhEmUtc,
            p.SugestaoDataInicio,
            p.SugestaoDataFim,
            p.RespostaDaSugestao,
            p.RespostaDaSugestaoEmUtc,
            p.MotivoRecusa))];
    }
}

using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ferias.Regras;
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

    public async Task<(IReadOnlyList<FeriasLida> Itens, int Total)> ListarDaEquipeAsync(EquipeDoGestor equipe, SituacaoDasFerias situacao, ParametrosDeLista parametros, CancellationToken ct)
    {
        var filtrada = DaEquipe(equipe).Where(NaSituacao(situacao));
        var total = await filtrada.CountAsync(ct);
        var ids = await filtrada.OrderBy(p => p.SolicitadaEmUtc).ThenBy(p => p.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return ([], total);
        }

        var lidos = await CarregarAsync(db.PedidosDeFerias.AsNoTracking().Where(p => ids.Contains(p.Id)), ct);
        return ([.. ids.Select(id => lidos.Single(l => l.Id == id))], total);
    }

    public async Task<FeriasLida?> ObterDaEquipeAsync(int feriasId, EquipeDoGestor equipe, CancellationToken ct) =>
        (await CarregarAsync(DaEquipe(equipe).Where(p => p.Id == feriasId), ct)).FirstOrDefault();

    public async Task<IReadOnlyDictionary<int, ContextoDoPedidoDaEquipe>> ContextoDaEquipeAsync(IReadOnlyList<FeriasLida> pedidos, CancellationToken ct)
    {
        if (pedidos.Count == 0)
        {
            return new Dictionary<int, ContextoDoPedidoDaEquipe>();
        }

        var pessoaIds = pedidos.Select(p => p.Funcionario.Id).Distinct().ToList();
        var setorDaPessoa = await db.Funcionarios.AsNoTracking().Where(f => pessoaIds.Contains(f.Id)).Select(f => new { f.Id, f.SetorId }).ToDictionaryAsync(f => f.Id, f => f.SetorId, ct);
        var setores = setorDaPessoa.Values.Where(s => s != null).Select(s => s!.Value).Distinct().ToList();
        var membros = await db.Funcionarios.AsNoTracking()
            .Where(f => f.SetorId != null && setores.Contains(f.SetorId.Value) && f.Ativo)
            .Select(f => new { f.SetorId, f.Id, f.Nome, f.Ativo, Cargo = f.Cargo != null ? f.Cargo.Nome : null, Setor = f.Setor != null ? f.Setor.Nome : null, f.DataAdmissao })
            .ToListAsync(ct);
        var membroIds = membros.Select(m => m.Id).ToList();
        var de = pedidos.Min(p => p.DataInicio);
        var ate = pedidos.Max(p => p.DataFim);
        var aprovadas = await db.PedidosDeFerias.AsNoTracking()
            .Where(p => membroIds.Contains(p.FuncionarioId) && (p.Status == StatusDasFerias.Aprovada || p.Status == StatusDasFerias.EmGozo || p.Status == StatusDasFerias.Concluida) && p.DataInicio <= ate && p.DataFim >= de)
            .Select(p => new FeriasAprovadaDoSetor(p.FuncionarioId, p.DataInicio, p.DataFim))
            .ToListAsync(ct);

        return pedidos.ToDictionary(
            p => p.Id,
            p =>
            {
                var setor = setorDaPessoa.GetValueOrDefault(p.Funcionario.Id);
                var doSetor = membros.Where(m => m.SetorId == setor).Select(m => new MembroDoSetor(m.Id, new FuncionarioResumo(m.Id, m.Nome, m.Ativo, m.Cargo, m.Setor), m.DataAdmissao)).ToList();
                return ConflitosDeFerias.Calcular(p.Funcionario.Id, p.DataInicio, p.DataFim, p.Status == StatusDasFerias.Solicitada, setor, doSetor, aprovadas);
            });
    }

    public async Task<(IReadOnlyList<FeriasLida> Itens, int Total)> ListarParaRhAsync(SituacaoDasFerias situacao, int? setorId, ParametrosDeLista parametros, CancellationToken ct)
    {
        var filtrada = db.PedidosDeFerias.AsNoTracking().Where(NaSituacao(situacao));
        if (setorId is { } setor)
        {
            filtrada = filtrada.Where(p => db.Funcionarios.Any(f => f.Id == p.FuncionarioId && f.SetorId == setor));
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            filtrada = filtrada.Where(p => db.Funcionarios.Any(f => f.Id == p.FuncionarioId && EF.Functions.Like(f.Nome, like)));
        }

        var total = await filtrada.CountAsync(ct);
        var ids = await filtrada.OrderBy(p => p.SolicitadaEmUtc).ThenBy(p => p.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(p => p.Id).ToListAsync(ct);
        if (ids.Count == 0)
        {
            return ([], total);
        }

        var lidos = await CarregarAsync(db.PedidosDeFerias.AsNoTracking().Where(p => ids.Contains(p.Id)), ct);
        return ([.. ids.Select(id => lidos.Single(l => l.Id == id))], total);
    }

    public async Task<FeriasLida?> ObterAsync(int feriasId, CancellationToken ct) =>
        (await CarregarAsync(db.PedidosDeFerias.AsNoTracking().Where(p => p.Id == feriasId), ct)).FirstOrDefault();

    public async Task<int?> GestorIdDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Gestores.AsNoTracking().Where(g => g.FuncionarioId == funcionarioId).Select(g => (int?)g.Id).FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<FeriasLida>> DaEquipeNaJanelaAsync(EquipeDoGestor equipe, DateOnly de, DateOnly ate, CancellationToken ct) =>
        await CarregarAsync(
            db.PedidosDeFerias.AsNoTracking().Where(p => db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(p.FuncionarioId)
                && p.Status != StatusDasFerias.Rejeitada && p.Status != StatusDasFerias.Cancelada && p.DataInicio <= ate && p.DataFim >= de),
            ct);

    public async Task<FuncionarioResumo?> ResumoAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => f.Id == funcionarioId)
            .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .FirstOrDefaultAsync(ct);

    public async Task<bool> EhDaEquipeAsync(int funcionarioId, EquipeDoGestor equipe, CancellationToken ct) =>
        await db.Funcionarios.DaEquipe(equipe).AnyAsync(f => f.Id == funcionarioId, ct);

    public async Task<string?> EmailCorporativoAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => f.EmailCorporativo).FirstOrDefaultAsync(ct);

    /// <summary>Os pedidos com o <c>ID_GESTOR</c> do gestor e de pessoas da equipe de hoje (sem ele e sem outros gestores): o resto é o mesmo 404.</summary>
    private IQueryable<PedidoDeFerias> DaEquipe(EquipeDoGestor equipe)
    {
        var gestorId = equipe.GestorId;
        return db.PedidosDeFerias.AsNoTracking().Where(p => p.GestorId == gestorId && db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(p.FuncionarioId));
    }

    /// <summary>A situação do pedido nas colunas (a mesma tabela de <see cref="SituacaoDoPedidoDeFerias"/>), para filtrar no banco.</summary>
    internal static Expression<Func<PedidoDeFerias, bool>> NaSituacao(SituacaoDasFerias situacao) => situacao switch
    {
        SituacaoDasFerias.ComOGestor => p => p.Status == StatusDasFerias.Solicitada && p.GestorId != null && p.ResultadoDoGestor == null,
        SituacaoDasFerias.SugestaoDeData => p => p.Status == StatusDasFerias.Solicitada && p.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && p.RespostaDaSugestao == null,
        SituacaoDasFerias.ComORh => p => p.Status == StatusDasFerias.Solicitada
            && ((p.ResultadoDoGestor == null && p.GestorId == null) || p.ResultadoDoGestor == ResultadoDoGestorNasFerias.Aprovou || (p.ResultadoDoGestor == ResultadoDoGestorNasFerias.SugeriuData && p.RespostaDaSugestao != null)),
        SituacaoDasFerias.Aprovada => p => p.Status == StatusDasFerias.Aprovada,
        SituacaoDasFerias.EmGozo => p => p.Status == StatusDasFerias.EmGozo,
        SituacaoDasFerias.Concluida => p => p.Status == StatusDasFerias.Concluida,
        SituacaoDasFerias.Rejeitada => p => p.Status == StatusDasFerias.Rejeitada,
        _ => p => p.Status == StatusDasFerias.Cancelada,
    };

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

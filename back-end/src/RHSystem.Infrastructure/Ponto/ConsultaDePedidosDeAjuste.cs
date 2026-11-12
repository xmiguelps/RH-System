using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// As leituras dos pedidos de ajuste. O escopo sai na PRIMEIRA consulta (<see cref="EscopoDePedidos"/>): a pessoa vê os dela, o gestor os da equipe que têm o <c>ID_GESTOR</c> dele, o RH todos. A página sai em dois passos (os ids, na ordem pedida, e depois os
/// pedidos completos) para ordenar por uma coluna que está em outra tabela (o dia do ponto) sem trazer o que não vai para a página.
/// </summary>
public sealed class ConsultaDePedidosDeAjuste(AppDbContext db) : IConsultaDePedidosDeAjuste
{
    private sealed class LinhaDoPedido
    {
        public int Id { get; init; }

        public DateOnly Dia { get; init; }

        public DateTime? Solicitado { get; init; }
    }

    private static readonly MapaDeOrdenacao<LinhaDoPedido> Ordem = new MapaDeOrdenacao<LinhaDoPedido>("CRIADO_EM", DirecaoDeOrdenacao.Desc, l => l.Id)
        .Por("CRIADO_EM", l => l.Solicitado)
        .Por("DIA_DO_PONTO", l => l.Dia);

    public async Task<PedidoLido?> ObterAsync(int pedidoId, EscopoDePedidos escopo, CancellationToken ct)
    {
        var existe = await NoEscopo(escopo).Where(p => p.Id == pedidoId).Select(p => p.Id).AnyAsync(ct);
        return existe ? (await CarregarAsync([pedidoId], ct)).Single() : null;
    }

    public async Task<PaginaDePedidos> ListarAsync(EscopoDePedidos escopo, FiltroDePedidos filtro, ParametrosDeLista parametros, CancellationToken ct)
    {
        var doEscopo = NoEscopo(escopo);
        var resumo = escopo.Papel == PapelNaConsulta.PropriaPessoa
            ? new PontoPedidoFilaResumo(0, 0)
            : new PontoPedidoFilaResumo(await doEscopo.CountAsync(NaEtapa(EtapaDoPedido.Gestor), ct), await doEscopo.CountAsync(NaEtapa(EtapaDoPedido.Rh), ct));

        var filtrada = doEscopo;
        if (filtro.Status is { } status)
        {
            filtrada = filtrada.Where(p => p.Status == status);
        }

        if (filtro.Etapa is { } etapa)
        {
            filtrada = filtrada.Where(NaEtapa(etapa));
        }

        if (filtro.FuncionarioId is { } funcionarioId)
        {
            filtrada = filtrada.Where(p => p.FuncionarioId == funcionarioId);
        }

        var linhas = from p in filtrada
                     join ponto in db.PontosDosDias on p.PontoId equals ponto.Id
                     select new LinhaDoPedido { Id = p.Id, Dia = ponto.Dia, Solicitado = p.SolicitadoEmUtc };
        var total = await linhas.CountAsync(ct);
        var ids = await Ordem.Aplicar(linhas, parametros with { Direcao = parametros.Direcao ?? DirecaoDeOrdenacao.Desc }).Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(l => l.Id).ToListAsync(ct);
        return new PaginaDePedidos(ids.Count == 0 ? [] : await CarregarAsync(ids, ct), total, resumo);
    }

    private IQueryable<PedidoDeAjuste> NoEscopo(EscopoDePedidos escopo)
    {
        var pedidos = db.PedidosDeAjuste.AsNoTracking();
        switch (escopo.Papel)
        {
            case PapelNaConsulta.PropriaPessoa:
                var minhaId = escopo.FuncionarioId;
                return pedidos.Where(p => p.FuncionarioId == minhaId);
            case PapelNaConsulta.Gestor:
                if (escopo.Equipe is not { } equipe)
                {
                    return pedidos.Where(_ => false);
                }

                var gestorId = equipe.GestorId;
                return pedidos.Where(p => p.GestorId == gestorId && db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(p.FuncionarioId));
            default:
                return pedidos;
        }
    }

    /// <summary><c>PENDENTE</c> com gestor e sem resultado dele é a etapa do gestor; <c>PENDENTE</c> sem gestor ou com o gestor aprovado é a do RH; o resto está concluído.</summary>
    private static Expression<Func<PedidoDeAjuste, bool>> NaEtapa(EtapaDoPedido etapa) => etapa switch
    {
        EtapaDoPedido.Gestor => p => p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId != null && p.ResultadoDoGestor == null,
        EtapaDoPedido.Rh => p => p.Status == StatusDoPedidoDeAjuste.Pendente && (p.GestorId == null || p.ResultadoDoGestor != null),
        _ => p => p.Status != StatusDoPedidoDeAjuste.Pendente,
    };

    private async Task<IReadOnlyList<PedidoLido>> CarregarAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var pedidos = await db.PedidosDeAjuste.AsNoTracking().Include(p => p.Itens).Where(p => ids.Contains(p.Id)).ToListAsync(ct);
        var pontoIds = pedidos.Select(p => p.PontoId).Distinct().ToList();
        var dias = await db.PontosDosDias.AsNoTracking().Where(x => pontoIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, x => x.Dia, ct);

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

        var batidas = (await db.Marcacoes.AsNoTracking()
            .Where(m => pontoIds.Contains(m.PontoId))
            .OrderBy(m => m.InstanteUtc).ThenBy(m => m.Id)
            .Select(m => new { m.PontoId, Batida = new BatidaLida(m.Id, m.InstanteUtc, m.Tipo, m.Origem, m.CorrigidaEmUtc) })
            .ToListAsync(ct)).ToLookup(x => x.PontoId, x => x.Batida);

        FuncionarioResumo? Pessoa(int? id) => id is { } valor ? pessoas.GetValueOrDefault(valor) : null;

        return [.. ids.Select(id => pedidos.Single(p => p.Id == id)).Select(p => new PedidoLido(
            p.Id,
            pessoas[p.FuncionarioId],
            dias[p.PontoId],
            p.SolicitadoEmUtc,
            p.Justificativa,
            p.Status,
            p.GestorId,
            p.GestorId is { } g && pessoaDoGestor.TryGetValue(g, out var pessoaId) ? pessoas[pessoaId] : null,
            p.Observacao,
            p.ResultadoDoGestor,
            p.EtapaDoGestorEmUtc,
            p.EtapaDoGestorAutomatica,
            Pessoa(p.FuncionarioRhId),
            p.ConfirmadoPeloRhEmUtc,
            [.. p.Itens.Select(i => new ItemLido(i.Id, i.MarcacaoId, i.Tipo, i.InstanteProposto))],
            [.. batidas[p.PontoId]]))];
    }
}

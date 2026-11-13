using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Beneficios;

/// <summary>
/// As leituras da tela "Benefícios" da própria pessoa: o catálogo, as linhas dela e os dependentes e trajetos delas, cada um em UMA consulta (nunca uma por adesão). Só lê: as gravações são do <see cref="AdesoesRepositorio"/>.
/// </summary>
public sealed class ConsultaDeAdesoes(AppDbContext db) : IConsultaDeAdesoes
{
    public async Task<IReadOnlyList<ItemDoCatalogo>> CatalogoAsync(CancellationToken ct) =>
        await db.Beneficios.AsNoTracking()
            .OrderBy(b => b.Id)
            .Select(b => new ItemDoCatalogo(b.Id, b.Nome, b.Descricao, b.Opcional, b.AceitaDependentes, b.AceitaTrajeto))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AdesaoDaPessoa>> AdesoesDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.BeneficiosDosFuncionarios.AsNoTracking()
            .Where(l => l.FuncionarioId == funcionarioId)
            .OrderBy(l => l.Id)
            .Select(l => new AdesaoDaPessoa(l.Id, l.BeneficioId, l.Status, l.Ativo, l.DataInicioDesejada, l.AutorizaDescontoNaFolha, l.MotivoDaRecusa, l.RespondidoEmUtc, l.PedidoEmUtc))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<DependenteDaPessoa>> DependentesDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await (from d in db.Dependentes.AsNoTracking()
               join l in db.BeneficiosDosFuncionarios.AsNoTracking() on d.AdesaoId equals l.Id
               where l.FuncionarioId == funcionarioId
               orderby d.Id
               select new DependenteDaPessoa(d.Id, d.AdesaoId, d.Nome, d.Parentesco, d.DataDeNascimento, d.Status, d.PedidoEmUtc, d.RespondidoEmUtc, d.MotivoDaRecusa))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TrajetoDaPessoa>> TrajetosDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await (from t in db.TrajetosDeValeTransporte.AsNoTracking()
               join l in db.BeneficiosDosFuncionarios.AsNoTracking() on t.AdesaoId equals l.Id
               where l.FuncionarioId == funcionarioId
               orderby t.Id
               select new TrajetoDaPessoa(t.Id, t.AdesaoId, t.Origem, t.Destino, t.LinhasEMeios, t.Status, t.PedidoEmUtc, t.RespondidoEmUtc, t.MotivoDaRecusa))
            .ToListAsync(ct);

    public async Task<(IReadOnlyList<AdesaoLidaPeloRh> Itens, int Total)> ListarParaORhAsync(StatusDoPedidoDeBeneficio situacao, int? beneficioId, ParametrosDeLista parametros, CancellationToken ct)
    {
        var filtrada = db.BeneficiosDosFuncionarios.AsNoTracking().AsQueryable();
        if (beneficioId is { } beneficio)
        {
            filtrada = filtrada.Where(l => l.BeneficioId == beneficio);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            filtrada = filtrada.Where(l => db.Funcionarios.Any(f => f.Id == l.FuncionarioId && EF.Functions.Like(f.Nome, like)));
        }

        List<int> ids;
        int total;
        if (situacao == StatusDoPedidoDeBeneficio.Pendente)
        {
            // A fila do RH é curta (só o que espera resposta): ordena em memória pela MENOR data pendente (a da adesão, se pendente, ou a do dependente ou trajeto pendente mais antigo).
            filtrada = filtrada.Where(l => l.Status == StatusDoPedidoDeBeneficio.Pendente
                || (l.Status == StatusDoPedidoDeBeneficio.Aprovado
                    && (db.Dependentes.Any(d => d.AdesaoId == l.Id && d.Status == StatusDoPedidoDeBeneficio.Pendente)
                        || db.TrajetosDeValeTransporte.Any(t => t.AdesaoId == l.Id && t.Status == StatusDoPedidoDeBeneficio.Pendente))));
            var fila = await filtrada.Select(l => new { l.Id, l.Status, l.PedidoEmUtc }).ToListAsync(ct);
            total = fila.Count;
            var idsDaFila = fila.Select(l => l.Id).ToList();
            var dependentes = await db.Dependentes.AsNoTracking().Where(d => idsDaFila.Contains(d.AdesaoId) && d.Status == StatusDoPedidoDeBeneficio.Pendente).Select(d => new { d.AdesaoId, d.PedidoEmUtc }).ToListAsync(ct);
            var trajetos = await db.TrajetosDeValeTransporte.AsNoTracking().Where(t => idsDaFila.Contains(t.AdesaoId) && t.Status == StatusDoPedidoDeBeneficio.Pendente).Select(t => new { t.AdesaoId, t.PedidoEmUtc }).ToListAsync(ct);
            var menorDosFilhos = dependentes.Concat(trajetos).GroupBy(f => f.AdesaoId).ToDictionary(g => g.Key, g => g.Min(f => f.PedidoEmUtc));
            ids = [.. fila
                .Select(l => new { l.Id, Chave = MenorData(l.Status == StatusDoPedidoDeBeneficio.Pendente ? l.PedidoEmUtc : null, menorDosFilhos.TryGetValue(l.Id, out var filho) ? filho : null) })
                .OrderBy(l => l.Chave)
                .ThenBy(l => l.Id)
                .Skip(parametros.Pular)
                .Take(parametros.TamanhoPagina)
                .Select(l => l.Id)];
        }
        else
        {
            filtrada = filtrada.Where(l => l.Status == situacao);
            total = await filtrada.CountAsync(ct);
            ids = await filtrada.OrderBy(l => l.PedidoEmUtc == null).ThenBy(l => l.PedidoEmUtc).ThenBy(l => l.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(l => l.Id).ToListAsync(ct);
        }

        if (ids.Count == 0)
        {
            return ([], total);
        }

        var lidas = await CarregarParaORhAsync(ids, ct);
        return ([.. ids.Select(id => lidas.Single(l => l.AdesaoId == id))], total);
    }

    public async Task<AdesaoLidaPeloRh?> ObterParaORhAsync(int adesaoId, CancellationToken ct) =>
        (await CarregarParaORhAsync([adesaoId], ct)).FirstOrDefault();

    /// <summary>A menor das duas datas (a que for nula não conta); sem nenhuma, <c>MaxValue</c> (vai para o fim da fila).</summary>
    private static DateTime MenorData(DateTime? a, DateTime? b) => a is null ? b ?? DateTime.MaxValue : b is null ? a.Value : a < b ? a.Value : b.Value;

    /// <summary>Tudo o que o RH vê das adesões pedidas, em poucas consultas por lote (as adesões, as pessoas, o catálogo, os dependentes e os trajetos), nunca uma por adesão.</summary>
    private async Task<List<AdesaoLidaPeloRh>> CarregarParaORhAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var linhas = await db.BeneficiosDosFuncionarios.AsNoTracking()
            .Where(l => ids.Contains(l.Id))
            .Select(l => new { l.Id, l.FuncionarioId, l.BeneficioId, l.Status, l.Ativo, l.PedidoEmUtc, l.DataInicioDesejada, l.AutorizaDescontoNaFolha, l.FuncionarioRhId, l.RespondidoEmUtc, l.MotivoDaRecusa })
            .ToListAsync(ct);
        var pessoaIds = linhas.Select(l => l.FuncionarioId).Concat(linhas.Where(l => l.FuncionarioRhId != null).Select(l => l.FuncionarioRhId!.Value)).Distinct().ToList();
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(f => pessoaIds.Contains(f.Id))
            .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .ToDictionaryAsync(f => f.Id, ct);
        var beneficioIds = linhas.Select(l => l.BeneficioId).Distinct().ToList();
        var catalogo = await db.Beneficios.AsNoTracking()
            .Where(b => beneficioIds.Contains(b.Id))
            .Select(b => new ItemDoCatalogo(b.Id, b.Nome, b.Descricao, b.Opcional, b.AceitaDependentes, b.AceitaTrajeto))
            .ToDictionaryAsync(b => b.Id, ct);
        var dependentes = (await db.Dependentes.AsNoTracking()
            .Where(d => ids.Contains(d.AdesaoId))
            .OrderBy(d => d.Id)
            .Select(d => new DependenteDaPessoa(d.Id, d.AdesaoId, d.Nome, d.Parentesco, d.DataDeNascimento, d.Status, d.PedidoEmUtc, d.RespondidoEmUtc, d.MotivoDaRecusa))
            .ToListAsync(ct)).ToLookup(d => d.AdesaoId);
        var trajetos = (await db.TrajetosDeValeTransporte.AsNoTracking()
            .Where(t => ids.Contains(t.AdesaoId))
            .OrderBy(t => t.Id)
            .Select(t => new TrajetoDaPessoa(t.Id, t.AdesaoId, t.Origem, t.Destino, t.LinhasEMeios, t.Status, t.PedidoEmUtc, t.RespondidoEmUtc, t.MotivoDaRecusa))
            .ToListAsync(ct)).ToLookup(t => t.AdesaoId);

        return
        [
            .. linhas.Select(l => new AdesaoLidaPeloRh(
                l.Id,
                pessoas[l.FuncionarioId],
                catalogo[l.BeneficioId],
                l.Status,
                l.Ativo,
                l.PedidoEmUtc,
                l.DataInicioDesejada,
                l.AutorizaDescontoNaFolha,
                l.FuncionarioRhId is { } rh ? pessoas[rh] : null,
                l.RespondidoEmUtc,
                l.MotivoDaRecusa,
                [.. dependentes[l.Id]],
                [.. trajetos[l.Id]])),
        ];
    }
}

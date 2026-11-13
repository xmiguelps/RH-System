using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Beneficios.Adesoes.Regras;
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
}

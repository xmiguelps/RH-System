using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comunicacao.Avisos;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>As quatro situações do aviso como filtro de banco (as mesmas de <c>RegraDaSituacaoDoAviso</c>, que calcula a do item): rascunho, agendado, ativo e expirado, todas pelo <c>agora</c> da aplicação.</summary>
internal static class AvisosPorSituacao
{
    public static IQueryable<Aviso> NaSituacao(this IQueryable<Aviso> avisos, SituacaoDoAviso situacao, DateTime agoraUtc) => situacao switch
    {
        SituacaoDoAviso.Rascunho => avisos.Where(a => !a.Enviado),
        SituacaoDoAviso.Agendado => avisos.Where(a => a.Enviado && a.PublicacaoEmUtc > agoraUtc),
        SituacaoDoAviso.Ativo => avisos.Where(a => a.Enviado && (a.PublicacaoEmUtc == null || a.PublicacaoEmUtc <= agoraUtc) && (a.ExpiracaoEmUtc == null || a.ExpiracaoEmUtc > agoraUtc)),
        _ => avisos.Where(a => a.Enviado && (a.PublicacaoEmUtc == null || a.PublicacaoEmUtc <= agoraUtc) && a.ExpiracaoEmUtc != null && a.ExpiracaoEmUtc <= agoraUtc),
    };
}

/// <summary>
/// A leitura dos avisos para o RH. A contagem de leitura (<c>Viram</c> de <c>Total</c>) olha só as linhas de pessoas <b>ativas hoje</b> (decisão 2 do plano da fatia 4): a linha de quem saiu fica no banco e fora da conta.
/// </summary>
public sealed class ConsultaDeAvisos(AppDbContext db) : IConsultaDeAvisos
{
    private sealed record Base(int Id, TipoDeAviso? Tipo, string Titulo, string Conteudo, DateTime? PublicacaoEmUtc, DateTime? ExpiracaoEmUtc, bool Fixado, bool Enviado, int? AutorId);

    public async Task<(IReadOnlyList<AvisoLido> Itens, int Total)> ListarAsync(FiltroDeAvisosRh filtro, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct)
    {
        var avisos = db.Avisos.AsNoTracking().NaSituacao(filtro.Situacao, agoraUtc);
        if (filtro.Tipo is { } tipo)
        {
            avisos = avisos.Where(a => a.Tipo == tipo);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            avisos = avisos.Where(a => EF.Functions.Like(a.Titulo, like) || EF.Functions.Like(a.Conteudo, like));
        }

        var total = await avisos.CountAsync(ct);
        var ordenados = filtro.Situacao switch
        {
            SituacaoDoAviso.Agendado => avisos.OrderBy(a => a.PublicacaoEmUtc).ThenBy(a => a.Id),
            SituacaoDoAviso.Rascunho => avisos.OrderByDescending(a => a.Id),
            _ => avisos.OrderByDescending(a => a.PublicacaoEmUtc).ThenByDescending(a => a.Id),
        };
        var pagina = await ordenados.Skip(parametros.Pular).Take(parametros.TamanhoPagina)
            .Select(a => new Base(a.Id, a.Tipo, a.Titulo, a.Conteudo, a.PublicacaoEmUtc, a.ExpiracaoEmUtc, a.Fixado, a.Enviado, a.AutorId))
            .ToListAsync(ct);
        return (await CompletarAsync(pagina, ct), total);
    }

    public async Task<AvisoLido?> ObterAsync(int avisoId, CancellationToken ct)
    {
        var aviso = await db.Avisos.AsNoTracking()
            .Where(a => a.Id == avisoId)
            .Select(a => new Base(a.Id, a.Tipo, a.Titulo, a.Conteudo, a.PublicacaoEmUtc, a.ExpiracaoEmUtc, a.Fixado, a.Enviado, a.AutorId))
            .FirstOrDefaultAsync(ct);
        return aviso is null ? null : (await CompletarAsync([aviso], ct))[0];
    }

    public async Task<ResumoDosAvisos> ResumoAsync(DateTime agoraUtc, CancellationToken ct)
    {
        var avisos = db.Avisos.AsNoTracking();
        var ativos = await avisos.NaSituacao(SituacaoDoAviso.Ativo, agoraUtc).Select(a => new { a.Id, a.Titulo, a.ExpiracaoEmUtc }).ToListAsync(ct);
        var agendados = await avisos.NaSituacao(SituacaoDoAviso.Agendado, agoraUtc).CountAsync(ct);
        var expirados = await avisos.NaSituacao(SituacaoDoAviso.Expirado, agoraUtc).CountAsync(ct);
        var rascunhos = await avisos.NaSituacao(SituacaoDoAviso.Rascunho, agoraUtc).CountAsync(ct);

        var leituras = await ContarLeiturasAsync([.. ativos.Select(a => a.Id)], ct);
        var proporcoes = ativos.Where(a => leituras.TryGetValue(a.Id, out var c) && c.Total > 0).Select(a => (double)leituras[a.Id].Viram / leituras[a.Id].Total).ToList();
        int? media = proporcoes.Count == 0 ? null : (int)Math.Round(proporcoes.Average() * 100d, MidpointRounding.AwayFromZero);

        var limite = agoraUtc.AddDays(7);
        var expiram = ativos.Where(a => a.ExpiracaoEmUtc != null && a.ExpiracaoEmUtc <= limite).OrderBy(a => a.ExpiracaoEmUtc).ThenBy(a => a.Id).ToList();
        var proximo = expiram.Count == 0 ? null : new AvisoProximoAExpirar(expiram[0].Id, expiram[0].Titulo, expiram[0].ExpiracaoEmUtc!.Value);
        return new ResumoDosAvisos(ativos.Count, agendados, expirados, rascunhos, media, expiram.Count, proximo);
    }

    public async Task<(IReadOnlyList<AvisoLeituraItem> Itens, int Total, int Viram, int AindaNaoViram)> ListarLeiturasAsync(int avisoId, FiltroDeLeitura? filtro, ParametrosDeLista parametros, CancellationToken ct)
    {
        var linhas = from l in db.LeiturasDeAviso.AsNoTracking()
                     where l.AvisoId == avisoId
                     join f in db.Funcionarios.AsNoTracking() on l.FuncionarioId equals f.Id
                     where f.Ativo
                     select new { l.Visualizado, l.VisualizadoEmUtc, f.Id, f.Nome, f.Ativo, Cargo = f.Cargo != null ? f.Cargo.Nome : null, Setor = f.Setor != null ? f.Setor.Nome : null };

        var viram = await linhas.CountAsync(l => l.Visualizado, ct);
        var total = await linhas.CountAsync(ct);
        var pedidas = filtro switch
        {
            FiltroDeLeitura.Lido => linhas.Where(l => l.Visualizado),
            FiltroDeLeitura.NaoLido => linhas.Where(l => !l.Visualizado),
            _ => linhas,
        };
        var totalDaLista = filtro is null ? total : filtro == FiltroDeLeitura.Lido ? viram : total - viram;
        var pagina = await pedidas.OrderBy(l => l.Nome).ThenBy(l => l.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina).ToListAsync(ct);
        return ([.. pagina.Select(l => new AvisoLeituraItem(new FuncionarioResumo(l.Id, l.Nome, l.Ativo, l.Cargo, l.Setor), l.Visualizado, l.VisualizadoEmUtc))], totalDaLista, viram, total - viram);
    }

    public async Task<IReadOnlyList<DestinatarioDeAviso>> NaoLidosParaLembreteAsync(int avisoId, DateOnly hoje, CancellationToken ct)
    {
        var pessoas = await (from l in db.LeiturasDeAviso.AsNoTracking()
                             where l.AvisoId == avisoId && !l.Visualizado
                             join f in db.Funcionarios.AsNoTracking() on l.FuncionarioId equals f.Id
                             where f.Ativo && f.EmailCorporativo != null && f.DataAdmissao <= hoje
                             orderby f.Id
                             select new { f.Id, f.Nome, f.EmailCorporativo }).ToListAsync(ct);
        return [.. pessoas.Select(p => new DestinatarioDeAviso(p.Id, p.Nome, p.EmailCorporativo!))];
    }

    private async Task<IReadOnlyList<AvisoLido>> CompletarAsync(IReadOnlyList<Base> bases, CancellationToken ct)
    {
        var autores = bases.Where(b => b.AutorId is not null).Select(b => b.AutorId!.Value).Distinct().ToList();
        var nomes = autores.Count == 0 ? [] : await db.Funcionarios.AsNoTracking().Where(f => autores.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        var leituras = await ContarLeiturasAsync([.. bases.Select(b => b.Id)], ct);
        return [.. bases.Select(b =>
        {
            leituras.TryGetValue(b.Id, out var contagem);
            return new AvisoLido(b.Id, b.Tipo, b.Titulo, b.Conteudo, b.PublicacaoEmUtc, b.ExpiracaoEmUtc, b.Fixado, b.Enviado, b.AutorId is { } autor && nomes.TryGetValue(autor, out var nome) ? nome : null, contagem.Viram, contagem.Total);
        })];
    }

    /// <summary>Por aviso, quantas pessoas ATIVAS têm linha de leitura (<c>Total</c>) e quantas delas já abriram (<c>Viram</c>).</summary>
    private async Task<Dictionary<int, (int Viram, int Total)>> ContarLeiturasAsync(List<int> avisoIds, CancellationToken ct)
    {
        if (avisoIds.Count == 0)
        {
            return [];
        }

        var linhas = await db.LeiturasDeAviso.AsNoTracking()
            .Where(l => avisoIds.Contains(l.AvisoId) && db.Funcionarios.Any(f => f.Id == l.FuncionarioId && f.Ativo))
            .GroupBy(l => l.AvisoId)
            .Select(g => new { AvisoId = g.Key, Total = g.Count(), Viram = g.Count(l => l.Visualizado) })
            .ToListAsync(ct);
        return linhas.ToDictionary(l => l.AvisoId, l => (l.Viram, l.Total));
    }
}

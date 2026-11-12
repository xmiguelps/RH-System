using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// Os holerites de quem está logado. Visível é o holerite JÁ PUBLICADO (a data de publicação chegou, no instante exato) e que é a MAIOR versão publicada da sua competência e tipo: o agendado e a versão substituída não aparecem, e com a versão 2 ainda agendada a versão 1 continua à
/// vista. A regra é uma só, para a lista e para o download.
/// </summary>
public sealed class ConsultaDeHoleritesDaPessoa(AppDbContext db) : IConsultaDeHoleritesDaPessoa
{
    private IQueryable<Holerite> Publicados(int funcionarioId, DateTime agoraUtc) =>
        db.Holerites.AsNoTracking().Where(h => h.FuncionarioId == funcionarioId && h.PublicacaoEmUtc != null && h.PublicacaoEmUtc <= agoraUtc);

    private IQueryable<Holerite> Visiveis(int funcionarioId, DateTime agoraUtc)
    {
        var publicados = Publicados(funcionarioId, agoraUtc);
        return publicados.Where(h => h.Versao == publicados.Where(o => o.Ano == h.Ano && o.Mes == h.Mes && o.Tipo == h.Tipo).Max(o => o.Versao));
    }

    public async Task<ListaDeHoleritesDaPessoa> ListarAsync(int funcionarioId, int ano, TipoDeHolerite? tipo, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct)
    {
        var doAno = Visiveis(funcionarioId, agoraUtc).Where(h => h.Ano == ano);
        var tiposDoAno = await doAno.Select(h => h.Tipo).ToListAsync(ct);

        var pedidos = tipo is { } filtro ? doAno.Where(h => h.Tipo == filtro) : doAno;
        var total = await pedidos.CountAsync(ct);
        var pagina = await pedidos
            .OrderByDescending(h => h.Mes)
            .ThenByDescending(h => h.PublicacaoEmUtc)
            .ThenByDescending(h => h.Id)
            .Skip(parametros.Pular)
            .Take(parametros.TamanhoPagina)
            .Select(h => new { h.Id, h.Tipo, h.Mes, h.Ano, h.PublicacaoEmUtc })
            .ToListAsync(ct);

        var anos = await Publicados(funcionarioId, agoraUtc).Select(h => h.Ano).Distinct().OrderByDescending(a => a).ToListAsync(ct);
        return new ListaDeHoleritesDaPessoa([.. pagina.Select(h => new HoleriteItemEu(h.Id, h.Tipo, h.Mes, h.Ano, h.PublicacaoEmUtc!.Value))], total, anos, tiposDoAno);
    }

    public async Task<HoleriteParaDownload?> ObterParaDownloadAsync(int funcionarioId, int holeriteId, DateTime agoraUtc, CancellationToken ct)
    {
        var holerite = await Visiveis(funcionarioId, agoraUtc)
            .Where(h => h.Id == holeriteId && h.CaminhoArquivo != null)
            .Select(h => new { h.Id, h.Mes, h.Ano, h.Tipo, h.Versao, h.CaminhoArquivo })
            .FirstOrDefaultAsync(ct);
        if (holerite is null)
        {
            return null;
        }

        var nome = await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => f.Nome).FirstAsync(ct);
        return new HoleriteParaDownload(holerite.Id, nome, holerite.Mes, holerite.Ano, holerite.Tipo, holerite.Versao, holerite.CaminhoArquivo!);
    }
}

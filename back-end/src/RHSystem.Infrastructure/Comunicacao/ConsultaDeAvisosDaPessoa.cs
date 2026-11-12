using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Avisos;
using RHSystem.Application.Escopo;
using RHSystem.Application.Painel;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// Os avisos vigentes para quem está logado, cada um com o lido DA PESSOA (a linha de leitura dela; sem linha é não lido, e quem foi admitido depois do aviso vê o aviso mesmo assim). Vigente é a situação ativa (<see cref="AvisosPorSituacao"/>):
/// publicado, já no ar e sem expirar.
/// </summary>
public sealed class ConsultaDeAvisosDaPessoa(AppDbContext db) : IConsultaDeAvisosDaPessoa
{
    /// <summary>A projeção por inicializador (e não por construtor): o EF compõe filtros e ordens sobre os membros dela.</summary>
    private sealed class Linha
    {
        public int Id { get; init; }

        public TipoDeAviso? Tipo { get; init; }

        public string Titulo { get; init; } = string.Empty;

        public string Conteudo { get; init; } = string.Empty;

        public bool Fixado { get; init; }

        public DateTime? PublicacaoEmUtc { get; init; }

        public DateTime? ExpiracaoEmUtc { get; init; }

        public int? AutorId { get; init; }

        public bool Lido { get; init; }

        public DateTime? LidoEmUtc { get; init; }
    }

    private IQueryable<Linha> Vigentes(int funcionarioId, DateTime agoraUtc) =>
        from a in db.Avisos.AsNoTracking().NaSituacao(SituacaoDoAviso.Ativo, agoraUtc)
        from l in db.LeiturasDeAviso.AsNoTracking().Where(x => x.AvisoId == a.Id && x.FuncionarioId == funcionarioId).DefaultIfEmpty()
        select new Linha
        {
            Id = a.Id,
            Tipo = a.Tipo,
            Titulo = a.Titulo,
            Conteudo = a.Conteudo,
            Fixado = a.Fixado,
            PublicacaoEmUtc = a.PublicacaoEmUtc,
            ExpiracaoEmUtc = a.ExpiracaoEmUtc,
            AutorId = a.AutorId,
            Lido = l != null && l.Visualizado,
            LidoEmUtc = l != null ? l.VisualizadoEmUtc : null,
        };

    public async Task<(IReadOnlyList<AvisoDaPessoaLido> Itens, int Total, AvisoContagensEu Contagens)> ListarAsync(int funcionarioId, FiltroDeMeusAvisos filtro, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct)
    {
        var vigentes = Vigentes(funcionarioId, agoraUtc);
        var contagens = new AvisoContagensEu(await vigentes.CountAsync(a => !a.Lido, ct), await vigentes.CountAsync(ct));

        var pedidos = vigentes;
        if (filtro.ApenasNaoLidos)
        {
            pedidos = pedidos.Where(a => !a.Lido);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            pedidos = pedidos.Where(a => EF.Functions.Like(a.Titulo, like) || EF.Functions.Like(a.Conteudo, like));
        }

        var total = await pedidos.CountAsync(ct);
        var ordenados = filtro.FixadosPrimeiro
            ? pedidos.OrderByDescending(a => a.Fixado).ThenByDescending(a => a.PublicacaoEmUtc).ThenByDescending(a => a.Id)
            : pedidos.OrderByDescending(a => a.PublicacaoEmUtc).ThenByDescending(a => a.Id);
        var pagina = await ordenados.Skip(parametros.Pular).Take(parametros.TamanhoPagina).ToListAsync(ct);
        return (await CompletarAsync(pagina, agoraUtc, ct), total, contagens);
    }

    public async Task<AvisoDaPessoaLido?> ObterAsync(int funcionarioId, int avisoId, DateTime agoraUtc, CancellationToken ct)
    {
        var linha = await Vigentes(funcionarioId, agoraUtc).Where(a => a.Id == avisoId).FirstOrDefaultAsync(ct);
        return linha is null ? null : (await CompletarAsync([linha], agoraUtc, ct))[0];
    }

    public Task<int> ContarNaoLidosAsync(int funcionarioId, DateTime agoraUtc, CancellationToken ct) => Vigentes(funcionarioId, agoraUtc).CountAsync(a => !a.Lido, ct);

    private async Task<IReadOnlyList<AvisoDaPessoaLido>> CompletarAsync(IReadOnlyList<Linha> linhas, DateTime agoraUtc, CancellationToken ct)
    {
        var autores = linhas.Where(l => l.AutorId is not null).Select(l => l.AutorId!.Value).Distinct().ToList();
        var nomes = autores.Count == 0 ? [] : await db.Funcionarios.AsNoTracking().Where(f => autores.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Nome, ct);
        return [.. linhas.Select(l => new AvisoDaPessoaLido(l.Id, l.Tipo, l.Titulo, l.Conteudo, l.Fixado, l.PublicacaoEmUtc ?? agoraUtc, l.ExpiracaoEmUtc, l.AutorId is { } autor && nomes.TryGetValue(autor, out var nome) ? nome : null, l.Lido, l.LidoEmUtc))];
    }
}

/// <summary>A bolinha "Avisos" do menu: os avisos vigentes que a pessoa logada ainda não abriu (a mesma conta de <c>contagens.naoLidos</c> da lista; uma consulta de contagem só).</summary>
public sealed class FonteDeContadorDeAvisos(IConsultaDeAvisosDaPessoa consulta, IRelogio relogio) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => ContadorDoMenu.AvisosNaoLidos;

    public Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct) => consulta.ContarNaoLidosAsync(escopo.FuncionarioId, relogio.AgoraUtc, ct);
}

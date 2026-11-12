using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A leitura dos formulários para o RH. Lê as colunas, o autor, as perguntas (com as alternativas) e o público já escolhido (setores e pessoas das linhas de destinatário), cada um em uma consulta curta. Nada daqui devolve o caminho de um arquivo.
/// A situação é calculada por quem usa (<c>RegraDaSituacaoDoFormulario</c>), nunca no banco: o banco não fecha o formulário no prazo.
/// </summary>
public sealed class ConsultaDeFormularios(AppDbContext db) : IConsultaDeFormularios
{
    private sealed record Base(
        int Id,
        string Titulo,
        string? Descricao,
        TipoDeFormulario? Tipo,
        SentidoDaAvaliacao? Sentido,
        bool Anonimo,
        bool Ativo,
        DateTime? CriadoEmUtc,
        int? AutorId,
        AlvoDoFormulario? Alvo,
        DateTime? PublicadoEmUtc,
        DateTime? EncerradoEmUtc,
        DateOnly? Prazo,
        int? RecorrenciaIntervalo,
        UnidadeDeRecorrencia? RecorrenciaUnidade,
        DateOnly? RecorrenciaAte,
        DateTime? ProximaReaberturaEmUtc,
        bool LembreteAutomatico,
        string? TokenDoLink,
        bool TemRespostas);

    public async Task<FormularioLido?> ObterAsync(int formularioId, CancellationToken ct)
    {
        var f = await db.Formularios.AsNoTracking()
            .Where(x => x.Id == formularioId)
            .Select(x => new Base(
                x.Id,
                x.Titulo,
                x.Descricao,
                x.Tipo,
                x.SentidoAvaliacao,
                x.Anonimo,
                x.Ativo,
                x.CriadoEmUtc,
                x.AutorId,
                x.Alvo,
                x.PublicadoEmUtc,
                x.EncerradoEmUtc,
                x.Prazo,
                x.RecorrenciaIntervalo,
                x.RecorrenciaUnidade,
                x.RecorrenciaAte,
                x.ProximaReaberturaEmUtc,
                x.LembreteAutomatico,
                x.TokenDoLink,
                db.FormulariosRespostas.Any(r => r.FormularioId == x.Id)))
            .SingleOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }

        var autor = f.AutorId is { } autorId
            ? await db.Funcionarios.AsNoTracking().Where(p => p.Id == autorId).Select(p => new FuncionarioResumo(p.Id, p.Nome, p.Ativo, p.Cargo != null ? p.Cargo.Nome : null, p.Setor != null ? p.Setor.Nome : null)).SingleOrDefaultAsync(ct)
            : null;
        var perguntas = await PerguntasAsync(formularioId, ct);
        // uma pessoa ou um setor pode ter várias linhas de destinatário (os pares da avaliação): aqui só interessa quem está escolhido, uma vez
        var setores = await db.Setores.AsNoTracking()
            .Where(s => db.FormulariosDestinatarios.Any(d => d.FormularioId == formularioId && d.SetorId == s.Id))
            .OrderBy(s => s.Nome)
            .ThenBy(s => s.Id)
            .Select(s => new FormularioSetor(s.Id, s.Nome))
            .ToListAsync(ct);
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(p => db.FormulariosDestinatarios.Any(d => d.FormularioId == formularioId && d.FuncionarioId == p.Id))
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .Select(p => new FuncionarioResumo(p.Id, p.Nome, p.Ativo, p.Cargo != null ? p.Cargo.Nome : null, p.Setor != null ? p.Setor.Nome : null))
            .ToListAsync(ct);

        return new FormularioLido(
            f.Id,
            f.Titulo,
            f.Descricao,
            f.Tipo ?? TipoDeFormulario.Pesquisa,
            f.Sentido,
            f.Anonimo,
            f.Ativo,
            f.CriadoEmUtc ?? DateTime.UnixEpoch,
            autor,
            f.Alvo,
            f.PublicadoEmUtc,
            f.EncerradoEmUtc,
            f.Prazo,
            f.RecorrenciaIntervalo,
            f.RecorrenciaUnidade,
            f.RecorrenciaAte,
            f.ProximaReaberturaEmUtc,
            f.LembreteAutomatico,
            f.TokenDoLink,
            f.TemRespostas,
            perguntas,
            setores,
            pessoas);
    }

    public async Task<int?> IdPorTokenAsync(string token, CancellationToken ct) => await db.Formularios.AsNoTracking().Where(f => f.TokenDoLink == token).Select(f => (int?)f.Id).FirstOrDefaultAsync(ct);

    public async Task<bool> PerguntaTemRespostaAsync(int perguntaId, CancellationToken ct) => await db.FormulariosRespostas.AsNoTracking().AnyAsync(r => r.PerguntaId == perguntaId, ct);

    public async Task<IReadOnlyList<PessoaParaPublico>> PessoasAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new PessoaParaPublico(f.Id, f.Ativo)).ToListAsync(ct);

    public async Task<int> ContarSetoresExistentesAsync(IReadOnlyList<int> ids, CancellationToken ct) => await db.Setores.AsNoTracking().CountAsync(s => ids.Contains(s.Id), ct);

    public async Task<IReadOnlyList<ParDeResposta>> ParesQueResponderamAsync(int formularioId, CancellationToken ct) =>
        [.. (await db.FormulariosRespostas.AsNoTracking().Where(r => r.FormularioId == formularioId && r.FuncionarioId != null).Select(r => new { r.FuncionarioId, r.AvaliadoId }).Distinct().ToListAsync(ct)).Select(r => new ParDeResposta(r.FuncionarioId!.Value, r.AvaliadoId))];

    private static readonly MapaDeOrdenacao<Formulario> Ordem = new MapaDeOrdenacao<Formulario>("criadoEm", DirecaoDeOrdenacao.Desc, f => f.Id)
        .Por("criadoEm", f => f.CriadoEmUtc)
        .Por("titulo", f => f.Titulo)
        .Por("prazo", f => f.Prazo);

    public async Task<(IReadOnlyList<FormularioDaLista> Itens, int Total, FormularioContagemPorTipo Contagem)> ListarAsync(FiltroDeFormulariosRh filtro, ParametrosDeLista parametros, DateOnly hoje, CancellationToken ct)
    {
        var baseDaLista = db.Formularios.AsNoTracking().NaSituacaoCalculada(filtro.Situacao, hoje);
        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            baseDaLista = baseDaLista.Where(f => EF.Functions.Like(f.Titulo, like));
        }

        // as abas: a contagem respeita a busca e a situação e ignora o tipo, para os números não mudarem quando a pessoa troca de aba
        var porTipo = (await baseDaLista.GroupBy(f => f.Tipo).Select(g => new { Tipo = g.Key, Quantos = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Tipo ?? TipoDeFormulario.Pesquisa, x => x.Quantos);
        int Quantos(TipoDeFormulario tipo) => porTipo.GetValueOrDefault(tipo);
        var contagem = new FormularioContagemPorTipo(porTipo.Values.Sum(), Quantos(TipoDeFormulario.Entrevista), Quantos(TipoDeFormulario.Admissao), Quantos(TipoDeFormulario.Avaliacao), Quantos(TipoDeFormulario.Pesquisa), Quantos(TipoDeFormulario.Outro));

        var filtrada = filtro.Tipo is { } tipoPedido ? baseDaLista.Where(f => f.Tipo == tipoPedido) : baseDaLista;
        var total = await filtrada.CountAsync(ct);
        var pagina = await Ordem.Aplicar(filtrada, parametros).Skip(parametros.Pular).Take(parametros.TamanhoPagina)
            .Select(f => new { f.Id, f.Titulo, f.Tipo, f.SentidoAvaliacao, f.Anonimo, f.Ativo, f.Alvo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo, f.CriadoEmUtc })
            .ToListAsync(ct);
        return ([.. pagina.Select(f => new FormularioDaLista(f.Id, f.Titulo, f.Tipo ?? TipoDeFormulario.Pesquisa, f.SentidoAvaliacao, f.Anonimo, f.Ativo, f.Alvo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo, f.CriadoEmUtc ?? DateTime.UnixEpoch))], total, contagem);
    }

    public async Task<IReadOnlyDictionary<int, RespostasDoFormulario>> RespostasPorFormularioAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var envios = await db.FormulariosRespostas.AsNoTracking()
            .Where(r => ids.Contains(r.FormularioId) && r.FuncionarioId != null)
            .Select(r => new { r.FormularioId, r.FuncionarioId, r.AvaliadoId, r.RespondidaEmUtc })
            .Distinct()
            .ToListAsync(ct);
        var candidatos = (await db.FormulariosCandidatos.AsNoTracking().Where(c => ids.Contains(c.FormularioId)).GroupBy(c => c.FormularioId).Select(g => new { Id = g.Key, Quantos = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Quantos);
        var participacoes = (await db.FormulariosParticipacoes.AsNoTracking().Where(p => ids.Contains(p.FormularioId)).GroupBy(p => p.FormularioId).Select(g => new { Id = g.Key, Quantos = g.Count() }).ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Quantos);
        return ids.Distinct().ToDictionary(
            id => id,
            id => new RespostasDoFormulario(
                [.. envios.Where(e => e.FormularioId == id).Select(e => new ParDeResposta(e.FuncionarioId!.Value, e.AvaliadoId)).Distinct()],
                candidatos.GetValueOrDefault(id),
                envios.Count(e => e.FormularioId == id),
                participacoes.GetValueOrDefault(id)));
    }

    private async Task<IReadOnlyList<PerguntaLida>> PerguntasAsync(int formularioId, CancellationToken ct)
    {
        var linhas = await db.FormulariosPerguntas.AsNoTracking().Where(p => p.FormularioId == formularioId).OrderBy(p => p.Ordem).ThenBy(p => p.Id).ToListAsync(ct);
        var ids = linhas.Select(p => p.Id).ToList();
        var alternativas = (await db.FormulariosAlternativas.AsNoTracking().Where(a => ids.Contains(a.PerguntaId)).OrderBy(a => a.Ordem).ThenBy(a => a.Id).ToListAsync(ct))
            .ToLookup(a => a.PerguntaId);
        return
        [
            .. linhas.Select(p => new PerguntaLida(
                p.Id,
                p.Ordem ?? 1,
                p.Texto,
                p.Tipo ?? TipoDePergunta.RespostaCurta,
                p.Obrigatoria,
                [.. alternativas[p.Id].Select(a => new AlternativaLida(a.Id, a.Ordem, a.Texto))])),
        ];
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// "Quem deve responder" (contrato, <c>L:D.8</c>). Formulário com nome: <b>calculado na hora</b> (quem entra no setor depois também responde, quem sai deixa de responder). Anônimo publicado: a <b>fotografia</b> guardada nas linhas de destinatário por pessoa
/// (uma linha por par), tirada no dia da publicação, que não muda. <b>Equipe</b> do gestor = o setor de que ele é o gestor (<c>SETORES.ID_GESTOR</c>), pessoas ativas, com a admissão já começada, sem ele; <b>gestor do setor</b> só vale se a pessoa dele está ativa.
/// Quem está em admissão (admissão ainda não começada) fica fora de <c>SETORES</c> e da equipe, e entra em <c>PESSOAS</c> (o formulário de admissão). Pessoa inativa nunca responde.
/// </summary>
public sealed class DestinatariosDoFormulario(AppDbContext db) : IDestinatariosDoFormulario
{
    private sealed record Membro(int Id, int? SetorId);

    private sealed record GestorDoSetor(int SetorId, int? GestorFuncionarioId);

    public async Task<QuemDeveResponder> CalcularAsync(PublicoDoFormulario publico, DateOnly hoje, CancellationToken ct)
    {
        var avaliacao = publico.Tipo == TipoDeFormulario.Avaliacao;
        var sentido = avaliacao ? publico.Sentido : null;
        if (publico.Alvo is null or AlvoDoFormulario.Link)
        {
            return new QuemDeveResponder(publico.Alvo, sentido, avaliacao, [], 0, 0, false, 0);
        }

        return publico.Alvo == AlvoDoFormulario.Pessoas
            ? await PorPessoasAsync(publico, avaliacao, sentido, hoje, ct)
            : await PorSetoresAsync(publico, avaliacao, sentido, hoje, ct);
    }

    public async Task<QuemDeveResponder?> ObterAsync(int formularioId, DateOnly hoje, CancellationToken ct)
    {
        var formulario = await db.Formularios.AsNoTracking().Where(f => f.Id == formularioId).Select(f => new { f.Tipo, f.SentidoAvaliacao, f.Alvo, f.Anonimo, f.Ativo, f.EncerradoEmUtc }).SingleOrDefaultAsync(ct);
        if (formulario is null)
        {
            return null;
        }

        var linhas = await LinhasAsync(formularioId, ct);
        var publico = PublicoDe(formulario.Tipo ?? TipoDeFormulario.Pesquisa, formulario.SentidoAvaliacao, formulario.Alvo, linhas);
        var publicado = formulario.Ativo || formulario.EncerradoEmUtc is not null;
        if (formulario.Anonimo && publicado && formulario.Alvo is AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores)
        {
            return await DaFotografiaAsync(publico, linhas, ct);
        }

        return await CalcularAsync(publico, hoje, ct);
    }

    public async Task<QuemDeveResponder> FotografarAsync(int formularioId, DateOnly hoje, CancellationToken ct)
    {
        var formulario = await db.Formularios.AsNoTracking().Where(f => f.Id == formularioId).Select(f => new { f.Tipo, f.SentidoAvaliacao, f.Alvo }).SingleAsync(ct);
        var linhas = await LinhasAsync(formularioId, ct);
        var quem = await CalcularAsync(PublicoDe(formulario.Tipo ?? TipoDeFormulario.Pesquisa, formulario.SentidoAvaliacao, formulario.Alvo, linhas), hoje, ct);

        // AUDITORIA-OK: a publicação do formulário é auditada à mão por quem publica (evento Publicou, sem número de destinatários no anônimo); as linhas de destinatário não são auditadas (nenhum rastro de quem responde).
        await db.FormulariosDestinatarios.Where(d => d.FormularioId == formularioId && d.FuncionarioId != null).ExecuteDeleteAsync(ct);
        db.FormulariosDestinatarios.AddRange(quem.Pares.Select(p => FormularioDestinatario.DaPessoa(formularioId, p.PessoaId, p.AvaliadoId)));
        await db.SaveChangesAsync(ct);
        return quem;
    }

    private sealed record Linha(int? FuncionarioId, int? SetorId, int? AvaliadoId);

    private async Task<List<Linha>> LinhasAsync(int formularioId, CancellationToken ct) =>
        await db.FormulariosDestinatarios.AsNoTracking().Where(d => d.FormularioId == formularioId).Select(d => new Linha(d.FuncionarioId, d.SetorId, d.AvaliadoId)).ToListAsync(ct);

    private static PublicoDoFormulario PublicoDe(TipoDeFormulario tipo, SentidoDaAvaliacao? sentido, AlvoDoFormulario? alvo, List<Linha> linhas) =>
        new(alvo, tipo, sentido, [.. linhas.Where(l => l.SetorId is not null).Select(l => l.SetorId!.Value).Distinct()], [.. linhas.Where(l => l.FuncionarioId is not null).Select(l => l.FuncionarioId!.Value).Distinct()]);

    /// <summary>A fotografia: os pares são as linhas por pessoa (e avaliado); os setores escolhidos são as linhas de setor que ficaram.</summary>
    private async Task<QuemDeveResponder> DaFotografiaAsync(PublicoDoFormulario publico, List<Linha> linhas, CancellationToken ct)
    {
        var avaliacao = publico.Tipo == TipoDeFormulario.Avaliacao;
        var pares = linhas.Where(l => l.FuncionarioId is not null).Select(l => new ParDeResposta(l.FuncionarioId!.Value, l.AvaliadoId)).Distinct().OrderBy(p => p.PessoaId).ThenBy(p => p.AvaliadoId).ToList();
        var setores = await db.Setores.AsNoTracking().CountAsync(s => publico.SetorIds.Contains(s.Id), ct);
        var todos = setores > 0 && setores == await db.Setores.AsNoTracking().CountAsync(ct);
        return new QuemDeveResponder(publico.Alvo, avaliacao ? publico.Sentido : null, avaliacao, pares, publico.FuncionarioIds.Count, setores, todos, 0);
    }

    private async Task<QuemDeveResponder> PorPessoasAsync(PublicoDoFormulario publico, bool avaliacao, SentidoDaAvaliacao? sentido, DateOnly hoje, CancellationToken ct)
    {
        var ids = publico.FuncionarioIds.Distinct().ToList();
        var escolhidas = await db.Funcionarios.AsNoTracking().Where(f => ids.Contains(f.Id) && f.Ativo).Select(f => new Membro(f.Id, f.SetorId)).ToListAsync(ct);
        if (!avaliacao)
        {
            return new QuemDeveResponder(AlvoDoFormulario.Pessoas, null, false, [.. escolhidas.OrderBy(m => m.Id).Select(m => new ParDeResposta(m.Id, null))], escolhidas.Count, 0, false, 0);
        }

        if (sentido == SentidoDaAvaliacao.EquipeAvaliaGestor)
        {
            var gestores = await GestoresDosSetoresAsync([.. escolhidas.Where(m => m.SetorId is not null).Select(m => m.SetorId!.Value).Distinct()], ct);
            var pares = ParesDeAvaliacao.EquipeAvaliaGestor(escolhidas.Select(m => (m.Id, GestorDe(m, gestores))));
            return new QuemDeveResponder(AlvoDoFormulario.Pessoas, sentido, true, pares, escolhidas.Count, 0, false, 0);
        }

        // GESTOR_AVALIA_EQUIPE: as pessoas escolhidas são gestores; quem não é gestor de setor nenhum não gera avaliação (e não conta como gestor)
        var idsEscolhidos = escolhidas.Select(m => m.Id).ToList();
        var setoresDosGestores = await db.Setores.AsNoTracking().Where(s => s.Gestor != null && idsEscolhidos.Contains(s.Gestor.FuncionarioId)).Select(s => new { SetorId = s.Id, GestorFuncionarioId = s.Gestor!.FuncionarioId }).ToListAsync(ct);
        return await ParesDoGestorAsync(AlvoDoFormulario.Pessoas, escolhidas.Count, 0, false, [.. setoresDosGestores.Select(s => (s.GestorFuncionarioId, s.SetorId))], hoje, ct);
    }

    private async Task<QuemDeveResponder> PorSetoresAsync(PublicoDoFormulario publico, bool avaliacao, SentidoDaAvaliacao? sentido, DateOnly hoje, CancellationToken ct)
    {
        var ids = publico.SetorIds.Distinct().ToList();
        var setores = await db.Setores.AsNoTracking().CountAsync(s => ids.Contains(s.Id), ct);
        var todos = setores > 0 && setores == await db.Setores.AsNoTracking().CountAsync(ct);
        var membros = await db.Funcionarios.AsNoTracking().Where(f => f.Ativo && f.DataAdmissao <= hoje && f.SetorId != null && ids.Contains(f.SetorId.Value)).Select(f => new Membro(f.Id, f.SetorId)).ToListAsync(ct);
        if (!avaliacao)
        {
            return new QuemDeveResponder(AlvoDoFormulario.Setores, null, false, [.. membros.OrderBy(m => m.Id).Select(m => new ParDeResposta(m.Id, null))], 0, setores, todos, 0);
        }

        if (sentido == SentidoDaAvaliacao.EquipeAvaliaGestor)
        {
            var gestores = await GestoresDosSetoresAsync(ids, ct);
            var pares = ParesDeAvaliacao.EquipeAvaliaGestor(membros.Select(m => (m.Id, GestorDe(m, gestores))));
            return new QuemDeveResponder(AlvoDoFormulario.Setores, sentido, true, pares, 0, setores, todos, 0);
        }

        var gestoresDosSetores = await GestoresDosSetoresAsync(ids, ct);
        return await ParesDoGestorAsync(AlvoDoFormulario.Setores, 0, setores, todos, [.. gestoresDosSetores.Select(g => (g.Value, g.Key))], hoje, ct);
    }

    /// <summary>Cada gestor com o setor dele: a equipe é o setor, sem o gestor, só ativos e com a admissão começada.</summary>
    private async Task<QuemDeveResponder> ParesDoGestorAsync(AlvoDoFormulario alvo, int pessoasEscolhidas, int setoresEscolhidos, bool todos, IReadOnlyList<(int GestorFuncionarioId, int SetorId)> gestores, DateOnly hoje, CancellationToken ct)
    {
        var setorIds = gestores.Select(g => g.SetorId).Distinct().ToList();
        var membros = await db.Funcionarios.AsNoTracking().Where(f => f.Ativo && f.DataAdmissao <= hoje && f.SetorId != null && setorIds.Contains(f.SetorId.Value)).Select(f => new Membro(f.Id, f.SetorId)).ToListAsync(ct);
        var porGestor = gestores.Select(g => (g.GestorFuncionarioId, (IReadOnlyList<int>)[.. membros.Where(m => m.SetorId == g.SetorId).Select(m => m.Id)]));
        var pares = ParesDeAvaliacao.GestorAvaliaEquipe(porGestor);
        return new QuemDeveResponder(alvo, SentidoDaAvaliacao.GestorAvaliaEquipe, true, pares, pessoasEscolhidas, setoresEscolhidos, todos, gestores.Select(g => g.GestorFuncionarioId).Distinct().Count());
    }

    /// <summary>O gestor do setor da pessoa, ou <c>null</c> se ela não tem setor ou o setor não tem gestor ativo.</summary>
    private static int? GestorDe(Membro membro, Dictionary<int, int> gestores) => membro.SetorId is { } setor && gestores.TryGetValue(setor, out var gestor) ? gestor : null;

    /// <summary>O gestor ATIVO de cada setor (o id da pessoa dele); setor sem gestor, ou com gestor inativo, não aparece.</summary>
    private async Task<Dictionary<int, int>> GestoresDosSetoresAsync(IReadOnlyList<int> setorIds, CancellationToken ct) =>
        (await db.Setores.AsNoTracking().Where(s => setorIds.Contains(s.Id) && s.Gestor != null && s.Gestor.Funcionario.Ativo).Select(s => new GestorDoSetor(s.Id, s.Gestor!.FuncionarioId)).ToListAsync(ct))
        .ToDictionary(g => g.SetorId, g => g.GestorFuncionarioId!.Value);
}

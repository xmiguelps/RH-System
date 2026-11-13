using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Application.Painel;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// Os formulários de quem está logado. Os candidatos saem do banco por uma consulta só (PUBLICADOS, por pessoas ou setores, em que a pessoa aparece nas linhas de destinatário: por ela, pelo setor dela ou por um setor que ela gerencia, mais todos os que ela já respondeu); depois cada
/// um passa pela porta única de "quem deve responder" (<see cref="IDestinatariosDoFormulario"/>), que decide se ela é mesmo uma das pessoas (a fotografia do anônimo, o setor de quem já começou, os pares das avaliações). Nada aqui lê o que OUTRA pessoa respondeu: só as linhas da própria pessoa.
/// </summary>
public sealed class ConsultaDeMeusFormularios(AppDbContext db, IDestinatariosDoFormulario destinatarios) : IConsultaDeMeusFormularios
{
    public async Task<IReadOnlyList<CartaoDoFormulario>> CartoesAsync(int funcionarioId, DateOnly hoje, DateTime agoraUtc, bool soParaResponder, CancellationToken ct)
    {
        var eu = await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => new { f.SetorId }).SingleOrDefaultAsync(ct);
        if (eu is null)
        {
            return [];
        }

        var setores = await db.Setores.AsNoTracking().Where(s => s.Gestor != null && s.Gestor.FuncionarioId == funcionarioId).Select(s => s.Id).ToListAsync(ct);
        if (eu.SetorId is { } meuSetor)
        {
            setores.Add(meuSetor);
        }

        var publicados = db.Formularios.AsNoTracking().NaSituacaoCalculada(SituacaoDoFormulario.Publicado, hoje).Where(f => f.Alvo == AlvoDoFormulario.Pessoas || f.Alvo == AlvoDoFormulario.Setores);
        if (soParaResponder)
        {
            publicados = publicados.Where(f => f.Tipo == TipoDeFormulario.Admissao || f.Tipo == TipoDeFormulario.Avaliacao);
        }

        var ids = await publicados
            .Where(f => db.FormulariosDestinatarios.Any(d => d.FormularioId == f.Id && (d.FuncionarioId == funcionarioId || (d.SetorId != null && setores.Contains(d.SetorId.Value)))))
            .Select(f => f.Id)
            .ToListAsync(ct);
        if (!soParaResponder)
        {
            ids.AddRange(await db.FormulariosRespostas.AsNoTracking().Where(r => r.FuncionarioId == funcionarioId).Select(r => r.FormularioId).Distinct().ToListAsync(ct));
            ids.AddRange(await db.FormulariosParticipacoes.AsNoTracking().Where(p => p.FuncionarioId == funcionarioId).Select(p => p.FormularioId).Distinct().ToListAsync(ct));
        }

        ids = [.. ids.Distinct()];
        if (ids.Count == 0)
        {
            return [];
        }

        var formularios = await db.Formularios.AsNoTracking().Where(f => ids.Contains(f.Id) && (f.Alvo == AlvoDoFormulario.Pessoas || f.Alvo == AlvoDoFormulario.Setores))
            .Select(f => new { f.Id, f.Titulo, f.Descricao, f.Tipo, f.SentidoAvaliacao, f.Anonimo, f.Ativo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo })
            .ToListAsync(ct);
        var tipos = (await db.FormulariosPerguntas.AsNoTracking().Where(p => ids.Contains(p.FormularioId)).OrderBy(p => p.Ordem).ThenBy(p => p.Id).Select(p => new { p.FormularioId, p.Tipo }).ToListAsync(ct)).ToLookup(p => p.FormularioId, p => p.Tipo ?? TipoDePergunta.RespostaCurta);
        var nomeados = await db.FormulariosRespostas.AsNoTracking().Where(r => ids.Contains(r.FormularioId) && r.FuncionarioId == funcionarioId).Select(r => new { r.FormularioId, r.AvaliadoId, r.RespondidaEmUtc }).Distinct().ToListAsync(ct);
        var anonimos = await db.FormulariosParticipacoes.AsNoTracking().Where(p => ids.Contains(p.FormularioId) && p.FuncionarioId == funcionarioId).Select(p => new { p.FormularioId, p.AvaliadoId }).ToListAsync(ct);

        var cartoes = new List<CartaoDoFormulario>(formularios.Count);
        foreach (var f in formularios)
        {
            var quem = await destinatarios.ObterAsync(f.Id, hoje, ct);
            if (quem is null)
            {
                continue;
            }

            var feitos = f.Anonimo
                ? anonimos.Where(a => a.FormularioId == f.Id).Select(a => new ParDeResposta(funcionarioId, a.AvaliadoId)).Distinct().ToList()
                : nomeados.Where(n => n.FormularioId == f.Id).Select(n => new ParDeResposta(funcionarioId, n.AvaliadoId)).Distinct().ToList();
            var ultima = f.Anonimo ? null : nomeados.Where(n => n.FormularioId == f.Id).Select(n => n.RespondidaEmUtc).Max();
            cartoes.Add(new CartaoDoFormulario(f.Id, f.Titulo, f.Descricao, f.Tipo ?? TipoDeFormulario.Pesquisa, f.SentidoAvaliacao, f.Anonimo, f.Ativo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo, [.. tipos[f.Id]], [.. quem.Pares.Where(p => p.PessoaId == funcionarioId)], feitos, ultima));
        }

        return cartoes;
    }

    public async Task<PosicaoDaPessoaNoFormulario> PosicaoAsync(int formularioId, int funcionarioId, DateOnly hoje, CancellationToken ct)
    {
        var anonimo = await db.Formularios.AsNoTracking().Where(f => f.Id == formularioId).Select(f => f.Anonimo).SingleOrDefaultAsync(ct);
        var quem = await destinatarios.ObterAsync(formularioId, hoje, ct);
        var meusPares = quem is null ? [] : quem.Pares.Where(p => p.PessoaId == funcionarioId).ToList();
        var feitos = anonimo
            ? await db.FormulariosParticipacoes.AsNoTracking().Where(p => p.FormularioId == formularioId && p.FuncionarioId == funcionarioId).Select(p => p.AvaliadoId).ToListAsync(ct)
            : await db.FormulariosRespostas.AsNoTracking().Where(r => r.FormularioId == formularioId && r.FuncionarioId == funcionarioId).Select(r => r.AvaliadoId).Distinct().ToListAsync(ct);
        return new PosicaoDaPessoaNoFormulario(meusPares, [.. feitos.Select(a => new ParDeResposta(funcionarioId, a)).Distinct()]);
    }

    public async Task<IReadOnlyList<FuncionarioResumo>> ResumosAsync(IReadOnlyList<int> ids, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null)).ToListAsync(ct);
}

/// <summary>A bolinha "Formulários" do menu: os formulários de admissão e de avaliação PUBLICADOS que a pessoa ainda deve responder (a mesma conta de <c>contagens.paraResponder</c> da lista). Só olha esses dois tipos: poucos formulários por pessoa e nenhuma lista carregada.</summary>
public sealed class FonteDeContadorDeFormularios(IConsultaDeMeusFormularios consulta, IRelogio relogio) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => ContadorDoMenu.FormulariosPendentes;

    public async Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var cartoes = await consulta.CartoesAsync(escopo.FuncionarioId, FusoBrasil.Hoje(agora), agora, soParaResponder: true, ct);
        return cartoes.Select(c => RegrasDosMeusFormularios.Montar(c, agora)).Count(i => i is { Situacao: SituacaoMinhaDoFormulario.ParaResponder });
    }
}

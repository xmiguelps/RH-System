using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Equipe;

/// <summary>O formulário que a equipe do gestor enxerga, com a equipe e os pares da EQUIPE (os do gestor e os de outros setores já ficaram de fora).</summary>
public sealed record AcessoDaEquipeAoFormulario(FormularioLido Lido, SituacaoDoFormulario Situacao, EquipeDoGestor Equipe, IReadOnlyList<MembroDaEquipe> Membros, QuemDeveResponder QuemDaEquipe);

/// <summary>
/// A porta de entrada do gestor nos formulários (a mesma para o painel e para o lembrete): 404 se o gestor não tem setor, o formulário não existe, ainda não foi publicado (rascunho, agendado), é por link, é Sugestão, é avaliação (GESTOR_AVALIA_EQUIPE: a parte é do próprio gestor; EQUIPE_AVALIA_GESTOR: ele é o
/// avaliado e não acompanha o andamento) ou não tem NINGUÉM da equipe dele para responder. "Não existe" e "é de outro setor" dão a mesma resposta. O formulário encerrado ainda abre (o painel), só o lembrete o recusa.
/// </summary>
public static class AcessoDaEquipe
{
    public static async Task<AcessoDaEquipeAoFormulario> ConferirAsync(IEscopoDoUsuario escopo, IConsultaDeFormularios consulta, IConsultaDeEquipeDoFormulario equipe, IDestinatariosDoFormulario destinatarios, int formularioId, DateTime agoraUtc, CancellationToken ct)
    {
        var time = (await escopo.ObterEquipeAsync(ct)).OuNaoEncontrado();
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agoraUtc);
        if (situacao is SituacaoDoFormulario.Rascunho or SituacaoDoFormulario.Agendado || lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores) || lido.Tipo is TipoDeFormulario.Outro or TipoDeFormulario.Avaliacao)
        {
            throw new NaoEncontradoException();
        }

        var membros = await equipe.MembrosAsync(time.SetorId, time.Hoje, ct);
        var quem = await destinatarios.ObterAsync(formularioId, time.Hoje, ct) ?? throw new NaoEncontradoException();
        var ids = membros.Select(m => m.Id).ToHashSet();
        var daEquipe = quem with { Pares = [.. quem.Pares.Where(p => ids.Contains(p.PessoaId))] };
        if (daEquipe.Pares.Count == 0)
        {
            throw new NaoEncontradoException();
        }

        return new AcessoDaEquipeAoFormulario(lido, situacao, time, membros, daEquipe);
    }
}

/// <summary>
/// <c>GET /api/gestor/formularios</c>: os pedidos do RH PUBLICADOS em que gente do setor do gestor deve responder, com <c>respondidos</c> e <c>esperados</c> só da equipe (o gestor não conta como membro). Não entram: por link, Sugestão, as duas avaliações, encerrado (nem o prazo vencido) e o que não tem ninguém da equipe. Lista curta, sem paginação,
/// pelo prazo mais próximo (sem prazo por último) e depois pelo título. No anônimo aberto só <c>esperados</c> é exato. Gestor sem setor: lista vazia.
/// </summary>
public sealed class ListarFormulariosDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeEquipeDoFormulario equipe, IDestinatariosDoFormulario destinatarios, IConsultaDeAcompanhamento acompanhamento, IRelogio relogio)
{
    public async Task<FormularioEquipeLista> ExecutarAsync(CancellationToken ct)
    {
        if (await escopo.ObterEquipeAsync(ct) is not { } time)
        {
            return new FormularioEquipeLista([]);
        }

        var agora = relogio.AgoraUtc;
        var ids = (await equipe.MembrosAsync(time.SetorId, time.Hoje, ct)).Select(m => m.Id).ToHashSet();
        var itens = new List<FormularioEquipeItem>();
        foreach (var f in await equipe.AbertosAsync(ct))
        {
            if (RegraDaSituacaoDoFormulario.De(f.Ativo, f.PublicadoEmUtc, f.EncerradoEmUtc, f.Prazo, agora) != SituacaoDoFormulario.Publicado || await destinatarios.ObterAsync(f.Id, time.Hoje, ct) is not { } quem)
            {
                continue;
            }

            var daEquipe = quem with { Pares = [.. quem.Pares.Where(p => ids.Contains(p.PessoaId))] };
            if (daEquipe.Pares.Count == 0)
            {
                continue;
            }

            IReadOnlyList<EnvioLido> envios = f.Anonimo ? [] : [.. (await acompanhamento.EnviosAsync(f.Id, ct)).Where(e => ids.Contains(e.PessoaId))];
            var resumo = RegrasDosRespondentes.Resumo(false, f.Anonimo, false, daEquipe, envios, 0);
            itens.Add(new FormularioEquipeItem(f.Id, f.Titulo, f.Tipo, null, f.Anonimo, f.Prazo, resumo.Respondidos, daEquipe.Esperados, resumo.TaxaDeRespostaPercentual));
        }

        return new FormularioEquipeLista([.. itens.OrderBy(i => i.Prazo is null).ThenBy(i => i.Prazo).ThenBy(i => i.Titulo, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Id)]);
    }
}

/// <summary>
/// <c>GET /api/gestor/formularios/{formularioId}/respondentes</c>: quem da equipe respondeu e quem falta (só quem e quando, nunca o conteúdo), em ordem de nome e sem paginação. 404 pelas regras de <see cref="AcessoDaEquipe"/>. <c>observacao</c> vem das férias aprovadas que cobrem hoje ("de férias até 17/10"). No ANÔNIMO <c>itens</c> é sempre vazio:
/// aberto, só <c>esperados</c> (nem número, nem faixa nos outros); encerrado (de verdade, pela mesma função de situação), os números exatos da equipe pelas participações.
/// </summary>
public sealed class ListarRespondentesDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFormularios consulta, IConsultaDeEquipeDoFormulario equipe, IDestinatariosDoFormulario destinatarios, IConsultaDeAcompanhamento acompanhamento, IFeriasDoPonto ferias, IRelogio relogio)
{
    public async Task<FormularioEquipeRespondentes> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var acesso = await AcessoDaEquipe.ConferirAsync(escopo, consulta, equipe, destinatarios, formularioId, relogio.AgoraUtc, ct);
        var lido = acesso.Lido;
        var encerrado = acesso.Situacao == SituacaoDoFormulario.Encerrado;
        var ids = acesso.Membros.Select(m => m.Id).ToHashSet();
        if (lido.Anonimo)
        {
            var participacoes = encerrado ? await equipe.ParticipacoesDaEquipeAsync(lido.Id, ids, ct) : 0;
            var anonimo = RegrasDosRespondentes.Resumo(false, true, encerrado, acesso.QuemDaEquipe, [], participacoes);
            return new FormularioEquipeRespondentes(lido.Id, lido.Titulo, true, anonimo.Respondidos, anonimo.Faltam, acesso.QuemDaEquipe.Esperados, anonimo.TaxaDeRespostaPercentual, []);
        }

        var envios = (await acompanhamento.EnviosAsync(lido.Id, ct)).Where(e => ids.Contains(e.PessoaId)).ToList();
        var resumo = RegrasDosRespondentes.Resumo(false, false, encerrado, acesso.QuemDaEquipe, envios, 0);
        var instantes = envios.GroupBy(e => e.PessoaId).ToDictionary(g => g.Key, g => g.Max(e => e.RespondidoEmUtc));
        var hoje = acesso.Equipe.Hoje;
        var deFerias = (await ferias.NoPeriodoAsync(hoje, hoje, ct)).GroupBy(f => f.FuncionarioId).ToDictionary(g => g.Key, g => g.Max(f => f.Fim));
        var itens = acesso.QuemDaEquipe.Pares.Select(p => p.PessoaId).Distinct()
            .Join(acesso.Membros, id => id, m => m.Id, (_, m) => m)
            .OrderBy(m => m.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(m => m.Id)
            .Select(m => new FormularioEquipeRespondente(
                new FuncionarioResumo(m.Id, m.Nome, m.Ativo, m.Cargo, m.Setor),
                deFerias.TryGetValue(m.Id, out var fim) ? $"de férias até {fim.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture)}" : null,
                instantes.ContainsKey(m.Id) ? SituacaoDoRespondente.Respondido : SituacaoDoRespondente.NaoRespondeu,
                instantes.TryGetValue(m.Id, out var instante) ? instante : null))
            .ToList();
        return new FormularioEquipeRespondentes(lido.Id, lido.Titulo, false, resumo.Respondidos, resumo.Faltam, acesso.QuemDaEquipe.Esperados, resumo.TaxaDeRespostaPercentual, itens);
    }
}

/// <summary>
/// <c>POST /api/gestor/formularios/{formularioId}/lembretes</c> (sem corpo): lembra quem da EQUIPE ainda não respondeu. 404 pelas regras de <see cref="AcessoDaEquipe"/> (outro setor, Sugestão, avaliações, por link), 422 <c>FORMULARIO_NAO_PUBLICADO</c> (encerrado). As mesmas regras do lembrete do RH (no máximo um e-mail por pessoa, formulário e dia).
/// Com nome: <c>enviados</c> e <c>ignorados</c> (os da equipe que não receberam agora: quem já respondeu, quem já recebeu hoje, sem e-mail). Anônimo: resposta SEMPRE <c>{anonimo: true, enviados: null, ignorados: null}</c>, um lembrete do formulário por dia E POR ALCANCE (o setor do gestor), e o evento sai sempre, sem número nem destinatário.
/// </summary>
public sealed class LembrarQuemFaltaNaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFormularios consulta, IConsultaDeEquipeDoFormulario equipe, IDestinatariosDoFormulario destinatarios, LembretesDoFormulario lembretes, IAuditor auditor, IRelogio relogio)
{
    public async Task<FormularioLembreteResultado> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var acesso = await AcessoDaEquipe.ConferirAsync(escopo, consulta, equipe, destinatarios, formularioId, agora, ct);
        if (acesso.Situacao != SituacaoDoFormulario.Publicado)
        {
            throw MensagensDosFormularios.FormularioNaoPublicadoParaLembrar.ComoProblema();
        }

        var lido = acesso.Lido;
        var ids = acesso.Membros.Select(m => m.Id).ToHashSet();
        var hoje = acesso.Equipe.Hoje;
        if (lido.Anonimo)
        {
            await lembretes.MarcarEAgendarSigilosoAsync(lido, hoje, agora, acesso.Equipe.SetorId, ct);
            await auditor.RegistrarEGravarAsync(LembrarQuemFaltaFormularioCasoDeUso.Evento(lido, $"Mandou o lembrete do formulário \"{lido.Titulo}\" para a equipe."), ct);
            return new FormularioLembreteResultado(true, null, null);
        }

        var (enviados, ignorados) = await lembretes.LembrarComNomeAsync(lido, [], hoje, ids, ct);
        if (enviados > 0)
        {
            await auditor.RegistrarEGravarAsync(LembrarQuemFaltaFormularioCasoDeUso.Evento(lido, $"Mandou o lembrete do formulário \"{lido.Titulo}\" para {enviados} pessoa(s) da equipe que ainda não responderam."), ct);
        }

        return new FormularioLembreteResultado(false, enviados, ignorados);
    }
}

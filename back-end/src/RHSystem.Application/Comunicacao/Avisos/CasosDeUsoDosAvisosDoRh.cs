using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Avisos;

/// <summary>Monta o aviso do contrato a partir do que a consulta leu: a situação e a prévia são calculadas aqui (do relógio da aplicação), e a leitura só aparece em ativo e expirado.</summary>
internal static class MapeadorDoAviso
{
    public static AvisoItemRh Item(AvisoLido l, DateTime agoraUtc)
    {
        var situacao = RegraDaSituacaoDoAviso.De(l.Enviado, l.PublicacaoEmUtc, l.ExpiracaoEmUtc, agoraUtc);
        var tipo = l.Tipo ?? TipoDeAviso.Simples;
        return new AvisoItemRh(l.Id, tipo, l.Titulo, PreviaDoAviso.Montar(tipo, l.Conteudo), situacao, l.Fixado, l.PublicacaoEmUtc, l.ExpiracaoEmUtc, l.AutorNome, Leitura(l, situacao));
    }

    public static AvisoDetalheRh Detalhe(AvisoLido l, DateTime agoraUtc)
    {
        var item = Item(l, agoraUtc);
        return new AvisoDetalheRh(item.Id, item.Tipo, item.Titulo, item.Previa, item.Situacao, item.Fixado, item.PublicacaoEmUtc, item.ExpiraEmUtc, item.AutorNome, item.Leitura, l.Conteudo);
    }

    private static AvisoLeituraContagem? Leitura(AvisoLido l, SituacaoDoAviso situacao) =>
        situacao is SituacaoDoAviso.Ativo or SituacaoDoAviso.Expirado ? new AvisoLeituraContagem(l.Viram, l.Total) : null;

    /// <summary>Uma data local de Brasília no texto das descrições de auditoria (<c>09/10/2026 08:00</c>).</summary>
    public static string DataLocal(DateTime instanteUtc) => FusoBrasil.ParaLocal(instanteUtc).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);
}

/// <summary>
/// <c>GET /api/rh/avisos</c>: os avisos de uma aba (<c>situacao</c>, padrão ativos), com filtro de <c>tipo</c> e <c>busca</c> no título e no conteúdo, páginas. A ordem é a da aba: ativos e expirados pela publicação (mais recente primeiro),
/// agendados por quem publica primeiro, rascunhos pelo mais recentemente criado. Nenhuma lista tem <c>ordenarPor</c>.
/// </summary>
public sealed class ListarAvisosDoRhCasoDeUso(IConsultaDeAvisos consulta, IRelogio relogio)
{
    public async Task<RespostaPaginada<AvisoItemRh>> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? situacao, string? tipo, CancellationToken ct)
    {
        var situacaoLida = EnumsDeConsulta.Ler<SituacaoDoAviso>(situacao, "situacao") ?? SituacaoDoAviso.Ativo;
        var tipoLido = EnumsDeConsulta.Ler<TipoDeAviso>(tipo, "tipo");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        var agora = relogio.AgoraUtc;
        var (itens, total) = await consulta.ListarAsync(new FiltroDeAvisosRh(situacaoLida, tipoLido), parametros, agora, ct);
        return RespostaPaginada<AvisoItemRh>.De([.. itens.Select(l => MapeadorDoAviso.Item(l, agora))], total, parametros);
    }
}

/// <summary><c>GET /api/rh/avisos/resumo</c>: os números do topo da tela (contagem de cada aba, média de leitura dos ativos, os que expiram em 7 dias e o próximo a expirar).</summary>
public sealed class ObterResumoDosAvisosCasoDeUso(IConsultaDeAvisos consulta, IRelogio relogio)
{
    public async Task<AvisoPainelRh> ExecutarAsync(CancellationToken ct)
    {
        var r = await consulta.ResumoAsync(relogio.AgoraUtc, ct);
        return new AvisoPainelRh(new AvisoContagens(r.Ativos, r.Agendados, r.Expirados, r.Rascunhos), r.LeituraMediaPercentual, r.ExpiramNosProximos7Dias, r.ProximoAExpirar);
    }
}

/// <summary><c>GET /api/rh/avisos/{avisoId}</c>: o aviso inteiro, em qualquer situação (404 se não existe).</summary>
public sealed class ObterAvisoDoRhCasoDeUso(IConsultaDeAvisos consulta, IRelogio relogio)
{
    public async Task<AvisoDetalheRh> ExecutarAsync(int avisoId, CancellationToken ct) =>
        MapeadorDoAviso.Detalhe((await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado(), relogio.AgoraUtc);
}

/// <summary>
/// <c>POST /api/rh/avisos</c>: cria o aviso como rascunho (<c>publicar: false</c>, com data prevista opcional) ou publica (data vazia ou passada = agora; futura = agendado). Os erros de campo vêm juntos num 400 antes de qualquer efeito. O aviso
/// publicado ou agendado ganha a tarefa que manda o e-mail na hora (a chave leva o instante). A data de publicação é SEMPRE da aplicação (o trigger usaria o <c>NOW()</c> real). Eventos <c>AVISOS</c>/<c>CRIOU</c> e, se publicou, <c>PUBLICOU</c>.
/// </summary>
public sealed class CriarAvisoCasoDeUso(IEscopoDoUsuario escopo, IAvisosRepositorio avisos, IConsultaDeAvisos consulta, IAgendamentoDoAviso agendamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<AvisoDetalheRh> ExecutarAsync(AvisoCriacaoComando corpo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        DateTime? publicacao = corpo.Publicar
            ? (corpo.PublicarEmUtc is { } data && data > agora ? InstanteAoSegundo.De(data) : InstanteAoSegundo.De(agora))
            : corpo.PublicarEmUtc is { } prevista ? InstanteAoSegundo.De(prevista) : null;

        var erros = new ColetorDeErros();
        var campos = ValidacaoDoAviso.Validar(erros, corpo.Tipo, corpo.Titulo, corpo.Conteudo, corpo.ExpiraEm, FusoBrasil.DiaLocalDe(publicacao ?? agora));
        erros.LancarSeHouver();

        var expiracao = corpo.ExpiraEm is { } dia ? ExpiracaoDoAviso.FimDoDia(dia) : (DateTime?)null;
        var aviso = Aviso.Criar(escopo.FuncionarioId, corpo.Tipo, campos!.Titulo, campos.Conteudo, corpo.Fixado, corpo.Publicar, publicacao, expiracao);

        var id = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                await avisos.AdicionarAsync(aviso, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Criou, $"Criou o aviso \"{campos.Titulo}\".", $"Aviso \"{campos.Titulo}\"", "AVISOS", aviso.Id));
                if (corpo.Publicar)
                {
                    await agendamento.AgendarAsync(aviso.Id, publicacao!.Value, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Publicou, DescricaoDaPublicacao(campos.Titulo, publicacao.Value, agora), $"Aviso \"{campos.Titulo}\"", "AVISOS", aviso.Id));
                }

                await unidade.SalvarAsync(ct);
                return aviso.Id;
            },
            ct);

        return MapeadorDoAviso.Detalhe((await consulta.ObterAsync(id, ct)).OuNaoEncontrado(), agora);
    }

    internal static string DescricaoDaPublicacao(string titulo, DateTime publicacao, DateTime agora) =>
        publicacao > agora ? $"Agendou o aviso \"{titulo}\" para {MapeadorDoAviso.DataLocal(publicacao)}." : $"Publicou o aviso \"{titulo}\".";
}

/// <summary>
/// <c>PUT /api/rh/avisos/{avisoId}</c>: edita título, conteúdo, fixado, expiração e (só em rascunho e agendado) a data de publicação. O tipo não muda e o e-mail não se repete. Ordem: 404, 400 (todos os campos juntos, conferidos pelo tipo do aviso), 422
/// <c>AVISO_PUBLICACAO_NAO_EDITAVEL</c> (aviso já no ar com outra data). Rascunho: data vazia apaga a data prevista; agendado: vazia mantém e outra data reagenda a tarefa (chave nova); ativo e expirado: vazia ou igual à atual. A edição é um
/// <c>UPDATE</c> condicional ao estado lido: se o aviso foi publicado ou excluído no meio, o caso de uso relê e dá o erro certo. Expirado com expiração futura volta a ativo (a situação é calculada).
/// </summary>
public sealed class EditarAvisoCasoDeUso(IAvisosRepositorio avisos, IConsultaDeAvisos consulta, IAgendamentoDoAviso agendamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const int TentativasNaCorrida = 5;

    public async Task<AvisoDetalheRh> ExecutarAsync(int avisoId, AvisoEdicaoComando corpo, CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < TentativasNaCorrida; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var lido = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
            var situacao = RegraDaSituacaoDoAviso.De(lido.Enviado, lido.PublicacaoEmUtc, lido.ExpiracaoEmUtc, agora);
            var novaPublicacao = lido.PublicacaoEmUtc;
            var reagendar = false;
            var pedida = corpo.PublicarEmUtc is { } p ? InstanteAoSegundo.De(p) : (DateTime?)null;
            switch (situacao)
            {
                case SituacaoDoAviso.Rascunho:
                    novaPublicacao = pedida;
                    break;
                case SituacaoDoAviso.Agendado when pedida is { } data:
                    novaPublicacao = data <= agora ? InstanteAoSegundo.De(agora) : data;
                    reagendar = novaPublicacao != lido.PublicacaoEmUtc;
                    break;
            }

            var erros = new ColetorDeErros();
            var campos = ValidacaoDoAviso.Validar(erros, lido.Tipo ?? TipoDeAviso.Simples, corpo.Titulo, corpo.Conteudo, corpo.ExpiraEm, FusoBrasil.DiaLocalDe(novaPublicacao ?? agora));
            erros.LancarSeHouver();

            if (situacao is SituacaoDoAviso.Ativo or SituacaoDoAviso.Expirado && pedida is { } outra && outra != lido.PublicacaoEmUtc)
            {
                throw MensagensDaComunicacao.AvisoPublicacaoNaoEditavel.ComoProblema();
            }

            var expiracao = corpo.ExpiraEm is { } dia ? ExpiracaoDoAviso.FimDoDia(dia) : (DateTime?)null;
            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await avisos.AtualizarAsync(avisoId, lido.Enviado, lido.PublicacaoEmUtc, campos!.Titulo, campos.Conteudo, corpo.Fixado, novaPublicacao, expiracao, ct))
                    {
                        return false;
                    }

                    if (reagendar)
                    {
                        await agendamento.CancelarAsync(avisoId, lido.PublicacaoEmUtc!.Value, ct);
                        await agendamento.AgendarAsync(avisoId, novaPublicacao!.Value, ct);
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Alterou, $"Alterou o aviso \"{campos.Titulo}\".", $"Aviso \"{campos.Titulo}\"", "AVISOS", avisoId));
                    await unidade.SalvarAsync(ct);
                    return true;
                },
                ct);

            if (gravou)
            {
                return MapeadorDoAviso.Detalhe((await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado(), agora);
            }
        }

        throw new InvalidOperationException("O aviso mudou várias vezes durante a edição.");
    }
}

/// <summary>
/// <c>DELETE /api/rh/avisos/{avisoId}</c>: exclui, de verdade, um aviso que ainda não foi publicado (rascunho ou agendado): apaga as leituras e o aviso na mesma transação, cancela a tarefa do e-mail e audita (<c>AVISOS</c>/<c>EXCLUIU</c>). Publicado só se
/// encerra: 422 <c>AVISO_NAO_PODE_SER_EXCLUIDO</c>. A exclusão é condicional ao aviso ainda não estar publicado, então o clique que perde a corrida de uma publicação dá o 422.
/// </summary>
public sealed class ExcluirAvisoCasoDeUso(IAvisosRepositorio avisos, IConsultaDeAvisos consulta, IAgendamentoDoAviso agendamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task ExecutarAsync(int avisoId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var lido = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
        var situacao = RegraDaSituacaoDoAviso.De(lido.Enviado, lido.PublicacaoEmUtc, lido.ExpiracaoEmUtc, agora);
        if (situacao is not (SituacaoDoAviso.Rascunho or SituacaoDoAviso.Agendado))
        {
            throw MensagensDaComunicacao.AvisoNaoPodeSerExcluido.ComoProblema();
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await avisos.ExcluirAsync(avisoId, agora, ct))
                {
                    // Outra decisão chegou antes: o estado de agora diz qual dos dois erros.
                    _ = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
                    throw MensagensDaComunicacao.AvisoNaoPodeSerExcluido.ComoProblema();
                }

                if (situacao == SituacaoDoAviso.Agendado)
                {
                    await agendamento.CancelarAsync(avisoId, lido.PublicacaoEmUtc!.Value, ct);
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Excluiu, $"Excluiu o aviso \"{lido.Titulo}\".", $"Aviso \"{lido.Titulo}\"", "AVISOS", avisoId));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);
    }
}

/// <summary>
/// <c>POST /api/rh/avisos/{avisoId}/publicar</c>: publica o rascunho agora, ou na data prevista se ela é futura (agendado), e grava a data EFETIVA. Ordem: 404, 422 <c>AVISO_JA_PUBLICADO</c> (ativo, agendado ou expirado), 400 (a expiração ficaria antes da publicação).
/// É um <c>UPDATE ... WHERE ENVIADO = FALSE</c>: de dois cliques em paralelo um publica e o outro dá o 422, e só o que publicou agenda a tarefa do e-mail e grava o evento.
/// </summary>
public sealed class PublicarAvisoCasoDeUso(IAvisosRepositorio avisos, IConsultaDeAvisos consulta, IAgendamentoDoAviso agendamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<AvisoDetalheRh> ExecutarAsync(int avisoId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var lido = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
        if (lido.Enviado)
        {
            throw MensagensDaComunicacao.AvisoJaPublicado.ComoProblema();
        }

        var efetiva = lido.PublicacaoEmUtc is { } prevista && prevista > agora ? prevista : InstanteAoSegundo.De(agora);
        if (lido.ExpiracaoEmUtc is { } expiracao && expiracao < efetiva)
        {
            throw new ErroDeValidacaoException("expiraEm", "A expiração do aviso é anterior à data da publicação. Edite a expiração antes de publicar.");
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await avisos.PublicarAsync(avisoId, efetiva, ct))
                {
                    _ = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
                    throw MensagensDaComunicacao.AvisoJaPublicado.ComoProblema();
                }

                await agendamento.AgendarAsync(avisoId, efetiva, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Publicou, CriarAvisoCasoDeUso.DescricaoDaPublicacao(lido.Titulo, efetiva, agora), $"Aviso \"{lido.Titulo}\"", "AVISOS", avisoId));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);

        return MapeadorDoAviso.Detalhe((await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado(), agora);
    }
}

/// <summary>
/// <c>POST /api/rh/avisos/{avisoId}/encerrar</c> ("Encerrar agora"): a expiração vira agora e o aviso passa a expirado (as leituras ficam). Só o ativo se encerra: 422 <c>AVISO_NAO_ESTA_ATIVO</c> nos demais (e no segundo clique). Audita como <c>ALTEROU</c>.
/// </summary>
public sealed class EncerrarAvisoCasoDeUso(IAvisosRepositorio avisos, IConsultaDeAvisos consulta, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<AvisoDetalheRh> ExecutarAsync(int avisoId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var lido = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
        if (RegraDaSituacaoDoAviso.De(lido.Enviado, lido.PublicacaoEmUtc, lido.ExpiracaoEmUtc, agora) != SituacaoDoAviso.Ativo)
        {
            throw MensagensDaComunicacao.AvisoNaoEstaAtivoParaEncerrar.ComoProblema();
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await avisos.EncerrarAsync(avisoId, InstanteAoSegundo.De(agora), ct))
                {
                    _ = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
                    throw MensagensDaComunicacao.AvisoNaoEstaAtivoParaEncerrar.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.Alterou, $"Encerrou o aviso \"{lido.Titulo}\".", $"Aviso \"{lido.Titulo}\"", "AVISOS", avisoId));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);

        return MapeadorDoAviso.Detalhe((await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado(), agora);
    }
}

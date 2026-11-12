using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Avisos;

/// <summary>
/// <c>GET /api/rh/avisos/{avisoId}/leituras</c> ("Quem viu"): os totais do aviso (<c>viram</c> e <c>aindaNaoViram</c>, que não mudam com <c>situacao</c>) e uma página das pessoas por nome. Ordem dos erros: 400 (os filtros), 404, 422
/// <c>AVISO_AINDA_NAO_PUBLICADO</c> (rascunho e agendado; o ativo e o expirado valem). A conta é por linha de leitura de pessoa ATIVA hoje.
/// </summary>
public sealed class ListarLeiturasDoAvisoCasoDeUso(IConsultaDeAvisos consulta, IRelogio relogio)
{
    public async Task<AvisoLeituras> ExecutarAsync(int avisoId, int pagina, int tamanhoPagina, string? situacao, CancellationToken ct)
    {
        var filtro = EnumsDeConsulta.Ler<FiltroDeLeitura>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var aviso = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
        if (RegraDaSituacaoDoAviso.De(aviso.Enviado, aviso.PublicacaoEmUtc, aviso.ExpiracaoEmUtc, relogio.AgoraUtc) is SituacaoDoAviso.Rascunho or SituacaoDoAviso.Agendado)
        {
            throw MensagensDaComunicacao.AvisoAindaNaoPublicado.ComoProblema();
        }

        var (itens, total, viram, aindaNaoViram) = await consulta.ListarLeiturasAsync(avisoId, filtro, parametros, ct);
        var resposta = RespostaPaginada<AvisoLeituraItem>.De(itens, total, parametros);
        return new AvisoLeituras(resposta.Itens, resposta.Pagina, resposta.TamanhoPagina, resposta.Total, resposta.TotalPaginas, viram, aindaNaoViram);
    }
}

/// <summary>
/// <c>POST /api/rh/avisos/{avisoId}/lembretes</c> ("Lembrar quem não viu"): enfileira um e-mail <c>AVISO_LEMBRETE</c> para cada pessoa ativa, admitida e com e-mail corporativo que ainda não abriu o aviso, e devolve 202 com a quantidade (0 se todos viram).
/// Só vale para aviso ativo (422 <c>AVISO_NAO_ESTA_ATIVO</c>). Não há limite de lembretes por aviso (o contrato diz), mas a chave leva o <b>minuto</b> do clique (<c>AVISO_LEMBRETE:aviso={id}:func={id}:em={minuto}</c>): o duplo clique e os cliques em
/// paralelo no mesmo minuto não mandam em dobro e recebem a mesma quantidade; um minuto depois é outro lote. O evento (<c>AVISOS</c>/<c>ENVIOU_EMAIL</c>) só sai quando algum e-mail realmente entrou na fila.
/// </summary>
public sealed class LembrarQuemNaoViuAvisoCasoDeUso(IConsultaDeAvisos consulta, IFilaDeEmails fila, IAuditor auditor, IRelogio relogio)
{
    public const string TipoDoEmail = "AVISO_LEMBRETE";
    private const int TamanhoDoLote = 100;

    public async Task<AvisoLembreteResultado> ExecutarAsync(int avisoId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var aviso = (await consulta.ObterAsync(avisoId, ct)).OuNaoEncontrado();
        if (RegraDaSituacaoDoAviso.De(aviso.Enviado, aviso.PublicacaoEmUtc, aviso.ExpiracaoEmUtc, agora) != SituacaoDoAviso.Ativo)
        {
            throw MensagensDaComunicacao.AvisoNaoEstaAtivoParaLembrar.ComoProblema();
        }

        var destinatarios = await consulta.NaoLidosParaLembreteAsync(avisoId, FusoBrasil.Hoje(agora), ct);
        if (destinatarios.Count == 0)
        {
            return new AvisoLembreteResultado(0);
        }

        var minuto = new DateTime(agora.Ticks - (agora.Ticks % TimeSpan.TicksPerMinute), DateTimeKind.Utc);
        var previa = PreviaDoAviso.Montar(aviso.Tipo ?? TipoDeAviso.Simples, aviso.Conteudo);

        // Fora de qualquer transação: cada mensagem é gravada sozinha. Dentro de uma transação, quatro cliques em paralelo disputavam as mesmas chaves únicas em lote e o MySQL desfazia três por deadlock (medido: 500);
        // gravando uma a uma, a chave repetida de quem chegou depois é só "já está na fila".
        var enfileirados = 0;
        foreach (var lote in destinatarios.Chunk(TamanhoDoLote))
        {
            enfileirados += await fila.EnfileirarLoteAsync(
                [.. lote.Select(d => new EmailParaEnfileirar(TipoDoEmail, ChaveDeIdempotencia.Montar(TipoDoEmail, ("aviso", avisoId), ("func", d.FuncionarioId), ("em", minuto)), d.Email, new { nome = d.Nome, titulo = aviso.Titulo, previa }, FuncionarioId: d.FuncionarioId, ReferenciaId: avisoId))],
                ct);
        }

        if (enfileirados > 0)
        {
            await auditor.RegistrarEGravarAsync(new EventoDeAuditoriaNovo(AuditoriaArea.Avisos, AuditoriaAcao.EnviouEmail, $"Mandou o lembrete do aviso \"{aviso.Titulo}\" para {enfileirados} pessoa(s) que ainda não viram.", $"Aviso \"{aviso.Titulo}\"", "AVISOS", avisoId), ct);
        }

        return new AvisoLembreteResultado(destinatarios.Count);
    }
}

using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// Monta o formulário do contrato a partir do que a consulta leu. A situação é calculada AQUI, do relógio da aplicação (o prazo vencido vale como encerrado mesmo com a tarefa de encerrar parada). O que o contrato esconde no anônimo
/// (<c>temRespostas</c>) e o que só existe em certas situações (<c>agendarPara</c>, <c>publicadoEmUtc</c>, <c>tokenDoLink</c>) é decidido aqui, num lugar só.
/// </summary>
internal static class MapeadorDoFormulario
{
    public static FormularioDetalheRh Detalhe(FormularioLido l, DateTime agoraUtc)
    {
        var situacao = SituacaoDe(l, agoraUtc);
        var publicou = situacao is SituacaoDoFormulario.Publicado or SituacaoDoFormulario.Encerrado;
        var porLink = l.Alvo == AlvoDoFormulario.Link;
        var recorrencia = l.RecorrenciaIntervalo is { } intervalo && l.RecorrenciaUnidade is { } unidade ? new FormularioRecorrencia(intervalo, unidade, l.RecorrenciaAte) : null;
        var publicacao = new FormularioPublicacao(
            l.Alvo,
            l.Alvo == AlvoDoFormulario.Setores ? l.Setores : [],
            l.Alvo == AlvoDoFormulario.Pessoas ? l.Pessoas : [],
            situacao == SituacaoDoFormulario.Agendado && l.PublicadoEmUtc is { } dia ? FusoBrasil.DiaLocalDe(dia) : null,
            l.Prazo,
            recorrencia,
            l.LembreteAutomatico,
            publicou ? l.PublicadoEmUtc : null,
            l.EncerradoEmUtc ?? (situacao == SituacaoDoFormulario.Encerrado && l.Prazo is { } prazo ? InstantesDoFormulario.EncerramentoNominal(prazo) : null),
            l.ProximaReaberturaEmUtc,
            porLink ? l.TokenDoLink : null);
        return new FormularioDetalheRh(l.Id, l.Titulo, l.Descricao, l.Tipo, l.Sentido, l.Anonimo, situacao, l.Autor, l.CriadoEmUtc, l.Anonimo ? null : l.TemRespostas, [.. l.Perguntas.Select(Pergunta)], publicacao);
    }

    public static PerguntaFormulario Pergunta(PerguntaLida p) => new(p.Id, p.Ordem, p.Texto, p.Tipo, p.Obrigatoria, [.. p.Alternativas.Select(a => new PerguntaAlternativa(a.Id, a.Ordem, a.Texto))]);

    public static SituacaoDoFormulario SituacaoDe(FormularioLido l, DateTime agoraUtc) => RegraDaSituacaoDoFormulario.De(l.Ativo, l.PublicadoEmUtc, l.EncerradoEmUtc, l.Prazo, agoraUtc);
}

/// <summary><c>GET /api/rh/formularios/{formularioId}</c>: o formulário inteiro, em qualquer situação (404 se não existe).</summary>
public sealed class ObterFormularioRhCasoDeUso(IConsultaDeFormularios consulta, IRelogio relogio)
{
    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, CancellationToken ct) =>
        MapeadorDoFormulario.Detalhe((await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado(), relogio.AgoraUtc);
}

/// <summary>
/// <c>POST /api/rh/formularios</c>: cria o formulário como RASCUNHO (<c>ATIVO</c> falso gravado, lembrete automático ligado, a data de criação do relógio da aplicação e o autor logado). Todos os erros de campo vêm juntos num 400 antes de qualquer efeito (a Sugestão
/// e a avaliação da equipe nunca são anônimas; o sentido só na avaliação). Evento <c>FORMULARIOS</c>/<c>CRIOU</c> só com o título: o conteúdo das perguntas nunca entra na auditoria.
/// </summary>
public sealed class CriarFormularioCasoDeUso(IEscopoDoUsuario escopo, IFormulariosRepositorio formularios, IConsultaDeFormularios consulta, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<FormularioDetalheRh> ExecutarAsync(FormularioDetalhesComando corpo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var campos = ValidacaoDoFormulario.Validar(erros, corpo);
        erros.LancarSeHouver();

        var agora = relogio.AgoraUtc;
        var formulario = Formulario.Criar(escopo.FuncionarioId, campos!.Titulo, campos.Descricao, corpo.Tipo, corpo.SentidoAvaliacao, corpo.Anonimo, agora);
        var id = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                await formularios.AdicionarAsync(formulario, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Criou, $"Criou o formulário \"{campos.Titulo}\".", $"Formulário \"{campos.Titulo}\"", "FORMULARIOS", formulario.Id));
                await unidade.SalvarAsync(ct);
                return formulario.Id;
            },
            ct);

        return MapeadorDoFormulario.Detalhe((await consulta.ObterAsync(id, ct)).OuNaoEncontrado(), agora);
    }
}

/// <summary>
/// <c>PUT /api/rh/formularios/{formularioId}</c>: edita título, descrição, tipo, sentido e anonimato pela matriz de situação e anonimato (<see cref="MatrizDeEdicao"/>). Ordem: 404, 400 (todos os campos juntos), a matriz (422 encerrado, 409 anonimato fixo,
/// 422 já respondido, 422 link só para entrevista), depois a coerência com o público já guardado (anônimo não vai por link; só a entrevista vai por link; anônimo não tem pergunta de arquivo). O anônimo publicado recebe o MESMO veredito com e sem resposta.
/// A gravação é um <c>UPDATE</c> condicional ao estado lido: se outra aba publicou, encerrou ou recebeu resposta no meio, o caso de uso relê e dá o erro certo (nunca grava por cima, nunca chega ao 1451 do banco). Virar Sugestão apaga o prazo e a recorrência
/// guardados (a Sugestão é sempre aberta). Evento <c>ALTEROU</c> só com o título.
/// </summary>
public sealed class EditarFormularioCasoDeUso(IFormulariosRepositorio formularios, IConsultaDeFormularios consulta, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const int TentativasNaCorrida = 5;

    public async Task<FormularioDetalheRh> ExecutarAsync(int formularioId, FormularioDetalhesComando corpo, CancellationToken ct)
    {
        for (var tentativa = 0; tentativa < TentativasNaCorrida; tentativa++)
        {
            var agora = relogio.AgoraUtc;
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var erros = new ColetorDeErros();
            var campos = ValidacaoDoFormulario.Validar(erros, corpo);
            erros.LancarSeHouver();

            var situacao = MapeadorDoFormulario.SituacaoDe(lido, agora);
            var mudaTipoOuSentido = corpo.Tipo != lido.Tipo || corpo.SentidoAvaliacao != lido.Sentido;
            Recusar(MatrizDeEdicao.Conferir(situacao, lido.Anonimo, lido.TemRespostas, corpo.Anonimo != lido.Anonimo, mudaTipoOuSentido, lido.Alvo, corpo.Tipo));
            Recusar(lido.Alvo is { } alvo ? RegrasDaPublicacao.Recusa(corpo.Tipo, corpo.Anonimo, alvo) : null);
            if (corpo.Anonimo && lido.Perguntas.Any(p => p.Tipo == TipoDePergunta.Arquivo))
            {
                throw MensagensDosFormularios.AnonimoSemArquivo.ComoProblema();
            }

            var novos = new DetalhesParaGravar(
                campos!.Titulo,
                campos.Descricao,
                corpo.Tipo,
                corpo.SentidoAvaliacao,
                corpo.Anonimo,
                LimparPrazoERecorrencia: corpo.Tipo == TipoDeFormulario.Outro && (lido.Prazo is not null || lido.RecorrenciaIntervalo is not null),
                ExigirSemResposta: situacao == SituacaoDoFormulario.Publicado && !lido.Anonimo && mudaTipoOuSentido);
            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var mudou = await formularios.AtualizarDetalhesAsync(lido, novos, ct);
                    if (mudou)
                    {
                        auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Alterou o formulário \"{campos.Titulo}\".", $"Formulário \"{campos.Titulo}\"", "FORMULARIOS", formularioId));
                        await unidade.SalvarAsync(ct);
                    }

                    return mudou;
                },
                ct);
            if (gravou)
            {
                return MapeadorDoFormulario.Detalhe((await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado(), relogio.AgoraUtc);
            }
        }

        throw MensagensDosFormularios.FormularioMudouEmOutraTela;
    }

    private static void Recusar(RecusaDeEdicao? recusa)
    {
        if (recusa is null)
        {
            return;
        }

        throw (recusa.Value switch
        {
            RecusaDeEdicao.FormularioEncerrado => MensagensDosFormularios.FormularioEncerradoParaEditar,
            RecusaDeEdicao.AnonimatoFixoAposPublicar => MensagensDosFormularios.AnonimatoFixoAposPublicar,
            RecusaDeEdicao.FormularioJaRespondido => MensagensDosFormularios.FormularioJaRespondido,
            _ => MensagensDosFormularios.LinkSoParaEntrevista,
        }).ComoProblema();
    }

    private static void Recusar(RecusaDePublicacao? recusa)
    {
        if (recusa is null)
        {
            return;
        }

        throw (recusa.Value == RecusaDePublicacao.AnonimoSemLink ? MensagensDosFormularios.AnonimoSemLink : MensagensDosFormularios.LinkSoParaEntrevista).ComoProblema();
    }
}

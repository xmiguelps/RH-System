using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// O que as três rotas de pergunta têm em comum: a recusa por situação e anonimato (encerrado não muda; o anônimo PUBLICADO não muda nada, com ou sem resposta, nem <c>obrigatoria</c> nem <c>ordem</c>: o mesmo 409 sempre), a pergunta de arquivo que o anônimo não pode ter,
/// a posição, a corrida e o evento <c>ALTEROU</c> só com o título (nunca o texto da pergunta).
/// </summary>
internal static class RegrasDasPerguntasDoRh
{
    /// <summary>Quantas vezes se tenta de novo quando outra aba muda o formulário ou as posições no meio: depois disso, 409.</summary>
    public const int Tentativas = 5;

    public static void ConferirEstado(SituacaoDoFormulario situacao, bool anonimo)
    {
        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaEditar.ComoProblema();
        }

        if (situacao == SituacaoDoFormulario.Publicado && anonimo)
        {
            throw MensagensDosFormularios.AnonimoPerguntasFixas.ComoProblema();
        }
    }

    public static void ConferirArquivoNoAnonimo(bool anonimo, TipoDePergunta tipo)
    {
        if (anonimo && tipo == TipoDePergunta.Arquivo)
        {
            throw MensagensDosFormularios.AnonimoSemArquivo.ComoProblema();
        }
    }

    /// <summary>A posição depois da última (as posições seguem a ordem gravada: não se supõe que comecem em 1 nem que não tenham buraco).</summary>
    public static int Fim(FormularioLido lido) => lido.Perguntas.Select(p => p.Ordem).DefaultIfEmpty(0).Max() + 1;

    public static bool Corrida(ProblemaDeApiException problema) => ReferenceEquals(problema, MensagensDosFormularios.PerguntasMudaramEmOutraTela);

    public static EventoDeAuditoriaNovo Evento(FormularioLido lido, string frase) =>
        new(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"{frase} \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id);

    public static ProblemaDeApiException PerdeuACorrida() => MensagensDosFormularios.FormularioMudouEmOutraTela;
}

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/perguntas</c>: acrescenta a pergunta (sem <c>ordem</c>, no fim; com <c>ordem</c>, ali, e as seguintes descem; além do fim, no fim). Ordem das recusas: 404, 400 (todos os campos juntos), 422 encerrado, 409 anônimo publicado
/// (sempre), 422 pergunta de arquivo em formulário anônimo. Formulário com nome publicado aceita pergunta nova mesmo já respondido. A gravação segura a linha do formulário e confere que ele continua como foi lido; se não, relê e dá o erro certo.
/// </summary>
public sealed class AdicionarPerguntaCasoDeUso(IConsultaDeFormularios consulta, IPerguntasDoFormularioRepositorio perguntas, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<PerguntaFormulario> ExecutarAsync(int formularioId, PerguntaComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= RegrasDasPerguntasDoRh.Tentativas; tentativa++)
        {
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var erros = new ColetorDeErros();
            var campos = ValidacaoDaPergunta.Validar(erros, corpo);
            erros.LancarSeHouver();
            RegrasDasPerguntasDoRh.ConferirEstado(MapeadorDoFormulario.SituacaoDe(lido, relogio.AgoraUtc), lido.Anonimo);
            RegrasDasPerguntasDoRh.ConferirArquivoNoAnonimo(lido.Anonimo, corpo.Tipo);

            var fim = RegrasDasPerguntasDoRh.Fim(lido);
            var posicao = Math.Clamp(campos!.Ordem ?? fim, 1, fim);
            try
            {
                var id = await unidade.ExecutarEmTransacaoAsync(
                    async () =>
                    {
                        if (!await perguntas.SegurarSeIgualAoLidoAsync(lido, ct))
                        {
                            return (int?)null;
                        }

                        var novo = await perguntas.AdicionarAsync(formularioId, new PerguntaParaGravar(campos.Texto, corpo.Tipo, campos.Obrigatoria, campos.Alternativas), posicao, ct);
                        auditor.Registrar(RegrasDasPerguntasDoRh.Evento(lido, "Acrescentou uma pergunta ao formulário"));
                        await unidade.SalvarAsync(ct);
                        return novo;
                    },
                    ct);
                if (id is { } criada)
                {
                    var depois = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
                    return MapeadorDoFormulario.Pergunta(depois.Perguntas.Single(p => p.Id == criada));
                }
            }
            catch (ProblemaDeApiException problema) when (RegrasDasPerguntasDoRh.Corrida(problema) && tentativa < RegrasDasPerguntasDoRh.Tentativas)
            {
                // duas abas na mesma posição: a chave única da ordem recusou a segunda; relê e tenta de novo
            }
        }

        throw RegrasDasPerguntasDoRh.PerdeuACorrida();
    }
}

/// <summary>
/// <c>PUT /api/rh/formularios/{formularioId}/perguntas/{perguntaId}</c>: troca o conteúdo inteiro (alternativa com <c>id</c> mantém, sem <c>id</c> é nova, a que sumiu é apagada) e, com <c>ordem</c>, move a pergunta (sem <c>ordem</c>, ela fica). Pergunta já
/// respondida (formulário com nome publicado): só <c>obrigatoria</c> e <c>ordem</c> mudam; texto, tipo e alternativas diferentes = 422 <c>PERGUNTA_JA_RESPONDIDA</c> com a <c>regraDoBanco</c> do trigger. Anônimo publicado: sempre 409, sem olhar resposta.
/// Ordem das recusas: 404 (formulário ou pergunta de outro formulário), 400, 422 encerrado, 409 anônimo publicado, 422 arquivo no anônimo, 422 respondida.
/// </summary>
public sealed class EditarPerguntaCasoDeUso(IConsultaDeFormularios consulta, IPerguntasDoFormularioRepositorio perguntas, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const string AlternativaInvalida = "Uma das alternativas não existe nesta pergunta ou foi repetida.";

    public async Task<PerguntaFormulario> ExecutarAsync(int formularioId, int perguntaId, PerguntaComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= RegrasDasPerguntasDoRh.Tentativas; tentativa++)
        {
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var atual = lido.Perguntas.FirstOrDefault(p => p.Id == perguntaId).OuNaoEncontrado();
            var erros = new ColetorDeErros();
            var campos = ValidacaoDaPergunta.Validar(erros, corpo);
            ConferirIdsDasAlternativas(erros, campos, atual);
            erros.LancarSeHouver();
            var situacao = MapeadorDoFormulario.SituacaoDe(lido, relogio.AgoraUtc);
            RegrasDasPerguntasDoRh.ConferirEstado(situacao, lido.Anonimo);
            RegrasDasPerguntasDoRh.ConferirArquivoNoAnonimo(lido.Anonimo, corpo.Tipo);
            var nova = new PerguntaParaGravar(campos!.Texto, corpo.Tipo, campos.Obrigatoria, campos.Alternativas);
            if (situacao == SituacaoDoFormulario.Publicado && !MudaSoObrigatoriaEOrdem(atual, nova) && await consulta.PerguntaTemRespostaAsync(perguntaId, ct))
            {
                throw MensagensDosFormularios.PerguntaJaRespondidaParaEditar.ComoProblema(RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoAlterar);
            }

            int? novaPosicao = campos.Ordem is { } pedida ? Math.Clamp(pedida, 1, Math.Max(RegrasDasPerguntasDoRh.Fim(lido) - 1, 1)) : null;
            try
            {
                var gravou = await unidade.ExecutarEmTransacaoAsync(
                    async () =>
                    {
                        if (!await perguntas.SegurarSeIgualAoLidoAsync(lido, ct))
                        {
                            return false;
                        }

                        await perguntas.EditarAsync(formularioId, atual, nova, novaPosicao, ct);
                        auditor.Registrar(RegrasDasPerguntasDoRh.Evento(lido, "Alterou uma pergunta do formulário"));
                        await unidade.SalvarAsync(ct);
                        return true;
                    },
                    ct);
                if (gravou)
                {
                    var depois = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
                    return MapeadorDoFormulario.Pergunta(depois.Perguntas.Single(p => p.Id == perguntaId));
                }
            }
            catch (ProblemaDeApiException problema) when (RegrasDasPerguntasDoRh.Corrida(problema) && tentativa < RegrasDasPerguntasDoRh.Tentativas)
            {
                // outra aba mexeu nas posições no meio: relê e tenta de novo
            }
        }

        throw RegrasDasPerguntasDoRh.PerdeuACorrida();
    }

    /// <summary>Os ids que vêm nas alternativas têm de ser de alternativas desta pergunta, sem repetir.</summary>
    private static void ConferirIdsDasAlternativas(ColetorDeErros erros, PerguntaValidada? campos, PerguntaLida atual)
    {
        if (campos is null)
        {
            return;
        }

        var ids = campos.Alternativas.Where(a => a.Id is not null).Select(a => a.Id!.Value).ToList();
        if (ids.Count != ids.Distinct().Count() || ids.Any(id => atual.Alternativas.All(a => a.Id != id)))
        {
            erros.Adicionar("alternativas", AlternativaInvalida);
        }
    }

    /// <summary>O texto, o tipo e as alternativas (mesmos ids, mesmos textos, na mesma ordem) continuam como estão: só <c>obrigatoria</c> e <c>ordem</c> podem estar mudando.</summary>
    private static bool MudaSoObrigatoriaEOrdem(PerguntaLida atual, PerguntaParaGravar nova) =>
        atual.Texto == nova.Texto
        && atual.Tipo == nova.Tipo
        && atual.Alternativas.Count == nova.Alternativas.Count
        && atual.Alternativas.Zip(nova.Alternativas).All(par => par.First.Id == par.Second.Id && par.First.Texto == par.Second.Texto);
}

/// <summary>
/// <c>DELETE /api/rh/formularios/{formularioId}/perguntas/{perguntaId}</c>: apaga a pergunta e as alternativas dela; as seguintes sobem. Pergunta respondida = 422 <c>PERGUNTA_JA_RESPONDIDA</c> com a <c>regraDoBanco</c> de excluir; anônimo publicado = sempre 409.
/// </summary>
public sealed class ExcluirPerguntaCasoDeUso(IConsultaDeFormularios consulta, IPerguntasDoFormularioRepositorio perguntas, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task ExecutarAsync(int formularioId, int perguntaId, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= RegrasDasPerguntasDoRh.Tentativas; tentativa++)
        {
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var atual = lido.Perguntas.FirstOrDefault(p => p.Id == perguntaId).OuNaoEncontrado();
            var situacao = MapeadorDoFormulario.SituacaoDe(lido, relogio.AgoraUtc);
            RegrasDasPerguntasDoRh.ConferirEstado(situacao, lido.Anonimo);
            if (situacao == SituacaoDoFormulario.Publicado && await consulta.PerguntaTemRespostaAsync(perguntaId, ct))
            {
                throw MensagensDosFormularios.PerguntaJaRespondidaParaExcluir.ComoProblema(RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoExcluir);
            }

            try
            {
                var gravou = await unidade.ExecutarEmTransacaoAsync(
                    async () =>
                    {
                        if (!await perguntas.SegurarSeIgualAoLidoAsync(lido, ct))
                        {
                            return false;
                        }

                        await perguntas.ExcluirAsync(formularioId, atual, ct);
                        auditor.Registrar(RegrasDasPerguntasDoRh.Evento(lido, "Excluiu uma pergunta do formulário"));
                        await unidade.SalvarAsync(ct);
                        return true;
                    },
                    ct);
                if (gravou)
                {
                    return;
                }
            }
            catch (ProblemaDeApiException problema) when (RegrasDasPerguntasDoRh.Corrida(problema) && tentativa < RegrasDasPerguntasDoRh.Tentativas)
            {
                // outra aba mexeu nas posições no meio: relê e tenta de novo
            }
        }

        throw RegrasDasPerguntasDoRh.PerdeuACorrida();
    }
}

using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>A resposta de <c>concluirModuloDeTreinamento</c>: <c>TreinamentoConcluido</c> é verdadeiro só quando ESTA chamada fechou o treinamento (o módulo era o último que faltava).</summary>
public sealed record TreinamentoModuloConclusaoResposta(int ModuloId, bool TreinamentoConcluido, TreinamentoMeuDetalhe Treinamento);

/// <summary>
/// <c>POST /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/concluir</c>. Não existe "concluir treinamento": ao concluir o último módulo a linha da pessoa passa a <c>CONCLUIDO</c>, o trigger cria a linha do certificado e a aplicação troca, na MESMA transação, o código (UUID v1) por um UUID versão 4 e a data de emissão pelo relógio dela; o PDF sai depois do commit. Ordem: 404 (como
/// o conteúdo; módulo de outro treinamento também), 422 <c>INSCRICAO_PENDENTE</c> (opcional sem inscrição), 422 <c>TREINAMENTO_ENCERRADO</c> (o RH encerrou e a pessoa não tinha começado; quem já estava em andamento conclui normalmente). A transação começa TRAVANDO a linha do treinamento (a mesma trava de encerrar e excluir; duas conclusões do mesmo
/// treinamento se enfileiram) e RELÊ tudo com ela travada, então os dois últimos módulos concluídos ao mesmo tempo dão um só <c>CONCLUIDO</c> e um só certificado. Repetir um módulo já concluído devolve o estado atual sem escrever nada, EXCETO no caso quebrado em que todos os módulos estão concluídos e a linha não é <c>CONCLUIDO</c>: aí fecha o treinamento. A primeira conclusão
/// em <c>INSCRITO</c> também inicia (<c>EM_ANDAMENTO</c>, <c>DATA_INICIO</c> = hoje). A nota nunca é gravada. Evento <c>ALTEROU</c> só quando o treinamento é concluído (sem o código do certificado).
/// </summary>
public sealed class ConcluirModuloDeTreinamentoCasoDeUso(
    IMinhaParticipacaoRepositorio participacao,
    IConclusaoDoModulo conclusao,
    IModulosRepositorio modulos,
    IConsultaDeMeusTreinamentos consulta,
    IEmissaoDoCertificado emissao,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IEscopoDoUsuario escopo,
    IRelogio relogio)
{
    public async Task<TreinamentoModuloConclusaoResposta> ExecutarAsync(int treinamentoId, int moduloId, CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        _ = Conferir(await participacao.LerAsync(eu, treinamentoId, ct), hoje);
        _ = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();

        var (fechou, emite) = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                _ = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                var dentro = Conferir(await participacao.LerAsync(eu, treinamentoId, ct), hoje);
                if (dentro.Situacao == SituacaoNoTreinamento.Concluido)
                {
                    return (false, false); // já concluído (o banco recusa mudar um CONCLUIDO): nada a escrever
                }

                var progresso = await conclusao.LerProgressoAsync(eu, treinamentoId, ct);
                var jaConcluido = progresso.ModulosConcluidos.Contains(moduloId);
                var concluidos = progresso.ModulosConcluidos.Count + (jaConcluido ? 0 : 1);
                var tudo = progresso.TotalDeModulos > 0 && concluidos == progresso.TotalDeModulos;
                if (jaConcluido && !tudo)
                {
                    return (false, false); // repetir não toca no banco
                }

                if (!jaConcluido)
                {
                    await conclusao.RegistrarProgressoAsync(moduloId, eu, InstanteAoSegundo.De(agora), ct);
                }

                if (dentro.Situacao == SituacaoNoTreinamento.Inscrito)
                {
                    _ = await participacao.IniciarAsync(eu, treinamentoId, hoje, ct); // a primeira conclusão também inicia
                }

                if (!tudo)
                {
                    await unidade.SalvarAsync(ct);
                    return (false, false);
                }

                if (!await conclusao.ConcluirTreinamentoAsync(eu, treinamentoId, hoje, ct))
                {
                    throw MensagensDosTreinamentos.TreinamentoMudouEmOutraTela;
                }

                var treinamento = (await consulta.ObterAsync(eu, treinamentoId, hoje, ct)).OuNaoEncontrado();
                _ = await conclusao.TrocarCodigoDoCertificadoAsync(eu, treinamentoId, CodigoDoCertificado.Novo(), hoje, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Concluiu o treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return (true, treinamento.EmiteCertificado);
            },
            ct);

        if (fechou && emite)
        {
            await emissao.EmitirDepoisDoCommitAsync(eu, treinamentoId, ct);
        }

        return new TreinamentoModuloConclusaoResposta(moduloId, fechou, MapeadorDoMeuTreinamento.Detalhe((await consulta.ObterAsync(eu, treinamentoId, hoje, ct)).OuNaoEncontrado()));
    }

    /// <summary>O acesso de quem conclui: o do conteúdo (404 / 422 de inscrição pendente) e, no encerrado, quem nunca começou recebe 422 <c>TREINAMENTO_ENCERRADO</c> (o encerramento fecha a porta de quem estava só inscrito).</summary>
    private static TreinamentoParaParticipar Conferir(TreinamentoParaParticipar? treinamento, DateOnly hoje)
    {
        var t = AcessoDaParticipacao.Conferir(treinamento, hoje);
        if (!t.TemLinha)
        {
            if (t.Obrigatorio)
            {
                throw new NaoEncontradoException();
            }

            throw (t.EncerradoEmUtc is not null ? MensagensDosTreinamentos.EncerradoParaAPessoa : MensagensDosTreinamentos.InscricaoPendente).ComoProblema();
        }

        if (!AcessoDaParticipacao.LinhaVisivel(t))
        {
            throw new NaoEncontradoException();
        }

        if (t.EncerradoEmUtc is not null && t.Situacao == SituacaoNoTreinamento.Inscrito)
        {
            throw MensagensDosTreinamentos.EncerradoParaAPessoa.ComoProblema();
        }

        return t;
    }
}

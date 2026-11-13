using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>O que as duas rotas da participação conferem antes de mexer na linha: o treinamento existe, está publicado e liberado, e a pessoa é dele (tem linha ou é do público); senão é 404 (nunca diz se existe).</summary>
internal static class AcessoDaParticipacao
{
    public static TreinamentoParaParticipar Conferir(TreinamentoParaParticipar? treinamento, DateOnly hoje)
    {
        var t = treinamento.OuNaoEncontrado();
        if (!RegraDaSituacaoDoTreinamento.Liberado(t.Publicado, t.ComecaEm, hoje) || (!t.TemLinha && !t.DoMeuPublico))
        {
            throw new NaoEncontradoException();
        }

        return t;
    }

    /// <summary>A linha <c>REPROVADO</c>, <c>CANCELADO</c> ou sem situação (dado antigo) não existe para a pessoa: 404.</summary>
    public static bool LinhaVisivel(TreinamentoParaParticipar t) => t.Situacao is SituacaoNoTreinamento.Inscrito or SituacaoNoTreinamento.EmAndamento or SituacaoNoTreinamento.Concluido;
}

/// <summary>
/// <c>POST /api/eu/treinamentos/{treinamentoId}/inscricao</c>: só treinamento OPCIONAL do público da pessoa, publicado e liberado. Ordem: 404 (não existe, não publicado, não liberado, fora do público); 422 <c>TREINAMENTO_NAO_E_OPCIONAL</c> (obrigatório não tem inscrição); linha já existente = inofensivo, devolve o treinamento sem mudar nada (a linha
/// <c>REPROVADO</c> é 404); 422 <c>TREINAMENTO_ENCERRADO</c> (ninguém novo entra, nem quem só tinha se inscrito e nunca começou); senão cria a linha <c>INSCRITO</c>. Duas telas ao mesmo tempo: a chave única recusa a segunda e ela devolve o mesmo treinamento. Sem evento de auditoria.
/// </summary>
public sealed class InscreverMeEmTreinamentoCasoDeUso(IMinhaParticipacaoRepositorio participacao, IConsultaDeMeusTreinamentos consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<TreinamentoMeuDetalhe> ExecutarAsync(int treinamentoId, CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var t = AcessoDaParticipacao.Conferir(await participacao.LerAsync(eu, treinamentoId, ct), hoje);
        if (t.Obrigatorio)
        {
            throw MensagensDosTreinamentos.NaoEOpcional.ComoProblema();
        }

        if (t.TemLinha)
        {
            if (!AcessoDaParticipacao.LinhaVisivel(t))
            {
                throw new NaoEncontradoException();
            }

            if (t.EncerradoEmUtc is not null && t.Situacao == SituacaoNoTreinamento.Inscrito)
            {
                throw MensagensDosTreinamentos.EncerradoParaAPessoa.ComoProblema();
            }
        }
        else
        {
            if (t.EncerradoEmUtc is not null)
            {
                throw MensagensDosTreinamentos.EncerradoParaAPessoa.ComoProblema();
            }

            _ = await participacao.InscreverAsync(eu, treinamentoId, ct); // false = outra tela inscreveu antes: o resultado é o mesmo
        }

        return MapeadorDoMeuTreinamento.Detalhe((await consulta.ObterAsync(eu, treinamentoId, hoje, ct)).OuNaoEncontrado());
    }
}

/// <summary>
/// <c>POST /api/eu/treinamentos/{treinamentoId}/iniciar</c> ("Começar"): <c>INSCRITO</c> vira <c>EM_ANDAMENTO</c> com <c>DATA_INICIO</c> = hoje (do relógio da aplicação), por <c>UPDATE</c> condicional. Repetir é inofensivo: já em andamento ou concluído devolve o estado atual SEM <c>UPDATE</c> (o banco recusa mudar o concluído). Ordem: 404 (como a inscrição; obrigatório sem linha também); 422
/// <c>TREINAMENTO_ENCERRADO</c> (o RH encerrou e a pessoa não tinha começado: vale também para o opcional ainda sem inscrição); 422 <c>INSCRICAO_PENDENTE</c> (opcional sem inscrição). Quem já estava em andamento continua. Sem evento de auditoria.
/// </summary>
public sealed class IniciarMeuTreinamentoCasoDeUso(IMinhaParticipacaoRepositorio participacao, IConsultaDeMeusTreinamentos consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<TreinamentoMeuDetalhe> ExecutarAsync(int treinamentoId, CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var t = AcessoDaParticipacao.Conferir(await participacao.LerAsync(eu, treinamentoId, ct), hoje);
        if (!t.TemLinha)
        {
            if (t.Obrigatorio)
            {
                throw new NaoEncontradoException(); // obrigatório sem linha: a pessoa não o vê (quem é do público ainda vai ganhar a linha)
            }

            throw (t.EncerradoEmUtc is not null ? MensagensDosTreinamentos.EncerradoParaAPessoa : MensagensDosTreinamentos.InscricaoPendente).ComoProblema();
        }

        if (!AcessoDaParticipacao.LinhaVisivel(t))
        {
            throw new NaoEncontradoException();
        }

        if (t.Situacao == SituacaoNoTreinamento.Inscrito)
        {
            if (t.EncerradoEmUtc is not null)
            {
                throw MensagensDosTreinamentos.EncerradoParaAPessoa.ComoProblema();
            }

            if (!await participacao.IniciarAsync(eu, treinamentoId, hoje, ct))
            {
                // outra tela iniciou (ou o RH encerrou) entre a leitura e o UPDATE: relê e decide pelo estado atual
                var atual = AcessoDaParticipacao.Conferir(await participacao.LerAsync(eu, treinamentoId, ct), hoje);
                if (atual.Situacao == SituacaoNoTreinamento.Inscrito && atual.EncerradoEmUtc is not null)
                {
                    throw MensagensDosTreinamentos.EncerradoParaAPessoa.ComoProblema();
                }
            }
        }

        return MapeadorDoMeuTreinamento.Detalhe((await consulta.ObterAsync(eu, treinamentoId, hoje, ct)).OuNaoEncontrado());
    }
}

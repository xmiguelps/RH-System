using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// <c>POST /api/eu/ponto/marcacoes</c>: a pessoa bate o ponto AGORA, com a hora do servidor; o corpo só diz que batida ela acha que está fazendo (<c>tipoEsperado</c>). Tudo acontece numa transação <c>READ COMMITTED</c> com a pessoa travada
/// (<c>FOR UPDATE</c>): o duplo clique e as duas abas se enfileiram, e o segundo vê a batida do primeiro. Recusas, nesta ordem: pessoa inativa, dia anterior à admissão ou posterior ao desligamento (a aplicação confere antes do banco, com o mesmo texto),
/// batida no mesmo segundo de outra (409 <c>PONTO_BATIDA_DUPLICADA</c>) e tipo diferente do que o servidor calcula (409 <c>PONTO_SEQUENCIA_MUDOU</c>). A SAÍDA de uma jornada que atravessou a meia-noite entra no dia da ENTRADA.
/// </summary>
public sealed class RegistrarMarcacaoCasoDeUso(
    IEscopoDoUsuario escopo,
    IPontoRepositorio repositorio,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio,
    MontadorDoCartaoDeHoje montador)
{
    public async Task<PontoHoje> ExecutarAsync(PontoMarcacaoRegistrar corpo, CancellationToken ct)
    {
        var tipoEsperado = corpo.TipoEsperado;
        var funcionarioId = escopo.FuncionarioId;

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var agoraAoSegundo = MarcacaoDePonto.AoSegundo(relogio.AgoraUtc);
                var hoje = FusoBrasil.Hoje(agoraAoSegundo);
                ConferirPessoa(pessoa, hoje);

                var deHoje = await repositorio.BatidasDoDiaAsync(funcionarioId, hoje, ct);
                var deOntem = await repositorio.BatidasDoDiaAsync(funcionarioId, hoje.AddDays(-1), ct);
                if (deHoje.Concat(deOntem).Any(b => b.InstanteUtc == agoraAoSegundo))
                {
                    throw MensagensDoPonto.BatidaDuplicadaNoRegistro.ComoProblema();
                }

                var proxima = SequenciaDoDia.ProximaMarcacao(Marcacoes(deHoje), Marcacoes(deOntem), agoraAoSegundo);
                if (proxima.Tipo != tipoEsperado)
                {
                    throw MensagensDoPonto.SequenciaMudou.ComoProblema();
                }

                var diaDoPonto = proxima.NoDiaAnterior ? hoje.AddDays(-1) : hoje;
                var batidaId = await repositorio.GravarBatidaAsync(funcionarioId, diaDoPonto, agoraAoSegundo, proxima.Tipo, ct);

                // O evento não leva a hora nem o corpo da requisição; o IP e a origem (portal) vêm do ator.
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ponto,
                    AuditoriaAcao.Registrou,
                    $"Registrou {(proxima.Tipo == TipoDeMarcacao.Entrada ? "uma entrada" : "uma saída")} no ponto do dia {diaDoPonto:dd/MM/yyyy}.",
                    $"Ponto de {diaDoPonto:dd/MM/yyyy}",
                    "PONTOS_MARCACOES",
                    batidaId));
                await unidade.SalvarAsync(ct);
                return batidaId;
            },
            IsolationLevel.ReadCommitted,
            ct);

        return await montador.MontarAsync(funcionarioId, ct);
    }

    /// <summary>O mesmo que o trigger de <c>PONTOS</c> confere, com as mesmas mensagens e a mesma resposta (o contrato manda a mensagem original em <c>regraDoBanco</c>).</summary>
    private static void ConferirPessoa(PessoaDoPonto pessoa, DateOnly hoje)
    {
        if (!pessoa.Ativo)
        {
            throw MensagensDoPonto.FuncionarioInativoNoRegistro.ComoProblema(RegrasDoBancoDoPonto.FuncionarioInativo);
        }

        if (hoje < pessoa.DataAdmissao)
        {
            throw MensagensDoPonto.OperacaoRecusada.ComoProblema(RegrasDoBancoDoPonto.AnteriorAAdmissao);
        }

        if (pessoa.DataDesligamento is { } desligamento && hoje > desligamento)
        {
            throw MensagensDoPonto.OperacaoRecusada.ComoProblema(RegrasDoBancoDoPonto.PosteriorAoDesligamento);
        }
    }

    private static IReadOnlyList<MarcacaoDoDia> Marcacoes(IReadOnlyList<BatidaLida> batidas) => [.. batidas.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo))];
}

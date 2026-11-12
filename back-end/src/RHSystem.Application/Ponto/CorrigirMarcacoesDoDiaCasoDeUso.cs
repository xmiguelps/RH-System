using System.Data;
using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// <c>PUT /api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}</c>: o RH SUBSTITUI a lista de batidas do dia (<see cref="SubstituicaoDoDia"/>), direto, sem segunda etapa e sem e-mail. Numa transação com a pessoa travada (a mesma trava do bater ponto). Recusas:
/// 400 do corpo (mais de 20 batidas, horário sem fuso, <c>id</c> de outro dia ou repetido); 422 pessoa inativa, dia anterior à admissão, dia futuro e horário no futuro ou fora do dia (o próprio dia ou o seguinte); 409 o dia tem pedido em análise
/// (o RH responde o pedido antes de editar); 409 batida no mesmo segundo; 409 batida ligada a um pedido que seria apagada; 422 sequência que não fecha. A batida antiga fica no log de auditoria (campo a campo); o banco de horas do dia é recalculado.
/// </summary>
public sealed class CorrigirMarcacoesDoDiaCasoDeUso(
    IConsultaDoPonto consultaDoPonto,
    IPontoRepositorio ponto,
    IPedidosDeAjusteRepositorio pedidos,
    IRecalculoDoBancoDeHoras recalculo,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio,
    MontadorDoEspelho montador)
{
    public const int MaximoDeBatidas = 20;

    public async Task<PontoEspelhoDia> ExecutarAsync(int funcionarioId, DateOnly dia, PontoDiaCorrigir corpo, CancellationToken ct)
    {
        var lista = Validar(corpo);
        var pessoa = (await consultaDoPonto.PessoaAsync(funcionarioId, ct)).OuNaoEncontrado();

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var travada = await ponto.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;
                ConferirPessoa(travada, dia);
                ConferirOsHorarios(dia, lista, agora);

                if (await pedidos.TemPendenteNoDiaAsync(funcionarioId, dia, ct))
                {
                    throw MensagensDoPonto.DiaComPedidoEmAnalise.ComoProblema();
                }

                var atuais = await ponto.BatidasDoDiaAsync(funcionarioId, dia, ct);
                var substituicao = SubstituicaoDoDia.Substituir(atuais, lista);
                if (substituicao.Erro is { } erro)
                {
                    throw DoErroDeAplicacao(erro);
                }

                await ponto.CorrigirDiaAsync(funcionarioId, dia, substituicao.Dia, substituicao.Removidas, agora, ct);

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ponto,
                    AuditoriaAcao.Alterou,
                    $"Corrigiu as batidas do ponto do dia {dia:dd/MM/yyyy}.",
                    $"Ponto de {pessoa.Nome} em {dia:dd/MM/yyyy}",
                    "FUNCIONARIOS",
                    funcionarioId,
                    Mudancas(atuais, substituicao)));
                await unidade.SalvarAsync(ct);
                await recalculo.RecalcularDiaAsync(funcionarioId, dia, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        return MapeamentoDoEspelho.Dia(await montador.MontarDiaAsync(pessoa, dia, ct));
    }

    private static IReadOnlyList<BatidaNaLista> Validar(PontoDiaCorrigir corpo)
    {
        var erros = new ColetorDeErros();
        var lista = new List<BatidaNaLista>();
        if (corpo.Marcacoes is null)
        {
            erros.Adicionar("marcacoes", "Informe a lista de batidas do dia (pode ser vazia).");
        }
        else if (corpo.Marcacoes.Count > MaximoDeBatidas)
        {
            erros.Adicionar("marcacoes", $"Um dia tem no máximo {MaximoDeBatidas} batidas.");
        }
        else
        {
            foreach (var item in corpo.Marcacoes)
            {
                if (item.InstanteUtc.Kind == DateTimeKind.Unspecified)
                {
                    erros.Adicionar("marcacoes", "Informe cada horário em UTC, com o Z no fim (por exemplo 2026-10-05T11:00:00Z).");
                    break;
                }

                lista.Add(new BatidaNaLista(item.Id, item.InstanteUtc.ToUniversalTime(), item.Tipo));
            }
        }

        erros.LancarSeHouver();
        return lista;
    }

    /// <summary>O mesmo que o trigger de <c>PONTOS</c> confere, com as mesmas mensagens e a mesma resposta.</summary>
    private static void ConferirPessoa(PessoaDoPonto pessoa, DateOnly dia)
    {
        if (!pessoa.Ativo)
        {
            throw MensagensDoPonto.FuncionarioInativoNaCorrecao.ComoProblema(RegrasDoBancoDoPonto.FuncionarioInativo);
        }

        if (dia < pessoa.DataAdmissao)
        {
            throw MensagensDoPonto.OperacaoRecusada.ComoProblema(RegrasDoBancoDoPonto.AnteriorAAdmissao);
        }

        if (pessoa.DataDesligamento is { } desligamento && dia > desligamento)
        {
            throw MensagensDoPonto.OperacaoRecusada.ComoProblema(RegrasDoBancoDoPonto.PosteriorAoDesligamento);
        }
    }

    private static void ConferirOsHorarios(DateOnly dia, IReadOnlyList<BatidaNaLista> lista, DateTime agora)
    {
        if (dia > FusoBrasil.Hoje(agora) || lista.Any(i => i.InstanteUtc > agora))
        {
            throw MensagensDoPonto.InstanteNoFuturo.ComoProblema();
        }

        var inicio = FusoBrasil.InicioDoDiaEmUtc(dia);
        var fim = FusoBrasil.InicioDoDiaEmUtc(dia.AddDays(2));
        if (lista.Any(i => i.InstanteUtc < inicio || i.InstanteUtc >= fim))
        {
            throw MensagensDoPonto.InstanteForaDoDia.ComoProblema();
        }
    }

    private static Exception DoErroDeAplicacao(ErroDeAplicacao erro) => erro switch
    {
        ErroDeAplicacao.MarcacaoInexistenteNoDia => new ErroDeValidacaoException("marcacoes", "A batida a corrigir não é deste dia."),
        ErroDeAplicacao.MarcacaoRepetida => new ErroDeValidacaoException("marcacoes", "Cada batida só pode aparecer uma vez na lista."),
        ErroDeAplicacao.BatidaDuplicada => MensagensDoPonto.BatidaDuplicadaNaCorrecao.ComoProblema(),
        _ => MensagensDoPonto.SequenciaInvalidaNoPedido.ComoProblema(),
    };

    /// <summary>O log da correção, batida por batida: a que mudou (antes e depois), a que entrou e a que saiu. Os horários são os de Brasília.</summary>
    private static IReadOnlyList<MudancaDeAuditoriaNova> Mudancas(IReadOnlyList<BatidaLida> atuais, ResultadoDaSubstituicao substituicao)
    {
        static string Hora(DateTime utc) => FusoBrasil.ParaLocal(utc).ToString("HH:mm:ss", CultureInfo.InvariantCulture);
        static string Nome(TipoDeMarcacao tipo) => tipo == TipoDeMarcacao.Entrada ? "entrada" : "saída";

        var mudancas = new List<MudancaDeAuditoriaNova>();
        foreach (var removida in atuais.Where(a => substituicao.Removidas.Contains(a.Id)))
        {
            mudancas.Add(new MudancaDeAuditoriaNova($"Batida removida ({Nome(removida.Tipo)})", Hora(removida.InstanteUtc), null));
        }

        foreach (var batida in substituicao.Dia.Where(b => b.Mudou))
        {
            var antes = batida.Id is { } id ? atuais.First(a => a.Id == id) : null;
            mudancas.Add(antes is null
                ? new MudancaDeAuditoriaNova($"Batida incluída ({Nome(batida.Tipo)})", null, Hora(batida.InstanteUtc))
                : new MudancaDeAuditoriaNova($"Batida corrigida ({Nome(antes.Tipo)})", $"{Nome(antes.Tipo)} {Hora(antes.InstanteUtc)}", $"{Nome(batida.Tipo)} {Hora(batida.InstanteUtc)}"));
        }

        return mudancas;
    }
}

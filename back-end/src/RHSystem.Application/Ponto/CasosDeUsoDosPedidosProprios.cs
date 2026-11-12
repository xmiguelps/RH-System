using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestores;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>Os valores de <c>ordenarPor</c> das listas de pedidos (<c>PontoPedidoOrdenarPor</c> do contrato: <c>CRIADO_EM</c> e <c>DIA_DO_PONTO</c>); só para o documento da API descrevê-los.</summary>
public enum PontoPedidoOrdenarPor
{
    CriadoEm,
    DiaDoPonto,
}

/// <summary>Lê os filtros de texto das listas de pedidos pelo nome do contrato (<c>PENDENTE</c>, <c>GESTOR</c>, <c>DIA_DO_PONTO</c>, <c>DESC</c>) e confere a paginação, com os erros por campo.</summary>
public static class LeitorDeFiltrosDePedidos
{
    public static (ParametrosDeLista Parametros, FiltroDePedidos Filtro) Ler(int pagina, int tamanhoPagina, string? status, string? etapa, string? ordenarPor, string? direcao, int? funcionarioId)
    {
        var filtroDeStatus = EnumsDeConsulta.Ler<StatusDoPedidoDeAjuste>(status, "status");
        var filtroDeEtapa = EnumsDeConsulta.Ler<EtapaDoPedido>(etapa, "etapa");
        var sentido = EnumsDeConsulta.Ler<DirecaoDeOrdenacao>(direcao, "direcao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, null, ordenarPor, sentido).Validado();
        return (parametros, new FiltroDePedidos(filtroDeStatus, filtroDeEtapa, funcionarioId));
    }
}

/// <summary><c>GET /api/eu/ponto/pedidos-de-ajuste</c>: os pedidos da PRÓPRIA pessoa (qualquer perfil); <c>podeDecidir</c> é sempre <c>false</c>.</summary>
public sealed class ListarMeusPedidosCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta)
{
    public async Task<RespostaPaginada<PontoPedidoAjuste>> ExecutarAsync(int pagina, int tamanhoPagina, string? status, string? etapa, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var (parametros, filtro) = LeitorDeFiltrosDePedidos.Ler(pagina, tamanhoPagina, status, etapa, ordenarPor, direcao, funcionarioId: null);
        var pagina1 = await consulta.ListarAsync(new EscopoDePedidos(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null, null), filtro, parametros, ct);
        var quem = new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null);
        return RespostaPaginada<PontoPedidoAjuste>.De([.. pagina1.Itens.Select(p => MapeadorDoPedidoDeAjuste.Montar(p, quem))], pagina1.Total, parametros);
    }
}

/// <summary><c>GET /api/eu/ponto/pedidos-de-ajuste/{pedidoId}</c>: um pedido da PRÓPRIA pessoa; o de outra pessoa dá o mesmo 404 de um que não existe.</summary>
public sealed class ObterMeuPedidoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDePedidosDeAjuste consulta)
{
    public async Task<PontoPedidoAjuste> ExecutarAsync(int pedidoId, CancellationToken ct)
    {
        var pedido = (await consulta.ObterAsync(pedidoId, new EscopoDePedidos(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null, null), ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(pedido, new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null));
    }
}

/// <summary>
/// <c>POST /api/eu/ponto/pedidos-de-ajuste</c>: a pessoa pede o ajuste das batidas de UM dia (batidas novas ou a correção de batidas do dia). Tudo se confere ANTES de gravar, numa transação com a pessoa travada (a mesma trava do bater ponto), nesta ordem:
/// 400 do corpo; pessoa inativa, dia anterior à admissão; o dia ainda não passou; horário no futuro; horário fora do dia (o próprio dia ou o seguinte); já há pedido em análise do dia (409); e a sequência do dia DEPOIS de aplicar os itens.
/// A etapa vem de <see cref="IGestorAusente"/> e já nasce gravada (aprovação por ausência inclusive). Nada é aplicado às batidas e <b>nenhum e-mail sai</b> (o e-mail é do resultado, a cada decisão).
/// </summary>
public sealed class CriarPedidoDeAjusteCasoDeUso(
    IEscopoDoUsuario escopo,
    IPontoRepositorio ponto,
    IPedidosDeAjusteRepositorio pedidos,
    IGestorAusente gestorAusente,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio,
    IConsultaDePedidosDeAjuste consulta)
{
    private const int TamanhoMinimoDaJustificativa = 5;
    private const int TamanhoMaximoDaJustificativa = 500;
    private const int MaximoDeItens = 10;

    public async Task<PontoPedidoAjuste> ExecutarAsync(PontoPedidoAjusteCriar corpo, CancellationToken ct)
    {
        var (justificativa, itens) = Validar(corpo);
        var funcionarioId = escopo.FuncionarioId;
        var dia = corpo.Data;

        var pedidoId = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await ponto.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;
                ConferirPessoa(pessoa, dia);
                ConferirOsHorarios(dia, itens, agora);

                if (await pedidos.TemPendenteNoDiaAsync(funcionarioId, dia, ct))
                {
                    throw MensagensDoPonto.PedidoJaEmAnalise.ComoProblema();
                }

                var aplicado = AplicacaoDosItens.Aplicar(await ponto.BatidasDoDiaAsync(funcionarioId, dia, ct), itens);
                if (aplicado.Erro is { } erro)
                {
                    throw DoErroDeAplicacao(erro);
                }

                var etapa = await gestorAusente.EtapaInicialDoPedidoAsync(funcionarioId, ct);
                var id = await pedidos.CriarAsync(funcionarioId, dia, justificativa, agora, etapa, itens, ct);

                // O evento não leva a justificativa nem os horários.
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ponto,
                    AuditoriaAcao.PediuAjuste,
                    $"Pediu o ajuste do ponto do dia {dia:dd/MM/yyyy}.",
                    $"Ponto de {dia:dd/MM/yyyy}",
                    "PONTOS_MANUTENCOES",
                    id));
                await unidade.SalvarAsync(ct);
                return id;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var criado = (await consulta.ObterAsync(pedidoId, new EscopoDePedidos(PapelNaConsulta.PropriaPessoa, funcionarioId, null, null), ct)).OuNaoEncontrado();
        return MapeadorDoPedidoDeAjuste.Montar(criado, new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, funcionarioId, null));
    }

    private static (string Justificativa, IReadOnlyList<ItemParaAplicar> Itens) Validar(PontoPedidoAjusteCriar corpo)
    {
        var erros = new ColetorDeErros();
        var justificativa = (corpo.Justificativa ?? string.Empty).Trim();
        if (justificativa.Length is < TamanhoMinimoDaJustificativa or > TamanhoMaximoDaJustificativa)
        {
            erros.Adicionar("justificativa", "A justificativa tem de 5 a 500 caracteres.");
        }

        var itens = new List<ItemParaAplicar>();
        if (corpo.Itens is null || corpo.Itens.Count is < 1 or > MaximoDeItens)
        {
            erros.Adicionar("itens", "Informe de 1 a 10 batidas.");
        }
        else
        {
            foreach (var item in corpo.Itens)
            {
                itens.Add(new ItemParaAplicar(item.MarcacaoId, item.InstanteUtc, item.Tipo)); // o conversor do JSON só aceita Z ou deslocamento e já entrega UTC
            }
        }

        erros.LancarSeHouver();
        return (justificativa, itens);
    }

    /// <summary>O mesmo que o trigger de <c>PONTOS</c> confere, com as mesmas mensagens e a mesma resposta.</summary>
    private static void ConferirPessoa(PessoaDoPonto pessoa, DateOnly dia)
    {
        if (!pessoa.Ativo)
        {
            throw MensagensDoPonto.FuncionarioInativoNoPedido.ComoProblema(RegrasDoBancoDoPonto.FuncionarioInativo);
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

    private static void ConferirOsHorarios(DateOnly dia, IReadOnlyList<ItemParaAplicar> itens, DateTime agora)
    {
        if (dia >= FusoBrasil.Hoje(agora))
        {
            throw MensagensDoPonto.PedidoDiaNaoPassou.ComoProblema();
        }

        if (itens.Any(i => i.InstanteUtc > agora))
        {
            throw MensagensDoPonto.InstanteNoFuturo.ComoProblema();
        }

        // O dia do ponto e o seguinte, em -03:00 (a jornada que atravessa a meia-noite): de 00:00 do dia até 00:00 do segundo dia depois.
        var inicio = FusoBrasil.InicioDoDiaEmUtc(dia);
        var fim = FusoBrasil.InicioDoDiaEmUtc(dia.AddDays(2));
        if (itens.Any(i => i.InstanteUtc < inicio || i.InstanteUtc >= fim))
        {
            throw MensagensDoPonto.InstanteForaDoDia.ComoProblema();
        }
    }

    private static Exception DoErroDeAplicacao(ErroDeAplicacao erro) => erro switch
    {
        ErroDeAplicacao.MarcacaoInexistenteNoDia => new ErroDeValidacaoException("itens", "A batida a corrigir não é deste dia."),
        ErroDeAplicacao.MarcacaoRepetida => new ErroDeValidacaoException("itens", "Cada batida só pode ser corrigida uma vez no mesmo pedido."),
        _ => MensagensDoPonto.SequenciaInvalidaNoPedido.ComoProblema(),
    };
}

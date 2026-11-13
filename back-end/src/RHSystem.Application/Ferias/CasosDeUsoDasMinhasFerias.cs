using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestores;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

/// <summary><c>GET /api/eu/ferias</c>: o saldo, os períodos aquisitivos e todos os pedidos da PRÓPRIA pessoa (qualquer perfil); <c>podeDecidir</c> é sempre <c>false</c>.</summary>
public sealed class ObterMinhasFeriasCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta, IGestorAusente gestorAusente, IRelogio relogio)
{
    public async Task<FeriasEuResposta> ExecutarAsync(CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        var pessoa = await consulta.PessoaAsync(eu, ct) ?? throw new NaoEncontradoException();
        var linhas = await consulta.DaPessoaAsync(eu, ct);
        var saldo = SaldoDeFerias.De(pessoa.DataAdmissao, pessoa.DataDesligamento, FusoBrasil.Hoje(relogio.AgoraUtc), [.. linhas.Select(l => l.ParaSaldo())]);
        var quem = new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, eu, null);
        var historico = linhas.OrderByDescending(l => l.SolicitadaEmUtc).ThenByDescending(l => l.Id).Select(l => MontadorDoPedidoDeFerias.Montar(l, quem)).ToList();

        // Quem faz a primeira etapa: o gestor do setor. Nulo para quem é gestor, para setor sem gestor ativo e para gestor desligado (o pedido só passa pelo RH).
        var etapa = await gestorAusente.EtapaInicialDoPedidoAsync(eu, ct);
        var gestor = etapa.GestorId is { } gestorId ? await consulta.ResumoDoGestorAsync(gestorId, ct) : null;
        return new FeriasEuResposta(MontadorDoSaldoDeFerias.Resumo(saldo), gestor, MontadorDoSaldoDeFerias.Periodos(saldo), historico);
    }
}

/// <summary>
/// <c>POST /api/eu/ferias/conferencia</c>: as regras de um pedido de férias sem gravar nada; sempre 200 (quem barra é o <c>POST /api/eu/ferias</c>). É a conferência da pessoa: todas as regras, inclusive a antecedência.
/// </summary>
public sealed class ConferirPedidoDeFeriasCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta, ConferidorDeFerias conferidor, IRelogio relogio)
{
    public async Task<FeriasConferencia> ExecutarAsync(FeriasConferenciaComando corpo, CancellationToken ct)
    {
        ValidacaoDeFerias.Validar(corpo.DataInicio, corpo.DataFim, corpo.DiasAbono, null, "mensagem");
        var pessoa = await consulta.PessoaAsync(escopo.FuncionarioId, ct) ?? throw new NaoEncontradoException();
        var linhas = await consulta.DaPessoaAsync(escopo.FuncionarioId, ct);
        var conferencia = await conferidor.ConferirAsync(new ComandoDeFerias(corpo.DataInicio, corpo.DataFim, corpo.AbonoPecuniario, corpo.DiasAbono), ModoDaConferencia.Pessoa, pessoa, linhas, FusoBrasil.Hoje(relogio.AgoraUtc), null, ct);
        return MontadorDoSaldoDeFerias.Conferencia(conferencia);
    }
}

/// <summary>
/// <c>POST /api/eu/ferias</c>: a pessoa pede as próprias férias. Tudo se confere ANTES de gravar, numa transação com a pessoa TRAVADA (<c>FOR UPDATE</c>): dois pedidos ao mesmo tempo, ou o pedido e o registro do RH, se serializam e o saldo nunca estoura. Pessoa inativa: 422; sem período
/// aquisitivo fechado: 422; regra não atendida: 422 com a lista. A etapa vem de <see cref="IGestorAusente"/> e já nasce gravada (aprovação por ausência inclusive). <b>Nenhum e-mail sai</b> (o e-mail é de cada decisão) e o evento de auditoria não leva a mensagem.
/// </summary>
public sealed class PedirFeriasCasoDeUso(
    IEscopoDoUsuario escopo,
    IFeriasRepositorio repositorio,
    IConsultaDeFerias consulta,
    ConferidorDeFerias conferidor,
    IGestorAusente gestorAusente,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task<FeriasPedido> ExecutarAsync(FeriasPedirComando corpo, CancellationToken ct)
    {
        var mensagem = ValidacaoDeFerias.TextoGravado(corpo.Mensagem);
        ValidacaoDeFerias.Validar(corpo.DataInicio, corpo.DataFim, corpo.DiasAbono, corpo.Mensagem, "mensagem");
        var funcionarioId = escopo.FuncionarioId;
        var comando = new ComandoDeFerias(corpo.DataInicio, corpo.DataFim, corpo.AbonoPecuniario, corpo.DiasAbono);

        var id = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                if (!pessoa.Ativo)
                {
                    throw MensagensDaGestao.FuncionarioInativoDeFerias();
                }

                var agora = relogio.AgoraUtc;
                var linhas = await consulta.DaPessoaAsync(funcionarioId, ct);
                var conferencia = await conferidor.ConferirAsync(comando, ModoDaConferencia.Pessoa, pessoa, linhas, FusoBrasil.Hoje(agora), null, ct);
                if (conferencia.SemPeriodoAquisitivo)
                {
                    throw MensagensDaGestao.FeriasSemPeriodoAquisitivo.ComoProblema();
                }

                if (!conferencia.PodeEnviar)
                {
                    throw MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.PedirOuAceitar, conferencia.Regras);
                }

                var etapa = await gestorAusente.EtapaInicialDoPedidoAsync(funcionarioId, ct);
                var periodo = conferencia.Periodo!;
                var novo = await repositorio.CriarAsync(
                    PedidoDeFerias.Solicitar(funcionarioId, periodo.Inicio, periodo.Fim, corpo.DataInicio, corpo.DataFim, corpo.AbonoPecuniario, corpo.DiasAbono, corpo.Adiantar13Salario, mensagem, agora, etapa.GestorId, etapa.EtapaDoGestorAprovadaPorAusencia),
                    ct);

                // O evento não leva a mensagem da pessoa.
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ferias,
                    AuditoriaAcao.Criou,
                    $"Pediu férias de {corpo.DataInicio:dd/MM/yyyy} a {corpo.DataFim:dd/MM/yyyy}.",
                    $"Férias de {corpo.DataInicio:dd/MM/yyyy}",
                    "FERIAS",
                    novo));
                await unidade.SalvarAsync(ct);
                return novo;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var criado = await consulta.ObterDaPessoaAsync(id, funcionarioId, ct) ?? throw new NaoEncontradoException();
        return MontadorDoPedidoDeFerias.Montar(criado, new QuemConsultaOPedido(PapelNaConsulta.PropriaPessoa, funcionarioId, null));
    }
}

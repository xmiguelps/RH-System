using System.Data;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

/// <summary>Monta a aba Férias da ficha (a mesma do gestor e do RH): os números do saldo calculado, os períodos e os pedidos do mais novo ao mais antigo.</summary>
internal static class MontadorDaFichaDeFerias
{
    public static async Task<FeriasFicha> MontarAsync(IConsultaDeFerias consulta, int funcionarioId, DateOnly hoje, QuemConsultaOPedido quem, Func<FeriasLida, bool> naLista, CancellationToken ct)
    {
        var resumo = await consulta.ResumoAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var linhas = await consulta.DaPessoaAsync(funcionarioId, ct);
        var saldo = SaldoDeFerias.De(pessoa.DataAdmissao, pessoa.DataDesligamento, hoje, [.. linhas.Select(l => l.ParaSaldo())]);
        var pedidos = linhas.Where(naLista).OrderByDescending(l => l.SolicitadaEmUtc).ThenByDescending(l => l.Id).Select(l => MontadorDoPedidoDeFerias.Montar(l, quem)).ToList();
        return new FeriasFicha(resumo, MontadorDoSaldoDeFerias.Resumo(saldo), MontadorDoSaldoDeFerias.Periodos(saldo), pedidos);
    }
}

/// <summary>
/// <c>GET /api/gestor/equipe/{funcionarioId}/ferias</c>: a aba Férias da ficha da equipe do gestor, com TODOS os pedidos da pessoa (inclusive os que esperam o gestor). Pessoa de outro setor, quem é gestor (ele mesmo e os outros) e quem não existe dão o mesmo 404.
/// </summary>
public sealed class ObterFeriasDaPessoaDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta, IRelogio relogio)
{
    public async Task<FeriasFicha> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        var equipe = await GestorDasFerias.EquipeAsync(escopo, ct);
        if (!await consulta.EhDaEquipeAsync(funcionarioId, equipe, ct))
        {
            throw new NaoEncontradoException();
        }

        return await MontadorDaFichaDeFerias.MontarAsync(consulta, funcionarioId, FusoBrasil.Hoje(relogio.AgoraUtc), GestorDasFerias.Quem(escopo, equipe), _ => true, ct);
    }
}

/// <summary>
/// <c>GET /api/rh/funcionarios/{funcionarioId}/ferias</c>: a aba Férias da ficha do RH. A lista NÃO traz os pedidos que ainda estão com o gestor ou esperando a resposta da pessoa ("os pedidos aparecem aqui depois que o gestor analisa"), mas <c>resumo.pedidosEmAnalise</c> os conta. Pessoa que não existe: 404.
/// </summary>
public sealed class ObterFeriasDaFichaDoFuncionarioCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta, IRelogio relogio)
{
    public async Task<FeriasFicha> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        var quem = new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, await consulta.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct));
        return await MontadorDaFichaDeFerias.MontarAsync(
            consulta,
            funcionarioId,
            FusoBrasil.Hoje(relogio.AgoraUtc),
            quem,
            l => SituacaoDoPedidoDeFerias.Situacao(l.ComoLinha()) is not (SituacaoDasFerias.ComOGestor or SituacaoDasFerias.SugestaoDeData),
            ct);
    }
}

/// <summary><c>POST /api/rh/funcionarios/{funcionarioId}/ferias/conferencia</c>: as regras de um registro do RH sem gravar nada; sempre 200. Sem a antecedência de 30 dias (o RH está isento) e com os textos "da pessoa".</summary>
public sealed class ConferirRegistroDeFeriasCasoDeUso(IConsultaDeFerias consulta, ConferidorDeFerias conferidor, IRelogio relogio)
{
    public async Task<FeriasConferencia> ExecutarAsync(int funcionarioId, FeriasConferenciaComando corpo, CancellationToken ct)
    {
        ValidacaoDeFerias.Validar(corpo.DataInicio, corpo.DataFim, corpo.DiasAbono, null, "observacao");
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var linhas = await consulta.DaPessoaAsync(funcionarioId, ct);
        var conferencia = await conferidor.ConferirAsync(new ComandoDeFerias(corpo.DataInicio, corpo.DataFim, corpo.AbonoPecuniario, corpo.DiasAbono), ModoDaConferencia.Rh, pessoa, linhas, FusoBrasil.Hoje(relogio.AgoraUtc), null, ct);
        return MontadorDoSaldoDeFerias.Conferencia(conferencia);
    }
}

/// <summary>
/// <c>POST /api/rh/funcionarios/{funcionarioId}/ferias</c>: o RH registra férias que já nascem <c>APROVADA</c> (<c>etapa: CONCLUIDA</c>; sem gestor, sem decisões, sem e-mail), até no passado. As mesmas regras do pedido da pessoa, menos a antecedência; só entram os períodos aquisitivos que terminam antes
/// do início (o banco recusa início antes do fim do período). Pessoa inativa: 422; sem período fechado: 422; regra não atendida: 422 com a lista. A pessoa é travada ANTES de conferir (o pedido dela e o aceite de uma sugestão contam no mesmo saldo). Registro no passado: o banco de horas dos dias que
/// já passaram é recalculado DEPOIS do commit (o dia consolidado como falta deixa de ser falta); a falha do recálculo é registrada e nunca desfaz o registro.
/// </summary>
public sealed class RegistrarFeriasPeloRhCasoDeUso(
    IEscopoDoUsuario escopo,
    IFeriasRepositorio repositorio,
    IConsultaDeFerias consulta,
    ConferidorDeFerias conferidor,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRecalculoDoBancoDeHoras recalculo,
    IRelogio relogio,
    ILogger<RegistrarFeriasPeloRhCasoDeUso> log)
{
    public async Task<FeriasPedido> ExecutarAsync(int funcionarioId, FeriasRegistrarComando corpo, CancellationToken ct)
    {
        var observacao = ValidacaoDeFerias.TextoGravado(corpo.Observacao);
        ValidacaoDeFerias.Validar(corpo.DataInicio, corpo.DataFim, corpo.DiasAbono, corpo.Observacao, "observacao");
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
                var conferencia = await conferidor.ConferirAsync(comando, ModoDaConferencia.Rh, pessoa, linhas, FusoBrasil.Hoje(agora), null, ct);
                if (conferencia.SemPeriodoAquisitivo)
                {
                    throw MensagensDaGestao.FeriasSemPeriodoAquisitivo.ComoProblema();
                }

                if (!conferencia.PodeEnviar)
                {
                    throw MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.Registrar, conferencia.Regras);
                }

                var periodo = conferencia.Periodo!;
                var novo = await repositorio.CriarAsync(
                    PedidoDeFerias.Registrar(funcionarioId, periodo.Inicio, periodo.Fim, corpo.DataInicio, corpo.DataFim, corpo.AbonoPecuniario, corpo.DiasAbono, corpo.Adiantar13Salario, observacao, agora),
                    ct);

                // O evento não leva a observação do RH.
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ferias,
                    AuditoriaAcao.Registrou,
                    $"Registrou férias de {corpo.DataInicio:dd/MM/yyyy} a {corpo.DataFim:dd/MM/yyyy}.",
                    $"Férias de {pessoa.Nome}",
                    "FERIAS",
                    novo));
                await unidade.SalvarAsync(ct);
                return novo;
            },
            IsolationLevel.ReadCommitted,
            ct);

        await RecalcularOsDiasQueJaPassaramAsync(funcionarioId, corpo.DataInicio, corpo.DataFim, id, ct);

        var criado = await consulta.ObterAsync(id, ct) ?? throw new NaoEncontradoException();
        return MontadorDoPedidoDeFerias.Montar(criado, new QuemConsultaOPedido(PapelNaConsulta.Rh, escopo.FuncionarioId, await consulta.GestorIdDaPessoaAsync(escopo.FuncionarioId, ct)));
    }

    /// <summary>
    /// O banco de horas dos dias do período que já passaram (um dia consolidado como falta deixa de ser falta com as férias). O recálculo ignora hoje, o futuro e os dias fora do vínculo; uma falha aqui é registrada e o registro das férias, já gravado, continua valendo (a consolidação do dia seguinte
    /// refaz).
    /// </summary>
    private async Task RecalcularOsDiasQueJaPassaramAsync(int funcionarioId, DateOnly inicio, DateOnly fim, int feriasId, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        for (var dia = inicio; dia <= fim && dia < hoje; dia = dia.AddDays(1))
        {
            try
            {
                await recalculo.RecalcularDiaAsync(funcionarioId, dia, ct);
            }
            catch (Exception erro) when (erro is not OperationCanceledException)
            {
                log.LogWarning(erro, "Falha ao recalcular o banco de horas do dia {Dia} depois de registrar as férias {FeriasId}.", dia, feriasId);
            }
        }
    }
}

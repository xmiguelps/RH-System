using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// O desligamento do colaborador (<see cref="IDesligamentoDeFuncionarios"/>). <b>Ordem de travas</b>, a mesma nos três métodos: a linha de <c>FUNCIONARIOS</c> e depois a de <c>DESLIGAMENTOS_AGENDADOS</c> (<c>FOR UPDATE</c>).
/// Travar sempre a pessoa primeiro é o que serializa o clique duplo e a corrida entre cancelar e efetivar; <see cref="EfetivarAsync"/> lê o <c>FuncionarioId</c> SEM trava (não há outro jeito de saber a
/// pessoa) e <b>relê o estado depois de travar</b>. As transações são <c>READ COMMITTED</c> (como as filas): no padrão, o <c>FOR UPDATE</c> de uma linha que ainda não existe (pessoa sem desligamento vigente)
/// seguraria bloqueio de intervalo e duas pessoas diferentes se desligando juntas dariam deadlock.
/// <para>
/// <b>Efetivar</b> (o miolo, <see cref="EfetivarNoMioloAsync"/>): <c>Funcionario.Desligar(último dia)</c> e <c>Efetivar</c> na mesma transação; o trigger do grupo desativa o login e os benefícios e cancela as
/// férias futuras, sem aviso e sem registro próprio (decisão do dono); <c>DATA_DESLIGAMENTO</c> é o último dia de trabalho, que pode ser do passado. Se a pessoa é gestora, a porta única
/// <see cref="IPedidosParadosNoGestor"/> passa ao RH o que esperava por ela, na mesma transação (a etapa de um pedido é fixada na criação: "passar ao RH" é uma ação explícita, não um cálculo na leitura).
/// </para>
/// <para>
/// <b>Auditoria:</b> o que NÃO efetiva na hora grava o evento explícito "Desligamento de {nome} agendado"; o que efetiva (na hora ou pela tarefa) grava SÓ o <c>Inativou</c> do interceptor, que leva o último dia
/// pela marca <c>[Auditar]</c>: nunca dois eventos para o mesmo clique. O cancelamento grava "Cancelamento do desligamento de {nome}".
/// </para>
/// </summary>
public sealed class DesligamentoDeFuncionarios(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio,
    IAgendador agendador,
    IAuditor auditor,
    IPedidosParadosNoGestor pedidos,
    IEnumerable<ICondicaoParaEfetivarDesligamento> condicoes,
    IEnumerable<IAoEfetivarDesligamento> aoEfetivar) : IDesligamentoDeFuncionarios
{
    /// <summary>Mais longe que isto o dia é erro de digitação (9999-12-31 daria 500 em quem faz conta com a data).</summary>
    private const int AnosAFrenteNoMaximo = 10;

    private const string CodigoDeJaRegistrado = "DESLIGAMENTO_JA_REGISTRADO";
    private const string CampoDoUltimoDia = "ultimoDiaDeTrabalho";

    public Task<ResultadoDoAgendamento> AgendarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct) =>
        OperacoesComTrava.EmTransacaoAsync(
            db,
            unidade,
            async () =>
            {
                var funcionario = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;
                ConferirOUltimoDia(funcionario, ultimoDiaDeTrabalho, agora);

                var vigente = await OperacoesComTrava.TravarVigenteAsync(db, funcionarioId, ct);
                if (!funcionario.Ativo || vigente is not null)
                {
                    throw new ConflitoException(
                        funcionario.Ativo ? "Esta pessoa já tem um desligamento registrado." : "Esta pessoa já está inativa.",
                        CodigoDeJaRegistrado);
                }

                var desligamento = DesligamentoAgendado.Criar(funcionarioId, ultimoDiaDeTrabalho, agora);
                db.DesligamentosAgendados.Add(desligamento);
                await db.SaveChangesAsync(ct);

                if (agora >= EfetivarDesligamentoTarefa.ExecutarApos(ultimoDiaDeTrabalho) && await CondicoesLiberamAsync(funcionarioId, ultimoDiaDeTrabalho, ct))
                {
                    await EfetivarNoMioloAsync(funcionario, desligamento, ct);
                    return new ResultadoDoAgendamento(desligamento.Id, true);
                }

                await agendador.AgendarAsync(
                    new NovaTarefa(EfetivarDesligamentoTarefa.TipoDaTarefa, EfetivarDesligamentoTarefa.Chave(desligamento.Id), EfetivarDesligamentoTarefa.ExecutarApos(ultimoDiaDeTrabalho), ReferenciaId: desligamento.Id),
                    ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Colaboradores,
                    AuditoriaAcao.Inativou,
                    $"Desligamento de {funcionario.Nome} agendado",
                    funcionario.RegistroAfetado,
                    "FUNCIONARIOS",
                    funcionario.Id,
                    [new MudancaDeAuditoriaNova("Último dia de trabalho", null, Data(ultimoDiaDeTrabalho))]));
                await db.SaveChangesAsync(ct);
                return new ResultadoDoAgendamento(desligamento.Id, false);
            },
            ct);

    public Task CancelarAsync(int funcionarioId, CancellationToken ct) =>
        OperacoesComTrava.EmTransacaoAsync(
            db,
            unidade,
            async () =>
            {
                var funcionario = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct) ?? throw new NaoEncontradoException();
                var vigente = await OperacoesComTrava.TravarVigenteAsync(db, funcionarioId, ct);
                if (vigente is not { Situacao: SituacaoDoDesligamento.Agendado })
                {
                    throw new ErroDeNegocioException("SEM_DESLIGAMENTO_AGENDADO", "Não há desligamento agendado para cancelar.");
                }

                vigente.Cancelar(relogio.AgoraUtc);
                await agendador.CancelarAsync(EfetivarDesligamentoTarefa.Chave(vigente.Id), ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Colaboradores,
                    AuditoriaAcao.Alterou,
                    $"Cancelamento do desligamento de {funcionario.Nome}",
                    funcionario.RegistroAfetado,
                    "FUNCIONARIOS",
                    funcionario.Id,
                    [new MudancaDeAuditoriaNova("Último dia de trabalho", Data(vigente.UltimoDiaTrabalho), null)]));
                await db.SaveChangesAsync(ct);
                return true;
            },
            ct);

    public async Task<ResultadoDaEfetivacao> EfetivarAsync(int desligamentoId, CancellationToken ct)
    {
        // A pessoa só se descobre lendo o desligamento, e a ordem de travas é a pessoa primeiro: esta leitura é SEM trava, e o estado é relido depois de travar.
        var funcionarioId = await db.DesligamentosAgendados.AsNoTracking()
            .Where(d => d.Id == desligamentoId)
            .Select(d => (int?)d.FuncionarioId)
            .FirstOrDefaultAsync(ct);
        if (funcionarioId is null)
        {
            return ResultadoDaEfetivacao.NaoEstavaAgendado;
        }

        return await OperacoesComTrava.EmTransacaoAsync(
            db,
            unidade,
            async () =>
            {
                var funcionario = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId.Value, ct);
                var desligamento = await OperacoesComTrava.TravarPorIdAsync(db, desligamentoId, ct);
                if (funcionario is null || desligamento is null)
                {
                    return ResultadoDaEfetivacao.NaoEstavaAgendado;
                }

                if (desligamento.Situacao == SituacaoDoDesligamento.Efetivado)
                {
                    return ResultadoDaEfetivacao.JaEstavaEfetivado;
                }

                if (desligamento.Situacao != SituacaoDoDesligamento.Agendado)
                {
                    return ResultadoDaEfetivacao.NaoEstavaAgendado; // cancelado (ou revertido por uma readmissão): não há o que efetivar
                }

                if (relogio.AgoraUtc < EfetivarDesligamentoTarefa.ExecutarApos(desligamento.UltimoDiaTrabalho) || !await CondicoesLiberamAsync(funcionario.Id, desligamento.UltimoDiaTrabalho, ct))
                {
                    return ResultadoDaEfetivacao.AindaNaoPode;
                }

                await EfetivarNoMioloAsync(funcionario, desligamento, ct);
                return ResultadoDaEfetivacao.Efetivado;
            },
            ct);
    }

    /// <summary>Sem abrir transação: quem chama já está dentro da dele, com a pessoa e o desligamento travados.</summary>
    private async Task EfetivarNoMioloAsync(Funcionario funcionario, DesligamentoAgendado desligamento, CancellationToken ct)
    {
        funcionario.Desligar(desligamento.UltimoDiaTrabalho);
        desligamento.Efetivar(relogio.AgoraUtc);
        await db.SaveChangesAsync(ct);

        var gestorId = await db.Gestores.AsNoTracking()
            .Where(g => g.FuncionarioId == funcionario.Id)
            .Select(g => (int?)g.Id)
            .FirstOrDefaultAsync(ct);
        if (gestorId is { } gestor)
        {
            await pedidos.PassarParaORhAsync(gestor, ct);
        }

        foreach (var fatia in aoEfetivar)
        {
            await fatia.AposEfetivarAsync(funcionario.Id, ct);
        }
    }

    private async Task<bool> CondicoesLiberamAsync(int funcionarioId, DateOnly ultimoDia, CancellationToken ct)
    {
        foreach (var condicao in condicoes)
        {
            if (!await condicao.PodeEfetivarAsync(funcionarioId, ultimoDia, ct))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>400 em <c>ultimoDiaDeTrabalho</c>: nunca antes da admissão (o desligamento sairia antes de a pessoa existir) e nunca além de 10 anos a partir de hoje.</summary>
    private static void ConferirOUltimoDia(Funcionario funcionario, DateOnly ultimoDia, DateTime agora)
    {
        if (ultimoDia < funcionario.DataAdmissao)
        {
            throw new ErroDeValidacaoException(CampoDoUltimoDia, $"O último dia de trabalho não pode ser anterior à data de admissão ({Data(funcionario.DataAdmissao)}).");
        }

        if (ultimoDia > FusoBrasil.Hoje(agora).AddYears(AnosAFrenteNoMaximo))
        {
            throw new ErroDeValidacaoException(CampoDoUltimoDia, $"O último dia de trabalho não pode passar de {AnosAFrenteNoMaximo} anos a partir de hoje.");
        }
    }

    private static string Data(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}

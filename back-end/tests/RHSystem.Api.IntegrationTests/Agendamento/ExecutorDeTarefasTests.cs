using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Agendamento;

/// <summary>
/// Um ciclo do agendador no banco REAL, com o relógio falso (07/10/2026 12:42 UTC) e as tarefas de teste da <c>ApiFactory</c>. Cada ciclo roda num escopo novo, como o serviço
/// em segundo plano faz. Quem agenda e recupera também usa escopos próprios.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ExecutorDeTarefasTests(MySqlFixture mysql)
{
    private static readonly DateTime Hoje = ElencoDeTeste.Hoje;

    private static NovaTarefa Tarefa(int n, string tipo = "TESTE_TAREFA", DateTime? executarApos = null, DateOnly? dia = null) =>
        new(tipo, ChaveDeIdempotencia.Montar(tipo, ("n", n)), executarApos ?? Hoje, ReferenciaId: n, DiaReferencia: dia);

    private static async Task AgendarAsync(ApiFactory api, params NovaTarefa[] tarefas)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        var agendador = escopo.ServiceProvider.GetRequiredService<IAgendador>();
        foreach (var tarefa in tarefas)
        {
            await agendador.AgendarAsync(tarefa, CancellationToken.None);
        }
    }

    private static async Task<int> CicloAsync(ApiFactory api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().ExecutarCicloAsync(CancellationToken.None);
    }

    private static async Task<int> RecuperarAsync(ApiFactory api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().RecuperarAsync(CancellationToken.None);
    }

    private static async Task<List<TarefaAgendada>> LerAsync(string conexao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        return await db.TarefasAgendadas.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
    }

    [Fact]
    public async Task Tarefa_vencida_roda_uma_vez_e_fica_CONCLUIDA_com_ConcluidaEm()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await AgendarAsync(api, Tarefa(1));

        Assert.Equal(1, await CicloAsync(api));

        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDaTarefa.Concluida, (DateTime?)Hoje, (DateTime?)Hoje, 1, (DateTime?)null, (string?)null), (linha.Situacao, linha.ConcluidaEm, linha.UltimaTentativaEm, linha.Tentativas, linha.ReservadaAte, linha.UltimoErro));
        Assert.Equal(0, await CicloAsync(api)); // concluída não roda de novo
        var execucao = Assert.Single(api.Tarefas.Execucoes);
        Assert.Equal((linha.Id, 1, 1), (execucao.Id, execucao.Tentativas, execucao.ReferenciaId));
    }

    [Fact]
    public async Task Tarefa_no_futuro_nao_roda_antes_da_hora()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await AgendarAsync(api, Tarefa(1, executarApos: Hoje.AddMinutes(10)));

        Assert.Equal(0, await CicloAsync(api));
        api.Relogio.Avancar(TimeSpan.FromMinutes(9));
        Assert.Equal(0, await CicloAsync(api));
        Assert.Empty(api.Tarefas.Execucoes);

        api.Relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await CicloAsync(api));
        Assert.Single(api.Tarefas.Execucoes);
    }

    [Fact]
    public async Task Ciclo_trata_no_maximo_LoteMaximoPorCiclo_tarefas()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await AgendarAsync(api, [.. Enumerable.Range(1, 25).Select(n => Tarefa(n))]);

        var ciclos = new List<int> { await CicloAsync(api), await CicloAsync(api), await CicloAsync(api), await CicloAsync(api) };

        Assert.Equal([10, 10, 5, 0], ciclos); // o lote é 10
        Assert.Equal(25, api.Tarefas.Execucoes.Select(e => e.Id).Distinct().Count());
    }

    [Fact]
    public async Task Reserva_vencida_e_retomada_por_outro_executor_e_reserva_valida_nao_e_tocada()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        const string colunas = "TIPO, CHAVE_IDEMPOTENCIA, REFERENCIA_ID, SITUACAO, EXECUTAR_APOS, TENTATIVAS, RESERVADA_ATE, CRIADA_EM";
        await ExecutarAsync(api.Conexao, $"INSERT INTO TAREFAS_AGENDADAS ({colunas}) VALUES ('TESTE_TAREFA', 'TESTE_TAREFA:n=1', 1, 'EM_EXECUCAO', '2026-10-07 12:00:00', 1, '2026-10-07 12:41:00', '2026-10-07 12:00:00')");
        await ExecutarAsync(api.Conexao, $"INSERT INTO TAREFAS_AGENDADAS ({colunas}) VALUES ('TESTE_TAREFA', 'TESTE_TAREFA:n=2', 2, 'EM_EXECUCAO', '2026-10-07 12:00:00', 1, '2026-10-07 12:45:00', '2026-10-07 12:00:00')");

        Assert.Equal(1, await CicloAsync(api));

        var linhas = await LerAsync(api.Conexao);
        var retomada = Assert.Single(linhas, l => l.ReferenciaId == 1);
        Assert.Equal((SituacaoDaTarefa.Concluida, 2), (retomada.Situacao, retomada.Tentativas)); // o processo que caiu tinha feito a 1ª tentativa; esta é a 2ª
        var valida = Assert.Single(linhas, l => l.ReferenciaId == 2);
        Assert.Equal((SituacaoDaTarefa.EmExecucao, new DateTime(2026, 10, 7, 12, 45, 0, DateTimeKind.Utc)), (valida.Situacao, valida.ReservadaAte)); // intocada
        Assert.Equal([1], api.Tarefas.Execucoes.Select(e => e.ReferenciaId));
    }

    [Fact]
    public async Task Dois_executores_simultaneos_nao_executam_a_mesma_tarefa_duas_vezes()
    {
        // Review Focus 4: 50 tarefas, dois executores largando juntos (Barrier), com uma pausa por tarefa para se sobreporem de verdade.
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await AgendarAsync(api, [.. Enumerable.Range(1, 50).Select(n => Tarefa(n))]);
        api.Tarefas.Pausa = TimeSpan.FromMilliseconds(15);
        using var barreira = new Barrier(2);

        Task Executor() => Task.Factory.StartNew(
            async () =>
            {
                barreira.SignalAndWait();
                while (await CicloAsync(api) > 0)
                {
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        await Task.WhenAll(Executor(), Executor());

        var execucoes = api.Tarefas.Execucoes;
        Assert.Equal(50, execucoes.Count); // todas rodaram...
        Assert.Equal(50, execucoes.Select(e => e.Id).Distinct().Count()); // ...e nenhuma duas vezes
        Assert.All(await LerAsync(api.Conexao), l => Assert.Equal(SituacaoDaTarefa.Concluida, l.Situacao));
    }

    [Fact]
    public async Task Tarefa_que_lanca_volta_a_PENDENTE_com_espera_crescente_e_ao_chegar_no_maximo_vira_FALHOU()
    {
        // 1, 5, 30 e 60 minutos; a 5ª tentativa (MaximoDeTentativas) é a última. A tarefa volta a PENDENTE com RESERVADA_ATE nulo a cada falha: o CHECK de reserva nunca é violado.
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        api.Tarefas.Comportamento = _ => throw new InvalidOperationException("falhou de proposito");
        await AgendarAsync(api, Tarefa(1));

        async Task<TarefaAgendada> UmaTentativaAsync(int tratadasEsperadas)
        {
            Assert.Equal(tratadasEsperadas, await CicloAsync(api));
            return Assert.Single(await LerAsync(api.Conexao));
        }

        var t1 = await UmaTentativaAsync(1);
        Assert.Equal((SituacaoDaTarefa.Pendente, 1, Hoje.AddMinutes(1), (DateTime?)null, "InvalidOperationException: falhou de proposito"), (t1.Situacao, t1.Tentativas, t1.ExecutarApos, t1.ReservadaAte, t1.UltimoErro));
        await UmaTentativaAsync(0); // sem avançar o relógio, nada roda antes da hora

        api.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var t2 = await UmaTentativaAsync(1);
        Assert.Equal((2, Hoje.AddMinutes(1 + 5)), (t2.Tentativas, t2.ExecutarApos));

        api.Relogio.Avancar(TimeSpan.FromMinutes(5));
        var t3 = await UmaTentativaAsync(1);
        Assert.Equal((3, Hoje.AddMinutes(1 + 5 + 30)), (t3.Tentativas, t3.ExecutarApos));

        api.Relogio.Avancar(TimeSpan.FromMinutes(30));
        var t4 = await UmaTentativaAsync(1);
        Assert.Equal((4, Hoje.AddMinutes(1 + 5 + 30 + 60)), (t4.Tentativas, t4.ExecutarApos));

        api.Relogio.Avancar(TimeSpan.FromMinutes(60));
        var t5 = await UmaTentativaAsync(1);
        Assert.Equal((SituacaoDaTarefa.Falhou, 5, (DateTime?)null), (t5.Situacao, t5.Tentativas, t5.ReservadaAte));
        Assert.Equal(5, api.Tarefas.Execucoes.Count);
        api.Relogio.Avancar(TimeSpan.FromDays(1));
        await UmaTentativaAsync(0); // FALHOU não volta
    }

    [Fact]
    public async Task Tarefa_que_falha_nao_impede_as_outras_do_mesmo_ciclo()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        api.Tarefas.Comportamento = t => t.ReferenciaId == 2 ? throw new InvalidOperationException("a 2 falha") : Task.CompletedTask;
        await AgendarAsync(api, Tarefa(1), Tarefa(2), Tarefa(3));

        Assert.Equal(3, await CicloAsync(api));

        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(SituacaoDaTarefa.Concluida, Assert.Single(linhas, l => l.ReferenciaId == 1).Situacao);
        Assert.Equal((SituacaoDaTarefa.Pendente, 1), (Assert.Single(linhas, l => l.ReferenciaId == 2).Situacao, Assert.Single(linhas, l => l.ReferenciaId == 2).Tentativas));
        Assert.Equal(SituacaoDaTarefa.Concluida, Assert.Single(linhas, l => l.ReferenciaId == 3).Situacao);
    }

    [Fact]
    public async Task Tipo_sem_handler_vira_FALHOU_com_erro_claro()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await AgendarAsync(api, Tarefa(1, "TESTE_SEM_EXECUTOR"));

        Assert.Equal(1, await CicloAsync(api));

        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDaTarefa.Falhou, "SemExecutor:TESTE_SEM_EXECUTOR", (DateTime?)null), (linha.Situacao, linha.UltimoErro, linha.ReservadaAte));
        Assert.Empty(api.Tarefas.Execucoes);
    }

    [Fact]
    public async Task Tarefa_executa_como_Sistema_e_o_evento_de_auditoria_sai_com_ator_SISTEMA_sem_ip()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var antes = ContextoDeTeste.Novo(api.Conexao);
        var quantosAntes = await antes.EventosDeAuditoria.CountAsync(e => e.Descricao == TarefaAuditadaDeTeste.Descricao);
        await AgendarAsync(api, Tarefa(1, TarefaAuditadaDeTeste.TipoDaTarefa));

        Assert.Equal(1, await CicloAsync(api));

        await using var depois = ContextoDeTeste.Novo(api.Conexao);
        var eventos = await depois.EventosDeAuditoria.AsNoTracking().Where(e => e.Descricao == TarefaAuditadaDeTeste.Descricao).ToListAsync();
        Assert.Equal(quantosAntes + 1, eventos.Count);
        var evento = eventos[^1];
        Assert.Equal((TipoDeAtor.Sistema, CanalDeOrigem.Sistema, (int?)null, (string?)null, "Sistema"), (evento.AtorTipo, evento.Canal, evento.FuncionarioAtorId, evento.Ip, evento.AtorNome));
    }

    [Fact]
    public async Task Parada_de_tres_dias_RecuperarAsync_cria_as_tarefas_dos_dias_parados_sem_duplicar()
    {
        // Review Focus 4: o tipo de teste cria uma tarefa por dia até ONTEM; o relógio avança 3 dias (a API ficou parada); recuperar duas vezes não duplica.
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        api.Tarefas.PrimeiroDiaDaRecuperacao = new DateOnly(2026, 10, 7);
        await RecuperarAsync(api);
        Assert.DoesNotContain(await LerAsync(api.Conexao), l => l.Tipo == TarefaRecuperavelDeTeste.TipoDaTarefa); // antes da parada não há dia para trás

        api.Relogio.Avancar(TimeSpan.FromDays(3)); // hoje é 10/10: faltam 07, 08 e 09
        await RecuperarAsync(api);
        await RecuperarAsync(api); // idempotente

        var recuperadas = (await LerAsync(api.Conexao)).Where(l => l.Tipo == TarefaRecuperavelDeTeste.TipoDaTarefa).ToList();
        Assert.Equal([new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)], recuperadas.Select(l => l.DiaReferencia!.Value).Order().ToList());
        Assert.Equal(3, recuperadas.Count); // sem duplicar

        // Um ciclo reserva no máximo LoteMaximoPorCiclo (10) tarefas, e três dias parados de cinco tipos diários (as quatro limpezas e a de teste) são quinze: a fila esvazia em mais de um ciclo.
        for (var ciclo = 0; ciclo < 10 && await CicloAsync(api) > 0; ciclo++)
        {
        }

        Assert.Equal(
            [new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9)],
            api.Tarefas.Execucoes.Select(e => e.DiaReferencia!.Value).Order().ToList()); // as três rodaram
    }

    [Fact]
    public async Task RecuperarAsync_de_um_tipo_que_lanca_nao_impede_os_outros()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        api.Tarefas.PrimeiroDiaDaRecuperacao = new DateOnly(2026, 10, 5);

        var recuperados = await RecuperarAsync(api);

        // Dezoito tipos (as cinco limpezas, a que efetiva desligamento, as duas dos feriados e a do banco de horas da fatia 3, as de publicar aviso e publicar holerite da fatia 4, as de publicar, encerrar e lembrar formulário da fatia 5 e quatro de teste; a LIMPAR_IMPORTACOES_VENCIDAS da fatia 2 é a quinta limpeza, e esta é por hora); o que lança não conta, e o resto recupera, inclusive os que vêm DEPOIS dele.
        Assert.Equal(17, recuperados);
        Assert.Contains(TarefaQueNaoRecuperaDeTeste.TipoDaTarefa, api.Tarefas.Recuperacoes);
        Assert.Contains(TarefaDeTeste.TipoDaTarefa, api.Tarefas.Recuperacoes);
        Assert.Contains(TarefaRecuperavelDeTeste.TipoDaTarefa, api.Tarefas.Recuperacoes);
        var tipos = (await LerAsync(api.Conexao)).Select(l => l.Tipo).Distinct().Order().ToList();
        Assert.Contains("LIMPAR_TAREFAS", tipos); // as limpezas (registradas antes) recuperaram
        Assert.Contains("LIMPAR_EMAILS", tipos);
        Assert.Contains("LIMPAR_TOKENS_DE_SENHA", tipos);
        Assert.Contains("LIMPAR_ARQUIVOS_ORFAOS", tipos); // a da fatia 1c (Task 6) também é uma limpeza diária recuperável
        Assert.Contains("LIMPAR_IMPORTACOES_VENCIDAS", tipos); // a da fatia 2 (Task 6) é por hora, e a recuperação agenda a da hora de agora
        Assert.Contains("IMPORTAR_FERIADOS", tipos); // as dos feriados (fatia 3) agendam o ano de hoje e o seguinte
        Assert.Contains("REPETIR_FERIADO", tipos);
        Assert.Contains("CONSOLIDAR_BANCO_HORAS", tipos); // a consolidação agenda os dias que faltam e o de hoje
        Assert.Contains(TarefaRecuperavelDeTeste.TipoDaTarefa, tipos); // e a que vem depois da que lança também criou as suas (05/10 e 06/10)
    }
}

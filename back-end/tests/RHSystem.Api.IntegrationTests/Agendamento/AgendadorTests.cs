using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Domain.Agendamento;
using RHSystem.Infrastructure.Agendamento;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Agendamento;

/// <summary>Agendar, registrar como concluída e cancelar, no banco REAL. O relógio é o falso, em 07/10/2026 12:42 UTC: as datas gravadas vêm dele, nunca da hora do banco.</summary>
[Collection(MySqlCollection.Nome)]
public class AgendadorTests(MySqlFixture mysql)
{
    private static readonly DateTime Hoje = ElencoDeTeste.Hoje;

    private static string Chave(int n, string tipo = "TESTE_TAREFA") => ChaveDeIdempotencia.Montar(tipo, ("id", n));

    private static async Task<List<TarefaAgendada>> LerAsync(string conexao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        return await db.TarefasAgendadas.AsNoTracking().OrderBy(t => t.Id).ToListAsync();
    }

    [Fact]
    public async Task Agendar_grava_PENDENTE_com_os_campos_e_CriadaEm_do_relogio_falso_e_a_mesma_chave_duas_vezes_devolve_false()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);
        var executarApos = Hoje.AddHours(3);
        var tarefa = new NovaTarefa("TESTE_TAREFA", Chave(1), executarApos, ReferenciaId: 15, DiaReferencia: new DateOnly(2026, 10, 9), Parametros: new { Aviso = 15 });

        var primeira = await agendador.AgendarAsync(tarefa, CancellationToken.None);
        var segunda = await agendador.AgendarAsync(tarefa, CancellationToken.None);

        Assert.True(primeira);
        Assert.False(segunda); // a chave já existe: vale como sucesso, nunca lança
        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDaTarefa.Pendente, executarApos, Hoje, 0, (DateTime?)null, 15, new DateOnly(2026, 10, 9)), (linha.Situacao, linha.ExecutarApos, linha.CriadaEm, linha.Tentativas, linha.ReservadaAte, linha.ReferenciaId, linha.DiaReferencia));
        using var parametros = JsonDocument.Parse(linha.Parametros!);
        Assert.Equal(15, parametros.RootElement.GetProperty("aviso").GetInt32()); // camelCase
    }

    [Fact]
    public async Task Agendar_com_tipo_acima_de_50_caracteres_lanca_ArgumentException_antes_do_banco()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);
        var tipoLongo = "T" + new string('A', 50); // 51 caracteres

        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar(tipoLongo, ("id", 1))); // a chave já recusa
        await Assert.ThrowsAsync<ArgumentException>(() => agendador.AgendarAsync(new NovaTarefa(tipoLongo, tipoLongo + ":id=1", Hoje), CancellationToken.None)); // e o agendador também

        Assert.Empty(await LerAsync(api.Conexao));
    }

    [Fact]
    public async Task Agendar_a_mesma_chave_em_20_chamadas_simultaneas_grava_uma_tarefa()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        using var barreira = new Barrier(20);
        var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Factory.StartNew(
            async () =>
            {
                await using var db = ContextoDeTeste.Novo(api.Conexao);
                var agendador = new Agendador(db, api.Relogio);
                barreira.SignalAndWait();
                return await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(7), Hoje), CancellationToken.None);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()).ToArray();

        var resultados = await Task.WhenAll(tarefas); // se alguma lançasse (1062 ou deadlock), o WhenAll lançaria aqui

        Assert.Equal((1, 19), (resultados.Count(r => r), resultados.Count(r => !r)));
        Assert.Single(await LerAsync(api.Conexao));
    }

    [Fact]
    public async Task Agendar_com_mudancas_pendentes_no_contexto_e_chave_repetida_nao_perde_nem_duplica_as_mudancas()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);
        Assert.True(await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(100), Hoje), CancellationToken.None));
        db.TarefasAgendadas.Add(TarefaAgendada.Criar("TESTE_TAREFA", Chave(101), Hoje)); // uma mudança que o chamador deixou pendente no contexto

        var repetida = await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(100), Hoje), CancellationToken.None);

        Assert.False(repetida);
        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(2, linhas.Count); // a chave repetida não entrou de novo...
        Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(101)); // ...e a mudança pendente foi gravada (uma vez), não perdida pelo 1062
    }

    [Fact]
    public async Task Agendar_chave_repetida_e_depois_outra_na_mesma_instancia_grava_a_outra()
    {
        // Depois do 1062 a tarefa que falhou tem de sair do contexto: se ficasse, o SaveChanges da PRÓXIMA tentaria gravá-la de novo e lançaria.
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);

        var resultados = new[]
        {
            await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(1), Hoje), CancellationToken.None),
            await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(1), Hoje), CancellationToken.None),
            await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(2), Hoje), CancellationToken.None),
        };

        Assert.Equal([true, false, true], resultados);
        Assert.Equal(2, (await LerAsync(api.Conexao)).Count);
    }

    [Fact]
    public async Task RegistrarComoConcluida_insere_CONCLUIDA_uma_vez_e_devolve_false_na_segunda()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);
        var dia = new DateOnly(2026, 10, 7);
        var chave = ChaveDeIdempotencia.Montar("LEMBRETE_FORMULARIO", ("form", 5), ("dia", dia));

        var primeira = await agendador.RegistrarComoConcluidaAsync("LEMBRETE_FORMULARIO", chave, dia, CancellationToken.None);
        var segunda = await agendador.RegistrarComoConcluidaAsync("LEMBRETE_FORMULARIO", chave, dia, CancellationToken.None);

        Assert.True(primeira);
        Assert.False(segunda); // a marca já estava lá: o chamador não repete o trabalho
        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDaTarefa.Concluida, (DateTime?)Hoje, dia, (DateTime?)null), (linha.Situacao, linha.ConcluidaEm, linha.DiaReferencia, linha.ReservadaAte));
    }

    [Fact]
    public async Task Cancelar_pendente_vira_CANCELADA_e_cancelar_concluida_devolve_0()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var agendador = new Agendador(db, api.Relogio);
        await agendador.AgendarAsync(new NovaTarefa("TESTE_TAREFA", Chave(1), Hoje.AddDays(1)), CancellationToken.None);
        await agendador.RegistrarComoConcluidaAsync("TESTE_TAREFA", Chave(2), null, CancellationToken.None);
        await ExecutarAsync(api.Conexao, $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, SITUACAO, EXECUTAR_APOS, TENTATIVAS, RESERVADA_ATE, CRIADA_EM) VALUES ('TESTE_TAREFA', '{Chave(3)}', 'EM_EXECUCAO', '2026-10-07 12:00:00', 1, '2026-10-07 13:00:00', '2026-10-07 12:00:00')");

        var pendente = await agendador.CancelarAsync(Chave(1), CancellationToken.None);
        var concluida = await agendador.CancelarAsync(Chave(2), CancellationToken.None);
        var rodando = await agendador.CancelarAsync(Chave(3), CancellationToken.None);
        var inexistente = await agendador.CancelarAsync(Chave(99), CancellationToken.None);

        Assert.Equal((1, 0, 0, 0), (pendente, concluida, rodando, inexistente)); // só a PENDENTE; nunca tira uma tarefa de dentro de uma execução
        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(SituacaoDaTarefa.Cancelada, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(1)).Situacao);
        Assert.Equal(SituacaoDaTarefa.Concluida, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(2)).Situacao);
        Assert.Equal(SituacaoDaTarefa.EmExecucao, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(3)).Situacao);
    }
}

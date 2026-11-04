using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Agendamento;

/// <summary>
/// As duas limpezas de retenção (<c>LIMPAR_TAREFAS</c> e <c>LIMPAR_EMAILS</c>) no banco REAL, com o relógio falso em 07/10/2026 12:42 UTC: o corte é de 30 dias para as tarefas
/// (07/09) e de 90 dias para os e-mails (09/07). Linhas "antigas" são de 01/08 (tarefas) e 01/06 (e-mails); "recentes", de 01 a 02/10.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TarefasDeLimpezaTests(MySqlFixture mysql)
{
    private static string Sql(string? valor) => valor is null ? "NULL" : $"'{valor}'";

    private static Task InserirTarefaAsync(string conexao, string chave, string situacao, string criadaEm, string? reservadaAte = null, string? concluidaEm = null) =>
        ExecutarAsync(
            conexao,
            $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, SITUACAO, EXECUTAR_APOS, TENTATIVAS, RESERVADA_ATE, CONCLUIDA_EM, CRIADA_EM) VALUES ('TESTE_TAREFA', '{chave}', '{situacao}', '{criadaEm}', 1, {Sql(reservadaAte)}, {Sql(concluidaEm)}, '{criadaEm}')");

    private static Task InserirEmailAsync(string conexao, string chave, string situacao, string criadoEm, string? carga = null, string? reservadoAte = null, string? enviadoEm = null) =>
        ExecutarAsync(
            conexao,
            $"INSERT INTO FILA_EMAILS (TIPO, CHAVE_IDEMPOTENCIA, DESTINATARIO_EMAIL, CARGA, SITUACAO, PROXIMA_TENTATIVA_EM, RESERVADO_ATE, ENVIADO_EM, CRIADO_EM) VALUES ('TESTE_ENVIO', '{chave}', 'pessoa@empresa.com.br', {Sql(carga)}, '{situacao}', '{criadoEm}', {Sql(reservadoAte)}, {Sql(enviadoEm)}, '{criadoEm}')");

    private static async Task ExecutarLimpezaAsync(ApiFactory api, string tipo)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        var limpeza = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo);
        await limpeza.ExecutarAsync(new TarefaParaExecutar(0, tipo + ":x=1", null, null, null, 1), CancellationToken.None);
    }

    private static async Task RecuperarLimpezaAsync(ApiFactory api, string tipo)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).RecuperarAsync(CancellationToken.None);
    }

    [Fact]
    public async Task LIMPAR_TAREFAS_apaga_concluidas_e_canceladas_antigas_e_nunca_pendentes_em_execucao_nem_falhas()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=concluida-antiga", "CONCLUIDA", "2026-08-01 12:00:00", concluidaEm: "2026-08-01 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=concluida-de-60-dias", "CONCLUIDA", "2026-08-08 12:00:00", concluidaEm: "2026-08-08 12:00:00"); // passou dos 30 dias: sai (com 90 ficaria)
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=concluida-recente", "CONCLUIDA", "2026-10-02 12:00:00", concluidaEm: "2026-10-02 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=cancelada-antiga", "CANCELADA", "2026-08-01 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=cancelada-recente", "CANCELADA", "2026-10-02 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=pendente-antiga", "PENDENTE", "2026-08-01 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=em-execucao-antiga", "EM_EXECUCAO", "2026-08-01 12:00:00", reservadaAte: "2026-10-08 12:00:00");
        await InserirTarefaAsync(api.Conexao, "TESTE_TAREFA:k=falhou-antiga", "FALHOU", "2026-08-01 12:00:00");

        await ExecutarLimpezaAsync(api, "LIMPAR_TAREFAS");

        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var restam = await db.TarefasAgendadas.AsNoTracking().Select(t => t.ChaveDeIdempotencia).OrderBy(c => c).ToListAsync();
        Assert.Equal(
            [
                "TESTE_TAREFA:k=cancelada-recente",
                "TESTE_TAREFA:k=concluida-recente",
                "TESTE_TAREFA:k=em-execucao-antiga",
                "TESTE_TAREFA:k=falhou-antiga",
                "TESTE_TAREFA:k=pendente-antiga",
            ],
            restam); // só as CONCLUIDA e CANCELADA ANTIGAS saíram; nunca PENDENTE, EM_EXECUCAO nem FALHOU
    }

    [Fact]
    public async Task LIMPAR_EMAILS_apaga_terminados_antigos_e_nunca_pendentes_nem_reservados()
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=enviado-antigo", "ENVIADO", "2026-06-01 12:00:00", enviadoEm: "2026-06-01 12:05:00");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=enviado-de-60-dias", "ENVIADO", "2026-08-08 12:00:00", enviadoEm: "2026-08-08 12:05:00"); // passou dos 30 dias das tarefas, mas NÃO dos 90 dos e-mails: fica
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=enviado-recente", "ENVIADO", "2026-10-01 12:00:00", enviadoEm: "2026-10-01 12:05:00");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=falhou-antigo", "FALHOU", "2026-06-01 12:00:00");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=devolvido-antigo", "DEVOLVIDO", "2026-06-01 12:00:00", enviadoEm: "2026-06-01 12:05:00");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=cancelado-antigo", "CANCELADO", "2026-06-01 12:00:00");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=pendente-antigo", "PENDENTE", "2026-06-01 12:00:00", carga: "{\"nome\":\"Ana\"}");
        await InserirEmailAsync(api.Conexao, "TESTE_ENVIO:k=em-envio-antigo", "EM_ENVIO", "2026-06-01 12:00:00", carga: "{\"nome\":\"Ana\"}", reservadoAte: "2026-10-08 12:00:00");

        await ExecutarLimpezaAsync(api, "LIMPAR_EMAILS");

        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var restam = await db.EmailsNaFila.AsNoTracking().Select(e => e.ChaveDeIdempotencia).OrderBy(c => c).ToListAsync();
        Assert.Equal(["TESTE_ENVIO:k=em-envio-antigo", "TESTE_ENVIO:k=enviado-de-60-dias", "TESTE_ENVIO:k=enviado-recente", "TESTE_ENVIO:k=pendente-antigo"], restam); // terminados ANTIGOS saíram; PENDENTE, EM_ENVIO e o recente ficam
    }

    [Theory]
    [InlineData("LIMPAR_TAREFAS")]
    [InlineData("LIMPAR_EMAILS")]
    public async Task RecuperarAsync_das_limpezas_agenda_uma_por_dia_sem_duplicar(string tipo)
    {
        var api = await ApiDeAgendamento.ComecarAsync(mysql);

        await RecuperarLimpezaAsync(api, tipo);
        await RecuperarLimpezaAsync(api, tipo); // no mesmo dia: não duplica
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var hoje = await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == tipo).ToListAsync();
            var tarefa = Assert.Single(hoje);
            Assert.Equal(($"{tipo}:dia=2026-10-07", new DateOnly(2026, 10, 7)), (tarefa.ChaveDeIdempotencia, tarefa.DiaReferencia));
        }

        api.Relogio.Avancar(TimeSpan.FromDays(1)); // amanhã, no horário de Brasília
        await RecuperarLimpezaAsync(api, tipo);
        await using var db2 = ContextoDeTeste.Novo(api.Conexao);
        var dias = await db2.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == tipo).Select(t => t.ChaveDeIdempotencia).OrderBy(c => c).ToListAsync();
        Assert.Equal([$"{tipo}:dia=2026-10-07", $"{tipo}:dia=2026-10-08"], dias);
    }
}

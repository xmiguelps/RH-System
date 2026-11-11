using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;
using static RHSystem.Api.IntegrationTests.Colaboradores.DesligamentoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// A tarefa <c>EFETIVAR_DESLIGAMENTO</c> pelo executor de verdade (<c>ExecutarCicloAsync</c> e <c>RecuperarAsync</c>), com o relógio falso: o ciclo roda num escopo novo, como o serviço em segundo plano
/// faz, sem requisição, logo o ator é o Sistema. Cada teste começa sem tarefas (o ciclo executa tudo o que estiver vencido).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EfetivarDesligamentoTarefaTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ComecarAsync(mysql, nameof(EfetivarDesligamentoTarefaTests));

    private static Task<PessoaDoTeste> PessoaAsync(ApiDeAcesso api, string sufixo) => api.NovaPessoaAsync(sufixo, login: TipoDeLogin.SenhaDefinida);

    private static async Task<int> CicloAsync(WebApplicationFactory<Program> api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().ExecutarCicloAsync(CancellationToken.None);
    }

    private static async Task<int> RecuperarAsync(WebApplicationFactory<Program> api)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().RecuperarAsync(CancellationToken.None);
    }

    private static async Task<TarefaAgendada> TarefaDoAsync(ApiDeAcesso api, int desligamentoId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.TarefasAgendadas.AsNoTracking().SingleAsync(t => t.Tipo == "EFETIVAR_DESLIGAMENTO" && t.ReferenciaId == desligamentoId);
    }

    /// <summary>Uma tarefa já gravada (por SQL, como a deixaria uma execução anterior) para o desligamento, na situação dada.</summary>
    private static Task TarefaPorSqlAsync(ApiDeAcesso api, int desligamentoId, string situacao) =>
        Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, SITUACAO, EXECUTAR_APOS, TENTATIVAS, REFERENCIA_ID, CRIADA_EM) VALUES ('EFETIVAR_DESLIGAMENTO', 'EFETIVAR_DESLIGAMENTO:desligamento={desligamentoId}', '{situacao}', '2026-10-06 03:00:00', 5, {desligamentoId}, '2026-10-01 12:00:00')");

    [Fact]
    public async Task Tarefa_efetiva_no_horario_e_nao_antes()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "tarefa1");
        var resultado = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 9)); // a tarefa vence em 2026-10-10T03:00Z

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 2, 59, 59, DateTimeKind.Utc));
        Assert.Equal(0, await CicloAsync(api.Api)); // ainda é 09/10 em Brasília: nada vencido
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("AGENDADO", await SituacaoAsync(api, pessoa.FuncionarioId));

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc));
        Assert.Equal(1, await CicloAsync(api.Api));

        Assert.False(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("2026-10-09", await DataDeDesligamentoAsync(api, pessoa.FuncionarioId));
        Assert.Equal(SituacaoDaTarefa.Concluida, (await TarefaDoAsync(api, resultado.DesligamentoId)).Situacao);
    }

    [Fact]
    public async Task Tarefa_com_condicao_recusando_lanca_e_volta_a_PENDENTE_para_nova_tentativa()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "tarefa2");
        var condicao = new CondicaoDeTeste(libera: false);
        void Configurar(IServiceCollection s) => s.AddScoped<ICondicaoParaEfetivarDesligamento>(_ => condicao);
        var agendada = await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(pessoa.FuncionarioId, new DateOnly(2026, 10, 5), CancellationToken.None));
        Assert.False(agendada.EfetivadoNaHora); // o último dia é passado, mas a condição recusa: agenda a tarefa
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureServices(Configurar));

        Assert.Equal(1, await CicloAsync(fabrica)); // a tarefa venceu, mas a condição continua recusando

        var tarefa = await TarefaDoAsync(api, agendada.DesligamentoId);
        Assert.Equal((SituacaoDaTarefa.Pendente, 1), (tarefa.Situacao, tarefa.Tentativas));
        Assert.Equal("InvalidOperationException: Desligamento ainda não pode ser efetivado.", tarefa.UltimoErro); // sem dado pessoal na mensagem
        Assert.True(tarefa.ExecutarApos > Agora); // tenta de novo depois da espera
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("AGENDADO", await SituacaoAsync(api, pessoa.FuncionarioId));

        condicao.Libera = true;
        api.Api.Relogio.Fixar(Agora.AddMinutes(2)); // passou a espera de 1 minuto
        Assert.Equal(1, await CicloAsync(fabrica));
        Assert.False(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal(SituacaoDaTarefa.Concluida, (await TarefaDoAsync(api, agendada.DesligamentoId)).Situacao);
    }

    [Fact]
    public async Task Recuperacao_efetiva_o_vencido_mesmo_com_a_tarefa_FALHOU_ou_inexistente()
    {
        var api = await ApiAsync();
        var comTarefaFalhada = await PessoaAsync(api, "recuperar1");
        var semTarefa = await PessoaAsync(api, "recuperar2");
        var falhada = await AgendadoPorSqlAsync(api, comTarefaFalhada.FuncionarioId, new DateOnly(2026, 10, 5));
        await TarefaPorSqlAsync(api, falhada, "FALHOU"); // o agendador já desistiu dela
        await AgendadoPorSqlAsync(api, semTarefa.FuncionarioId, new DateOnly(2026, 10, 5)); // e esta nunca teve tarefa (perdida)

        await RecuperarAsync(api.Api);

        Assert.False(await AtivoAsync(api, comTarefaFalhada.FuncionarioId));
        Assert.False(await AtivoAsync(api, semTarefa.FuncionarioId));
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, comTarefaFalhada.FuncionarioId));
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, semTarefa.FuncionarioId));
    }

    [Fact]
    public async Task Parada_de_3_dias_a_recuperacao_efetiva_todos_os_vencidos_sem_duplicar_e_agenda_os_futuros_sem_tarefa()
    {
        var api = await ApiAsync();
        var vencidos = new[] { await PessoaAsync(api, "parada1"), await PessoaAsync(api, "parada2"), await PessoaAsync(api, "parada3") };
        var futuros = new[] { await PessoaAsync(api, "parada4"), await PessoaAsync(api, "parada5") };
        var dias = new[] { new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 8), new DateOnly(2026, 10, 9) };
        for (var i = 0; i < vencidos.Length; i++)
        {
            await AgendadoPorSqlAsync(api, vencidos[i].FuncionarioId, dias[i]);
        }

        var primeiroFuturo = await AgendadoPorSqlAsync(api, futuros[0].FuncionarioId, new DateOnly(2026, 10, 20));
        var segundoFuturo = await AgendadoPorSqlAsync(api, futuros[1].FuncionarioId, new DateOnly(2026, 10, 25));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 12, 0, 0, DateTimeKind.Utc)); // a API ficou parada 3 dias

        await RecuperarAsync(api.Api);
        await RecuperarAsync(api.Api); // de novo: não duplica nem estraga

        foreach (var p in vencidos)
        {
            Assert.Equal("EFETIVADO", await SituacaoAsync(api, p.FuncionarioId));
            Assert.False(await AtivoAsync(api, p.FuncionarioId));
        }

        foreach (var p in futuros)
        {
            Assert.Equal("AGENDADO", await SituacaoAsync(api, p.FuncionarioId)); // os futuros continuam agendados
            Assert.True(await AtivoAsync(api, p.FuncionarioId));
        }

        Assert.Equal(2, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'EFETIVAR_DESLIGAMENTO'")); // uma por futuro, nenhuma para os vencidos
        Assert.Equal(new DateTime(2026, 10, 21, 3, 0, 0, DateTimeKind.Utc), (await TarefaDoAsync(api, primeiroFuturo)).ExecutarApos);
        Assert.Equal(new DateTime(2026, 10, 26, 3, 0, 0, DateTimeKind.Utc), (await TarefaDoAsync(api, segundoFuturo)).ExecutarApos);
    }

    [Fact]
    public async Task Tarefa_executa_como_Sistema_e_o_Inativou_sai_com_ator_SISTEMA_e_canal_SISTEMA()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "sistema1");
        await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 9));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(1, await CicloAsync(api.Api));

        var inativou = Assert.Single(await EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.FuncionarioId), e => e.Acao == AuditoriaAcao.Inativou && e.Descricao.StartsWith("Inativação", StringComparison.Ordinal));
        Assert.Equal((TipoDeAtor.Sistema, CanalDeOrigem.Sistema, (int?)null, (string?)null, "Sistema"), (inativou.AtorTipo, inativou.Canal, inativou.FuncionarioAtorId, inativou.Ip, inativou.AtorNome));
        var ultimoDia = Assert.Single(inativou.Mudancas, m => m.Campo == "Último dia de trabalho");
        Assert.Equal("09/10/2026", ultimoDia.ValorDepois);
    }
}

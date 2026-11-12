using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ponto;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A tarefa <c>CONSOLIDAR_BANCO_HORAS:dia=AAAA-MM-DD</c>, no banco REAL: no dia seguinte, a partir das 12:00 (-03:00) (a jornada que atravessa a meia-noite já fechou), lança o banco de horas de todo mundo que trabalhava naquele dia. A recuperação cria as tarefas do dia seguinte
/// ao último consolidado até hoje, com horizonte de 60 dias. O último dia consolidado é o da maior tarefa concluída, e a efetivação do desligamento só vale depois de o último dia da pessoa estar consolidado. O relógio fica em 2026-10-07T12:42Z.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConsolidacaoDoBancoDeHorasTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ConsolidacaoDoBancoDeHorasTests));
        await ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo da consolidacao {Guid.NewGuid():N}", 44m);
        var e = api.Elenco;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({e.MembroA.FuncionarioId}, {e.MembroB.FuncionarioId}, {e.Gestor.FuncionarioId}, {e.Inativo.FuncionarioId}, {e.EmAdmissao.FuncionarioId})");
        return api;
    }

    private static string Utc(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    private static Task LimparTarefasAsync(ApiDeAcesso api) => Sql.ExecutarAsync(api.Conexao, $"DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = '{ConsolidarBancoDeHorasTarefa.TipoDaTarefa}'");

    private static TarefaParaExecutar DoDia(DateOnly dia) => new(1, ConsolidarBancoDeHorasTarefa.Chave(dia), null, dia, null, 0);

    private static async Task ExecutarAsync(ApiDeAcesso api, DateOnly dia)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa).ExecutarAsync(DoDia(dia), Ct);
    }

    private static async Task RecuperarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa).RecuperarAsync(Ct);
    }

    private static async Task MarcarComoConsolidadoAsync(ApiDeAcesso api, DateOnly dia)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IAgendador>().RegistrarComoConcluidaAsync(ConsolidarBancoDeHorasTarefa.TipoDaTarefa, ConsolidarBancoDeHorasTarefa.Chave(dia), dia, Ct);
    }

    private static async Task<DateOnly?> UltimoAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IUltimoDiaConsolidado>().ObterAsync(Ct);
    }

    private static async Task<List<TarefaAgendada>> TarefasAsync(ApiDeAcesso api)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa).OrderBy(t => t.DiaReferencia).ToListAsync();
    }

    private static async Task<List<(string Tipo, decimal Horas, string? Observacao)>> LinhasDoDiaAsync(ApiDeAcesso api, int funcionarioId, DateOnly dia)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var linhas = await db.LancamentosDoBancoDeHoras.AsNoTracking().Where(l => l.FuncionarioId == funcionarioId && l.DiaDeReferencia == dia).ToListAsync();
        return [.. linhas.Select(l => (l.Tipo!.Value.ToString().ToUpperInvariant(), l.Quantidade!.Value, l.Observacao))];
    }

    // ---------------------------------------------------------------- executar o dia

    [Fact]
    public async Task Consolidar_o_dia_lanca_para_quem_trabalhava_nele_e_nao_para_quem_ainda_nao_tinha_entrado_nem_para_o_inativo_sem_linha()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 21); // segunda-feira
        await BaterEmAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 15)), (TipoDeMarcacao.Entrada, Utc(dia, 16)), (TipoDeMarcacao.Saida, Utc(dia, 21, 50))); // +62

        await ExecutarAsync(api, dia);
        await ExecutarAsync(api, dia); // idempotente

        Assert.Equal([("CREDITO", 1.03m, (string?)"Hora extra")], await LinhasDoDiaAsync(api, api.Elenco.MembroA.FuncionarioId, dia));
        Assert.Equal([("DEBITO", 8.80m, (string?)"Falta")], await LinhasDoDiaAsync(api, api.Elenco.MembroB.FuncionarioId, dia));
        Assert.Empty(await LinhasDoDiaAsync(api, api.Elenco.EmAdmissao.FuncionarioId, dia)); // só entra em 13/10
        Assert.Empty(await LinhasDoDiaAsync(api, api.Elenco.Inativo.FuncionarioId, dia)); // inativo sem linha não ganha linha nova
    }

    [Fact]
    public async Task Quem_foi_admitido_no_proprio_dia_ja_entra_na_consolidacao_daquele_dia()
    {
        var api = await ApiAsync();
        var gestor = api.Elenco.Gestor.FuncionarioId;
        var dia = new DateOnly(2026, 9, 23);
        var original = Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ADMISSAO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {gestor}"), System.Globalization.CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '{dia:yyyy-MM-dd}' WHERE ID_FUNCIONARIO = {gestor}");
        try
        {
            await ExecutarAsync(api, dia);

            Assert.Equal([("DEBITO", 8.80m, (string?)"Falta")], await LinhasDoDiaAsync(api, gestor, dia)); // o primeiro dia sem batida já é falta
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '{original:yyyy-MM-dd}' WHERE ID_FUNCIONARIO = {gestor}");
        }
    }

    [Fact]
    public async Task Quem_ja_estava_desligado_no_dia_nao_entra_e_o_ultimo_dia_de_trabalho_ainda_entra()
    {
        var api = await ApiAsync();
        var henrique = api.Elenco.Inativo.FuncionarioId; // desligado em 30/09
        var ultimoDia = new DateOnly(2026, 9, 30);
        var depois = new DateOnly(2026, 10, 1);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE, DATA_DESLIGAMENTO = NULL WHERE ID_FUNCIONARIO = {henrique}");
        await BaterEmAsync(api.Conexao, henrique, ultimoDia, (TipoDeMarcacao.Entrada, Utc(ultimoDia, 11)), (TipoDeMarcacao.Saida, Utc(ultimoDia, 21)));
        await BaterEmAsync(api.Conexao, henrique, depois, (TipoDeMarcacao.Entrada, Utc(depois, 11)), (TipoDeMarcacao.Saida, Utc(depois, 21)));
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, OBSERVACAO, DIA_REFERENCIA) VALUES ({henrique}, '2026-09-30', 'DEBITO', 8.80, 'Falta', '2026-09-30'), ({henrique}, '2026-10-01', 'DEBITO', 8.80, 'Falta', '2026-10-01')");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {henrique}");

        await ExecutarAsync(api, ultimoDia);
        await ExecutarAsync(api, depois);

        Assert.Equal([("CREDITO", 1.20m, (string?)"Hora extra")], await LinhasDoDiaAsync(api, henrique, ultimoDia)); // 08:00 a 18:00: 600 contra 528 = +72 (1,20 h)
        Assert.Equal([("DEBITO", 8.80m, (string?)"Falta")], await LinhasDoDiaAsync(api, henrique, depois)); // depois do desligamento a tarefa nem olha a pessoa: a linha de antes fica
    }

    [Fact]
    public async Task Uma_pessoa_que_falha_nao_impede_as_outras_e_a_tarefa_lanca_para_o_agendador_tentar_de_novo()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 22);
        await BaterEmAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 21, 50)));
        var falha = api.Elenco.MembroB.FuncionarioId;
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.Remove(servicos.Single(s => s.ServiceType == typeof(IRecalculoDoBancoDeHoras)));
            servicos.AddScoped<IRecalculoDoBancoDeHoras>(sp => new RecalculoQueFalhaPara(falha, ActivatorUtilities.CreateInstance<RecalculoDoBancoDeHoras>(sp)));
        }));

        await using var escopo = fabrica.Services.CreateAsyncScope();
        var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa);
        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => tarefa.ExecutarAsync(DoDia(dia), Ct));

        Assert.DoesNotContain("Júlia", erro.Message); // a mensagem vai para ULTIMO_ERRO: sem dado pessoal
        Assert.Equal([("CREDITO", 2.03m, (string?)"Hora extra")], await LinhasDoDiaAsync(api, api.Elenco.MembroA.FuncionarioId, dia)); // 08:00 a 18:50 = 650 contra 528: +122; a pessoa que falhou não impediu esta
        Assert.Empty(await LinhasDoDiaAsync(api, falha, dia));
    }

    private sealed class RecalculoQueFalhaPara(int funcionarioIdQueFalha, IRecalculoDoBancoDeHoras real) : IRecalculoDoBancoDeHoras
    {
        public Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct) =>
            funcionarioId == funcionarioIdQueFalha ? throw new InvalidOperationException("Falha de teste no recálculo.") : real.RecalcularDiaAsync(funcionarioId, dia, ct);
    }

    // ---------------------------------------------------------------- as chaves e a hora de rodar

    [Fact]
    public void A_chave_e_o_dia_e_a_tarefa_roda_a_partir_das_12h_de_Brasilia_do_dia_seguinte()
    {
        Assert.Equal("CONSOLIDAR_BANCO_HORAS:dia=2026-10-05", ConsolidarBancoDeHorasTarefa.Chave(new DateOnly(2026, 10, 5)));
        Assert.Equal(new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc), ConsolidarBancoDeHorasTarefa.ExecutarApos(new DateOnly(2026, 10, 5))); // 12:00 -03:00 do dia 6
        Assert.Equal(new DateTime(2027, 1, 1, 15, 0, 0, DateTimeKind.Utc), ConsolidarBancoDeHorasTarefa.ExecutarApos(new DateOnly(2026, 12, 31)));
    }

    // ---------------------------------------------------------------- recuperar

    [Fact]
    public async Task Recuperar_cria_as_tarefas_do_dia_seguinte_ao_ultimo_consolidado_ate_hoje_sem_duplicar()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 3));

        await RecuperarAsync(api);
        await RecuperarAsync(api);

        var tarefas = (await TarefasAsync(api)).Where(t => t.Situacao == SituacaoDaTarefa.Pendente).ToList();
        Assert.Equal(
            [
                ("CONSOLIDAR_BANCO_HORAS:dia=2026-10-04", new DateOnly(2026, 10, 4), new DateTime(2026, 10, 5, 15, 0, 0, DateTimeKind.Utc)),
                ("CONSOLIDAR_BANCO_HORAS:dia=2026-10-05", new DateOnly(2026, 10, 5), new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc)),
                ("CONSOLIDAR_BANCO_HORAS:dia=2026-10-06", new DateOnly(2026, 10, 6), new DateTime(2026, 10, 7, 15, 0, 0, DateTimeKind.Utc)),
                ("CONSOLIDAR_BANCO_HORAS:dia=2026-10-07", new DateOnly(2026, 10, 7), new DateTime(2026, 10, 8, 15, 0, 0, DateTimeKind.Utc)),
            ],
            tarefas.Select(t => (t.ChaveDeIdempotencia, t.DiaReferencia!.Value, t.ExecutarApos)));
    }

    [Fact]
    public async Task Sem_nenhum_dia_consolidado_ou_com_parada_de_mais_de_60_dias_a_recuperacao_cobre_so_os_ultimos_60()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);

        await RecuperarAsync(api);
        var semNenhum = (await TarefasAsync(api)).Select(t => t.DiaReferencia!.Value).ToList();

        await LimparTarefasAsync(api);
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 6, 29)); // 100 dias atrás
        await RecuperarAsync(api);
        var comParada = (await TarefasAsync(api)).Where(t => t.Situacao == SituacaoDaTarefa.Pendente).Select(t => t.DiaReferencia!.Value).ToList();

        Assert.Equal((61, new DateOnly(2026, 8, 8), new DateOnly(2026, 10, 7)), (semNenhum.Count, semNenhum.Min(), semNenhum.Max()));
        Assert.Equal((61, new DateOnly(2026, 8, 8), new DateOnly(2026, 10, 7)), (comParada.Count, comParada.Min(), comParada.Max()));
    }

    [Fact]
    public async Task Com_tudo_consolidado_ate_hoje_a_recuperacao_so_cria_a_de_amanha_a_partir_de_hoje()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 6));

        await RecuperarAsync(api);

        Assert.Equal([new DateOnly(2026, 10, 7)], (await TarefasAsync(api)).Where(t => t.Situacao == SituacaoDaTarefa.Pendente).Select(t => t.DiaReferencia!.Value));
    }

    [Fact]
    public async Task O_ciclo_do_executor_roda_o_dia_so_a_partir_das_12h_de_Brasilia_do_dia_seguinte()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 2));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 6, 14, 59, 0, DateTimeKind.Utc)); // 11:59 em Brasília

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == ConsolidarBancoDeHorasTarefa.TipoDaTarefa).RecuperarAsync(Ct);
            var executor = escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>();
            for (var ciclo = 0; ciclo < 5; ciclo++)
            {
                await executor.ExecutarCicloAsync(Ct);
            }
        }

        var antes = (await TarefasAsync(api)).Where(t => t.DiaReferencia > new DateOnly(2026, 10, 2)).ToDictionary(t => t.DiaReferencia!.Value, t => t.Situacao);
        Assert.Equal(
            new Dictionary<DateOnly, SituacaoDaTarefa>
            {
                [new DateOnly(2026, 10, 3)] = SituacaoDaTarefa.Concluida,
                [new DateOnly(2026, 10, 4)] = SituacaoDaTarefa.Concluida,
                [new DateOnly(2026, 10, 5)] = SituacaoDaTarefa.Pendente, // só às 12:00 de 06/10
                [new DateOnly(2026, 10, 6)] = SituacaoDaTarefa.Pendente,
            },
            antes);
        Assert.Equal(new DateOnly(2026, 10, 4), await UltimoAsync(api));

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc)); // 12:00 em Brasília
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>().ExecutarCicloAsync(Ct);
        }

        Assert.Equal(new DateOnly(2026, 10, 5), await UltimoAsync(api));
    }

    // ---------------------------------------------------------------- o último dia consolidado e a condição do desligamento

    [Fact]
    public async Task O_ultimo_dia_consolidado_e_o_da_maior_tarefa_concluida_e_nulo_sem_nenhuma()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);
        Assert.Null(await UltimoAsync(api));

        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 1));
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 3));
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IAgendador>().AgendarAsync(new NovaTarefa(ConsolidarBancoDeHorasTarefa.TipoDaTarefa, ConsolidarBancoDeHorasTarefa.Chave(new DateOnly(2026, 10, 5)), ElencoDeTeste.Hoje.AddDays(1), DiaReferencia: new DateOnly(2026, 10, 5)), Ct);
        }

        Assert.Equal(new DateOnly(2026, 10, 3), await UltimoAsync(api)); // a pendente não conta
    }

    [Fact]
    public async Task O_desligamento_so_efetiva_depois_de_o_ultimo_dia_da_pessoa_estar_consolidado()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var condicao = Assert.Single(escopo.ServiceProvider.GetServices<ICondicaoParaEfetivarDesligamento>(), c => c is CondicaoDoBancoDeHorasParaDesligamento);
        var pessoa = api.Elenco.MembroA.FuncionarioId;

        Assert.False(await condicao.PodeEfetivarAsync(pessoa, new DateOnly(2026, 10, 3), Ct)); // nada consolidado
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 2));
        Assert.False(await condicao.PodeEfetivarAsync(pessoa, new DateOnly(2026, 10, 3), Ct)); // consolidou até o dia 2, o último dia dele é 3
        await MarcarComoConsolidadoAsync(api, new DateOnly(2026, 10, 3));
        Assert.True(await condicao.PodeEfetivarAsync(pessoa, new DateOnly(2026, 10, 3), Ct));
        Assert.True(await condicao.PodeEfetivarAsync(pessoa, new DateOnly(2026, 9, 1), Ct)); // um dia mais antigo que o último consolidado
    }
}

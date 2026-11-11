using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Empresa;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Ponto;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// As duas tarefas dos feriados, no banco REAL, com o relógio fixo em 2026-10-07T12:42Z: <c>IMPORTAR_FERIADOS:ano=AAAA</c> traz os nacionais (calculados, com a Páscoa) e os estaduais da UF da empresa, sem duplicar o que já existe (a carga do <c>V001</c> de 2026 tem os
/// nomes sem acento: a conta é por data e tipo) e sem reativar o que alguém desativou; <c>REPETIR_FERIADO:ano=AAAA</c> copia para o ano os feriados ativos marcados "repete todo ano" (29/02 só em ano bissexto). As duas recuperam o ano de hoje e o seguinte. Cada teste usa o seu ano (de
/// 2041 em diante).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TarefasDeFeriadosTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(TarefasDeFeriadosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static TarefaParaExecutar DoAno(string tipo, int ano) => new(1, $"{tipo}:ano={ano}", null, new DateOnly(ano, 1, 1), null, 0);

    private static async Task ExecutarAsync(ApiDeAcesso api, string tipo, TarefaParaExecutar tarefa)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).ExecutarAsync(tarefa, Ct);
    }

    private static Task ImportarAsync(ApiDeAcesso api, int ano) => ExecutarAsync(api, ImportarFeriadosTarefa.TipoDaTarefa, DoAno(ImportarFeriadosTarefa.TipoDaTarefa, ano));

    private static Task RepetirAsync(ApiDeAcesso api, int ano) => ExecutarAsync(api, RepetirFeriadoTarefa.TipoDaTarefa, DoAno(RepetirFeriadoTarefa.TipoDaTarefa, ano));

    private static async Task RecuperarAsync(ApiDeAcesso api, string tipo)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).RecuperarAsync(Ct);
    }

    private static async Task<List<(string Nome, DateOnly Data, string Tipo, string? Uf, string? Municipio, bool Repete, bool Ativo)>> FeriadosAsync(ApiDeAcesso api, int ano)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var linhas = await db.Feriados.AsNoTracking().Where(f => f.Data >= new DateOnly(ano, 1, 1) && f.Data <= new DateOnly(ano, 12, 31)).ToListAsync();
        return [.. linhas.OrderBy(f => f.Data).ThenBy(f => f.Nome, StringComparer.Ordinal).Select(f => (f.Nome, f.Data, f.Tipo.ToString().ToUpperInvariant(), f.Uf, f.Municipio, f.RepeteTodoAno, f.Ativo))];
    }

    private static async Task SemearAsync(ApiDeAcesso api, string nome, string data, string tipo = "MUNICIPAL", string? uf = "SP", string? municipio = "São Paulo", bool ativo = true, bool repete = false) =>
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF, MUNICIPIO, ATIVO, REPETE_TODO_ANO) VALUES (@nome, @data, @tipo, @uf, @municipio, @ativo, @repete)",
            ("@nome", nome),
            ("@data", DateTime.Parse(data, System.Globalization.CultureInfo.InvariantCulture)),
            ("@tipo", tipo),
            ("@uf", uf),
            ("@municipio", municipio),
            ("@ativo", ativo),
            ("@repete", repete));

    /// <summary>A repetição parte de QUALQUER ano anterior: sem limpar as origens dos outros testes da classe, elas apareceriam nos anos deste.</summary>
    private static Task LimparOrigensAsync(ApiDeAcesso api) => ApoioDeTeste.ExecutarAsync(api.Conexao, "DELETE FROM FERIADOS WHERE REPETE_TODO_ANO = TRUE");

    private static Task LimparTarefasAsync(ApiDeAcesso api) => ApoioDeTeste.ExecutarAsync(api.Conexao, "DELETE FROM TAREFAS_AGENDADAS WHERE TIPO IN ('IMPORTAR_FERIADOS', 'REPETIR_FERIADO')");

    private static async Task<List<TarefaAgendada>> TarefasAsync(ApiDeAcesso api, string tipo)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == tipo).OrderBy(t => t.Id).ToListAsync();
    }

    // ---------------------------------------------------------------- importar

    [Fact]
    public async Task Importar_o_ano_cria_os_dez_nacionais_e_os_estaduais_da_UF_da_empresa_e_o_log_sai_como_SISTEMA()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");

        await ImportarAsync(api, 2041);

        var feriados = await FeriadosAsync(api, 2041);
        var esperados = FeriadosNacionais.Do(2041).Select(f => (f.Nome, f.Data, "NACIONAL", (string?)null, (string?)null, false, true))
            .Append(("Revolução Constitucionalista", new DateOnly(2041, 7, 9), "ESTADUAL", "SP", (string?)null, false, true))
            .OrderBy(f => f.Item2).ThenBy(f => f.Item1, StringComparer.Ordinal).ToList();
        Assert.Equal(esperados, feriados);
        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "FERIADOS");
        var doAno = eventos.Where(e => e.Acao == AuditoriaAcao.Criou && e.RegistroAfetado.StartsWith("Feriado ", StringComparison.Ordinal) && feriados.Any(f => e.RegistroAfetado == $"Feriado {f.Nome}")).ToList();
        Assert.True(doAno.Count >= 11);
        Assert.All(doAno, e => Assert.Equal(TipoDeAtor.Sistema, e.AtorTipo));
    }

    [Fact]
    public async Task Importar_duas_vezes_o_mesmo_ano_nao_duplica_nada()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");

        await ImportarAsync(api, 2042);
        var primeira = await FeriadosAsync(api, 2042);
        await ImportarAsync(api, 2042);

        Assert.Equal(11, primeira.Count);
        Assert.Equal(primeira, await FeriadosAsync(api, 2042));
    }

    [Fact]
    public async Task O_ano_de_2026_da_carga_do_banco_com_nomes_sem_acento_nao_duplica_os_nacionais()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        var antes = (await FeriadosAsync(api, 2026)).Where(f => f.Tipo == "NACIONAL").ToList();

        await ImportarAsync(api, 2026);

        var depois = await FeriadosAsync(api, 2026);
        Assert.Equal(10, antes.Count);
        Assert.Equal(antes, depois.Where(f => f.Tipo == "NACIONAL").ToList()); // os mesmos dez, com os nomes sem acento da carga
        Assert.Contains(depois, f => f is { Tipo: "ESTADUAL", Uf: "SP" } && f.Data == new DateOnly(2026, 7, 9)); // e o estadual de São Paulo entrou
    }

    [Fact]
    public async Task Feriado_nacional_desativado_nao_volta_nem_e_reativado_pela_importacao()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        await SemearAsync(api, "Natal", "2043-12-25", "NACIONAL", null, null, ativo: false);

        await ImportarAsync(api, 2043);

        var natais = (await FeriadosAsync(api, 2043)).Where(f => f.Data == new DateOnly(2043, 12, 25)).ToList();
        Assert.Equal([("Natal", new DateOnly(2043, 12, 25), "NACIONAL", (string?)null, (string?)null, false, false)], natais);
    }

    [Fact]
    public async Task Municipal_com_a_mesma_data_e_o_mesmo_nome_de_um_nacional_nao_derruba_a_importacao_e_os_outros_entram()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        await SemearAsync(api, "Natal", "2044-12-25");

        await ImportarAsync(api, 2044);

        var feriados = await FeriadosAsync(api, 2044);
        Assert.Equal(10, feriados.Count(f => f.Tipo is "NACIONAL" or "MUNICIPAL")); // os nove que faltavam entram e o Natal continua um só (o municipal)
        Assert.Single(feriados, f => f.Data == new DateOnly(2044, 12, 25));
    }

    [Fact]
    public async Task Sem_a_empresa_vem_so_os_nacionais_e_UF_sem_feriado_fixo_na_tabela_nao_cria_estadual()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        try
        {
            await ImportarAsync(api, 2045);
            Assert.Equal((10, 0), ((await FeriadosAsync(api, 2045)).Count(f => f.Tipo == "NACIONAL"), (await FeriadosAsync(api, 2045)).Count(f => f.Tipo == "ESTADUAL")));
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao, "MG", "Belo Horizonte");
        }

        try
        {
            await ImportarAsync(api, 2046);
            Assert.Equal((10, 0), ((await FeriadosAsync(api, 2046)).Count(f => f.Tipo == "NACIONAL"), (await FeriadosAsync(api, 2046)).Count(f => f.Tipo == "ESTADUAL")));
            await ConfigurarEmpresaAsync(api.Conexao, "RJ", "Niterói");
            await ImportarAsync(api, 2047);
            Assert.Contains(await FeriadosAsync(api, 2047), f => f is { Tipo: "ESTADUAL", Uf: "RJ", Nome: "Dia de São Jorge" });
        }
        finally
        {
            await ConfigurarEmpresaAsync(api.Conexao);
        }
    }

    [Fact]
    public async Task Tarefa_sem_o_dia_de_referencia_lanca_para_o_agendador_tentar_de_novo()
    {
        var api = await ApiAsync();

        var tarefa = new TarefaParaExecutar(1, "IMPORTAR_FERIADOS:ano=0", null, null, null, 0);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(api, ImportarFeriadosTarefa.TipoDaTarefa, tarefa));
        await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(api, RepetirFeriadoTarefa.TipoDaTarefa, tarefa));
    }

    [Fact]
    public async Task O_feriado_estadual_importado_vale_no_calendario_do_ponto()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        await ImportarAsync(api, 2026);

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var dia = await escopo.ServiceProvider.GetRequiredService<ICalendarioDoPonto>().DiaAsync(new DateOnly(2026, 7, 9), Ct); // quinta-feira

        Assert.Equal(new DiaDoCalendario(false, "Revolução Constitucionalista"), dia);
    }

    [Fact]
    public async Task Recuperar_agenda_a_importacao_do_ano_de_hoje_e_do_seguinte_sem_duplicar()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);

        await RecuperarAsync(api, ImportarFeriadosTarefa.TipoDaTarefa);
        await RecuperarAsync(api, ImportarFeriadosTarefa.TipoDaTarefa);

        var tarefas = await TarefasAsync(api, ImportarFeriadosTarefa.TipoDaTarefa);
        Assert.Equal(
            new (string, DateOnly?, SituacaoDaTarefa)[] { ("IMPORTAR_FERIADOS:ano=2026", new DateOnly(2026, 1, 1), SituacaoDaTarefa.Pendente), ("IMPORTAR_FERIADOS:ano=2027", new DateOnly(2027, 1, 1), SituacaoDaTarefa.Pendente) },
            tarefas.Select(t => (t.ChaveDeIdempotencia, t.DiaReferencia, t.Situacao)));
        Assert.All(tarefas, t => Assert.True(t.ExecutarApos <= ElencoDeTeste.Hoje));
    }

    [Fact]
    public async Task Recuperar_agenda_a_repeticao_do_ano_de_hoje_e_do_seguinte_sem_duplicar()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);

        await RecuperarAsync(api, RepetirFeriadoTarefa.TipoDaTarefa);
        await RecuperarAsync(api, RepetirFeriadoTarefa.TipoDaTarefa);

        var tarefas = await TarefasAsync(api, RepetirFeriadoTarefa.TipoDaTarefa);
        Assert.Equal(["REPETIR_FERIADO:ano=2026", "REPETIR_FERIADO:ano=2027"], tarefas.Select(t => t.ChaveDeIdempotencia));
    }

    [Fact]
    public async Task O_ciclo_do_executor_recupera_e_roda_as_duas_tarefas_dos_anos_de_hoje_e_do_seguinte()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo");
        await LimparTarefasAsync(api);

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            var executor = escopo.ServiceProvider.GetRequiredService<IExecutorDeTarefas>();
            await executor.RecuperarAsync(Ct);
            for (var ciclo = 0; ciclo < 5; ciclo++)
            {
                await executor.ExecutarCicloAsync(Ct); // o lote por ciclo é de 10: as outras tarefas recuperadas dividem os ciclos
            }
        }

        var importacoes = await TarefasAsync(api, ImportarFeriadosTarefa.TipoDaTarefa);
        Assert.Equal([SituacaoDaTarefa.Concluida, SituacaoDaTarefa.Concluida], importacoes.Where(t => t.ChaveDeIdempotencia is "IMPORTAR_FERIADOS:ano=2026" or "IMPORTAR_FERIADOS:ano=2027").Select(t => t.Situacao));
        Assert.Equal(10, (await FeriadosAsync(api, 2027)).Count(f => f.Tipo == "NACIONAL"));
        Assert.Contains(await FeriadosAsync(api, 2027), f => f is { Tipo: "ESTADUAL", Uf: "SP" });
    }

    // ---------------------------------------------------------------- repetir

    [Fact]
    public async Task Repetir_copia_so_os_ativos_marcados_para_repetir_com_a_UF_e_o_municipio_deles_e_nao_duplica()
    {
        var api = await ApiAsync();
        await LimparOrigensAsync(api);
        await SemearAsync(api, "Aniversário da cidade", "2050-03-15", repete: true);
        await SemearAsync(api, "Niterói em festa", "2050-03-16", uf: "RJ", municipio: "Niterói", repete: true);
        await SemearAsync(api, "Não repete", "2050-03-17");
        await SemearAsync(api, "Desativado que repetia", "2050-03-18", ativo: false, repete: true);

        await RepetirAsync(api, 2051);
        var primeira = await FeriadosAsync(api, 2051);
        await RepetirAsync(api, 2051);

        Assert.Equal(
            [
                ("Aniversário da cidade", new DateOnly(2051, 3, 15), "MUNICIPAL", "SP", "São Paulo", true, true),
                ("Niterói em festa", new DateOnly(2051, 3, 16), "MUNICIPAL", "RJ", "Niterói", true, true),
            ],
            primeira);
        Assert.Equal(primeira, await FeriadosAsync(api, 2051));
    }

    [Fact]
    public async Task Repetir_cada_ano_parte_dos_anteriores_e_o_ano_pulado_nao_perde_o_feriado()
    {
        var api = await ApiAsync();
        await LimparOrigensAsync(api);
        await SemearAsync(api, "Feriado de data fixa", "2052-08-10", repete: true);

        await RepetirAsync(api, 2055); // o 2053 e o 2054 nunca rodaram (a aplicação ficou parada)

        Assert.Equal([("Feriado de data fixa", new DateOnly(2055, 8, 10))], (await FeriadosAsync(api, 2055)).Select(f => (f.Nome, f.Data)));
        Assert.Empty(await FeriadosAsync(api, 2053));
    }

    [Fact]
    public async Task Vinte_e_nove_de_fevereiro_so_repete_em_ano_bissexto_e_o_ano_comum_nao_apaga_a_origem()
    {
        var api = await ApiAsync();
        await LimparOrigensAsync(api);
        await SemearAsync(api, "Dia bissexto da cidade", "2056-02-29", repete: true);

        await RepetirAsync(api, 2057); // ano comum
        await RepetirAsync(api, 2060); // bissexto: parte do 2056

        Assert.Empty(await FeriadosAsync(api, 2057));
        Assert.Equal([("Dia bissexto da cidade", new DateOnly(2060, 2, 29))], (await FeriadosAsync(api, 2060)).Select(f => (f.Nome, f.Data)));
    }

    [Fact]
    public async Task Se_o_ano_ja_tem_a_copia_desativada_pelo_RH_a_repeticao_nao_a_recria_nem_a_reativa()
    {
        var api = await ApiAsync();
        await LimparOrigensAsync(api);
        await SemearAsync(api, "Aniversário desativado", "2061-04-12", repete: true);
        await SemearAsync(api, "Aniversário desativado", "2062-04-12", ativo: false, repete: true);

        await RepetirAsync(api, 2062);

        Assert.Equal([("Aniversário desativado", new DateOnly(2062, 4, 12), "MUNICIPAL", "SP", "São Paulo", true, false)], await FeriadosAsync(api, 2062));
    }

    [Fact]
    public async Task Repetir_nao_copia_do_proprio_ano_nem_de_ano_futuro()
    {
        var api = await ApiAsync();
        await LimparOrigensAsync(api);
        await SemearAsync(api, "Do ano seguinte", "2064-05-20", repete: true);

        await RepetirAsync(api, 2063);
        await RepetirAsync(api, 2064);

        Assert.Empty(await FeriadosAsync(api, 2063));
        Assert.Equal([("Do ano seguinte", new DateOnly(2064, 5, 20))], (await FeriadosAsync(api, 2064)).Select(f => (f.Nome, f.Data)));
    }

    [Fact]
    public async Task Parametros_da_tarefa_nao_levam_dado_pessoal_so_o_ano_na_chave_e_no_dia_de_referencia()
    {
        var api = await ApiAsync();
        await LimparTarefasAsync(api);

        await RecuperarAsync(api, ImportarFeriadosTarefa.TipoDaTarefa);

        var tarefa = (await TarefasAsync(api, ImportarFeriadosTarefa.TipoDaTarefa)).First();
        Assert.Equal((null, null), (tarefa.Parametros, tarefa.ReferenciaId));
    }
}

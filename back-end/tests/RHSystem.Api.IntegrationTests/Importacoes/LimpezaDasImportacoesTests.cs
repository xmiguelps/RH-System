using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Documentos;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Infrastructure.Importacoes;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;
using static RHSystem.Api.IntegrationTests.Importacoes.PlanilhasDeTeste;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>
/// <c>LIMPAR_IMPORTACOES_VENCIDAS</c>: a prévia tem dado pessoal e o arquivo tem o mesmo dado, então os dois somem quando a importação vence (1 hora), sem esperar o dia seguinte. A linha fica com os totais; a limpeza é por
/// hora, de até 100 importações por execução, e falha de um arquivo não impede as outras importações. A tarefa vem do contêiner (o que prova também o registro). Relógio fixo em 2026-10-07T12:42Z.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimpezaDasImportacoesTests(MySqlFixture mysql)
{
    private const string Tipo = "LIMPAR_IMPORTACOES_VENCIDAS";

    private const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static int _proximo = 850_000_000;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(LimpezaDasImportacoesTests));

    private static async Task<string?> ColunaAsync(ApiDeAcesso api, int id, string coluna) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT {coluna} FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}") is { } valor and not DBNull ? Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) : null;

    /// <summary>Uma prévia criada pela API, como o RH a faria: devolve o id e o caminho do arquivo.</summary>
    private static async Task<(int Id, string Caminho)> PreviaAsync(ApiDeAcesso api, bool comErro = false, bool tokenNovo = false)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo {s}", 40);
        await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor {s}");
        var n = Interlocked.Increment(ref _proximo);
        var linha = Linha(nome: $"Pessoa {n}", cpf: comErro ? "123.456.789-00" : CpfsDeTeste.Valido(n), email: $"pessoa{n}@limpeza.teste.com", cargo: $"Cargo {s}", setor: $"Setor {s}");
        var cliente = tokenNovo ? await api.ClienteDoRhAgoraAsync() : api.Api.ClienteComo(api.Elenco.Rh);
        var resposta = await cliente.PostAsync("/api/rh/importacoes", FormulariosDeTeste.Envio("novos.xlsx", Xlsx([linha]), TipoXlsx));
        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("importacaoId").GetInt32();
        return (id, (await ColunaAsync(api, id, "CAMINHO_ARQUIVO"))!);
    }

    private static async Task ExecutarAsync(IServiceProvider servicos)
    {
        await using var escopo = servicos.CreateAsyncScope();
        var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().OfType<LimparImportacoesVencidasTarefa>().Single();
        await tarefa.ExecutarAsync(new TarefaParaExecutar(0, $"{Tipo}:hora=2026-10-07T12", null, null, null, 1), CancellationToken.None);
    }

    private static Task ExecutarAsync(ApiDeAcesso api) => ExecutarAsync(api.Api.Services);

    /// <summary>Roda com o relógio no instante dado e o devolve ao de sempre.</summary>
    private static async Task NoInstanteAsync(ApiDeAcesso api, DateTime instante, Func<Task> acao)
    {
        try
        {
            api.Api.Relogio.Fixar(instante);
            await acao();
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Vencida_perde_a_previa_e_o_arquivo_e_a_linha_fica_com_os_totais()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, comErro: false);
        Assert.True(api.Api.Armazenamento.Existe(caminho));

        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(1), () => ExecutarAsync(api)); // 13:42:00, o instante exato do vencimento

        Assert.Null(await ColunaAsync(api, id, "PREVIA"));
        Assert.Null(await ColunaAsync(api, id, "CAMINHO_ARQUIVO"));
        Assert.Equal(("PRONTA", "1", "1", "0", "novos.xlsx"), (await ColunaAsync(api, id, "SITUACAO"), await ColunaAsync(api, id, "TOTAL_LINHAS"), await ColunaAsync(api, id, "LINHAS_VALIDAS"), await ColunaAsync(api, id, "LINHAS_COM_ERRO"), await ColunaAsync(api, id, "NOME_ARQUIVO")));
        Assert.False(api.Api.Armazenamento.Existe(caminho));
        Assert.Equal([caminho], api.Api.Armazenamento.Apagados);
    }

    [Fact]
    public async Task So_a_vencida_e_limpa_a_importacao_ainda_valida_continua_inteira()
    {
        var api = await ApiAsync();
        var (vencida, caminhoDaVencida) = await PreviaAsync(api); // vence às 13:42
        int valida = 0;
        string caminhoDaValida = string.Empty;
        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddMinutes(30), async () => (valida, caminhoDaValida) = await PreviaAsync(api, tokenNovo: true)); // criada às 13:12, vence às 14:12

        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(1), () => ExecutarAsync(api)); // 13:42

        Assert.Null(await ColunaAsync(api, vencida, "PREVIA"));
        Assert.False(api.Api.Armazenamento.Existe(caminhoDaVencida));
        Assert.NotNull(await ColunaAsync(api, valida, "PREVIA"));
        Assert.Equal(caminhoDaValida, await ColunaAsync(api, valida, "CAMINHO_ARQUIVO"));
        Assert.True(api.Api.Armazenamento.Existe(caminhoDaValida));
    }

    [Fact]
    public async Task Com_erros_vencida_tambem_e_limpa()
    {
        var api = await ApiAsync();
        var (id, caminho) = await PreviaAsync(api, comErro: true);
        Assert.Equal("COM_ERROS", await ColunaAsync(api, id, "SITUACAO"));

        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(2), () => ExecutarAsync(api));

        Assert.Null(await ColunaAsync(api, id, "PREVIA"));
        Assert.Null(await ColunaAsync(api, id, "CAMINHO_ARQUIVO"));
        Assert.False(api.Api.Armazenamento.Existe(caminho));
    }

    [Fact]
    public async Task Ainda_valida_nao_e_tocada()
    {
        var api = await ApiAsync();
        var (id, caminho) = await PreviaAsync(api);

        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(1).AddSeconds(-1), () => ExecutarAsync(api)); // 13:41:59: falta um segundo

        Assert.NotNull(await ColunaAsync(api, id, "PREVIA"));
        Assert.Equal(caminho, await ColunaAsync(api, id, "CAMINHO_ARQUIVO"));
        Assert.True(api.Api.Armazenamento.Existe(caminho));
    }

    [Fact]
    public async Task Confirmada_nao_e_tocada_porque_ja_esta_sem_previa_e_sem_caminho()
    {
        var api = await ApiAsync();
        var (id, caminho) = await PreviaAsync(api);
        Assert.Equal(200, (int)(await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync($"/api/rh/importacoes/{id}/confirmar", new { enviarConvite = false })).StatusCode);
        Assert.Equal(1, api.Api.Armazenamento.ExclusoesPedidas.Count(c => c == caminho)); // a confirmação já apagou o arquivo dela

        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(3), () => ExecutarAsync(api));

        Assert.Equal(("CONFIRMADA", "1"), (await ColunaAsync(api, id, "SITUACAO"), await ColunaAsync(api, id, "TOTAL_CRIADOS")));
        Assert.Equal(1, api.Api.Armazenamento.ExclusoesPedidas.Count(c => c == caminho)); // nenhuma exclusão nova dela: a limpeza nem olhou para ela (as de outras importações vencidas são de outros testes)
    }

    [Fact]
    public async Task Roda_duas_vezes_sem_efeito_novo()
    {
        var api = await ApiAsync();
        var (id, caminho) = await PreviaAsync(api);
        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(2), async () =>
        {
            await ExecutarAsync(api);
            var exclusoesDepoisDaPrimeira = api.Api.Armazenamento.ExclusoesPedidas.Count(c => c == caminho);

            await ExecutarAsync(api);

            Assert.Equal(1, exclusoesDepoisDaPrimeira);
            Assert.Equal(1, api.Api.Armazenamento.ExclusoesPedidas.Count(c => c == caminho)); // a segunda não pediu nada
        });
        Assert.Null(await ColunaAsync(api, id, "PREVIA"));
    }

    [Fact]
    public async Task Falha_ao_apagar_um_blob_nao_impede_as_outras_a_previa_some_e_a_tarefa_lanca_sem_o_texto_da_excecao()
    {
        var api = await ApiAsync();
        var (idA, caminhoA) = await PreviaAsync(api);
        var (idB, caminhoB) = await PreviaAsync(api);
        var (idC, caminhoC) = await PreviaAsync(api);
        var logs = new ColetorDeLogs();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        api.Api.Armazenamento.AoApagar = caminho => caminho == caminhoB ? throw new IOException("segredo-do-blob-9999") : Task.CompletedTask;
        try
        {
            await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(2), async () =>
            {
                var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => ExecutarAsync(fabrica.Services));

                Assert.DoesNotContain("segredo-do-blob-9999", erro.Message);
                Assert.Equal("1 arquivo(s) de importação não puderam ser apagados; ficam para a próxima execução.", erro.Message);
                foreach (var id in new[] { idA, idB, idC })
                {
                    Assert.Null(await ColunaAsync(api, id, "PREVIA")); // o dado pessoal some mesmo quando o arquivo não pôde ser apagado
                }

                Assert.Null(await ColunaAsync(api, idA, "CAMINHO_ARQUIVO"));
                Assert.Null(await ColunaAsync(api, idC, "CAMINHO_ARQUIVO"));
                Assert.Equal(caminhoB, await ColunaAsync(api, idB, "CAMINHO_ARQUIVO")); // o caminho fica: a próxima execução tenta de novo
                Assert.False(api.Api.Armazenamento.Existe(caminhoA));
                Assert.True(api.Api.Armazenamento.Existe(caminhoB));
                Assert.False(api.Api.Armazenamento.Existe(caminhoC));
                string texto;
                lock (logs.Mensagens)
                {
                    texto = string.Join('\n', logs.Mensagens);
                }

                Assert.DoesNotContain("segredo-do-blob-9999", texto); // só o tipo da exceção vai para o log

                api.Api.Armazenamento.AoApagar = null; // o armazenamento voltou
                await ExecutarAsync(api);

                Assert.Null(await ColunaAsync(api, idB, "CAMINHO_ARQUIVO"));
                Assert.False(api.Api.Armazenamento.Existe(caminhoB));
            });
        }
        finally
        {
            api.Api.Armazenamento.AoApagar = null;
        }
    }

    [Fact]
    public async Task A_recuperacao_agenda_uma_tarefa_por_hora_com_chave_estavel()
    {
        // Banco só deste teste: as execuções dos outros testes da classe também agendam a hora seguinte e sujariam a fila do tipo.
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(LimpezaDasImportacoesTests) + "-recuperacao");
        async Task<List<string>> ChavesAsync()
        {
            await using var db = ContextoDeTeste.Novo(api.Conexao);
            return await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == Tipo).OrderBy(t => t.Id).Select(t => t.ChaveDeIdempotencia).ToListAsync();
        }

        async Task<List<DateTime>> ExecutarAposAsync()
        {
            await using var db = ContextoDeTeste.Novo(api.Conexao);
            return await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == Tipo).OrderBy(t => t.Id).Select(t => t.ExecutarApos).ToListAsync();
        }

        async Task RecuperarAsync()
        {
            await using var escopo = api.Api.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().OfType<LimparImportacoesVencidasTarefa>().Single().RecuperarAsync(CancellationToken.None);
        }

        await RecuperarAsync();
        await RecuperarAsync(); // a mesma hora: a mesma chave, nada novo
        var primeira = await ChavesAsync();
        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddMinutes(17), RecuperarAsync); // 12:59: ainda a hora das 12
        var mesmaHora = await ChavesAsync();
        await NoInstanteAsync(api, ElencoDeTeste.Hoje.AddHours(1), RecuperarAsync); // 13:42: a hora das 13
        var proximaHora = await ChavesAsync();

        Assert.Equal([ElencoDeTeste.Hoje], (await ExecutarAposAsync()).Take(1).Select(d => DateTime.SpecifyKind(d, DateTimeKind.Utc))); // pronta para rodar já, não amanhã
        Assert.Equal([$"{Tipo}:hora=2026-10-07T12"], primeira);
        Assert.Equal(primeira, mesmaHora);
        Assert.Equal([$"{Tipo}:hora=2026-10-07T12", $"{Tipo}:hora=2026-10-07T13"], proximaHora);
    }

    [Fact]
    public async Task A_execucao_agenda_a_da_proxima_hora_para_nao_deixar_uma_hora_sem_tarefa()
    {
        // A recuperação roda a cada 60 minutos CONTADOS DO FIM da anterior, então o espaçamento deriva e pode pular uma hora (11:59 e depois 13:00): cada execução encadeia a da hora seguinte, para a prévia que vence
        // às 12:05 não esperar até as 13:00.
        var api = await ApiAsync();

        await ExecutarAsync(api); // 12:42
        await ExecutarAsync(api); // de novo: a mesma chave, nada a mais

        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var proxima = await db.TarefasAgendadas.AsNoTracking().SingleAsync(t => t.Tipo == Tipo && t.ChaveDeIdempotencia == $"{Tipo}:hora=2026-10-07T13");
        Assert.Equal(new DateTime(2026, 10, 7, 13, 0, 0), DateTime.SpecifyKind(proxima.ExecutarApos, DateTimeKind.Unspecified)); // às 13:00, não já
    }

    [Fact]
    public async Task Mais_de_100_vencidas_sao_limpas_em_execucoes_seguidas()
    {
        var api = await ApiAsync();
        await LimparTudoQueJaVenceuAsync(api);
        var lote = $"lote-{Guid.NewGuid():N}";
        var caminhos = new List<string>();
        for (var i = 0; i < 105; i++)
        {
            var caminho = CaminhosDeArquivo.Novo("importacoes", ".xlsx");
            caminhos.Add(caminho);
            api.Api.Armazenamento.Semear(caminho, [1, 2, 3], ElencoDeTeste.Hoje.AddHours(-3), TipoXlsx);
            await Sql.ExecutarAsync(
                api.Conexao,
                $"INSERT INTO IMPORTACOES (ID_FUNCIONARIO_RH, NOME_ARQUIVO, SITUACAO, TOTAL_LINHAS, LINHAS_VALIDAS, LINHAS_COM_ERRO, CRIADA_EM, EXPIRA_EM, CAMINHO_ARQUIVO, PREVIA) VALUES ({api.Elenco.Rh.FuncionarioId}, '{lote}.xlsx', 'PRONTA', 1, 1, 0, '2026-10-07 10:00:00', '2026-10-07 11:00:00', '{caminho}', '[]')");
        }

        async Task<long> PendentesAsync() => await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM IMPORTACOES WHERE NOME_ARQUIVO = '{lote}.xlsx' AND (PREVIA IS NOT NULL OR CAMINHO_ARQUIVO IS NOT NULL)");

        Assert.Equal(105, await PendentesAsync());
        await ExecutarAsync(api);
        Assert.Equal(5, await PendentesAsync()); // 100 por execução
        var maisNovos = await Sql.ColunaAsync(api.Conexao, $"SELECT ID_IMPORTACAO FROM IMPORTACOES WHERE NOME_ARQUIVO = '{lote}.xlsx' ORDER BY ID_IMPORTACAO DESC LIMIT 5");
        var pendentes = await Sql.ColunaAsync(api.Conexao, $"SELECT ID_IMPORTACAO FROM IMPORTACOES WHERE NOME_ARQUIVO = '{lote}.xlsx' AND (PREVIA IS NOT NULL OR CAMINHO_ARQUIVO IS NOT NULL) ORDER BY ID_IMPORTACAO DESC");
        Assert.Equal(maisNovos, pendentes); // as mais antigas primeiro: sobram as 5 mais novas
        await ExecutarAsync(api);
        Assert.Equal(0, await PendentesAsync());
        Assert.All(caminhos, c => Assert.False(api.Api.Armazenamento.Existe(c)));
    }

    /// <summary>Esvazia o que já venceu (de outros testes), para a contagem do lote de 105 ser só a dele.</summary>
    private static async Task LimparTudoQueJaVenceuAsync(ApiDeAcesso api)
    {
        for (var i = 0; i < 20 && await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM IMPORTACOES WHERE EXPIRA_EM <= '2026-10-07 12:42:00' AND (PREVIA IS NOT NULL OR CAMINHO_ARQUIVO IS NOT NULL)") > 0; i++)
        {
            await ExecutarAsync(api);
        }
    }
}

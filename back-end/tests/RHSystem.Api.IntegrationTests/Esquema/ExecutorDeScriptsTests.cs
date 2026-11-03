using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Esquema;

[Collection(MySqlCollection.Nome)]
public class ExecutorDeScriptsTests(MySqlFixture mysql)
{
    private static ExecutorDeScripts NovoExecutor(string conexao) => new(conexao, NullLogger<ExecutorDeScripts>.Instance);

    private static ScriptSql Script(int versao, string nome, string conteudo) => new(versao, nome, conteudo);

    private static async Task<long> ContarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt64(await comando.ExecuteScalarAsync());
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task Aplica_em_ordem_e_registra_o_hash()
    {
        var conexao = await mysql.NovoBancoAsync();
        var s1 = Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY);");
        var s2 = Script(2, "dados_de_a", "INSERT INTO A VALUES (1);\nINSERT INTO A VALUES (2);");

        await NovoExecutor(conexao).AplicarPendentesAsync([s2, s1], CancellationToken.None); // fora de ordem: o executor ordena

        Assert.Equal(2, await ContarAsync(conexao, "SELECT COUNT(*) FROM A"));
        Assert.Equal(2, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand("SELECT VERSAO, NOME, HASH_SHA256 FROM SCHEMA_VERSOES ORDER BY VERSAO", c);
        await using var leitor = await comando.ExecuteReaderAsync();
        Assert.True(await leitor.ReadAsync());
        Assert.Equal((1, "tabela_a", ExecutorDeScripts.CalcularHash(s1.Conteudo)), (leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2)));
        Assert.True(await leitor.ReadAsync());
        Assert.Equal((2, "dados_de_a", ExecutorDeScripts.CalcularHash(s2.Conteudo)), (leitor.GetInt32(0), leitor.GetString(1), leitor.GetString(2)));
    }

    [Fact]
    public async Task Segunda_execucao_nao_reaplica_nenhum_script()
    {
        var conexao = await mysql.NovoBancoAsync();
        var scripts = new[]
        {
            Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY);"),
            Script(2, "dados_de_a", "INSERT INTO A VALUES (1);"),
        };

        await NovoExecutor(conexao).AplicarPendentesAsync(scripts, CancellationToken.None);
        await NovoExecutor(conexao).AplicarPendentesAsync(scripts, CancellationToken.None); // reaplicar daria "tabela já existe" e chave repetida

        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM A"));
        Assert.Equal(2, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
    }

    [Fact]
    public async Task Script_aplicado_e_depois_editado_faz_o_executor_recusar_com_mensagem_clara()
    {
        var conexao = await mysql.NovoBancoAsync();
        await NovoExecutor(conexao).AplicarPendentesAsync([Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY);")], CancellationToken.None);

        var editado = Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY, NOME VARCHAR(10));");
        var erro = await Assert.ThrowsAsync<ScriptJaAplicadoFoiAlteradoException>(
            () => NovoExecutor(conexao).AplicarPendentesAsync([editado], CancellationToken.None));

        Assert.Equal("O script V001 já foi aplicado e foi alterado. Não edite scripts aplicados: crie um script novo.", erro.Message);
        Assert.Equal(1, erro.Versao);
    }

    [Fact]
    public async Task Duas_execucoes_simultaneas_aplicam_cada_script_uma_unica_vez()
    {
        var conexao = await mysql.NovoBancoAsync();
        // NÃO idempotente, com uma pausa no meio para forçar a sobreposição: sem a trava, o segundo executor bateria em "tabela já existe".
        var scripts = new[] { Script(1, "tabela_t", "CREATE TABLE T (ID INT PRIMARY KEY);\nDO SLEEP(1);\nINSERT INTO T VALUES (1);") };
        using var barreira = new Barrier(2);

        Task Executar() => Task.Run(() =>
        {
            barreira.SignalAndWait();
            return NovoExecutor(conexao).AplicarPendentesAsync(scripts, CancellationToken.None);
        });

        await Task.WhenAll(Executar(), Executar());

        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM T"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
    }

    [Fact]
    public async Task Script_que_falha_no_meio_lanca_FalhaNoScript_e_nao_registra_a_versao()
    {
        var conexao = await mysql.NovoBancoAsync();
        var script = Script(1, "quebrado", "CREATE TABLE A (ID INT PRIMARY KEY);\nINSERT INTO TABELA_QUE_NAO_EXISTE VALUES (1);\nCREATE TABLE B (ID INT);");

        var erro = await Assert.ThrowsAsync<FalhaNoScriptException>(
            () => NovoExecutor(conexao).AplicarPendentesAsync([script], CancellationToken.None));

        Assert.Equal(1, erro.Versao);
        Assert.Equal(2, erro.IndiceDoComando); // o segundo comando (contagem a partir de 1)
        Assert.Contains("parcialmente", erro.Message);
        Assert.Equal(0, await ContarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES"));
        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'A'"));
        Assert.Equal(0, await ContarAsync(conexao, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'B'"));
    }

    [Fact]
    public async Task Hash_ignora_a_diferenca_entre_CRLF_e_LF()
    {
        Assert.Equal(ExecutorDeScripts.CalcularHash("SELECT 1;\nSELECT 2;"), ExecutorDeScripts.CalcularHash("SELECT 1;\r\nSELECT 2;"));
        Assert.Equal(ExecutorDeScripts.CalcularHash("SELECT 1;"), ExecutorDeScripts.CalcularHash("﻿SELECT 1;")); // BOM não conta
        Assert.NotEqual(ExecutorDeScripts.CalcularHash("SELECT 1;"), ExecutorDeScripts.CalcularHash("SELECT 2;"));

        var conexao = await mysql.NovoBancoAsync();
        await NovoExecutor(conexao).AplicarPendentesAsync([Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY);\nSELECT 1;")], CancellationToken.None);
        await NovoExecutor(conexao).AplicarPendentesAsync([Script(1, "tabela_a", "CREATE TABLE A (ID INT PRIMARY KEY);\r\nSELECT 1;")], CancellationToken.None); // não lança
    }

    [Fact]
    public async Task Versao_pendente_menor_que_a_maior_aplicada_e_recusada()
    {
        var conexao = await mysql.NovoBancoAsync();
        var s1 = Script(1, "a", "CREATE TABLE A (ID INT);");
        var s2 = Script(2, "b", "CREATE TABLE B (ID INT);");
        var s3 = Script(3, "c", "CREATE TABLE C (ID INT);");
        await NovoExecutor(conexao).AplicarPendentesAsync([s1, s3], CancellationToken.None);

        var erro = await Assert.ThrowsAsync<VersaoForaDeOrdemException>(
            () => NovoExecutor(conexao).AplicarPendentesAsync([s1, s2, s3], CancellationToken.None));

        Assert.Equal(2, erro.VersaoPendente);
        Assert.Equal(3, erro.MaiorVersaoAplicada);
        Assert.Contains("V002", erro.Message);
        Assert.Contains("V003", erro.Message);
    }

    [Fact]
    public async Task Script_aplicado_cujo_arquivo_sumiu_e_recusado()
    {
        var conexao = await mysql.NovoBancoAsync();
        var s1 = Script(1, "a", "CREATE TABLE A (ID INT);");
        var s2 = Script(2, "b", "CREATE TABLE B (ID INT);");
        await NovoExecutor(conexao).AplicarPendentesAsync([s1, s2], CancellationToken.None);

        var erro = await Assert.ThrowsAsync<ScriptAplicadoSumiuException>(
            () => NovoExecutor(conexao).AplicarPendentesAsync([s1], CancellationToken.None));

        Assert.Equal(2, erro.Versao);
        Assert.Contains("V002", erro.Message);
    }

    [Fact]
    public async Task Banco_com_tabelas_mas_sem_historico_em_SCHEMA_VERSOES_e_recusado()
    {
        var conexao = await mysql.NovoBancoAsync();
        await ExecutarAsync(conexao, "CREATE TABLE FEITA_A_MAO (ID INT)");

        var erro = await Assert.ThrowsAsync<BancoSemHistoricoDeScriptsException>(
            () => NovoExecutor(conexao).AplicarPendentesAsync([Script(1, "a", "CREATE TABLE A (ID INT);")], CancellationToken.None));

        Assert.Equal(1, erro.QuantidadeDeTabelas);
        Assert.Equal(0, await ContarAsync(conexao, "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = 'A'"));
    }

    [Fact]
    public async Task Trava_distinta_por_banco_nao_bloqueia_outro_banco_no_mesmo_servidor()
    {
        var bancoLento = await mysql.NovoBancoAsync();
        var bancoRapido = await mysql.NovoBancoAsync();
        var lento = Script(1, "lento", "DO SLEEP(4);\nCREATE TABLE L (ID INT);");
        var rapido = Script(1, "rapido", "CREATE TABLE R (ID INT);");

        var tarefaLenta = Task.Run(() => NovoExecutor(bancoLento).AplicarPendentesAsync([lento], CancellationToken.None));
        await Task.Delay(1000); // dá tempo de o executor lento pegar a trava dele

        var relogio = Stopwatch.StartNew();
        await NovoExecutor(bancoRapido).AplicarPendentesAsync([rapido], CancellationToken.None);
        relogio.Stop();

        Assert.False(tarefaLenta.IsCompleted, "o executor lento deveria ainda estar rodando");
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(2), $"o outro banco esperou {relogio.Elapsed}: a trava não é por banco");
        await tarefaLenta;
    }
}

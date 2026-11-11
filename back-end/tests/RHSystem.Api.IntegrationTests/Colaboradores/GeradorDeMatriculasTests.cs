using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// O contador <c>SEQUENCIAS</c> no banco REAL (MySQL em contêiner com os scripts de verdade). A técnica foi provada pela sonda da fatia 0
/// (Task 5); aqui ela é o produto: <c>UPDATE ... LAST_INSERT_ID(ULTIMO_VALOR + n)</c> e <c>SELECT LAST_INSERT_ID()</c> na conexão e na
/// transação do chamador.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class GeradorDeMatriculasTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static async Task<IReadOnlyList<string>> ReservarEmTransacaoAsync(AppDbContext db, int quantidade) =>
        await new UnidadeDeTrabalho(db).ExecutarEmTransacaoAsync(() => new GeradorDeMatriculas(db).ReservarAsync(quantidade, Ct), Ct);

    private static async Task<object?> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return await comando.ExecuteScalarAsync();
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<long> ContadorAsync(string conexao) =>
        Convert.ToInt64(await EscalarAsync(conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));

    [Fact]
    public async Task Reservar_1_devolve_00001_e_depois_00002()
    {
        // A linha MATRICULA nasce com ULTIMO_VALOR = 0 (V001).
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        Assert.Equal(["00001"], await ReservarEmTransacaoAsync(db, 1));
        Assert.Equal(["00002"], await ReservarEmTransacaoAsync(db, 1));
        Assert.Equal(2, await ContadorAsync(conexao));
    }

    [Fact]
    public async Task Reservar_3_devolve_tres_matriculas_consecutivas_de_5_digitos_com_zero_a_esquerda()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        Assert.Equal(["00001", "00002", "00003"], await ReservarEmTransacaoAsync(db, 3));
        Assert.Equal(["00004", "00005"], await ReservarEmTransacaoAsync(db, 2));
    }

    [Fact]
    public async Task Reservar_500_devolve_500_matriculas_consecutivas_sem_repetir()
    {
        // O limite de cima da quantidade (a importação de planilha tem até 500 linhas).
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        var matriculas = await ReservarEmTransacaoAsync(db, 500);

        Assert.Equal(500, matriculas.Count);
        Assert.Equal(Enumerable.Range(1, 500).Select(n => n.ToString("D5")), matriculas);
        Assert.Equal(500, await ContadorAsync(conexao));
    }

    [Fact]
    public async Task Reservar_fora_de_transacao_lanca_InvalidOperationException()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new GeradorDeMatriculas(db).ReservarAsync(1, Ct));

        // Sem transação o UPDATE confirmaria sozinho (e o LAST_INSERT_ID poderia vir de outra conexão): nada pode ter sido gravado.
        Assert.Equal(0, await ContadorAsync(conexao));
    }

    [Fact]
    public async Task Rollback_da_transacao_devolve_o_numero_e_nao_deixa_buraco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var unidade = new UnidadeDeTrabalho(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() => unidade.ExecutarEmTransacaoAsync<bool>(
            async () =>
            {
                Assert.Equal(["00001", "00002"], await new GeradorDeMatriculas(db).ReservarAsync(2, Ct));
                throw new InvalidOperationException("o cadastro falhou depois de reservar");
            },
            Ct));

        Assert.Equal(0, await ContadorAsync(conexao));
        Assert.Equal(["00001"], await ReservarEmTransacaoAsync(db, 1));
    }

    [Fact]
    public async Task Vinte_cadastros_simultaneos_recebem_vinte_matriculas_distintas_e_consecutivas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        using var barreira = new Barrier(20);

        // 20 tarefas, cada uma com o seu DbContext, a sua conexão e a sua transação, liberadas juntas. Threads dedicadas: a barreira bloqueia
        // 20 threads de uma vez e o pool as criaria devagar demais. Cada uma grava um Funcionario com a matrícula (chave única).
        var tarefas = Enumerable.Range(1, 20).Select(i => Task.Factory.StartNew(
            async () =>
            {
                await using var db = ContextoDeTeste.Novo(conexao);
                var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH", Ct);
                var unidade = new UnidadeDeTrabalho(db);
                barreira.SignalAndWait();

                return await unidade.ExecutarEmTransacaoAsync(
                    async () =>
                    {
                        var pessoa = Funcionario.Criar($"Pessoa {i}", $"pessoa{i}@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
                        pessoa.AtribuirMatricula((await new GeradorDeMatriculas(db).ReservarAsync(1, Ct)).Single());
                        db.Funcionarios.Add(pessoa);
                        await unidade.SalvarAsync(Ct);
                        return pessoa.Matricula!;
                    },
                    Ct);
            },
            Ct,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()).ToArray();

        var matriculas = await Task.WhenAll(tarefas);

        Assert.Equal(Enumerable.Range(1, 20).Select(n => n.ToString("D5")), matriculas.Order());
        Assert.Equal(20, await ContadorAsync(conexao));
        Assert.Equal(20L, Convert.ToInt64(await EscalarAsync(conexao, "SELECT COUNT(DISTINCT MATRICULA) FROM FUNCIONARIOS")));
    }

    [Fact]
    public async Task Contador_acima_de_99999_lanca_InvalidOperationException_e_nao_grava_matricula_de_outro_tamanho()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await ExecutarAsync(conexao, "UPDATE SEQUENCIAS SET ULTIMO_VALOR = 99998 WHERE NOME = 'MATRICULA'");
        await using var db = ContextoDeTeste.Novo(conexao);

        // O último número que cabe em 5 dígitos ainda sai.
        Assert.Equal(["99999"], await ReservarEmTransacaoAsync(db, 1));

        // O seguinte (100000) não cabe: recusa, e a transação desfeita deixa o contador onde estava.
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReservarEmTransacaoAsync(db, 1));
        Assert.Equal(99999, await ContadorAsync(conexao));

        // Pedir mais do que sobra também recusa, sem entregar parte dos números.
        await ExecutarAsync(conexao, "UPDATE SEQUENCIAS SET ULTIMO_VALOR = 99997 WHERE NOME = 'MATRICULA'");
        await Assert.ThrowsAsync<InvalidOperationException>(() => ReservarEmTransacaoAsync(db, 3));
        Assert.Equal(99997, await ContadorAsync(conexao));
    }

    [Fact]
    public async Task Linha_MATRICULA_ausente_lanca_InvalidOperationException()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await ExecutarAsync(conexao, "DELETE FROM SEQUENCIAS WHERE NOME = 'MATRICULA'");
        await using var db = ContextoDeTeste.Novo(conexao);

        await Assert.ThrowsAsync<InvalidOperationException>(() => ReservarEmTransacaoAsync(db, 1));
    }

    [Fact]
    public async Task Matricula_informada_a_mao_nao_move_o_contador()
    {
        // Decisão 3 do V001: o contador só anda por quem pede número; uma matrícula digitada à mão não o move (e a chave única do
        // FUNCIONARIOS é quem impede a colisão).
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH", Ct);
        var pessoa = Funcionario.Criar("Pessoa à mão", "mao@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
        pessoa.AtribuirMatricula("00500");
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync(Ct);

        Assert.Equal(0, await ContadorAsync(conexao));
        Assert.Equal(["00001"], await ReservarEmTransacaoAsync(db, 1));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(501)]
    public async Task Quantidade_zero_ou_acima_de_500_lanca_ArgumentOutOfRangeException(int quantidade)
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => ReservarEmTransacaoAsync(db, quantidade));
        Assert.Equal(0, await ContadorAsync(conexao));
    }
}

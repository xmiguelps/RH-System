using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Sondas;

public class LinhaDaSonda
{
    public int Id { get; set; }

    public string Situacao { get; set; } = string.Empty;
}

public class ChaveDaSonda
{
    public int Id { get; set; }

    public string Chave { get; set; } = string.Empty;
}

/// <summary>O <see cref="AppDbContext"/> de verdade mais duas tabelas só da sonda (o EF guarda um modelo por tipo de contexto).</summary>
public sealed class ContextoDaSonda(DbContextOptions<AppDbContext> opcoes) : AppDbContext(opcoes)
{
    public DbSet<LinhaDaSonda> LinhasDaSonda => Set<LinhaDaSonda>();

    public DbSet<ChaveDaSonda> ChavesDaSonda => Set<ChaveDaSonda>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<LinhaDaSonda>(e =>
        {
            e.ToTable("SONDA_RESERVA");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Situacao).HasColumnName("SITUACAO");
        });
        modelBuilder.Entity<ChaveDaSonda>(e =>
        {
            e.ToTable("SONDA_UNICA");
            e.Property(x => x.Id).HasColumnName("ID");
            e.Property(x => x.Chave).HasColumnName("CHAVE");
        });
    }
}

/// <summary>
/// A sonda do passo 0 da Task 10 (premissa não medida da qual a fila de e-mails e o agendador dependem): <c>SELECT ... FOR UPDATE SKIP LOCKED</c> pelo
/// <c>FromSql</c> do EF, DENTRO de <c>ExecutarEmTransacaoAsync</c>, que é a forma que a fila e o agendador vão usar. Fica como teste de regressão: se o
/// EF ou o Pomelo mudarem de jeito e a reserva deixar de travar, é aqui que aparece, e não como duas mensagens enviadas duas vezes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SkipLockedPeloFromSqlTests(MySqlFixture mysql)
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static string? _conexao;

    private async Task<string> BancoAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return _conexao ??= await mysql.NovoBancoComScriptsAsync();
        }
        finally
        {
            Trava.Release();
        }
    }

    private static ContextoDaSonda Novo(string conexao) =>
        new(new DbContextOptionsBuilder<AppDbContext>().UseMySql(conexao, new MySqlServerVersion(new Version(8, 4, 0))).Options);

    /// <summary>Recria as duas tabelas da sonda e põe 10 linhas PENDENTE (ids 1 a 10) na primeira.</summary>
    private static async Task PrepararAsync(string conexao)
    {
        foreach (var sql in new[]
        {
            "DROP TABLE IF EXISTS SONDA_RESERVA",
            "DROP TABLE IF EXISTS SONDA_UNICA",
            "CREATE TABLE SONDA_RESERVA (ID INT AUTO_INCREMENT PRIMARY KEY, SITUACAO VARCHAR(20) NOT NULL)",
            "CREATE TABLE SONDA_UNICA (ID INT AUTO_INCREMENT PRIMARY KEY, CHAVE VARCHAR(50) NOT NULL, CONSTRAINT UQ_SONDA_UNICA_CHAVE UNIQUE (CHAVE))",
            "INSERT INTO SONDA_RESERVA (SITUACAO) VALUES ('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE'),('PENDENTE')",
        })
        {
            await ExecutarAsync(conexao, sql);
        }
    }

    /// <summary>A reserva, exatamente como a fila vai fazer: as n primeiras PENDENTE, na ordem do id, travadas, pulando as que outra transação já travou.</summary>
    private static Task<List<LinhaDaSonda>> ReservarAsync(ContextoDaSonda db, int n) =>
        db.LinhasDaSonda
            .FromSql($"SELECT ID, SITUACAO FROM SONDA_RESERVA WHERE SITUACAO = 'PENDENTE' ORDER BY ID LIMIT {n} FOR UPDATE SKIP LOCKED")
            .ToListAsync();

    private static MySqlException? DoMySql(Exception? erro)
    {
        for (; erro is not null; erro = erro.InnerException)
        {
            if (erro is MySqlException mysql)
            {
                return mysql;
            }
        }

        return null;
    }

    [Fact]
    public async Task Sonda_FromSql_com_FOR_UPDATE_SKIP_LOCKED_roda_e_devolve_as_n_primeiras_linhas_na_ordem()
    {
        var conexao = await BancoAsync();
        await PrepararAsync(conexao);
        await using var db = Novo(conexao);

        var ids = await new UnidadeDeTrabalho(db).ExecutarEmTransacaoAsync(
            async () =>
            {
                var linhas = await ReservarAsync(db, 4);
                foreach (var linha in linhas)
                {
                    linha.Situacao = "EM_ENVIO"; // o UPDATE das linhas lidas, pelo próprio EF, na mesma transação
                }

                await db.SaveChangesAsync();
                return linhas.Select(l => l.Id).ToList();
            },
            CancellationToken.None);

        Assert.Equal([1, 2, 3, 4], ids); // as 4 primeiras, na ordem do id
        Assert.Equal(4, await EscalarAsync(conexao, "SELECT COUNT(*) FROM SONDA_RESERVA WHERE SITUACAO = 'EM_ENVIO'")); // o UPDATE valeu e foi confirmado
        Assert.Equal(6, await EscalarAsync(conexao, "SELECT COUNT(*) FROM SONDA_RESERVA WHERE SITUACAO = 'PENDENTE'"));
    }

    [Fact]
    public async Task Sonda_controle_a_primeira_conexao_de_fato_segura_as_linhas()
    {
        // CONTROLE POSITIVO: com a transação da primeira aberta, o mesmo tipo de SELECT com NOWAIT na segunda conexão FALHA com o erro 3572 (não conseguiu
        // travar na hora). Sem este controle, "a segunda não recebeu as mesmas linhas" admitiria duas explicações: "o SKIP LOCKED funciona" e "a primeira
        // nunca travou nada".
        var conexao = await BancoAsync();
        await PrepararAsync(conexao);
        await using var primeira = Novo(conexao);
        await using var segunda = Novo(conexao);

        await new UnidadeDeTrabalho(primeira).ExecutarEmTransacaoAsync(
            async () =>
            {
                var travadas = await ReservarAsync(primeira, 3);
                Assert.Equal([1, 2, 3], travadas.Select(l => l.Id));

                var erro = await Record.ExceptionAsync(async () =>
                    await new UnidadeDeTrabalho(segunda).ExecutarEmTransacaoAsync(
                        async () => await segunda.Database.SqlQuery<int>($"SELECT ID AS Value FROM SONDA_RESERVA WHERE ID IN (1, 2, 3) FOR UPDATE NOWAIT").ToListAsync(),
                        CancellationToken.None));

                Assert.NotNull(erro);
                Assert.Equal(3572, DoMySql(erro)?.Number);
                return 0;
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task Sonda_duas_conexoes_com_transacao_aberta_recebem_linhas_diferentes()
    {
        // A pega 3 e não confirma; B pega 3 com a MESMA consulta e recebe outras 3, sem esperar nem falhar.
        var conexao = await BancoAsync();
        await PrepararAsync(conexao);
        await using var a = Novo(conexao);
        await using var b = Novo(conexao);

        await new UnidadeDeTrabalho(a).ExecutarEmTransacaoAsync(
            async () =>
            {
                var deA = (await ReservarAsync(a, 3)).Select(l => l.Id).ToList();

                var deB = await new UnidadeDeTrabalho(b)
                    .ExecutarEmTransacaoAsync(async () => (await ReservarAsync(b, 3)).Select(l => l.Id).ToList(), CancellationToken.None)
                    .WaitAsync(TimeSpan.FromSeconds(15)); // se B esperasse a trava de A, estouraria aqui

                Assert.Equal([1, 2, 3], deA);
                Assert.Equal([4, 5, 6], deB);
                return 0;
            },
            CancellationToken.None);
    }

    [Fact]
    public async Task Sonda_1062_dentro_de_transacao_nao_aborta_a_transacao()
    {
        // O INSERT que viola a chave única lança MySqlException 1062; o INSERT seguinte na mesma transação e o COMMIT funcionam. A fila depende disto
        // para enfileirar a mesma chave duas vezes sem perder o resto do que a transação já fez.
        var conexao = await BancoAsync();
        await PrepararAsync(conexao);
        await using var db = Novo(conexao);

        await new UnidadeDeTrabalho(db).ExecutarEmTransacaoAsync(
            async () =>
            {
                db.ChavesDaSonda.Add(new ChaveDaSonda { Chave = "a" });
                await db.SaveChangesAsync();

                var repetida = new ChaveDaSonda { Chave = "a" };
                db.ChavesDaSonda.Add(repetida);
                var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
                Assert.Equal(1062, DoMySql(erro)?.Number);
                db.Entry(repetida).State = EntityState.Detached; // a entidade que falhou sai do contexto, como a fila fará

                db.ChavesDaSonda.Add(new ChaveDaSonda { Chave = "b" });
                await db.SaveChangesAsync(); // a transação continua viva depois do 1062
                return 0;
            },
            CancellationToken.None);

        Assert.Equal(2, await EscalarAsync(conexao, "SELECT COUNT(*) FROM SONDA_UNICA")); // "a" e "b" confirmados pelo COMMIT; a repetida nunca entrou
    }
}

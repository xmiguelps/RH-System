using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Autenticacao;

[Collection(MySqlCollection.Nome)]
public class RefreshTokenRepositorioTests(MySqlFixture mysql)
{
    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);

    private static string HashHex(int numero) => numero.ToString("x").PadLeft(64, '0');

    private static async Task<int> NovoUsuarioAsync(string conexao, string email)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");
        var usuario = Usuario.Criar(Funcionario.Criar("Pessoa", email, new DateOnly(2026, 1, 2), nivel), HashDeTeste);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario.Id;
    }

    private static async Task AdicionarAsync(string conexao, int usuarioId, string hash)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        await new RefreshTokenRepositorio(db).AdicionarAsync(RefreshToken.Criar(usuarioId, hash, Agora, TimeSpan.FromDays(7)), CancellationToken.None);
        await new UnidadeDeTrabalho(db).SalvarAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ObterPorHash_devolve_o_token_guardado_e_null_para_hash_desconhecido()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuarioId = await NovoUsuarioAsync(conexao, "ana@empresa.com.br");
        await AdicionarAsync(conexao, usuarioId, HashHex(1));

        await using var db = ContextoDeTeste.Novo(conexao);
        var repositorio = new RefreshTokenRepositorio(db);

        var achado = await repositorio.ObterPorHashAsync(HashHex(1), CancellationToken.None);

        Assert.NotNull(achado);
        Assert.Equal(usuarioId, achado.UsuarioId);
        Assert.Equal(Agora, achado.CriadoEm);
        Assert.Equal(Agora.AddDays(7), achado.ExpiraEm);
        Assert.Null(achado.RevogadoEm);
        Assert.True(achado.EstaAtivo(Agora.AddDays(1)));
        Assert.Null(await repositorio.ObterPorHashAsync(HashHex(999), CancellationToken.None));
    }

    [Fact]
    public async Task TentarRevogar_revoga_uma_vez_e_depois_devolve_false()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuarioId = await NovoUsuarioAsync(conexao, "ana@empresa.com.br");
        await AdicionarAsync(conexao, usuarioId, HashHex(1));
        await using var db = ContextoDeTeste.Novo(conexao);
        var repositorio = new RefreshTokenRepositorio(db);

        Assert.True(await repositorio.TentarRevogarAsync(HashHex(1), Agora.AddMinutes(5), CancellationToken.None));
        Assert.False(await repositorio.TentarRevogarAsync(HashHex(1), Agora.AddMinutes(6), CancellationToken.None));
        Assert.False(await repositorio.TentarRevogarAsync(HashHex(404), Agora, CancellationToken.None));

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var revogado = await new RefreshTokenRepositorio(leitura).ObterPorHashAsync(HashHex(1), CancellationToken.None);
        Assert.Equal(Agora.AddMinutes(5), revogado!.RevogadoEm); // ficou a data da primeira revogação
    }

    [Fact]
    public async Task RevogarTodosDoUsuario_revoga_so_os_tokens_dele_que_ainda_valem()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var ana = await NovoUsuarioAsync(conexao, "ana@empresa.com.br");
        var bia = await NovoUsuarioAsync(conexao, "bia@empresa.com.br");
        await AdicionarAsync(conexao, ana, HashHex(1));
        await AdicionarAsync(conexao, ana, HashHex(2));
        await AdicionarAsync(conexao, bia, HashHex(3));
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            await new RefreshTokenRepositorio(db).TentarRevogarAsync(HashHex(1), Agora.AddMinutes(1), CancellationToken.None);
            await new RefreshTokenRepositorio(db).RevogarTodosDoUsuarioAsync(ana, Agora.AddMinutes(9), CancellationToken.None);
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var repositorio = new RefreshTokenRepositorio(leitura);
        Assert.Equal(Agora.AddMinutes(1), (await repositorio.ObterPorHashAsync(HashHex(1), CancellationToken.None))!.RevogadoEm); // já revogado: mantém a data
        Assert.Equal(Agora.AddMinutes(9), (await repositorio.ObterPorHashAsync(HashHex(2), CancellationToken.None))!.RevogadoEm);
        Assert.Null((await repositorio.ObterPorHashAsync(HashHex(3), CancellationToken.None))!.RevogadoEm); // o token da Bia segue valendo
    }

    [Fact]
    public async Task TentarRevogar_com_20_chamadas_simultaneas_devolve_true_para_exatamente_uma() // Review Focus 3
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuarioId = await NovoUsuarioAsync(conexao, "ana@empresa.com.br");

        for (var rodada = 1; rodada <= 5; rodada++)
        {
            var hash = HashHex(rodada);
            await AdicionarAsync(conexao, usuarioId, hash);
            using var barreira = new Barrier(20);

            // 20 tarefas, cada uma com o seu DbContext e a sua conexão, liberadas juntas. Threads dedicadas: a barreira bloqueia 20 threads
            // de uma vez e o pool as criaria devagar demais.
            var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Factory.StartNew(
                async () =>
                {
                    await using var db = ContextoDeTeste.Novo(conexao);
                    var repositorio = new RefreshTokenRepositorio(db);
                    barreira.SignalAndWait();
                    return await repositorio.TentarRevogarAsync(hash, Agora.AddMinutes(rodada), CancellationToken.None);
                },
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default).Unwrap()).ToArray();

            var resultados = await Task.WhenAll(tarefas);

            Assert.Equal(1, resultados.Count(r => r));
            await using var leitura = ContextoDeTeste.Novo(conexao);
            Assert.NotNull((await new RefreshTokenRepositorio(leitura).ObterPorHashAsync(hash, CancellationToken.None))!.RevogadoEm);
        }
    }

    [Fact]
    public async Task UnidadeDeTrabalho_desfaz_tudo_se_a_acao_da_transacao_falha_e_confirma_se_termina()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuarioId = await NovoUsuarioAsync(conexao, "ana@empresa.com.br");
        await AdicionarAsync(conexao, usuarioId, HashHex(1));

        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var repositorio = new RefreshTokenRepositorio(db);
            var unidade = new UnidadeDeTrabalho(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => unidade.ExecutarEmTransacaoAsync<bool>(
                async () =>
                {
                    await repositorio.TentarRevogarAsync(HashHex(1), Agora, CancellationToken.None);
                    await repositorio.AdicionarAsync(RefreshToken.Criar(usuarioId, HashHex(2), Agora, TimeSpan.FromDays(7)), CancellationToken.None);
                    await unidade.SalvarAsync(CancellationToken.None);
                    throw new InvalidOperationException("falha no meio da rotação");
                },
                CancellationToken.None));
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var repositorio = new RefreshTokenRepositorio(leitura);
            Assert.Null((await repositorio.ObterPorHashAsync(HashHex(1), CancellationToken.None))!.RevogadoEm); // a revogação foi desfeita
            Assert.Null(await repositorio.ObterPorHashAsync(HashHex(2), CancellationToken.None)); // e o token novo não ficou
        }

        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var repositorio = new RefreshTokenRepositorio(db);
            var unidade = new UnidadeDeTrabalho(db);
            var resultado = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var revogou = await repositorio.TentarRevogarAsync(HashHex(1), Agora, CancellationToken.None);
                    await repositorio.AdicionarAsync(RefreshToken.Criar(usuarioId, HashHex(2), Agora, TimeSpan.FromDays(7)), CancellationToken.None);
                    await unidade.SalvarAsync(CancellationToken.None);
                    return revogou;
                },
                CancellationToken.None);
            Assert.True(resultado);
        }

        await using var depois = ContextoDeTeste.Novo(conexao);
        var confirmado = new RefreshTokenRepositorio(depois);
        Assert.NotNull((await confirmado.ObterPorHashAsync(HashHex(1), CancellationToken.None))!.RevogadoEm);
        Assert.NotNull(await confirmado.ObterPorHashAsync(HashHex(2), CancellationToken.None));
    }
}

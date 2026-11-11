using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// A auditoria como o contêiner de dependências a monta (o que a API e o seed da inicialização usam). Regressão de um defeito medido: o EF Core 9
/// ACUMULA a configuração de cada <c>AddDbContext</c>/<c>AddDbContextFactory</c>, então passar a mesma função aos dois registrava o MESMO interceptor duas
/// vezes; o EF o chamava duas vezes por <c>SaveChanges</c>, a transação própria ficava aberta sem confirmar e o seed do administrador "salvava" sem gravar nada.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaNoContainerTests(MySqlFixture mysql)
{
    private static async Task<long> ContarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }

    [Fact]
    public async Task O_interceptor_esta_nas_opcoes_do_container_uma_vez_so()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        using var fabrica = new ApiFactory(conexao);
        _ = fabrica.CreateClient();
        await using var escopo = fabrica.Services.CreateAsyncScope();
        var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();

        var interceptores = ((IInfrastructure<IServiceProvider>)db).Instance
            .GetRequiredService<IDbContextOptions>().Extensions.OfType<CoreOptionsExtension>()
            .SelectMany(e => e.Interceptors ?? [])
            .OfType<AuditoriaInterceptor>()
            .ToList();

        Assert.Single(interceptores);
    }

    [Fact]
    public async Task Contexto_do_container_grava_a_alteracao_e_o_evento_juntos_e_nao_deixa_transacao_aberta()
    {
        // O caminho do seed do administrador: escopo do contêiner, sem HttpContext (ator Sistema), Funcionario e Usuario no mesmo SaveChanges.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        using var fabrica = new ApiFactory(conexao);
        _ = fabrica.CreateClient();
        int funcionarioId;
        await using (var escopo = fabrica.Services.CreateAsyncScope())
        {
            var db = escopo.ServiceProvider.GetRequiredService<AppDbContext>();
            var nivel = await db.NiveisDeAcesso.FirstAsync(n => n.Nome == "RH");
            var funcionario = Funcionario.Criar("Pessoa do Contêiner", "pessoa.do.conteiner@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
            db.Usuarios.Add(Usuario.Criar(funcionario, new string('x', 60)));

            await db.SaveChangesAsync();

            Assert.Null(db.Database.CurrentTransaction); // nenhuma transação deixada aberta pelo interceptor
            funcionarioId = funcionario.Id;
            // Visto por OUTRA conexão, ainda com o escopo vivo: se a transação não foi confirmada, nada aparece.
            Assert.Equal(1, await ContarAsync(conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}"));
        }

        Assert.Equal(1, await ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = " + funcionarioId));
        var evento = await ContarAsync(
            conexao,
            $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'FUNCIONARIOS' AND ID_REGISTRO = {funcionarioId} AND ACAO = 'CRIOU' AND ATOR_TIPO = 'SISTEMA' AND CANAL = 'SISTEMA'");
        Assert.Equal(1, evento);
    }
}

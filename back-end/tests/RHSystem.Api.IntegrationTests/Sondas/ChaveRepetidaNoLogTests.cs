using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Agendamento;
using RHSystem.Api.IntegrationTests.Emails;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Comum;
using RHSystem.Application.Emails;

namespace RHSystem.Api.IntegrationTests.Sondas;

/// <summary>
/// Chave de idempotência repetida (erro <b>1062</b>, "Duplicate entry '&lt;valor&gt;' for key ...") vale como sucesso e é engolida pelo código; mas o EF Core registra a falha de gravação
/// por conta própria, e a mensagem da exceção traz o VALOR da chave. Se a chave tiver dado pessoal (na 1b, CPF ou e-mail; no lembrete anônimo, a pessoa), o log o guardaria. Aqui o
/// log da API inteira (todas as categorias, inclusive <c>Microsoft.EntityFrameworkCore</c>) vai para um coletor, e a marca que vai na chave nunca pode aparecer nele.
/// Cada teste tem um controle positivo: o coletor TEM de ter visto o SQL da tabela (senão o teste não prova nada).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ChaveRepetidaNoLogTests(MySqlFixture mysql)
{
    private const string Marca = "marca-secreta-4815162342";

    private static WebApplicationFactory<Program> ComColetor(ApiFactory api, ColetorDeLogs coletor) =>
        api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos => servicos.AddLogging(log => log.AddProvider(coletor))));

    private static EmailParaEnfileirar Email(string destinatario = "pessoa@empresa.com.br") =>
        new("TESTE_SIGILOSO", ChaveDeIdempotencia.Montar("TESTE_SIGILOSO", ("pessoa", Marca)), destinatario, new { Nome = "Ana" }, null, null, true, null);

    [Fact]
    public async Task Chave_repetida_na_fila_de_emails_nao_deixa_o_valor_da_chave_em_log_nenhum()
    {
        var coletor = new ColetorDeLogs();
        var api = ComColetor(await ApiDeEmails.ComecarAsync(mysql), coletor);
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            Assert.True(await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarAsync(Email(), CancellationToken.None));
        }

        await using (var escopo = api.Services.CreateAsyncScope())
        {
            Assert.False(await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarAsync(Email(), CancellationToken.None)); // a chave já existe: o 1062 aconteceu
        }

        Assert.Contains(coletor.Mensagens, m => m.Contains("FILA_EMAILS")); // controle positivo: o coletor vê o SQL
        Assert.DoesNotContain(coletor.Mensagens, m => m.Contains(Marca));
    }

    [Fact]
    public async Task Chave_repetida_no_lote_de_emails_nao_deixa_o_valor_da_chave_em_log_nenhum()
    {
        var coletor = new ColetorDeLogs();
        var api = ComColetor(await ApiDeEmails.ComecarAsync(mysql), coletor);
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            Assert.Equal(1, await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarLoteAsync([Email()], CancellationToken.None));
        }

        await using (var escopo = api.Services.CreateAsyncScope())
        {
            var outra = new EmailParaEnfileirar("TESTE_ENVIO", ChaveDeIdempotencia.Montar("TESTE_ENVIO", ("n", 1)), "outra@empresa.com.br", new { Nome = "Bia" }, null, null, false, null);
            Assert.Equal(1, await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarLoteAsync([Email(), outra], CancellationToken.None)); // só a nova entra; a repetida é o 1062
        }

        Assert.Contains(coletor.Mensagens, m => m.Contains("FILA_EMAILS"));
        Assert.DoesNotContain(coletor.Mensagens, m => m.Contains(Marca));
    }

    [Fact]
    public async Task Chave_repetida_no_agendador_nao_deixa_o_valor_da_chave_em_log_nenhum()
    {
        var coletor = new ColetorDeLogs();
        var api = ComColetor(await ApiDeAgendamento.ComecarAsync(mysql), coletor);
        var tarefa = new NovaTarefa("TESTE_TAREFA", ChaveDeIdempotencia.Montar("TESTE_TAREFA", ("pessoa", Marca)), ElencoDeTeste.Hoje);
        await using (var escopo = api.Services.CreateAsyncScope())
        {
            Assert.True(await escopo.ServiceProvider.GetRequiredService<IAgendador>().AgendarAsync(tarefa, CancellationToken.None));
        }

        await using (var escopo = api.Services.CreateAsyncScope())
        {
            Assert.False(await escopo.ServiceProvider.GetRequiredService<IAgendador>().AgendarAsync(tarefa, CancellationToken.None)); // o 1062 aconteceu
        }

        Assert.Contains(coletor.Mensagens, m => m.Contains("TAREFAS_AGENDADAS"));
        Assert.DoesNotContain(coletor.Mensagens, m => m.Contains(Marca));
    }
}

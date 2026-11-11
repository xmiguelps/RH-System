using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Application.Emails;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Emails;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Emails;

/// <summary>A fila de e-mails no banco REAL (Task 10). O relógio é o falso, em 07/10/2026 12:42 UTC: as datas gravadas vêm dele, nunca da hora do banco.</summary>
[Collection(MySqlCollection.Nome)]
public class FilaDeEmailsTests(MySqlFixture mysql)
{
    private static readonly DateTime Hoje = ElencoDeTeste.Hoje;

    private static string Chave(int n, string tipo = "TESTE_ENVIO") => ChaveDeIdempotencia.Montar(tipo, ("n", n));

    private static EmailParaEnfileirar Email(
        string chave,
        string tipo = "TESTE_ENVIO",
        string para = "ana@empresa.com.br",
        object? carga = null,
        int? funcionarioId = null,
        int? referenciaId = null,
        bool sigiloso = false,
        DateTime? enviarApos = null) =>
        new(tipo, chave, para, carga, funcionarioId, referenciaId, sigiloso, enviarApos);

    private static async Task<List<EmailNaFila>> LerAsync(string conexao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        return await db.EmailsNaFila.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
    }

    [Fact]
    public async Task Enfileirar_grava_PENDENTE_com_carga_json_e_datas_do_relogio()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);

        Assert.True(await fila.EnfileirarAsync(Email(Chave(1), carga: new { Nome = "Ana", Idade = 3 }), CancellationToken.None));
        Assert.True(await fila.EnfileirarAsync(Email(Chave(2), enviarApos: Hoje.AddHours(2)), CancellationToken.None));

        var linhas = await LerAsync(api.Conexao);
        var primeira = Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(1));
        Assert.Equal((SituacaoDoEmail.Pendente, 0, Hoje, Hoje, (DateTime?)null), (primeira.Situacao, primeira.Tentativas, primeira.CriadoEm, primeira.ProximaTentativaEm, primeira.ReservadoAte));
        using var carga = JsonDocument.Parse(primeira.Carga!);
        Assert.Equal(("Ana", 3), (carga.RootElement.GetProperty("nome").GetString(), carga.RootElement.GetProperty("idade").GetInt32())); // camelCase
        var segunda = Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(2));
        Assert.Equal((Hoje, Hoje.AddHours(2)), (segunda.CriadoEm, segunda.ProximaTentativaEm)); // EnviarApos adia a tentativa, não a criação
        Assert.Null(segunda.Carga);
    }

    [Fact]
    public async Task Enfileirar_a_mesma_chave_duas_vezes_devolve_false_e_grava_uma_linha()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);

        var primeira = await fila.EnfileirarAsync(Email(Chave(1)), CancellationToken.None);
        var segunda = await fila.EnfileirarAsync(Email(Chave(1)), CancellationToken.None);

        Assert.True(primeira);
        Assert.False(segunda); // a chave já existe: vale como sucesso, nunca lança
        Assert.Single(await LerAsync(api.Conexao));
    }

    [Fact]
    public async Task Enfileirar_a_mesma_chave_em_20_chamadas_simultaneas_grava_uma_linha_e_nunca_lanca()
    {
        // Review Focus 4: 20 tarefas, cada uma com DbContext e conexão próprios, largando juntas (Barrier).
        var api = await ApiDeEmails.ComecarAsync(mysql);
        using var barreira = new Barrier(20);
        var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Factory.StartNew(
            async () =>
            {
                await using var db = ContextoDeTeste.Novo(api.Conexao);
                var fila = new FilaDeEmails(db, api.Relogio);
                barreira.SignalAndWait();
                return await fila.EnfileirarAsync(Email(Chave(7)), CancellationToken.None);
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap()).ToArray();

        var resultados = await Task.WhenAll(tarefas); // se alguma lançasse (1062 ou deadlock), o WhenAll lançaria aqui

        Assert.Equal((1, 19), (resultados.Count(r => r), resultados.Count(r => !r)));
        Assert.Single(await LerAsync(api.Conexao));
    }

    [Fact]
    public async Task Lote_com_chave_repetida_no_meio_grava_as_outras_e_conta_so_as_novas()
    {
        // Depois do 1062 a entidade que falhou tem de sair do contexto: se ficasse, o SaveChanges da PRÓXIMA mensagem tentaria gravá-la de novo e lançaria.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);

        var entraram = await fila.EnfileirarLoteAsync([Email(Chave(1)), Email(Chave(1)), Email(Chave(2)), Email(Chave(3))], CancellationToken.None);

        Assert.Equal(3, entraram); // a repetida não conta
        Assert.Equal(3, (await LerAsync(api.Conexao)).Count);
    }

    [Fact]
    public async Task Enfileirar_com_mudancas_pendentes_no_contexto_e_chave_repetida_nao_perde_nem_duplica_as_mudancas()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);
        Assert.True(await fila.EnfileirarAsync(Email(Chave(100)), CancellationToken.None));
        db.EmailsNaFila.Add(EmailNaFila.Criar("TESTE_ENVIO", Chave(101), "pendente@empresa.com.br", Hoje)); // uma mudança que o chamador deixou pendente no contexto

        var repetida = await fila.EnfileirarAsync(Email(Chave(100)), CancellationToken.None);

        Assert.False(repetida);
        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(2, linhas.Count); // a chave repetida não entrou de novo...
        Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(101)); // ...e a mudança pendente foi gravada (uma vez), não perdida pelo 1062
    }

    [Fact]
    public async Task Enfileirar_dentro_de_transacao_que_desfaz_nao_deixa_linha()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new UnidadeDeTrabalho(db).ExecutarEmTransacaoAsync<int>(
            async () =>
            {
                await fila.EnfileirarAsync(Email(Chave(1)), CancellationToken.None);
                throw new InvalidOperationException("o fato de negócio falhou depois de enfileirar");
            },
            CancellationToken.None));

        Assert.Empty(await LerAsync(api.Conexao)); // a mensagem estava na mesma transação do fato: some junto
    }

    [Fact]
    public async Task Enfileirar_sigiloso_com_funcionario_lanca_ArgumentException_e_o_lote_sigiloso_nao_grava_FuncionarioId()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);

        await Assert.ThrowsAsync<ArgumentException>(() => fila.EnfileirarAsync(Email(Chave(1, "TESTE_SIGILOSO"), "TESTE_SIGILOSO", sigiloso: true, funcionarioId: 7), CancellationToken.None));
        var lote = Enumerable.Range(1, 6).Select(n => Email(Chave(n, "TESTE_SIGILOSO"), "TESTE_SIGILOSO", para: $"pessoa{n}@empresa.com.br", sigiloso: true)).ToList();
        var entraram = await fila.EnfileirarLoteAsync(lote, CancellationToken.None);

        Assert.Equal(6, entraram);
        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(6, linhas.Count);
        Assert.All(linhas, l => Assert.Equal((true, (int?)null), (l.Sigiloso, l.FuncionarioId))); // nada liga a mensagem a uma pessoa
    }

    [Fact]
    public async Task Enfileirar_com_tipo_acima_de_40_caracteres_lanca_ArgumentException_antes_do_banco()
    {
        // FILA_EMAILS.TIPO é de 3 a 40; ChaveDeIdempotencia aceita até 50 por causa do agendador. O erro aparece aqui, não no CHECK do banco.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);
        var tipoLongo = "T" + new string('A', 40); // 41 caracteres
        var chave = ChaveDeIdempotencia.Montar(tipoLongo, ("n", 1)); // a chave aceita (até 50)

        await Assert.ThrowsAsync<ArgumentException>(() => fila.EnfileirarAsync(Email(chave, tipoLongo), CancellationToken.None));

        Assert.Empty(await LerAsync(api.Conexao));
    }

    [Fact]
    public async Task CancelarPendentes_marca_CANCELADO_e_apaga_a_carga_das_nao_sigilosas_e_APAGA_as_sigilosas()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var fila = new FilaDeEmails(db, api.Relogio);
        await fila.EnfileirarAsync(Email(Chave(1), referenciaId: 5, carga: new { Nome = "A" }), CancellationToken.None); // vai cancelar
        await fila.EnfileirarAsync(Email(ChaveDeIdempotencia.Montar("TESTE_ENVIO", ("s", 2)), referenciaId: 5, sigiloso: true, carga: new { Nome = "B" }), CancellationToken.None); // sigilosa: vai ser APAGADA
        await fila.EnfileirarAsync(Email(Chave(3), referenciaId: 6), CancellationToken.None); // outra referência: fica
        await fila.EnfileirarAsync(Email(Chave(4, "TESTE_OUTRO"), "TESTE_OUTRO", referenciaId: 5), CancellationToken.None); // outro tipo: fica
        await fila.EnfileirarAsync(Email(Chave(5), referenciaId: 5), CancellationToken.None); // já enviada: fica
        await ExecutarAsync(api.Conexao, $"UPDATE FILA_EMAILS SET SITUACAO = 'ENVIADO', CARGA = NULL, ENVIADO_EM = '2026-10-07 12:00:00' WHERE CHAVE_IDEMPOTENCIA = '{Chave(5)}'");

        var quantas = await fila.CancelarPendentesAsync("TESTE_ENVIO", 5, CancellationToken.None);

        Assert.Equal(2, quantas); // a soma das duas: 1 cancelada + 1 apagada
        var linhas = await LerAsync(api.Conexao);
        var cancelada = Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(1));
        Assert.Equal((SituacaoDoEmail.Cancelado, (string?)null), (cancelada.Situacao, cancelada.Carga));
        Assert.DoesNotContain(linhas, l => l.Sigiloso); // a sigilosa não virou CANCELADO (o CHECK do V001 a recusaria): foi apagada
        Assert.Equal(SituacaoDoEmail.Pendente, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(3)).Situacao);
        Assert.Equal(SituacaoDoEmail.Pendente, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(4, "TESTE_OUTRO")).Situacao);
        Assert.Equal(SituacaoDoEmail.Enviado, Assert.Single(linhas, l => l.ChaveDeIdempotencia == Chave(5)).Situacao);
    }
}

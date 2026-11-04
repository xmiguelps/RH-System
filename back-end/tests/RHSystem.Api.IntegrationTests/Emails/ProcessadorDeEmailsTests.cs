using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Emails;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Emails;

/// <summary>
/// Um ciclo do envio no banco REAL, com o enviador em memória e o relógio falso (07/10/2026 12:42 UTC). Cada ciclo roda num escopo novo, como o serviço em segundo
/// plano faz. Os testes de log montam o processador com um coletor de log.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ProcessadorDeEmailsTests(MySqlFixture mysql)
{
    private static readonly DateTime Hoje = ElencoDeTeste.Hoje;

    private static EmailParaEnfileirar Email(int n, string tipo = "TESTE_ENVIO", string? para = null, string nome = "Ana", bool sigiloso = false, DateTime? enviarApos = null) =>
        new(tipo, ChaveDeIdempotencia.Montar(tipo, ("n", n)), para ?? $"pessoa{n}@empresa.com.br", new { Nome = nome }, null, null, sigiloso, enviarApos);

    private static async Task EnfileirarAsync(ApiFactory api, params EmailParaEnfileirar[] emails)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<IFilaDeEmails>().EnfileirarLoteAsync(emails, CancellationToken.None);
    }

    /// <summary>Um ciclo, num escopo novo. Com <paramref name="coletor"/>, o log do processador vai para ele.</summary>
    private static async Task<int> CicloAsync(ApiFactory api, ColetorDeLogs? coletor = null)
    {
        await using var escopo = api.Services.CreateAsyncScope();
        var processador = coletor is null
            ? escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>()
            : ActivatorUtilities.CreateInstance<ProcessadorDeEmails>(escopo.ServiceProvider, LoggerFactory.Create(b => b.AddProvider(coletor)).CreateLogger<ProcessadorDeEmails>());
        return await processador.ProcessarCicloAsync(CancellationToken.None);
    }

    private static async Task<List<EmailNaFila>> LerAsync(string conexao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        return await db.EmailsNaFila.AsNoTracking().OrderBy(e => e.Id).ToListAsync();
    }

    private static async Task<List<EventoDeAuditoria>> EventosComAsync(string conexao, string descricao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        return await db.EventosDeAuditoria.AsNoTracking().Where(e => e.Descricao == descricao).ToListAsync();
    }

    [Fact]
    public async Task Ciclo_envia_no_maximo_LoteMaximoPorCiclo_e_500_convites_saem_em_25_ciclos_cada_um_uma_vez()
    {
        // Review Focus 4: 500 mensagens, 20 por ciclo.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await EnfileirarAsync(api, [.. Enumerable.Range(1, 500).Select(n => Email(n))]);

        var primeiro = await CicloAsync(api);
        Assert.Equal((20, 20), (primeiro, api.Enviador.Enviadas.Count)); // no máximo o lote do ciclo
        var tratadas = new List<int> { primeiro };
        for (var i = 1; i < 25; i++)
        {
            tratadas.Add(await CicloAsync(api));
        }

        Assert.All(tratadas, n => Assert.Equal(20, n));
        Assert.Equal(0, await CicloAsync(api)); // o 26º ciclo não encontra nada
        var enviadas = api.Enviador.Enviadas;
        Assert.Equal(500, enviadas.Count);
        Assert.Equal(500, enviadas.Select(m => m.Para).Distinct().Count()); // cada uma uma vez só
        Assert.All(await LerAsync(api.Conexao), l => Assert.Equal(SituacaoDoEmail.Enviado, l.Situacao));
    }

    [Fact]
    public async Task Mensagem_enviada_vira_ENVIADO_com_CARGA_nula_ENVIADO_EM_e_reserva_limpa()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await EnfileirarAsync(api, Email(1, nome: "Ana <b>&"));

        Assert.Equal(1, await CicloAsync(api));

        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDoEmail.Enviado, Hoje, (string?)null, (DateTime?)null, (string?)null, 0), (linha.Situacao, linha.EnviadoEm, linha.Carga, linha.ReservadoAte, linha.UltimoErro, linha.Tentativas));
        var enviada = Assert.Single(api.Enviador.Enviadas);
        Assert.Equal(("pessoa1@empresa.com.br", "Olá, Ana <b>&", "<p>Ana &lt;b&gt;&amp;</p>"), (enviada.Para, enviada.Assunto, enviada.Html)); // o HTML sai codificado
    }

    [Fact]
    public async Task Mensagem_nao_sigilosa_enviada_grava_ENVIOU_EMAIL_com_ator_SISTEMA_e_sigilosa_nao_grava_evento()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        var antesNormal = (await EventosComAsync(api.Conexao, "Aviso de teste enviado")).Count;
        var antesSigilosa = (await EventosComAsync(api.Conexao, "Lembrete anonimo de teste enviado")).Count;
        await EnfileirarAsync(api, Email(1), Email(2, "TESTE_SIGILOSO", sigiloso: true));

        Assert.Equal(2, await CicloAsync(api));

        var normais = await EventosComAsync(api.Conexao, "Aviso de teste enviado");
        Assert.Equal(antesNormal + 1, normais.Count);
        var evento = normais[^1];
        Assert.Equal((AuditoriaAcao.EnviouEmail, AuditoriaArea.Avisos, TipoDeAtor.Sistema, CanalDeOrigem.Sistema, (int?)null, (string?)null, "Aviso de teste"), (evento.Acao, evento.Area, evento.AtorTipo, evento.Canal, evento.FuncionarioAtorId, evento.Ip, evento.RegistroAfetado));
        Assert.Equal(antesSigilosa, (await EventosComAsync(api.Conexao, "Lembrete anonimo de teste enviado")).Count); // a sigilosa saiu e não deixou evento
        Assert.Equal(2, api.Enviador.Enviadas.Count);
    }

    [Fact]
    public async Task Falha_temporaria_reagenda_com_espera_crescente_e_ao_chegar_no_maximo_vira_FALHOU_com_carga_nula()
    {
        // 1, 5, 30 e 120 minutos; a 5ª falha (MaximoDeTentativas) é a última. A mensagem volta a PENDENTE com RESERVADO_ATE nulo a cada falha: o CHECK de reserva nunca é violado.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        api.Enviador.Comportamento = _ => new FalhaDeEnvioException("temporaria", permanente: false, new TimeoutException(), "451");
        await EnfileirarAsync(api, Email(1));

        async Task<EmailNaFila> UmaTentativaAsync(int tratadasEsperadas)
        {
            Assert.Equal(tratadasEsperadas, await CicloAsync(api));
            return Assert.Single(await LerAsync(api.Conexao));
        }

        var t1 = await UmaTentativaAsync(1);
        Assert.Equal((SituacaoDoEmail.Pendente, 1, Hoje.AddMinutes(1), (DateTime?)null), (t1.Situacao, t1.Tentativas, t1.ProximaTentativaEm, t1.ReservadoAte));
        Assert.NotNull(t1.Carga); // ainda espera: a carga fica
        await UmaTentativaAsync(0); // sem avançar o relógio, nada sai antes da hora

        api.Relogio.Avancar(TimeSpan.FromMinutes(1));
        var t2 = await UmaTentativaAsync(1);
        Assert.Equal((2, Hoje.AddMinutes(1 + 5)), (t2.Tentativas, t2.ProximaTentativaEm));

        api.Relogio.Avancar(TimeSpan.FromMinutes(5));
        var t3 = await UmaTentativaAsync(1);
        Assert.Equal((3, Hoje.AddMinutes(1 + 5 + 30)), (t3.Tentativas, t3.ProximaTentativaEm));

        api.Relogio.Avancar(TimeSpan.FromMinutes(30));
        var t4 = await UmaTentativaAsync(1);
        Assert.Equal((4, Hoje.AddMinutes(1 + 5 + 30 + 120)), (t4.Tentativas, t4.ProximaTentativaEm));

        api.Relogio.Avancar(TimeSpan.FromMinutes(120));
        var t5 = await UmaTentativaAsync(1);
        Assert.Equal((SituacaoDoEmail.Falhou, 5, (string?)null, (DateTime?)null, "FalhaDeEnvioException:TimeoutException 451"), (t5.Situacao, t5.Tentativas, t5.Carga, t5.ReservadoAte, t5.UltimoErro));
        Assert.Empty(api.Enviador.Enviadas); // nunca saiu
        api.Relogio.Avancar(TimeSpan.FromDays(1));
        await UmaTentativaAsync(0); // FALHOU não volta
    }

    [Fact]
    public async Task Falha_permanente_vira_FALHOU_na_hora_e_UltimoErro_nao_tem_o_endereco_nem_a_mensagem_da_excecao()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        api.Enviador.Comportamento = m => new FalhaDeEnvioException($"550 5.1.1 {m.Para} nao existe", permanente: true, new InvalidOperationException($"caixa {m.Para}"), "550");
        await EnfileirarAsync(api, Email(1, para: "quem.nao.existe@empresa.com.br"));

        Assert.Equal(1, await CicloAsync(api));

        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDoEmail.Falhou, 1, (string?)null, (DateTime?)null), (linha.Situacao, linha.Tentativas, linha.Carga, linha.ReservadoAte));
        Assert.Equal("FalhaDeEnvioException:InvalidOperationException 550", linha.UltimoErro); // só os NOMES das exceções e o status
        Assert.DoesNotContain("@", linha.UltimoErro);
        Assert.DoesNotContain("nao existe", linha.UltimoErro, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Reserva_vencida_e_retomada_e_reserva_valida_nao_e_tocada()
    {
        // Review Focus 4: um processo que caiu deixou a mensagem EM_ENVIO. Se a reserva venceu, outro a retoma; se ainda vale, ninguém a toca.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        const string colunas = "TIPO, CHAVE_IDEMPOTENCIA, DESTINATARIO_EMAIL, CARGA, SITUACAO, PROXIMA_TENTATIVA_EM, RESERVADO_ATE, CRIADO_EM";
        await ExecutarAsync(api.Conexao, $"INSERT INTO FILA_EMAILS ({colunas}) VALUES ('TESTE_ENVIO', 'TESTE_ENVIO:n=1', 'vencida@empresa.com.br', '{{\"nome\":\"Vencida\"}}', 'EM_ENVIO', '2026-10-07 12:00:00', '2026-10-07 12:41:00', '2026-10-07 12:00:00')");
        await ExecutarAsync(api.Conexao, $"INSERT INTO FILA_EMAILS ({colunas}) VALUES ('TESTE_ENVIO', 'TESTE_ENVIO:n=2', 'valida@empresa.com.br', '{{\"nome\":\"Valida\"}}', 'EM_ENVIO', '2026-10-07 12:00:00', '2026-10-07 12:45:00', '2026-10-07 12:00:00')");

        Assert.Equal(1, await CicloAsync(api));

        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(SituacaoDoEmail.Enviado, Assert.Single(linhas, l => l.DestinatarioEmail == "vencida@empresa.com.br").Situacao);
        var valida = Assert.Single(linhas, l => l.DestinatarioEmail == "valida@empresa.com.br");
        Assert.Equal((SituacaoDoEmail.EmEnvio, new DateTime(2026, 10, 7, 12, 45, 0, DateTimeKind.Utc)), (valida.Situacao, valida.ReservadoAte)); // intocada
        Assert.Equal(["vencida@empresa.com.br"], api.Enviador.Enviadas.Select(m => m.Para));
    }

    [Fact]
    public async Task Dois_processadores_simultaneos_nunca_pegam_a_mesma_mensagem()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await EnfileirarAsync(api, [.. Enumerable.Range(1, 100).Select(n => Email(n))]);
        api.Enviador.Pausa = TimeSpan.FromMilliseconds(15); // para os dois se sobreporem de verdade no tempo
        using var barreira = new Barrier(2);

        Task Processador() => Task.Factory.StartNew(
            async () =>
            {
                barreira.SignalAndWait();
                while (await CicloAsync(api) > 0)
                {
                }
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();

        await Task.WhenAll(Processador(), Processador());

        var enviadas = api.Enviador.Enviadas;
        Assert.Equal(100, enviadas.Count); // todas saíram...
        Assert.Equal(100, enviadas.Select(m => m.Para).Distinct().Count()); // ...e nenhuma duas vezes
        Assert.All(await LerAsync(api.Conexao), l => Assert.Equal(SituacaoDoEmail.Enviado, l.Situacao));
    }

    [Fact]
    public async Task Mensagem_com_PROXIMA_TENTATIVA_no_futuro_nao_sai_antes_da_hora()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await EnfileirarAsync(api, Email(1, enviarApos: Hoje.AddMinutes(10)));

        Assert.Equal(0, await CicloAsync(api));
        api.Relogio.Avancar(TimeSpan.FromMinutes(9));
        Assert.Equal(0, await CicloAsync(api));
        Assert.Empty(api.Enviador.Enviadas);

        api.Relogio.Avancar(TimeSpan.FromMinutes(1));
        Assert.Equal(1, await CicloAsync(api));
        Assert.Single(api.Enviador.Enviadas);
    }

    [Fact]
    public async Task Tipo_sem_renderizador_vira_FALHOU_com_erro_claro()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        await EnfileirarAsync(api, Email(1, "TESTE_SEM_RENDER"));

        Assert.Equal(1, await CicloAsync(api));

        var linha = Assert.Single(await LerAsync(api.Conexao));
        Assert.Equal((SituacaoDoEmail.Falhou, "SemRenderizador:TESTE_SEM_RENDER", (string?)null), (linha.Situacao, linha.UltimoErro, linha.Carga));
        Assert.Empty(api.Enviador.Enviadas);
    }

    [Fact]
    public async Task Sigiloso_enviado_ou_desistido_apaga_a_linha_e_nada_do_endereco_aparece_no_log_nem_na_auditoria()
    {
        // Review Focus 4: o endereço da mensagem sigilosa só existe enquanto ela espera; depois de enviar OU de desistir, a linha some.
        var api = await ApiDeEmails.ComecarAsync(mysql);
        var coletor = new ColetorDeLogs();
        api.Enviador.Comportamento = m => m.Para.StartsWith("falha.", StringComparison.Ordinal) ? new FalhaDeEnvioException($"550 {m.Para}", permanente: true, null, "550") : null;
        await EnfileirarAsync(api, Email(1, "TESTE_SIGILOSO", para: "sigilo.enviado@empresa.com.br", sigiloso: true), Email(2, "TESTE_SIGILOSO", para: "falha.sigilo@empresa.com.br", sigiloso: true));

        Assert.Equal(2, await CicloAsync(api, coletor));

        Assert.Empty(await LerAsync(api.Conexao)); // as duas linhas sumiram: a enviada e a que desistiu
        Assert.Equal(["sigilo.enviado@empresa.com.br"], api.Enviador.Enviadas.Select(m => m.Para));
        string[] enderecos = ["sigilo.enviado@empresa.com.br", "falha.sigilo@empresa.com.br"];
        lock (coletor.Mensagens)
        {
            Assert.All(enderecos, e => Assert.DoesNotContain(coletor.Mensagens, m => m.Contains(e, StringComparison.OrdinalIgnoreCase)));
        }

        var auditoria = (await TudoDoLogAsync(api.Conexao)).Texto;
        Assert.All(enderecos, e => Assert.DoesNotContain(e, auditoria, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Log_do_processador_nunca_contem_o_endereco_de_nenhuma_mensagem()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        var coletor = new ColetorDeLogs();
        api.Enviador.Comportamento = m => m.Para switch
        {
            var p when p.StartsWith("temp.", StringComparison.Ordinal) => new FalhaDeEnvioException($"451 {p}", permanente: false, new TimeoutException(p), "451"),
            var p when p.StartsWith("perm.", StringComparison.Ordinal) => new FalhaDeEnvioException($"550 {p}", permanente: true, new InvalidOperationException(p), "550"),
            var p when p.StartsWith("boom.", StringComparison.Ordinal) => new InvalidOperationException($"explodiu com {p}"),
            _ => null,
        };
        string[] enderecos = ["ok@empresa.com.br", "temp.um@empresa.com.br", "perm.um@empresa.com.br", "boom.um@empresa.com.br", "sem.render@empresa.com.br"];
        await EnfileirarAsync(
            api,
            Email(1, para: enderecos[0]),
            Email(2, para: enderecos[1]),
            Email(3, para: enderecos[2]),
            Email(4, para: enderecos[3]),
            Email(5, "TESTE_SEM_RENDER", para: enderecos[4]));

        Assert.Equal(5, await CicloAsync(api, coletor));

        lock (coletor.Mensagens)
        {
            Assert.NotEmpty(coletor.Mensagens); // o log existe (as falhas aparecem nele)...
            Assert.All(enderecos, e => Assert.DoesNotContain(coletor.Mensagens, m => m.Contains(e, StringComparison.OrdinalIgnoreCase))); // ...mas sem nenhum endereço
            Assert.DoesNotContain(coletor.Mensagens, m => m.Contains('@', StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Falha_de_uma_mensagem_nao_impede_as_outras_do_mesmo_ciclo()
    {
        var api = await ApiDeEmails.ComecarAsync(mysql);
        api.Enviador.Comportamento = m => m.Para.StartsWith("b.", StringComparison.Ordinal) ? new FalhaDeEnvioException("recusada", permanente: true, null, "550") : null;
        await EnfileirarAsync(api, Email(1, para: "a.um@empresa.com.br"), Email(2, para: "b.dois@empresa.com.br"), Email(3, para: "c.tres@empresa.com.br"));

        Assert.Equal(3, await CicloAsync(api));

        var linhas = await LerAsync(api.Conexao);
        Assert.Equal(SituacaoDoEmail.Enviado, Assert.Single(linhas, l => l.DestinatarioEmail == "a.um@empresa.com.br").Situacao);
        Assert.Equal(SituacaoDoEmail.Falhou, Assert.Single(linhas, l => l.DestinatarioEmail == "b.dois@empresa.com.br").Situacao);
        Assert.Equal(SituacaoDoEmail.Enviado, Assert.Single(linhas, l => l.DestinatarioEmail == "c.tres@empresa.com.br").Situacao);
        Assert.Equal(["a.um@empresa.com.br", "c.tres@empresa.com.br"], api.Enviador.Enviadas.Select(m => m.Para));
    }
}

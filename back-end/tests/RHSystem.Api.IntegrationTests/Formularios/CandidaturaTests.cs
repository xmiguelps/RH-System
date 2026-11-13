using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Formularios.Regras;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.CandidaturasDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>responderFormularioPublico</c> (a candidatura pelo link, sem login), no banco REAL com o relógio em 2026-10-07T12:42Z. O candidato e as respostas dele são gravados com o MESMO instante, sem funcionário; o e-mail (aparado, em minúsculas) responde UMA vez por formulário, também em paralelo; currículo e arquivos
/// são PDF ou Word até 5 MB pelo conteúdo e o blob é apagado se o banco recusa; os erros de campo vêm juntos; o limite é de 10 envios por hora por IP; NENHUM dado do candidato vai a log, auditoria ou mensagem de erro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CandidaturaTests(MySqlFixture mysql)
{
    private const string Instante = "2026-10-07 09:42:00";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(CandidaturaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        return api;
    }

    private static async Task<long> CandidatosAsync(ApiDeAcesso api, int formulario) => await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {formulario}");

    private static async Task<long> AuditoriaAsync(ApiDeAcesso api) => await ContarAsync(api, "SELECT (SELECT COUNT(*) FROM AUDITORIA_EVENTOS) + (SELECT COUNT(*) FROM AUDITORIA_MUDANCAS)");

    [Fact]
    public async Task A_candidatura_grava_o_candidato_e_as_respostas_com_o_mesmo_instante_sem_funcionario_e_sem_auditoria()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand1");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var auditoriaAntes = await AuditoriaAsync(api);
        var pdf = ArquivosDeTeste.Pdf(2048);

        var resposta = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e, comArquivo: true), nome: "  Mariana Teixeira  ", email: "  Mariana.Teixeira@Email.COM.br ", curriculo: ("cv.pdf", ArquivosDeTeste.Pdf()), arquivos: ("../../comprovante.pdf", pdf)));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("responderFormularioPublico");
        Assert.Equal("Recebemos as suas respostas. Obrigado por se candidatar.", (await CorpoAsync(resposta)).GetProperty("mensagem").GetString());
        Assert.Equal(("Mariana Teixeira", "mariana.teixeira@email.com.br", "(11) 98877-1204", Instante), (
            await TextoAsync(api, $"SELECT NOME FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT EMAIL FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT TELEFONE FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CADASTRO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}")));
        Assert.Equal(("3", "0", "0", "1", Instante), (
            await TextoAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id} AND ID_FORMULARIO_CANDIDATO IS NOT NULL"),
            await TextoAsync(api, $"SELECT COUNT(ID_FUNCIONARIO) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT SUM(ANONIMO) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT COUNT(DISTINCT DATA_RESPOSTA) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(MIN(DATA_RESPOSTA), '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}")));
        Assert.Equal("Sim", await TextoAsync(api, $"SELECT RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {e.Multipla}"));
        Assert.Equal(e.Sim.ToString(), await TextoAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {e.Multipla}"));
        var novos = api.Api.Armazenamento.Gravados.Skip(gravadosAntes).ToList();
        Assert.Equal(2, novos.Count);
        var caminhoDoCurriculo = await TextoAsync(api, $"SELECT CAMINHO_CURRICULO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}");
        var caminhoDoAnexo = await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {e.Anexo}");
        Assert.Equal(novos.Order(), new[] { caminhoDoCurriculo!, caminhoDoAnexo! }.Order());
        Assert.All(novos, c => Assert.StartsWith("formularios/", c, StringComparison.Ordinal));
        Assert.Equal(pdf, api.Api.Armazenamento.Conteudo(caminhoDoAnexo!));
        Assert.Equal("comprovante.pdf", await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {e.Anexo}")); // sem o diretório do nome
        Assert.Equal(auditoriaAntes, await AuditoriaAsync(api)); // nenhuma linha de auditoria: nem o nome, nem o e-mail
    }

    [Fact]
    public async Task O_telefone_e_o_curriculo_sao_opcionais_e_o_candidato_sem_eles_fica_sem_os_dois()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand2");

        var resposta = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), telefone: null));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal((null, null), (await TextoAsync(api, $"SELECT TELEFONE FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}"), await TextoAsync(api, $"SELECT CAMINHO_CURRICULO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}")));
    }

    [Fact]
    public async Task Cada_erro_de_campo_e_um_400_com_a_chave_certa_sem_gravar_nada_nem_guardar_arquivo()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand3");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var ok = RespostasOk(e);
        (string Nome, MultipartFormDataContent Corpo, string Chave)[] casos =
        [
            ("sem nome", Candidatura(ok, nome: "   "), "nome"),
            ("nome com 151 caracteres", Candidatura(ok, nome: new string('n', 151)), "nome"),
            ("sem e-mail", Candidatura(ok, email: null), "email"),
            ("e-mail sem arroba", Candidatura(ok, email: "mariana.email.com"), "email"),
            ("e-mail com 151 caracteres", Candidatura(ok, email: new string('a', 142) + "@email.com"), "email"),
            ("telefone com letra", Candidatura(ok, telefone: "(11) 9887-ABCD"), "telefone"),
            ("telefone curto", Candidatura(ok, telefone: "12345"), "telefone"),
            ("aceite falso", Candidatura(ok, aceitou: "false"), "aceitouAvisoDePrivacidade"),
            ("sem o aceite", Candidatura(ok, aceitou: null), "aceitouAvisoDePrivacidade"),
            ("aceite que não é booleano", Candidatura(ok, aceitou: "sim"), "aceitouAvisoDePrivacidade"),
            ("obrigatória fora da lista", Candidatura(Json(new { perguntaId = e.Multipla, alternativaIds = new[] { e.Sim } })), "respostas"),
            ("respostas que não são JSON", Candidatura("isto não é json"), "respostas"),
            ("sem a parte respostas", Candidatura(null), "respostas"),
            ("arquivo que nenhuma resposta usa", Candidatura(ok, arquivos: ("solto.pdf", ArquivosDeTeste.Pdf())), "arquivos"),
            ("currículo de 6 MB", Candidatura(ok, curriculo: ("cv.pdf", ArquivosDeTeste.Pdf((5 * 1024 * 1024) + 1))), "curriculo"),
            ("currículo PNG", Candidatura(ok, curriculo: ("cv.png", ArquivosDeTeste.Png())), "curriculo"),
            ("zip renomeado .docx", Candidatura(ok, curriculo: ("cv.docx", Zip())), "curriculo"),
            ("arquivo de pergunta ruim", Candidatura(RespostasOk(e, comArquivo: true), arquivos: ("a.exe", ArquivosDeTeste.Exe())), "arquivos"),
        ];

        var falhas = new List<string>();
        foreach (var (nome, corpo, chave) in casos)
        {
            var resposta = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, corpo);
            if ((int)resposta.StatusCode != 400 || !(await ErrosAsync(resposta)).TryGetProperty(chave, out _))
            {
                falhas.Add($"{nome}: {(int)resposta.StatusCode}");
            }
        }

        Assert.Empty(falhas);
        Assert.Equal(0L, await CandidatosAsync(api, e.Id));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    private static byte[] Zip() => Docx(comODocumento: false);

    [Fact]
    public async Task O_mesmo_email_em_qualquer_caixa_responde_uma_vez_so_e_dois_envios_ao_mesmo_tempo_dao_um_201_e_um_409()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand4");
        var outro = await EntrevistaAsync(api, "ZCand4 outro");
        var cliente = api.Api.ClienteAnonimo();

        var juntos = await Task.WhenAll(EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), email: "paralelo@email.com")), EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), email: "PARALELO@email.com")));
        var depois = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), email: " Paralelo@Email.com "));
        var outroEmail = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), email: "outra@email.com"));
        var outroFormulario = await EnviarAsync(cliente, outro.Token, Candidatura(RespostasOk(outro), email: "paralelo@email.com"));

        Assert.Equal([201, 409], juntos.Select(r => (int)r.StatusCode).Order());
        var conflito = await CorpoAsync(juntos.Single(r => (int)r.StatusCode == 409));
        Assert.Equal(("JA_RESPONDIDO", "Já recebemos uma candidatura com este e-mail."), (Codigo(conflito), conflito.GetProperty("detail").GetString()));
        Assert.Equal((409, 201, 201), ((int)depois.StatusCode, (int)outroEmail.StatusCode, (int)outroFormulario.StatusCode));
        Assert.Equal(2L, await CandidatosAsync(api, e.Id));
        Assert.Equal(4L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}")); // duas candidaturas de duas respostas
    }

    [Fact]
    public async Task Agendado_encerrado_e_prazo_vencido_sao_422_e_o_codigo_que_nao_vale_e_404_antes_de_olhar_o_corpo()
    {
        var api = await ApiAsync();
        var agendado = await EntrevistaAsync(api, "ZCand5 agendado", "AGENDADO");
        var encerrado = await EntrevistaAsync(api, "ZCand5 encerrado", "ENCERRADO");
        var vencido = await EntrevistaAsync(api, "ZCand5 vencido", prazo: "2026-10-06");
        var cliente = api.Api.ClienteAnonimo();

        var respostas = new List<HttpResponseMessage>();
        foreach (var e in new[] { agendado, encerrado, vencido })
        {
            respostas.Add(await EnviarAsync(cliente, e.Token, Candidatura(null, nome: null, email: null)));
        }

        var inexistente = await EnviarAsync(cliente, TokenDoLink.Novo(), Candidatura(null, nome: null));

        Assert.Equal([422, 422, 422, 404], respostas.Append(inexistente).Select(r => (int)r.StatusCode));
        Assert.Equal(["FORMULARIO_AINDA_NAO_ABERTO", "FORMULARIO_ENCERRADO", "FORMULARIO_ENCERRADO"], await Task.WhenAll(respostas.Select(async r => Codigo(await CorpoAsync(r)))));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO IN ({agendado.Id}, {encerrado.Id}, {vencido.Id})"));
    }

    [Fact]
    public async Task Se_o_formulario_e_encerrado_na_hora_de_gravar_e_422_nada_e_gravado_e_os_arquivos_guardados_sao_apagados()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand6");
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {e.Id}"));

        var resposta = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e, comArquivo: true), curriculo: ("cv.pdf", ArquivosDeTeste.Pdf()), arquivos: ("a.pdf", ArquivosDeTeste.Pdf())));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal((0L, 0L), (await CandidatosAsync(api, e.Id), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {e.Id}")));
        var guardados = api.Api.Armazenamento.Gravados.Skip(gravadosAntes).ToList();
        Assert.Equal(2, guardados.Count); // os dois foram guardados antes da transação...
        Assert.All(guardados, c => Assert.False(api.Api.Armazenamento.Existe(c))); // ...e apagados quando ela recusou
    }

    [Fact]
    public async Task O_limite_de_10_envios_por_hora_vale_por_IP_e_roda_antes_de_tudo()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand7");
        var cliente = api.Api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Formularios:LimiteEnvioPorHora", "10");
            b.UseSetting("ProxyReverso:Habilitado", "true");
        }).CreateClient();
        async Task<HttpResponseMessage> EnviarDoIpAsync(string ip, string token, string email)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Post, $"/api/publico/formularios/{token}/respostas") { Content = Candidatura(RespostasOk(e), email: email) };
            pedido.Headers.Add("X-Forwarded-For", ip);
            return await cliente.SendAsync(pedido);
        }

        var dentro = new List<int>();
        for (var i = 1; i <= 10; i++)
        {
            dentro.Add((int)(await EnviarDoIpAsync("203.0.113.20", e.Token, $"candidato{i}@email.com")).StatusCode);
        }

        var decima_primeira = await EnviarDoIpAsync("203.0.113.20", TokenDoLink.Novo(), "outro@email.com"); // o 429 vem antes do 404
        var outroIp = await EnviarDoIpAsync("203.0.113.21", e.Token, "de-outro-ip@email.com");

        Assert.All(dentro, status => Assert.Equal(201, status));
        Assert.Equal((429, 201), ((int)decima_primeira.StatusCode, (int)outroIp.StatusCode));
        Assert.InRange(int.Parse(decima_primeira.Headers.GetValues("Retry-After").Single()), 1, 3600);
    }

    [Fact]
    public async Task Nada_do_candidato_vai_a_log_a_auditoria_nem_a_mensagem_de_erro()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand8");
        var logs = new ColetorDeLogs();
        var cliente = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();
        var auditoriaAntes = (await AuditoriaDeTesteAsync(api)).Total;
        const string Nome = "Zuleica Quintanilha";
        const string Email = "zuleica.quintanilha@email.com.br";
        const string Telefone = "(11) 91234-5678";

        var primeira = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), nome: Nome, email: Email, telefone: Telefone));
        var repetida = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), nome: Nome, email: Email, telefone: Telefone));
        var invalida = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), nome: Nome, email: "invalido", telefone: Telefone));

        Assert.Equal((201, 409, 400), ((int)primeira.StatusCode, (int)repetida.StatusCode, (int)invalida.StatusCode));
        var corpos = (await repetida.Content.ReadAsStringAsync()) + (await invalida.Content.ReadAsStringAsync());
        string[] segredos = [Nome, Email, Telefone, "Quintanilha", e.Token];
        Assert.All(segredos, s => Assert.DoesNotContain(s, corpos, StringComparison.OrdinalIgnoreCase));
        string[] registros;
        lock (logs.Mensagens)
        {
            registros = [.. logs.Mensagens];
        }

        Assert.All(segredos, s => Assert.DoesNotContain(registros, m => m.Contains(s, StringComparison.OrdinalIgnoreCase)));
        var auditoria = await AuditoriaDeTesteAsync(api);
        Assert.Equal(auditoriaAntes, auditoria.Total);
        Assert.All(segredos, s => Assert.DoesNotContain(s, auditoria.Texto, StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<(int Total, string Texto)> AuditoriaDeTesteAsync(ApiDeAcesso api) => await RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste.TudoDoLogAsync(api.Conexao);

    [Fact]
    public async Task A_candidatura_nao_exige_login_e_o_gestor_ou_o_RH_logado_tambem_pode_enviar_pelo_link()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand9");

        var semLogin = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), email: "a@email.com"));
        var comLogin = await EnviarAsync(api.Api.ClienteComo(api.Elenco.Gestor), e.Token, Candidatura(RespostasOk(e), email: "b@email.com"));

        Assert.Equal([201, 201], new[] { semLogin, comLogin }.Select(r => (int)r.StatusCode));
        _ = JsonSerializer.Deserialize<JsonElement>(await semLogin.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Nome_e_email_com_exatamente_150_caracteres_valem_e_com_151_sao_400()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand10");
        var email150 = new string('a', 137) + "@email.com.br";
        var email151 = new string('a', 138) + "@email.com.br";

        var nome150 = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), nome: new string('n', 150), email: "nome150@email.com.br"));
        var email150Valido = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), email: email150));
        var email151Recusado = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(RespostasOk(e), email: email151));

        Assert.Equal([201, 201, 400], new[] { nome150, email150Valido, email151Recusado }.Select(r => (int)r.StatusCode));
        Assert.True((await ErrosAsync(email151Recusado)).TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Um_erro_inesperado_na_rota_publica_e_logado_sem_o_codigo_do_link()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCand11");
        var logs = new ColetorDeLogs();
        var cliente = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();
        api.Api.Armazenamento.FalharNaGravacao = new IOException("armazenamento fora do ar");
        HttpResponseMessage resposta;
        try
        {
            resposta = await EnviarAsync(cliente, e.Token, Candidatura(RespostasOk(e), curriculo: ("cv.pdf", ArquivosDeTeste.Pdf())));
        }
        finally
        {
            api.Api.Armazenamento.FalharNaGravacao = null;
        }

        string[] registros;
        lock (logs.Mensagens)
        {
            registros = [.. logs.Mensagens];
        }

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.Contains(registros, m => m.Contains("Erro inesperado em POST", StringComparison.Ordinal)); // o erro foi logado...
        Assert.DoesNotContain(registros, m => m.Contains(e.Token, StringComparison.Ordinal)); // ...sem o código do link (o caminho vai como /api/publico/formularios/{token}/respostas)
    }
}

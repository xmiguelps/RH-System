using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Formularios.Regras;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>obterFormularioPublico</c> (a página do candidato, sem login), no banco REAL com o relógio em 2026-10-07T12:42Z. Código inexistente, de rascunho, fora do padrão ou de um formulário que não é por link: o MESMO 404; agendado, encerrado (inclusive o prazo vencido com a tarefa parada) e empresa sem configuração: 422;
/// nada do RH nem o <c>id</c> do formulário na resposta; o limite de 30 consultas por minuto por IP roda antes de tudo (429 com <c>Retry-After</c>), por IP e só nas rotas públicas do formulário.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PaginaPublicaTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PaginaPublicaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        return api;
    }

    private static string Rota(string token) => $"/api/publico/formularios/{token}";

    /// <summary>Uma entrevista por link, com perguntas, no estado pedido. Devolve o id e o código.</summary>
    private static async Task<(int Id, string Token)> EntrevistaAsync(ApiDeAcesso api, string prefixo, string situacao = "PUBLICADO", string? prazo = null, string? descricao = "Responda com calma.")
    {
        var token = TokenDoLink.Novo();
        var id = await SemearAsync(api, Titulo(prefixo), situacao, tipo: "ENTREVISTA", alvo: "LINK", prazo: prazo, token: token, descricao: descricao);
        await PerguntaAsync(api, id, "Você tem disponibilidade aos sábados?", "MULTIPLA_ESCOLHA", 1, true, "Sim", "Não");
        await PerguntaAsync(api, id, "Conte uma experiência.", "RESPOSTA_LONGA", 2, true);
        await PerguntaAsync(api, id, "Envie seu currículo.", "ARQUIVO", 3);
        return (id, token);
    }

    [Fact]
    public async Task A_pagina_traz_a_empresa_o_formulario_o_aviso_com_a_retencao_e_as_perguntas_sem_id_do_formulario_nem_codigo()
    {
        var api = await ApiAsync();
        var (_, token) = await EntrevistaAsync(api, "ZPub1 auxiliar de logística", prazo: "2026-10-31");

        var resposta = await api.Api.ClienteAnonimo().GetAsync(Rota(token));
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFormularioPublico");
        Assert.Equal(("Empresa de teste", "Responda com calma.", "2026-10-31", 180), (corpo.GetProperty("empresaNome").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("prazo").GetString(), corpo.GetProperty("retencaoDias").GetInt32()));
        Assert.Contains("Guardamos por até 180 dias e depois apagamos.", corpo.GetProperty("avisoDePrivacidade").GetString(), StringComparison.Ordinal);
        var perguntas = corpo.GetProperty("perguntas").EnumerateArray().ToList();
        Assert.Equal(["MULTIPLA_ESCOLHA", "RESPOSTA_LONGA", "ARQUIVO"], perguntas.Select(p => p.GetProperty("tipo").GetString()));
        Assert.Equal(["Sim", "Não"], perguntas[0].GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("texto").GetString()));
        Assert.Equal(["avisoDePrivacidade", "descricao", "empresaNome", "perguntas", "prazo", "retencaoDias", "titulo"], corpo.EnumerateObject().Select(p => p.Name).Order());
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain(token, texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Codigo_inexistente_de_rascunho_ou_fora_do_padrao_dao_o_mesmo_404_e_nunca_400()
    {
        var api = await ApiAsync();
        var rascunho = await EntrevistaAsync(api, "ZPub2 rascunho", "RASCUNHO");
        var cliente = api.Api.ClienteAnonimo();
        var tokens = new[] { TokenDoLink.Novo(), rascunho.Token, "curto", "2147483647", "com espaço e símbolos!!", new string('a', 65), "%2e%2e", "abc.def" };

        var respostas = new List<(string Token, HttpResponseMessage Resposta)>();
        foreach (var token in tokens)
        {
            respostas.Add((token, await cliente.GetAsync(Rota(Uri.EscapeDataString(token)))));
        }

        Assert.All(respostas, r => Assert.Equal(404, (int)r.Resposta.StatusCode));
        var corpos = new List<string>();
        foreach (var (_, resposta) in respostas)
        {
            var problema = await CorpoAsync(resposta);
            corpos.Add(string.Join("|", problema.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}")));
        }

        Assert.Single(corpos.Distinct()); // o mesmo corpo para qualquer dos casos
    }

    [Fact]
    public async Task Agendado_encerrado_e_prazo_vencido_com_a_tarefa_parada_sao_422_e_sem_empresa_tambem()
    {
        var api = await ApiAsync();
        var agendado = await EntrevistaAsync(api, "ZPub3 agendado", "AGENDADO");
        var encerrado = await EntrevistaAsync(api, "ZPub3 encerrado", "ENCERRADO");
        var vencido = await EntrevistaAsync(api, "ZPub3 vencido", prazo: "2026-10-06");
        var semEmpresa = await EntrevistaAsync(api, "ZPub3 sem empresa");
        var cliente = api.Api.ClienteAnonimo();

        var doAgendado = await cliente.GetAsync(Rota(agendado.Token));
        var doEncerrado = await cliente.GetAsync(Rota(encerrado.Token));
        var doVencido = await cliente.GetAsync(Rota(vencido.Token));
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        var semConfiguracao = await cliente.GetAsync(Rota(semEmpresa.Token));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);

        Assert.Equal([422, 422, 422, 422], new[] { doAgendado, doEncerrado, doVencido, semConfiguracao }.Select(r => (int)r.StatusCode));
        Assert.Equal(["FORMULARIO_AINDA_NAO_ABERTO", "FORMULARIO_ENCERRADO", "FORMULARIO_ENCERRADO", "EMPRESA_NAO_CONFIGURADA"], new[] { Codigo(await CorpoAsync(doAgendado)), Codigo(await CorpoAsync(doEncerrado)), Codigo(await CorpoAsync(doVencido)), Codigo(await CorpoAsync(semConfiguracao)) });
        Assert.Equal("Este formulário ainda não está disponível. Fale com o RH.", (await CorpoAsync(semConfiguracao)).GetProperty("detail").GetString());
    }

    [Fact]
    public async Task O_limite_de_30_consultas_por_minuto_vale_por_IP_roda_antes_de_tudo_e_so_nas_rotas_publicas_do_formulario()
    {
        var api = await ApiAsync();
        var (_, token) = await EntrevistaAsync(api, "ZPub4 limite");
        var cliente = api.Api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Formularios:LimiteConsultaPorMinuto", "30");
            b.UseSetting("ProxyReverso:Habilitado", "true");
        }).CreateClient();
        async Task<HttpResponseMessage> ConsultarAsync(string ip, string codigo)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, Rota(codigo));
            pedido.Headers.Add("X-Forwarded-For", ip);
            return await cliente.SendAsync(pedido);
        }

        var dentro = new List<int>();
        for (var i = 1; i <= 30; i++)
        {
            dentro.Add((int)(await ConsultarAsync("203.0.113.10", i % 2 == 0 ? token : "naoexiste1234567890123456")).StatusCode);
        }

        var trigesimaPrimeira = await ConsultarAsync("203.0.113.10", "naoexiste1234567890123456"); // o 429 vem antes do 404
        var outroIp = await ConsultarAsync("203.0.113.11", token);
        var corpo = await CorpoAsync(trigesimaPrimeira);

        Assert.Equal(15, dentro.Count(s => s == 200));
        Assert.Equal(15, dentro.Count(s => s == 404));
        Assert.Equal((429, 200), ((int)trigesimaPrimeira.StatusCode, (int)outroIp.StatusCode));
        Assert.Equal(("Muitas tentativas.", "Muitas tentativas. Aguarde um minuto e tente de novo."), (corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString()));
        Assert.InRange(int.Parse(trigesimaPrimeira.Headers.GetValues("Retry-After").Single()), 1, 60);
        Assert.Equal(200, (int)(await cliente.GetAsync("/health")).StatusCode); // o limite não vale para as outras rotas
    }

    [Fact]
    public async Task A_pagina_publica_nunca_responde_401_nem_403_para_quem_esta_logado_ou_nao()
    {
        var api = await ApiAsync();
        var (_, token) = await EntrevistaAsync(api, "ZPub5 sem login");

        var semLogin = await api.Api.ClienteAnonimo().GetAsync(Rota(token));
        var rh = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(token));
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(token));
        var inexistente = await api.Api.ClienteAnonimo().GetAsync(Rota(TokenDoLink.Novo()));

        Assert.Equal([200, 200, 200, 404], new[] { semLogin, rh, gestor, inexistente }.Select(r => (int)r.StatusCode));
        _ = JsonSerializer.Deserialize<JsonElement>(await semLogin.Content.ReadAsStringAsync());
    }
}

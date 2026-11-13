using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>validarCertificado</c>, pública, no banco REAL com o relógio em 2026-10-07T12:42Z. O limite de 10 consultas por minuto por IP roda antes de tudo (429 até para código que não existe); texto que não é UUID é 400; código que não existe, de treinamento que não emite, de pessoa que não concluiu ou que NÃO é versão 4 (o v1 do trigger) dão o MESMO 404 (mesmo status, mesmo corpo);
/// a resposta tem só quatro campos; quem saiu da empresa continua aparecendo; a rota nunca responde 401 nem 403; um erro 500 não escreve o código no log.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConferirCertificadoTests(MySqlFixture mysql)
{
    private static string Rota(string codigo) => $"/api/publico/certificados/{codigo}";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ConferirCertificadoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, '{status}')");

    /// <summary>Um certificado com o código dado (o trigger cria a linha na conclusão; aqui o código e a data são trocados como a API faz).</summary>
    private static async Task<string> EmitidoAsync(ApiDeAcesso api, int treinamento, int pessoa, string? codigo = null)
    {
        await LinhaAsync(api, treinamento, pessoa, "CONCLUIDO");
        var novo = codigo ?? CodigoDoCertificado.Novo();
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET CODIGO_VALIDACAO = '{novo}', DATA_EMISSAO = '2026-09-15' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");
        return novo;
    }

    [Fact]
    public async Task Um_certificado_valido_confere_com_so_quatro_campos_com_ou_sem_login_e_o_nome_de_quem_saiu_continua()
    {
        var api = await ApiAsync();
        var nome = Titulo("ZConf1 Bruno Lima");
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
        var titulo = Titulo("ZConf1 LGPD na prática");
        var t = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true);
        var codigo = await EmitidoAsync(api, t, pessoa);

        var anonimo = await api.Api.ClienteAnonimo().GetAsync(Rota(codigo));
        var maiusculo = await api.Api.ClienteAnonimo().GetAsync(Rota(codigo.ToUpperInvariant()));
        var rh = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(codigo));
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(codigo));
        await AdvertenciasDeTeste.InativarAsync(api, pessoa);
        var depoisDeSair = await api.Api.ClienteAnonimo().GetAsync(Rota(codigo));

        Assert.Equal([200, 200, 200, 200, 200], new[] { anonimo, maiusculo, rh, gestor, depoisDeSair }.Select(r => (int)r.StatusCode));
        await anonimo.AfirmarConformeAoContratoAsync("validarCertificado");
        var corpo = await CorpoAsync(anonimo);
        Assert.Equal(["codigoValidacao", "emitidoEm", "titularNome", "treinamentoTitulo"], corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // nenhum id, número, setor, e-mail nem situação
        Assert.Equal((codigo, nome, titulo, "2026-09-15"), (corpo.GetProperty("codigoValidacao").GetString(), corpo.GetProperty("titularNome").GetString(), corpo.GetProperty("treinamentoTitulo").GetString(), corpo.GetProperty("emitidoEm").GetString()));
        Assert.Equal(codigo, (await CorpoAsync(maiusculo)).GetProperty("codigoValidacao").GetString()); // sempre em minúsculas
        Assert.Equal(nome, (await CorpoAsync(depoisDeSair)).GetProperty("titularNome").GetString()); // o código continua válido
    }

    [Fact]
    public async Task Inexistente_treinamento_que_nao_emite_pessoa_que_nao_concluiu_e_codigo_v1_dao_o_mesmo_404_com_o_mesmo_corpo()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZConf2 Ana");
        var naoEmite = await SemearAsync(api, Titulo("ZConf2 nao emite"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        var codigoNaoEmite = await EmitidoAsync(api, naoEmite, pessoa);
        var naoConcluiu = await SemearAsync(api, Titulo("ZConf2 nao concluiu"), "EM_ANDAMENTO", obrigatorio: true);
        var outra = await PessoaAsync(api, "ZConf2 Bia");
        await LinhaAsync(api, naoConcluiu, outra, "EM_ANDAMENTO");
        var codigoNaoConcluiu = CodigoDoCertificado.Novo();
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_CERTIFICADOS (ID_TREINAMENTO, ID_FUNCIONARIO, DATA_EMISSAO, CODIGO_VALIDACAO) VALUES ({naoConcluiu}, {outra}, '2026-09-15', '{codigoNaoConcluiu}')"); // só por SQL: a API nunca insere certificado
        var comV1 = await SemearAsync(api, Titulo("ZConf2 v1"), "EM_ANDAMENTO", obrigatorio: true);
        var terceira = await PessoaAsync(api, "ZConf2 Caio");
        await LinhaAsync(api, comV1, terceira, "CONCLUIDO"); // o código é o v1 do trigger
        var v1 = (await TextoAsync(api, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {comV1} AND ID_FUNCIONARIO = {terceira}"))!;
        var cliente = api.Api.ClienteAnonimo();

        var respostas = new[] { await cliente.GetAsync(Rota(CodigoDoCertificado.Novo())), await cliente.GetAsync(Rota(codigoNaoEmite)), await cliente.GetAsync(Rota(codigoNaoConcluiu)), await cliente.GetAsync(Rota(v1)) };

        Assert.Equal([404, 404, 404, 404], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("validarCertificado");
        var corpos = new List<(string?, int, string?, bool)>();
        foreach (var r in respostas)
        {
            var c = await CorpoAsync(r);
            corpos.Add((c.GetProperty("title").GetString(), c.GetProperty("status").GetInt32(), c.GetProperty("detail").GetString(), c.TryGetProperty("codigo", out _)));
        }

        Assert.Equal(("Certificado não encontrado.", 404, "Não encontramos um certificado com este código.", false), corpos[0]);
        Assert.All(corpos, c => Assert.Equal(corpos[0], c)); // o mesmo corpo nos quatro casos
    }

    [Fact]
    public async Task Texto_que_nao_e_uuid_e_400_com_o_campo_e_nunca_vira_404_nem_500()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();

        var respostas = new[] { await cliente.GetAsync(Rota("abc")), await cliente.GetAsync(Rota("12345")), await cliente.GetAsync(Rota("a7f3c1d25e9b4c68b2d18f04e6a93b57")), await cliente.GetAsync(Rota("zzzzzzzz-zzzz-zzzz-zzzz-zzzzzzzzzzzz")) };

        Assert.Equal([400, 400, 400, 400], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("validarCertificado");
        Assert.Equal(["codigoValidacao"], (await CorpoAsync(respostas[0])).GetProperty("errors").EnumerateObject().Select(e => e.Name));
    }

    [Fact]
    public async Task O_limite_de_10_consultas_por_minuto_vale_por_IP_e_roda_antes_do_404()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZConf4 Ana");
        var t = await SemearAsync(api, Titulo("ZConf4"), "EM_ANDAMENTO", obrigatorio: true);
        var codigo = await EmitidoAsync(api, t, pessoa);
        var cliente = api.Api.WithWebHostBuilder(b =>
        {
            b.UseSetting("Certificados:LimiteConferenciaPorMinuto", "10");
            b.UseSetting("ProxyReverso:Habilitado", "true");
        }).CreateClient();
        async Task<HttpResponseMessage> ConsultarAsync(string ip, string valor)
        {
            var pedido = new HttpRequestMessage(HttpMethod.Get, Rota(valor));
            pedido.Headers.Add("X-Forwarded-For", ip);
            return await cliente.SendAsync(pedido);
        }

        var dentro = new List<int>();
        for (var i = 1; i <= 10; i++)
        {
            dentro.Add((int)(await ConsultarAsync("203.0.113.20", i % 2 == 0 ? codigo : CodigoDoCertificado.Novo())).StatusCode);
        }

        var decimaPrimeira = await ConsultarAsync("203.0.113.20", CodigoDoCertificado.Novo()); // o 429 vem antes do 404
        var malformada = await ConsultarAsync("203.0.113.20", "abc"); // e antes do 400
        var outroIp = await ConsultarAsync("203.0.113.21", codigo);
        var corpo = await CorpoAsync(decimaPrimeira);

        Assert.Equal(5, dentro.Count(s => s == 200));
        Assert.Equal(5, dentro.Count(s => s == 404));
        Assert.Equal([429, 429, 200], new[] { decimaPrimeira, malformada, outroIp }.Select(r => (int)r.StatusCode));
        Assert.Equal(("Muitas tentativas.", "Muitas tentativas. Aguarde um minuto e tente de novo."), (corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString()));
        Assert.InRange(int.Parse(decimaPrimeira.Headers.GetValues("Retry-After").Single()), 1, 60);
        Assert.Equal(200, (int)(await cliente.GetAsync("/health")).StatusCode); // o limite não vale para as outras rotas
    }

    [Fact]
    public async Task Um_erro_inesperado_na_conferencia_nao_escreve_o_codigo_no_log_e_o_caminho_sai_com_o_marcador()
    {
        var api = await ApiAsync();
        var logs = new ColetorDeLogs();
        var codigo = CodigoDoCertificado.Novo();
        await using var fabrica = api.Api.WithWebHostBuilder(b =>
        {
            b.ConfigureLogging(l => l.AddProvider(logs));
            b.ConfigureTestServices(s => s.AddScoped<ICertificadosRepositorio, RepositorioQueFalha>());
        });

        var resposta = await fabrica.CreateClient().GetAsync(Rota(codigo));

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain(codigo, await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
        var erros = logs.Mensagens.Where(m => m.StartsWith("[Error]", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(erros);
        Assert.All(erros, m => Assert.DoesNotContain(codigo, m, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(erros, m => m.Contains("/api/publico/certificados/{codigo}", StringComparison.Ordinal));
    }

    private sealed class RepositorioQueFalha : ICertificadosRepositorio
    {
        public Task<CertificadoParaEmitir?> LerAsync(int treinamentoId, int funcionarioId, CancellationToken ct) => throw new InvalidOperationException("falha forçada");

        public Task<CertificadoConferido?> ConferirPorCodigoAsync(string codigo, CancellationToken ct) => throw new InvalidOperationException("falha forçada");

        public Task<bool> GravarCaminhoAsync(int treinamentoId, int funcionarioId, string caminho, CancellationToken ct) => throw new InvalidOperationException("falha forçada");
    }
}

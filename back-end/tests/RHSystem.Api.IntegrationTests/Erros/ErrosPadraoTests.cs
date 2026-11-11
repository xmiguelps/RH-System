using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.Configuracao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Erros;

/// <summary>
/// Como cada tipo de falha vira resposta HTTP: sempre <c>application/problem+json</c>, em português, com <c>traceId</c> e sem stack trace
/// (rotas de teste em <c>/__teste/erros/*</c>, sem login e fora do documento). Roda contra o MySQL de verdade: a chave única e o <c>CHECK</c>
/// são do banco.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ErrosPadraoTests(MySqlFixture mysql)
{
    // Um banco com os scripts por classe de teste; as rotas só criam linhas com e-mail repetido (que falham) e não mexem umas nas outras.
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static ApiFactory? _compartilhada;

    private async Task<ApiFactory> ApiAsync()
    {
        await Trava.WaitAsync();
        try
        {
            return _compartilhada ??= await ApiFactory.CriarAsync(mysql);
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task<JsonElement> LerAsync(HttpResponseMessage resposta) =>
        JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync());

    private static async Task<JsonElement> AssertProblemaAsync(HttpResponseMessage resposta, int status, string titulo, string detalhe)
    {
        Assert.Equal(status, (int)resposta.StatusCode);
        Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
        var corpo = await LerAsync(resposta);
        Assert.Equal(status, corpo.GetProperty("status").GetInt32());
        Assert.Equal(titulo, corpo.GetProperty("title").GetString());
        Assert.Equal(detalhe, corpo.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrWhiteSpace(corpo.GetProperty("traceId").GetString()));
        return corpo;
    }

    [Fact]
    public async Task NaoEncontrado_vira_404_problem_json_com_title_e_detail_do_contrato()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var padrao = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/nao-encontrado"), 404, "Não encontrado.", "O registro não foi encontrado.");
        var comDetalhe = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/nao-encontrado-com-detalhe"), 404, "Não encontrado.", "A pessoa não foi encontrada neste setor.");

        Assert.False(padrao.TryGetProperty("errors", out _));
        Assert.False(comDetalhe.TryGetProperty("codigo", out _));
    }

    [Fact]
    public async Task Conflito_vira_409_e_leva_o_codigo_quando_informado()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var comCodigo = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/conflito"), 409, "Conflito.", "Esta matrícula já está em uso.");
        var semCodigo = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/conflito-sem-codigo"), 409, "Conflito.", "O registro foi alterado por outra pessoa.");

        Assert.Equal("MATRICULA_EM_USO", comCodigo.GetProperty("codigo").GetString());
        Assert.False(semCodigo.TryGetProperty("codigo", out _));
    }

    [Fact]
    public async Task ErroDeNegocio_vira_422_com_codigo_e_sem_regraDoBanco()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var corpo = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/negocio"), 422, "Operação recusada.", "O período já foi fechado.");

        Assert.Equal("PERIODO_FECHADO", corpo.GetProperty("codigo").GetString());
        Assert.False(corpo.TryGetProperty("regraDoBanco", out _));
    }

    [Fact]
    public async Task Validacao_vira_400_com_errors_por_campo_em_camelCase()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var corpo = await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/validacao"), 400, "Dados inválidos.", "Confira os campos indicados.");

        var erros = corpo.GetProperty("errors");
        Assert.Equal(["dataNascimento", "nome"], erros.EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["O nome é obrigatório.", "O nome é longo demais."], erros.GetProperty("nome").EnumerateArray().Select(m => m.GetString()));
        Assert.Equal(["A data de nascimento não pode ser no futuro."], erros.GetProperty("dataNascimento").EnumerateArray().Select(m => m.GetString()));
    }

    [Fact]
    public async Task Corpo_json_invalido_vira_400_problem_json_em_portugues()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await cliente.PostAsync("/__teste/erros/corpo", new StringContent("{ \"nome\": ", Encoding.UTF8, "application/json"));

        var corpo = await AssertProblemaAsync(resposta, 400, "Dados inválidos.", "Confira os campos indicados.");
        Assert.True(corpo.TryGetProperty("errors", out _));
    }

    [Fact]
    public async Task Enum_desconhecido_e_parametro_que_nao_converte_viram_400_com_o_mesmo_texto()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var enumDesconhecido = await cliente.PostAsync("/__teste/erros/corpo", new StringContent("{ \"situacao\": \"INEXISTENTE\" }", Encoding.UTF8, "application/json"));
        var parametroRuim = await cliente.GetAsync("/__teste/erros/numero/abc");

        await AssertProblemaAsync(enumDesconhecido, 400, "Dados inválidos.", "Confira os campos indicados.");
        await AssertProblemaAsync(parametroRuim, 400, "Dados inválidos.", "Confira os campos indicados.");
    }

    [Fact]
    public async Task Violacao_de_chave_unica_vira_409_generico_sem_nome_de_chave_nem_valor()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await cliente.GetAsync("/__teste/erros/chave-unica"); // dois funcionários com o mesmo EMAIL_CORPORATIVO

        await AssertProblemaAsync(resposta, 409, "Conflito.", "Já existe um registro com estes dados.");
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("UQ_FUNCIONARIOS_EMAIL_CORPORATIVO", texto);
        Assert.DoesNotContain("duplicada@empresa.com.br", texto);
        Assert.DoesNotContain("Duplicate", texto);
    }

    [Fact]
    public async Task Erro_de_trigger_continua_422_com_regraDoBanco()
    {
        var cliente = (await ApiAsync()).CreateClient();

        var corpo = await AssertProblemaAsync(await cliente.GetAsync("/__teste/trigger"), 422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.");

        Assert.Contains("Nao e possivel criar usuario para funcionario inativo.", corpo.GetProperty("regraDoBanco").GetString());
    }

    [Fact]
    public async Task Violacao_de_CHECK_vira_500_e_a_resposta_nao_tem_nome_de_constraint_nem_SQL()
    {
        var logs = new ColetorDeLogs();
        var cliente = (await ApiAsync()).WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs))).CreateClient();

        var resposta = await cliente.GetAsync("/__teste/erros/check"); // DATA_DESLIGAMENTO antes de DATA_ADMISSAO: CHK_FUNCIONARIOS_DATAS

        await AssertProblemaAsync(resposta, 500, "Erro no servidor.", "Erro no servidor. Tente novamente em instantes.");
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("CHK_FUNCIONARIOS_DATAS", texto);
        Assert.DoesNotContain("FUNCIONARIOS", texto);
        Assert.DoesNotContain("INSERT", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("constraint", texto, StringComparison.OrdinalIgnoreCase);

        // O detalhe técnico fica no log, para quem opera; só não vai para quem chamou.
        lock (logs.Mensagens)
        {
            Assert.Contains(logs.Mensagens, m => m.StartsWith("[Error]", StringComparison.Ordinal) && m.Contains("CHK_FUNCIONARIOS_DATAS", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task Rota_inexistente_devolve_404_problem_json_com_title_e_detail_do_contrato()
    {
        var cliente = (await ApiAsync()).CreateClient();

        await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/esta-rota-nao-existe"), 404, "Não encontrado.", "O registro não foi encontrado.");
    }

    [Fact]
    public async Task Erro_gerado_pelo_framework_pelo_IProblemDetailsService_sai_em_portugues_com_traceId()
    {
        var cliente = (await ApiAsync()).CreateClient();

        // Sem a customização, o framework escreveria o título em inglês ("Not Found") e sem o detalhe do contrato.
        await AssertProblemaAsync(await cliente.GetAsync("/__teste/erros/framework"), 404, "Não encontrado.", "O registro não foi encontrado.");
    }

    [Theory]
    [InlineData("/__teste/erros/validacao", 400)]
    [InlineData("/__teste/erros/nao-encontrado", 404)]
    [InlineData("/__teste/erros/conflito", 409)]
    [InlineData("/__teste/erros/negocio", 422)]
    [InlineData("/__teste/erro", 500)]
    public async Task Todo_erro_tem_traceId_e_nenhum_tem_stack_trace(string rota, int status)
    {
        var cliente = (await ApiAsync()).CreateClient();

        var resposta = await cliente.GetAsync(rota);

        Assert.Equal(status, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrWhiteSpace(JsonSerializer.Deserialize<JsonElement>(texto).GetProperty("traceId").GetString()));
        Assert.DoesNotContain("   at ", texto);
        Assert.DoesNotContain("StackTrace", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("RHSystem.", texto);
        Assert.DoesNotContain("segredo interno", texto);
    }

    [Fact]
    public void Titulos_e_detalhes_padrao_sao_os_dos_exemplos_do_contrato()
    {
        // Os oito erros genéricos de components.responses (Erro400Validacao..Erro500Interno). Fora da lista: Erro400Login ("Preencha o e-mail e a
        // senha.") é só do `entrar`, e os Erro409.../Erro422... de cada fatia têm exemplo próprio, com o texto da regra deles.
        string[] genericos =
        [
            "Erro400Validacao", "Erro401NaoAutenticado", "Erro403SemPermissao", "Erro404NaoEncontrado",
            "Erro409Conflito", "Erro422RegraDeNegocio", "Erro429MuitasTentativas", "Erro500Interno",
        ];
        var respostas = LocalizadorDoContrato.CarregarContrato()["components"]!["responses"]!.AsObject();

        foreach (var nome in genericos)
        {
            // Sem este Assert, uma resposta renomeada no contrato faria o teste cair numa NullReferenceException sem dizer qual.
            Assert.True(respostas.ContainsKey(nome), $"O contrato não tem components.responses.{nome}.");
            var status = int.Parse(nome.AsSpan(4, 3), System.Globalization.CultureInfo.InvariantCulture);
            var exemplo = respostas[nome]!["content"]!["application/problem+json"]!["example"]!;

            Assert.Equal(exemplo["title"]!.GetValue<string>(), TextosDeErro.Titulo(status));
            Assert.Equal(exemplo["detail"]!.GetValue<string>(), TextosDeErro.DetalhePadrao(status));
        }
    }
}

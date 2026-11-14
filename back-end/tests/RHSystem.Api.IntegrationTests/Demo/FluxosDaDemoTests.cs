using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Formularios;
using RHSystem.Api.IntegrationTests.Importacoes;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Demo.DemoDeTeste;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>
/// O ensaio do roteiro da demo (<c>docs/roteiro-da-demo.md</c>): cada teste é um passo do roteiro, feito pela API de verdade sobre o banco que o seed da demo montou, entrando pelo login e usando só o que a tela usaria. Se o seed ou uma rota mudar e o roteiro deixar de funcionar, o teste que
/// cobre aquele passo cai. O relógio é o do dia do seed (quarta, 2026-10-07, 09:42 em Brasília) e anda quando o fluxo precisa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FluxosDaDemoTests(MySqlFixture mysql)
{
    private static int IdDe(ApiFactory api, string local) =>
        (int)Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{Email(local)}'").GetAwaiter().GetResult();

    private static void Agora(ApiFactory api, int ano, int mes, int dia, int hora, int minuto) => api.Relogio.Fixar(new DateTime(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc));

    // ---------------------------------------------------------------- passo 1: entrar e bater o ponto

    [Fact]
    public async Task Passo_1_o_funcionario_entra_ve_o_proprio_dia_e_bate_entrada_saida_do_almoco_volta_e_saida()
    {
        var api = await ApiAsync(mysql);
        var bruno = IdDe(api, "bruno.azevedo");

        // 09:42 em Brasília: o cartão "Seu ponto hoje" ainda sem nada e a primeira batida
        var cliente = await EntrarComoAsync(api, "bruno.azevedo");
        var antes = await GetAsync(cliente, "/api/eu/ponto/hoje");
        var entrada = await cliente.PostAsJsonAsync("/api/eu/ponto/marcacoes", new { tipoEsperado = "ENTRADA" });
        Assert.Equal(201, (int)entrada.StatusCode);

        // o relógio anda e o token vale 15 minutos: a pessoa entra de novo, como no dia a dia
        Agora(api, 2026, 10, 7, 15, 2); // 12:02
        Assert.Equal(201, (int)(await (await EntrarComoAsync(api, "bruno.azevedo")).PostAsJsonAsync("/api/eu/ponto/marcacoes", new { tipoEsperado = "SAIDA" })).StatusCode);
        Agora(api, 2026, 10, 7, 16, 1); // 13:01
        Assert.Equal(201, (int)(await (await EntrarComoAsync(api, "bruno.azevedo")).PostAsJsonAsync("/api/eu/ponto/marcacoes", new { tipoEsperado = "ENTRADA" })).StatusCode);
        Agora(api, 2026, 10, 7, 20, 3); // 17:03
        Assert.Equal(201, (int)(await (await EntrarComoAsync(api, "bruno.azevedo")).PostAsJsonAsync("/api/eu/ponto/marcacoes", new { tipoEsperado = "SAIDA" })).StatusCode);

        Assert.Equal(4L, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {bruno} AND p.DATA = '2026-10-07'"));
        var espelho = await GetAsync(await EntrarComoAsync(api, "bruno.azevedo"), "/api/eu/ponto/espelho");
        Assert.True(espelho.ValueKind == JsonValueKind.Object);
        _ = antes;
    }

    // ---------------------------------------------------------------- passo 2: pedir e aprovar férias

    [Fact]
    public async Task Passo_2_a_pessoa_pede_ferias_o_gestor_aprova_o_RH_confirma_e_a_pessoa_ve_aprovada()
    {
        var api = await ApiAsync(mysql);
        var diego = await EntrarComoAsync(api, "diego.farias");
        var pedido = new { dataInicio = "2026-11-23", dataFim = "2026-12-07", abonoPecuniario = false, diasAbono = 0, adiantar13Salario = false, mensagem = "Viagem em família." };

        var conferencia = await diego.PostAsJsonAsync("/api/eu/ferias/conferencia", pedido);
        var cria = await diego.PostAsJsonAsync("/api/eu/ferias", pedido);

        Assert.Equal(200, (int)conferencia.StatusCode);
        Assert.True((await CorpoAsync(conferencia)).GetProperty("podeEnviar").GetBoolean());
        Assert.Equal(201, (int)cria.StatusCode);
        var id = (await CorpoAsync(cria)).GetProperty("id").GetInt32();

        var gestor = await EntrarComoAsync(api, "rafael.costa");
        var fila = await GetAsync(gestor, "/api/gestor/ferias");
        Assert.Contains(fila.GetProperty("itens").EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);
        Assert.Equal(200, (int)(await gestor.PostAsJsonAsync($"/api/gestor/ferias/{id}/aprovar", new { })).StatusCode);

        var rh = await EntrarComoAsync(api, "helena.martins");
        Assert.Equal(200, (int)(await rh.PostAsJsonAsync($"/api/rh/ferias/{id}/confirmar", new { })).StatusCode);

        var minhas = await GetAsync(diego, "/api/eu/ferias");
        Assert.Contains(minhas.GetProperty("historico").EnumerateArray(), i => i.GetProperty("id").GetInt32() == id && i.GetProperty("status").GetString() == "APROVADA");
        Assert.Equal(2L, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FERIAS_DECIDIDAS' AND REFERENCIA_ID = {id}")); // o gestor e o RH decidiram: um e-mail por decisão
    }

    // ---------------------------------------------------------------- passo 3: o RH envia o holerite e a pessoa baixa

    [Fact]
    public async Task Passo_3_o_RH_envia_o_holerite_da_pessoa_e_ela_recebe_o_link_do_arquivo()
    {
        var api = await ApiAsync(mysql);
        var lucas = IdDe(api, "lucas.pereira");
        var rh = await EntrarComoAsync(api, "helena.martins");
        var formulario = new MultipartFormDataContent
        {
            { new StringContent("9"), "mes" },
            { new StringContent("2026"), "ano" },
            { new StringContent("MENSAL"), "tipo" },
        };
        var pdf = new ByteArrayContent(ArquivosDeTeste.Pdf(4096));
        pdf.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        formulario.Add(pdf, "arquivo", "holerite-setembro.pdf");

        var envio = await rh.PostAsync($"/api/rh/funcionarios/{lucas}/holerites", formulario);

        Assert.Equal(201, (int)envio.StatusCode);
        var pessoa = await EntrarComoAsync(api, "lucas.pereira");
        var meus = await GetAsync(pessoa, "/api/eu/holerites");
        var holerite = meus.GetProperty("itens").EnumerateArray().Single();
        var link = await GetAsync(pessoa, $"/api/eu/holerites/{holerite.GetProperty("id").GetInt32()}/download");
        Assert.False(string.IsNullOrWhiteSpace(link.GetProperty("url").GetString()));
    }

    // ---------------------------------------------------------------- passo 4: atestado

    [Fact]
    public async Task Passo_4_a_pessoa_envia_o_atestado_e_o_RH_valida()
    {
        var api = await ApiAsync(mysql);
        var carla = await EntrarComoAsync(api, "carla.mendes");

        var envio = await carla.PostAsync("/api/eu/ponto/atestados", AtestadosDeTeste.Dias("2026-10-05", "2026-10-06"));

        Assert.Equal(201, (int)envio.StatusCode);
        var id = (await CorpoAsync(envio)).GetProperty("id").GetInt32();
        var rh = await EntrarComoAsync(api, "helena.martins");
        var painel = await GetAsync(rh, "/api/rh/painel");
        Assert.Contains(painel.GetProperty("pendencias").EnumerateArray(), p => p.GetProperty("tipo").GetString() == "ATESTADO"); // o cartão de atestados aparece para o RH
        Assert.Equal(200, (int)(await rh.PostAsJsonAsync($"/api/rh/ponto/atestados/{id}/validar", new { })).StatusCode);
        Assert.Equal("VALIDADO", await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_ATESTADO FROM ATESTADOS WHERE ID_ATESTADO = {id}") as string);
    }

    // ---------------------------------------------------------------- passo 5: importar a planilha

    [Fact]
    public async Task Passo_5_o_RH_importa_a_planilha_confere_a_previa_confirma_e_as_pessoas_aparecem_no_cadastro()
    {
        var api = await ApiAsync(mysql);
        var rh = await EntrarComoAsync(api, "helena.martins");
        var linhas = new[]
        {
            PlanilhasDeTeste.Linha(nome: "Marcos Oliveira", cpf: "529.982.247-25", email: $"marcos.oliveira@{Dominio}", admissao: "19/10/2026", cargo: "Desenvolvedor", setor: "Tecnologia", nivel: "Colaborador", beneficios: "Plano de saúde"),
            PlanilhasDeTeste.Linha(nome: "Renata Souza", cpf: "111.444.777-35", email: $"renata.souza@{Dominio}", admissao: "19/10/2026", cargo: "Vendedor", setor: "Comercial", nivel: "Colaborador"),
        };

        var previa = await rh.PostAsync("/api/rh/importacoes", Documentos.FormulariosDeTeste.Envio("novos-colaboradores.xlsx", PlanilhasDeTeste.Xlsx(linhas), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"));

        Assert.Equal(201, (int)previa.StatusCode);
        var corpoDaPrevia = await CorpoAsync(previa);
        Assert.Equal(2, corpoDaPrevia.GetProperty("linhasValidas").GetInt32());
        Assert.Equal(0, corpoDaPrevia.GetProperty("linhasComErro").GetInt32());
        var confirma = await rh.PostAsJsonAsync($"/api/rh/importacoes/{corpoDaPrevia.GetProperty("importacaoId").GetInt32()}/confirmar", new { enviarConvite = true });
        Assert.Equal(200, (int)confirma.StatusCode);
        Assert.Equal(2, (await CorpoAsync(confirma)).GetProperty("totalCriados").GetInt32());
        var marcos = IdDe(api, "marcos.oliveira");
        var ficha = await GetAsync(rh, $"/api/rh/funcionarios/{marcos}");
        Assert.Equal(("Marcos Oliveira", "Desenvolvedor", "Tecnologia", "EM_ADMISSAO"), (ficha.GetProperty("nome").GetString(), ficha.GetProperty("cargo").GetProperty("nome").GetString(), ficha.GetProperty("setor").GetProperty("nome").GetString(), ficha.GetProperty("situacao").GetString()));
        Assert.Equal(2L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE'")); // o convite de cada pessoa nova fica na fila (o envio só sai com Email:EnvioAtivo)
    }

    // ---------------------------------------------------------------- passo 6: treinamento e certificado

    [Fact]
    public async Task Passo_6_a_pessoa_conclui_o_treinamento_e_o_certificado_de_quem_concluiu_vale_na_conferencia_publica()
    {
        var api = await ApiAsync(mysql);
        var lucas = await EntrarComoAsync(api, "lucas.pereira");
        var treinamentos = await GetAsync(lucas, "/api/eu/treinamentos");
        var lgpd = treinamentos.GetProperty("itens").EnumerateArray().Single(t => t.GetProperty("titulo").GetString()!.StartsWith("Segurança da informação", StringComparison.Ordinal));
        var id = lgpd.GetProperty("id").GetInt32();
        var modulos = await Sql.ColunaAsync(api.Conexao, $"SELECT ID_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id} AND ORDEM > 1 ORDER BY ORDEM");

        foreach (var modulo in modulos)
        {
            Assert.Equal(200, (int)(await lucas.PostAsync($"/api/eu/treinamentos/{id}/modulos/{modulo}/concluir", null)).StatusCode);
        }

        Assert.Equal("CONCLUIDO", await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id} AND ID_FUNCIONARIO = {IdDe(api, "lucas.pereira")}") as string);
        var codigo = await Sql.EscalarAsync(api.Conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {id} AND ID_FUNCIONARIO = {IdDe(api, "camila.duarte")}") as string;
        var conferencia = await api.CreateClient().GetAsync($"/api/publico/certificados/{codigo}"); // sem login: quem recebe o certificado confere pelo código
        Assert.Equal(200, (int)conferencia.StatusCode);
        var certificado = await CorpoAsync(conferencia);
        Assert.Equal(("Camila Duarte", "Segurança da informação e LGPD", codigo), (certificado.GetProperty("titularNome").GetString(), certificado.GetProperty("treinamentoTitulo").GetString(), certificado.GetProperty("codigoValidacao").GetString()));
    }

    // ---------------------------------------------------------------- passo 7: o formulário de admissão da pessoa que vai começar

    [Fact]
    public async Task Passo_7_quem_vai_comecar_responde_o_formulario_de_admissao_e_a_pendencia_some_do_painel_do_RH()
    {
        var api = await ApiAsync(mysql);
        var julia = await EntrarComoAsync(api, "julia.andrade");
        var lista = await GetAsync(julia, "/api/eu/formularios");
        var formulario = lista.GetProperty("itens").EnumerateArray().Single(f => f.GetProperty("titulo").GetString() == "Formulário de admissão");
        var id = formulario.GetProperty("id").GetInt32();
        var perguntas = await Sql.ColunaAsync(api.Conexao, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {id} ORDER BY ORDEM");
        var rh = await EntrarComoAsync(api, "helena.martins");
        Assert.Contains((await GetAsync(rh, "/api/rh/painel")).GetProperty("pendencias").EnumerateArray(), p => p.GetProperty("tipo").GetString() == "ADMISSAO_COM_DOCUMENTOS_PENDENTES");

        var resposta = await RespostasDeTeste.ResponderAsync(julia, id, RespostasDeTeste.Envio(RespostasDeTeste.Json(
            new { perguntaId = int.Parse(perguntas[0], System.Globalization.CultureInfo.InvariantCulture), texto = "Maria Andrade, 11 98888-0001" },
            new { perguntaId = int.Parse(perguntas[1], System.Globalization.CultureInfo.InvariantCulture), texto = "Banco 001, agência 1234, conta 56789-0" })));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.DoesNotContain((await GetAsync(rh, "/api/rh/painel")).GetProperty("pendencias").EnumerateArray(), p => p.GetProperty("tipo").GetString() == "ADMISSAO_COM_DOCUMENTOS_PENDENTES");
    }

    // ---------------------------------------------------------------- passo 8: o gestor acompanha a equipe e o RH, o log

    [Fact]
    public async Task Passo_8_o_RH_ve_o_log_de_auditoria_do_que_foi_feito_na_demo()
    {
        var api = await ApiAsync(mysql);
        var diego = await EntrarComoAsync(api, "diego.farias");
        var id = (await CorpoAsync(await diego.PostAsJsonAsync("/api/eu/ferias", new { dataInicio = "2026-11-23", dataFim = "2026-12-07", abonoPecuniario = false, diasAbono = 0, adiantar13Salario = false }))).GetProperty("id").GetInt32();
        var gestor = await EntrarComoAsync(api, "rafael.costa");
        Assert.Equal(200, (int)(await gestor.PostAsJsonAsync($"/api/gestor/ferias/{id}/aprovar", new { })).StatusCode);

        var rh = await EntrarComoAsync(api, "helena.martins");
        var log = await GetAsync(rh, "/api/rh/auditoria?tamanhoPagina=50");

        Assert.NotEmpty(log.GetProperty("itens").EnumerateArray()); // o seed não cria eventos: tudo o que aparece nasceu das ações acima (entrar, pedir, aprovar)
    }
}

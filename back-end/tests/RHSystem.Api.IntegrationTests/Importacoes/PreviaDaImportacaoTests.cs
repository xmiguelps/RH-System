using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Documentos;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Importacoes;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;
using static RHSystem.Api.IntegrationTests.Importacoes.PlanilhasDeTeste;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>
/// <c>POST /api/rh/importacoes</c> (<c>validarPlanilhaDeImportacao</c>): só o RH envia a planilha e recebe a PRÉVIA, sem cadastrar ninguém. No banco REAL com o relógio fixo em 2026-10-07T12:42Z. Problema no arquivo todo é 422
/// com <c>codigo</c> e nada é gravado; o resto é uma prévia (201) com os erros por linha e coluna. O arquivo vai para o armazenamento FORA de qualquer transação e a linha e o evento vão numa transação curta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PreviaDaImportacaoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/importacoes";

    private const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static int _proximo = 810_000_000;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(PreviaDaImportacaoTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, byte[] xlsx, string nome = "novos-colaboradores.xlsx") =>
        cliente.PostAsync(Rota, FormulariosDeTeste.Envio(nome, xlsx, TipoXlsx));

    /// <summary>Cargo, setor e benefício que só este teste conhece (com acento, para provar o casamento sem acento).</summary>
    private sealed record Apoio(string Sufixo, string Cargo, string Setor, string Beneficio);

    private static async Task<Apoio> ApoioAsync(ApiDeAcesso api)
    {
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        var apoio = new Apoio(sufixo, $"Análise {sufixo}", $"Operações {sufixo}", $"Vale-refeição {sufixo}");
        await SementeDoCadastro.CargoAsync(api.Conexao, apoio.Cargo, 40);
        await SementeDoCadastro.SetorAsync(api.Conexao, apoio.Setor);
        await SementeDoCadastro.BeneficioAsync(api.Conexao, apoio.Beneficio);
        return apoio;
    }

    /// <summary>Uma linha válida com CPF e e-mail que nunca se repetem; os argumentos trocam o que o teste quer provar.</summary>
    private static object?[] Valida(Apoio apoio, string? nome = null, string? cpf = null, string? email = null, string? cargo = null, string? setor = null, string? beneficios = null)
    {
        var n = Interlocked.Increment(ref _proximo);
        return Linha(
            nome: nome ?? $"Pessoa {n}",
            cpf: cpf ?? CpfsDeTeste.Valido(n),
            email: email ?? $"pessoa{n}@previa.teste.com",
            cargo: cargo ?? apoio.Cargo,
            setor: setor ?? apoio.Setor,
            beneficios: beneficios);
    }

    private static Task<long> ImportacoesAsync(ApiDeAcesso api) => Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM IMPORTACOES");

    private static List<(string Coluna, string Mensagem)> Erros(JsonElement linha) =>
        [.. linha.GetProperty("erros").EnumerateArray().Select(e => (e.GetProperty("coluna").GetString()!, e.GetProperty("mensagem").GetString()!))];

    private static async Task<string?> TextoDaImportacaoAsync(ApiDeAcesso api, int id, string coluna) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT {coluna} FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}") is { } valor and not DBNull ? Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) : null;

    // ---------------------------------------------------------------- o corpo da requisição

    [Fact]
    public async Task Sem_arquivo_da_400_em_arquivo()
    {
        var api = await ApiAsync();
        var antes = await ImportacoesAsync(api);
        var formulario = new MultipartFormDataContent { { new StringContent("1"), "outro" } };

        var resposta = await ComoRh(api).PostAsync(Rota, formulario);

        await resposta.AfirmarConformeAoContratoAsync("validarPlanilhaDeImportacao");
        var erros = await ErrosAsync(resposta);
        Assert.Equal(["O arquivo é obrigatório."], erros.GetProperty("arquivo").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(antes, await ImportacoesAsync(api));
    }

    [Fact]
    public async Task Dois_arquivos_dao_400_em_arquivo()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var antes = await ImportacoesAsync(api);
        var bytes = Xlsx([Valida(apoio)]);
        var formulario = FormulariosDeTeste.Documento(null, null, null, null, new FormulariosDeTeste.Parte("arquivo", "a.xlsx", bytes, TipoXlsx), new FormulariosDeTeste.Parte("outro", "b.xlsx", bytes, TipoXlsx));

        var resposta = await ComoRh(api).PostAsync(Rota, formulario);

        var erros = await ErrosAsync(resposta);
        Assert.Equal(["Envie um arquivo só."], erros.GetProperty("arquivo").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(antes, await ImportacoesAsync(api));
    }

    [Fact]
    public async Task Arquivo_vazio_da_400()
    {
        var api = await ApiAsync();
        var formulario = FormulariosDeTeste.Envio("vazio.xlsx", [], TipoXlsx);

        var resposta = await ComoRh(api).PostAsync(Rota, formulario);

        var erros = await ErrosAsync(resposta);
        Assert.Equal(["O arquivo está vazio."], erros.GetProperty("arquivo").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Multipart_quebrado_da_400_e_nao_500()
    {
        var api = await ApiAsync();
        var quebrado = new StringContent("isto nao e um corpo multipart", System.Text.Encoding.UTF8, "multipart/form-data");
        quebrado.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data") { Parameters = { new System.Net.Http.Headers.NameValueHeaderValue("boundary", "limite-que-nao-aparece") } };

        var resposta = await ComoRh(api).PostAsync(Rota, quebrado);

        Assert.Equal(400, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Corpo_que_nao_e_multipart_da_415()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);

        var json = await cliente.PostAsJsonAsync(Rota, new { arquivo = "x" });
        var comum = await cliente.PostAsync(Rota, new FormUrlEncodedContent([new("arquivo", "x")]));

        Assert.Equal(415, (int)json.StatusCode);
        Assert.Equal(415, (int)comum.StatusCode);
    }

    [Fact]
    public async Task Funcionario_e_Gestor_recebem_403_antes_do_415_e_sem_token_401()
    {
        var api = await ApiAsync();

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).PostAsJsonAsync(Rota, new { arquivo = "x" })).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PostAsJsonAsync(Rota, new { arquivo = "x" })).StatusCode);
    }

    // ---------------------------------------------------------------- problema no arquivo todo: 422, sem prévia, sem linha, sem blob

    private static async Task AssertArquivoInvalidoAsync(ApiDeAcesso api, HttpResponseMessage resposta, string codigo, long importacoesAntes)
    {
        Assert.Equal((422, codigo), ((await ProblemaAsync(resposta)).Status, (await ProblemaAsync(resposta)).Codigo));
        Assert.Equal("Arquivo inválido.", (await CorpoAsync(resposta)).GetProperty("title").GetString());
        await resposta.AfirmarConformeAoContratoAsync("validarPlanilhaDeImportacao");
        Assert.Equal(importacoesAntes, await ImportacoesAsync(api));
        Assert.Empty(api.Api.Armazenamento.Gravados);
    }

    [Fact]
    public async Task Nao_e_xlsx_da_ARQUIVO_INVALIDO()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), ArquivosDeTeste.Pdf(), "planilha.xlsx");

        await AssertArquivoInvalidoAsync(api, resposta, "ARQUIVO_INVALIDO", antes);
    }

    [Fact]
    public async Task Mais_de_5_MiB_da_ARQUIVO_MUITO_GRANDE()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), new byte[(5 * 1024 * 1024) + 1]); // entre 5 e 6 MiB (o teto do corpo) responde o 422 do contrato

        await AssertArquivoInvalidoAsync(api, resposta, "ARQUIVO_MUITO_GRANDE", antes);
    }

    [Fact]
    public async Task Exatamente_5_MiB_ainda_passa_do_limite_de_tamanho_e_cai_no_leitor()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), new byte[5 * 1024 * 1024]); // o limite é de 5 MiB INCLUSIVE

        await AssertArquivoInvalidoAsync(api, resposta, "ARQUIVO_INVALIDO", antes);
    }

    [Fact]
    public async Task A_rota_declara_o_teto_do_corpo_de_6_MiB_e_dispensa_o_antiforgery()
    {
        // O servidor de teste não aplica o teto do Kestrel, então o que se prova aqui é o que a rota DECLARA (o teto vale em produção).
        var api = await ApiAsync();
        var rotas = api.Api.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        var rota = Assert.Single(rotas, e => e.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName == "validarPlanilhaDeImportacao");

        Assert.Equal(6L * 1024 * 1024, rota.Metadata.GetMetadata<IRequestSizeLimitMetadata>()!.MaxRequestBodySize);
        Assert.Equal(6L * 1024 * 1024, rota.Metadata.GetMetadata<RequestFormLimitsAttribute>()!.MultipartBodyLengthLimit);
        Assert.False(rota.Metadata.GetMetadata<IAntiforgeryMetadata>()!.RequiresValidation); // a autenticação é por cabeçalho, sem cookie
    }

    [Fact]
    public async Task Colunas_trocadas_da_MODELO_ALTERADO()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);
        var titulos = TitulosDoModelo.Reverse().Cast<string?>().ToList();

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio)], titulos));

        await AssertArquivoInvalidoAsync(api, resposta, "MODELO_ALTERADO", antes);
    }

    [Fact]
    public async Task Sem_linhas_da_PLANILHA_VAZIA()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([]));

        await AssertArquivoInvalidoAsync(api, resposta, "PLANILHA_VAZIA", antes);
    }

    [Fact]
    public async Task Quinhentas_e_uma_linhas_da_LIMITE_DE_LINHAS_EXCEDIDO()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx(Enumerable.Range(0, 501).Select(_ => Valida(apoio))));

        await AssertArquivoInvalidoAsync(api, resposta, "LIMITE_DE_LINHAS_EXCEDIDO", antes);
        Assert.Equal("A planilha tem 501 linhas. O limite é de 500 por importação.", (await ProblemaAsync(resposta)).Detalhe);
    }

    // ---------------------------------------------------------------- a prévia

    [Fact]
    public async Task Planilha_sem_erro_e_PRONTA_com_podeConfirmar_true_e_linhas_validas()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var mascarado = $"{cpf[..3]}.{cpf[3..6]}.{cpf[6..9]}-{cpf[9..]}";

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio, nome: "Ana Souza", cpf: mascarado, beneficios: apoio.Beneficio), Valida(apoio), Valida(apoio)]));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("validarPlanilhaDeImportacao");
        Assert.Null(resposta.Headers.Location); // o contrato não declara Location
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(
            new object[] { "novos-colaboradores.xlsx", "PRONTA", 3, 3, 0, true, "2026-10-07T13:42:00Z" },
            new object[]
            {
                corpo.GetProperty("nomeArquivo").GetString()!, corpo.GetProperty("situacao").GetString()!, corpo.GetProperty("totalLinhas").GetInt32(), corpo.GetProperty("linhasValidas").GetInt32(),
                corpo.GetProperty("linhasComErro").GetInt32(), corpo.GetProperty("podeConfirmar").GetBoolean(), corpo.GetProperty("expiraEmUtc").GetString()!,
            });
        Assert.True(corpo.GetProperty("importacaoId").GetInt32() > 0);
        var linhas = corpo.GetProperty("linhas");
        Assert.Equal([2, 3, 4], linhas.EnumerateArray().Select(l => l.GetProperty("numeroLinha").GetInt32()));
        Assert.All(linhas.EnumerateArray(), l =>
        {
            Assert.Equal("OK", l.GetProperty("situacao").GetString());
            Assert.Empty(Erros(l));
        });
        var primeira = linhas[0].GetProperty("valores");
        Assert.Equal(["nome", "cpf", "dataNascimento", "telefone", "emailCorporativo", "dataAdmissao", "tipoContrato", "cargo", "setor", "nivelDeAcesso", "beneficios"], primeira.EnumerateObject().Select(p => p.Name));
        Assert.Equal(("Ana Souza", mascarado, apoio.Beneficio), (primeira.GetProperty("nome").GetString(), primeira.GetProperty("cpf").GetString(), primeira.GetProperty("beneficios").GetString())); // como foi digitado
        Assert.Equal(JsonValueKind.Null, linhas[1].GetProperty("valores").GetProperty("beneficios").ValueKind);
    }

    [Fact]
    public async Task Planilha_com_erros_e_COM_ERROS_com_podeConfirmar_false_e_o_total_bate()
    {
        // O exemplo "comErros" do contrato: CPF inválido na linha 3; e-mail repetido da linha 2, cargo e setor que não existem na linha 5.
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var repetido = $"repetido{apoio.Sufixo}@previa.teste.com";

        var resposta = await EnviarAsync(
            ComoRh(api),
            Xlsx(
            [
                Valida(apoio, email: repetido),
                Valida(apoio, cpf: "123.456.789-00"),
                Valida(apoio),
                Valida(apoio, email: repetido, cargo: "Analista de compras (novo)", setor: "Marketing"),
            ]));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("validarPlanilhaDeImportacao");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal("COM_ERROS", corpo.GetProperty("situacao").GetString());
        Assert.Equal((4, 2, 2, false), (corpo.GetProperty("totalLinhas").GetInt32(), corpo.GetProperty("linhasValidas").GetInt32(), corpo.GetProperty("linhasComErro").GetInt32(), corpo.GetProperty("podeConfirmar").GetBoolean()));
        var linhas = corpo.GetProperty("linhas");
        Assert.Equal(["OK", "COM_ERRO", "OK", "COM_ERRO"], linhas.EnumerateArray().Select(l => l.GetProperty("situacao").GetString()));
        Assert.Equal([("CPF", "CPF inválido.")], Erros(linhas[1]));
        Assert.Equal(
            [
                ("EMAIL_CORPORATIVO", "Este e-mail aparece também na linha 2."),
                ("CARGO", "O cargo \"Analista de compras (novo)\" não existe. Use um cargo da aba Valores válidos."),
                ("SETOR", "O setor \"Marketing\" não existe. Use um setor da aba Valores válidos."),
            ],
            Erros(linhas[3]));
        Assert.Equal("COM_ERROS", await TextoDaImportacaoAsync(api, corpo.GetProperty("importacaoId").GetInt32(), "SITUACAO"));
    }

    [Fact]
    public async Task Cpf_ou_email_ja_cadastrado_da_erro_na_coluna_e_inativo_manda_usar_Readmitir()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var s = apoio.Sufixo;
        var cpfAtivo = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var cpfInativo = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var ativo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Existente Ativo {s}", $"existente.ativo.{s}@previa.teste.com", new DateOnly(2020, 1, 6));
        await SementeDoCadastro.DetalhesAsync(api.Conexao, ativo, cpfAtivo);
        var inativo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Existente Inativo {s}", $"existente.inativo.{s}@previa.teste.com", new DateOnly(2019, 1, 7), ativo: false);
        await SementeDoCadastro.DetalhesAsync(api.Conexao, inativo, cpfInativo);

        var resposta = await EnviarAsync(
            ComoRh(api),
            Xlsx(
            [
                Valida(apoio, cpf: cpfAtivo),
                Valida(apoio, email: $"EXISTENTE.ATIVO.{s}@PREVIA.TESTE.COM"),
                Valida(apoio, cpf: cpfInativo),
                Valida(apoio, email: $"existente.inativo.{s}@previa.teste.com"),
            ]));

        Assert.Equal(201, (int)resposta.StatusCode);
        var linhas = (await CorpoAsync(resposta)).GetProperty("linhas");
        Assert.Equal([("CPF", "Já existe um colaborador com este CPF.")], Erros(linhas[0]));
        Assert.Equal([("EMAIL_CORPORATIVO", "Já existe um colaborador com este e-mail.")], Erros(linhas[1]));
        Assert.Equal([("CPF", "Este CPF pertence a um cadastro inativo. Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.")], Erros(linhas[2]));
        Assert.Equal([("EMAIL_CORPORATIVO", "Este e-mail pertence a um cadastro inativo. Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.")], Erros(linhas[3]));
        Assert.DoesNotContain($"Existente Inativo {s}", await resposta.Content.ReadAsStringAsync()); // a prévia não diz de quem é o cadastro
    }

    [Fact]
    public async Task Cargo_setor_e_beneficio_casam_sem_diferenciar_maiusculas_nem_acentos()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio, cargo: $"  ANALISE {apoio.Sufixo} ", setor: $"operacoes {apoio.Sufixo}", beneficios: $"VALE-REFEICAO {apoio.Sufixo}")]));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal("PRONTA", corpo.GetProperty("situacao").GetString());
        Assert.Empty(Erros(corpo.GetProperty("linhas")[0]));
    }

    [Fact]
    public async Task Linhas_em_branco_sao_ignoradas_e_o_numero_da_linha_e_o_do_Excel()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio), null, null, Valida(apoio)]));

        var corpo = await CorpoAsync(resposta);
        Assert.Equal(2, corpo.GetProperty("totalLinhas").GetInt32());
        Assert.Equal([2, 5], corpo.GetProperty("linhas").EnumerateArray().Select(l => l.GetProperty("numeroLinha").GetInt32()));
    }

    [Fact]
    public async Task Quinhentas_linhas_dao_a_previa_com_as_quinhentas()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx(Enumerable.Range(0, 500).Select(_ => Valida(apoio))));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((500, 500, "PRONTA"), (corpo.GetProperty("totalLinhas").GetInt32(), corpo.GetProperty("linhasValidas").GetInt32(), corpo.GetProperty("situacao").GetString()));
        Assert.Equal(500, corpo.GetProperty("linhas").GetArrayLength());
    }

    [Fact]
    public async Task Previas_diferentes_do_mesmo_arquivo_geram_importacoes_diferentes()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var bytes = Xlsx([Valida(apoio)]);
        var antes = await ImportacoesAsync(api);

        var primeira = await CorpoAsync(await EnviarAsync(ComoRh(api), bytes));
        var segunda = await CorpoAsync(await EnviarAsync(ComoRh(api), bytes));

        Assert.NotEqual(primeira.GetProperty("importacaoId").GetInt32(), segunda.GetProperty("importacaoId").GetInt32());
        Assert.Equal(antes + 2, await ImportacoesAsync(api));
        Assert.Equal(2, api.Api.Armazenamento.Gravados.Distinct().Count());
    }

    // ---------------------------------------------------------------- o que fica guardado

    [Fact]
    public async Task Grava_a_importacao_com_totais_validade_de_1_hora_o_RH_que_enviou_e_a_previa_em_JSON()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio), Valida(apoio, cpf: "123.456.789-00"), Valida(apoio)]));

        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("importacaoId").GetInt32();
        Assert.Equal(api.Elenco.Rh.FuncionarioId.ToString(), await TextoDaImportacaoAsync(api, id, "ID_FUNCIONARIO_RH"));
        Assert.Equal("COM_ERROS", await TextoDaImportacaoAsync(api, id, "SITUACAO"));
        Assert.Equal(("3", "2", "1"), (await TextoDaImportacaoAsync(api, id, "TOTAL_LINHAS"), await TextoDaImportacaoAsync(api, id, "LINHAS_VALIDAS"), await TextoDaImportacaoAsync(api, id, "LINHAS_COM_ERRO")));
        Assert.Equal(ElencoDeTeste.Hoje, DateTime.SpecifyKind((DateTime)(await Sql.EscalarAsync(api.Conexao, $"SELECT CRIADA_EM FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"))!, DateTimeKind.Utc));
        Assert.Equal(ElencoDeTeste.Hoje.AddHours(1), DateTime.SpecifyKind((DateTime)(await Sql.EscalarAsync(api.Conexao, $"SELECT EXPIRA_EM FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"))!, DateTimeKind.Utc));
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT CONFIRMADA_EM FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"));
        var guardada = SerializadorDaPrevia.Ler((await TextoDaImportacaoAsync(api, id, "PREVIA"))!);
        Assert.Equal([2, 3, 4], guardada.Select(l => l.NumeroLinha));
        Assert.Equal([SituacaoDaLinha.Ok, SituacaoDaLinha.ComErro, SituacaoDaLinha.Ok], guardada.Select(l => l.Situacao));
        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "CPF inválido.")], guardada[1].Erros);
    }

    [Fact]
    public async Task O_arquivo_vai_para_importacoes_barra_guid_xlsx_e_o_caminho_fica_na_linha()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var bytes = Xlsx([Valida(apoio)]);

        var resposta = await EnviarAsync(ComoRh(api), bytes, "C:/fakepath/../segredos/novos colaboradores.xlsx");

        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("importacaoId").GetInt32();
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Matches(new Regex(@"^importacoes/[0-9a-f]{32}\.xlsx\z"), caminho);
        Assert.Equal(bytes, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal(TipoXlsx, api.Api.Armazenamento.TipoDeConteudo(caminho));
        Assert.Equal(caminho, await TextoDaImportacaoAsync(api, id, "CAMINHO_ARQUIVO"));
        Assert.Equal("novos colaboradores.xlsx", await TextoDaImportacaoAsync(api, id, "NOME_ARQUIVO")); // sem pasta
        Assert.Equal("novos colaboradores.xlsx", corpo.GetProperty("nomeArquivo").GetString());
    }

    [Fact]
    public async Task Falha_ao_gravar_a_linha_apaga_o_blob_e_nao_deixa_linha()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);
        var eventosAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'IMPORTACOES'");
        await using var fabrica = api.Api.ComFalhaNoBancoAoSalvarImportacao();

        var resposta = await EnviarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), Xlsx([Valida(apoio)]));

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain(ImportacoesRepositorioComFalha.TextoDaFalha, await resposta.Content.ReadAsStringAsync());
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal([caminho], api.Api.Armazenamento.Apagados);
        Assert.Equal(0, api.Api.Armazenamento.Quantidade); // o armazenamento termina vazio
        Assert.Equal(antes, await ImportacoesAsync(api)); // a transação desfez a linha
        Assert.Equal(eventosAntes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'IMPORTACOES'")); // e o evento
    }

    [Fact]
    public async Task Falha_do_blob_nao_deixa_linha_nem_evento()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var antes = await ImportacoesAsync(api);
        var eventosAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'IMPORTACOES'");
        api.Api.Armazenamento.FalharNaGravacao = new IOException("armazenamento-fora-do-ar-7777");
        try
        {
            var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio)]));

            Assert.Equal(500, (int)resposta.StatusCode);
            Assert.DoesNotContain("armazenamento-fora-do-ar-7777", await resposta.Content.ReadAsStringAsync());
            Assert.Equal(antes, await ImportacoesAsync(api));
            Assert.Equal(eventosAntes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'IMPORTACOES'"));
        }
        finally
        {
            api.Api.Armazenamento.FalharNaGravacao = null;
            api.Api.Armazenamento.Limpar();
        }
    }

    [Fact]
    public async Task Nenhuma_transacao_fica_aberta_durante_o_envio_do_arquivo()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio)]));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta);
    }

    [Fact]
    public async Task FonteDeReferenciasDasImportacoes_marca_como_referenciados_so_os_caminhos_com_linha()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio)]));
        await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio)]));
        var guardados = api.Api.Armazenamento.Gravados.ToList();
        var semLinha = CaminhosDeArquivo.Novo("importacoes", ".xlsx");

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = Assert.Single(escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>(), f => f.Prefixo == "importacoes/");
        var referenciados = await fonte.ReferenciadosAsync([.. guardados, semLinha], CancellationToken.None);

        Assert.Equal(2, guardados.Count);
        Assert.Equal(guardados.Order(), referenciados.Order());
        Assert.Empty(await fonte.ReferenciadosAsync([semLinha], CancellationToken.None));
    }

    // ---------------------------------------------------------------- privacidade

    [Fact]
    public async Task A_previa_so_entra_na_auditoria_como_totais()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var nome = $"Zeferino Xavante {apoio.Sufixo}";
        var email = $"zeferino.{apoio.Sufixo}@previa.teste.com";
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio, nome: nome, cpf: cpf, email: email), Valida(apoio, cpf: "123.456.789-00")]), $"folha-secreta-{apoio.Sufixo}.xlsx");

        var id = (await CorpoAsync(resposta)).GetProperty("importacaoId").GetInt32();
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "IMPORTACOES", id));
        Assert.Equal((AuditoriaArea.Importacao, AuditoriaAcao.Importou), (evento.Area, evento.Acao));
        Assert.Equal("Enviou uma planilha de importação (2 linhas, 1 com erro)", evento.Descricao);
        Assert.Equal($"Importação {id}", evento.RegistroAfetado);
        Assert.Equal(api.Elenco.Rh.FuncionarioId, evento.FuncionarioAtorId);
        var tudo = await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS");
        foreach (var segredo in new[] { nome, email, cpf, $"folha-secreta-{apoio.Sufixo}" })
        {
            Assert.DoesNotContain(segredo, tudo);
        }
    }

    [Fact]
    public async Task Nenhum_colaborador_e_criado_pela_previa()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS");

        var resposta = await EnviarAsync(ComoRh(api), Xlsx([Valida(apoio), Valida(apoio)]));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(antes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
    }

    [Fact]
    public async Task Os_valores_da_previa_nao_aparecem_no_log()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var logs = new ColetorDeLogs();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        var cliente = api.Api.ClienteComo(fabrica, api.Elenco.Rh);
        var nome = $"Zeferino Xavante {apoio.Sufixo}";
        var email = $"zeferino.{apoio.Sufixo}@previa.teste.com";
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var cargoErrado = $"Cargo-que-nao-existe-{apoio.Sufixo}";

        var resposta = await EnviarAsync(cliente, Xlsx([Valida(apoio, nome: nome, cpf: cpf, email: email, cargo: cargoErrado)]), $"folha-secreta-{apoio.Sufixo}.xlsx");

        Assert.Equal(201, (int)resposta.StatusCode);
        string texto;
        lock (logs.Mensagens)
        {
            texto = string.Join('\n', logs.Mensagens);
        }

        Assert.NotEmpty(texto);
        foreach (var segredo in new[] { nome, email, cpf, cargoErrado, $"folha-secreta-{apoio.Sufixo}" })
        {
            Assert.DoesNotContain(segredo, texto);
        }
    }
}

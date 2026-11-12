using System.IO.Compression;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Infrastructure.Formularios;
using Microsoft.Extensions.DependencyInjection;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>responderFormulario</c> COM NOME e <c>obterMinhasRespostas</c>, no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 locais). Responder grava uma linha por pergunta respondida, todas com o MESMO instante, e NÃO deixa linha na auditoria; os erros de campo vêm juntos num 400 e não gravam nada nem guardam
/// arquivo; uma resposta por pessoa (e por avaliado), também com dois envios ao mesmo tempo; a Sugestão aceita várias, separadas pelo instante; PDF ou Word até 5 MB, o tipo pelo conteúdo; encerrar na hora de responder = 422 e o blob apagado; "ver minhas respostas" devolve só o nome do arquivo.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ResponderComNomeTests(MySqlFixture mysql)
{
    private const string Instante = "2026-10-07 09:42:00";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ResponderComNomeTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private sealed record Pesquisa(int Id, int Curta, int Longa, int Multipla, int Escala, int Sim, int Nao);

    private static async Task<(HttpClient Cliente, int Id)> PessoaAsync(ApiDeAcesso api)
    {
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var (id, _) = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        return (await AdvertenciasDeTeste.ClienteDeAsync(api, id), id);
    }

    private static Task EscolherPessoasAsync(ApiDeAcesso api, int formularioId, params int[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({formularioId}, {p})"))}");

    /// <summary>Uma pesquisa publicada para as pessoas: curta obrigatória, longa opcional, múltipla escolha (Sim/Não) obrigatória e escala obrigatória.</summary>
    private static async Task<Pesquisa> PesquisaAsync(ApiDeAcesso api, string prefixo, params int[] pessoas)
    {
        var id = await SemearAsync(api, Titulo(prefixo), "PUBLICADO", alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "Seu nome?", "RESPOSTA_CURTA", 1, true);
        var longa = await PerguntaAsync(api, id, "Comentários", "RESPOSTA_LONGA", 2, false);
        var multipla = await PerguntaAsync(api, id, "Gostou?", "MULTIPLA_ESCOLHA", 3, true, "Sim", "Não");
        var escala = await PerguntaAsync(api, id, "De 1 a 5", "ESCALA", 4, true);
        await EscolherPessoasAsync(api, id, pessoas);
        var sim = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {multipla} AND ORDEM = 1");
        var nao = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {multipla} AND ORDEM = 2");
        return new Pesquisa(id, curta, longa, multipla, escala, sim, nao);
    }

    private static object VC(Pesquisa p, string texto = "Ana Souza") => new { perguntaId = p.Curta, texto };

    private static object VM(Pesquisa p) => new { perguntaId = p.Multipla, alternativaIds = new[] { p.Sim } };

    private static object VE(Pesquisa p, int valor = 4) => new { perguntaId = p.Escala, valorEscala = valor };

    private static string Json(params object[] itens) => JsonSerializer.Serialize(itens);

    private static MultipartFormDataContent Envio(string? respostas, int? avaliado = null, params (string Nome, byte[] Bytes)[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        if (respostas is null && avaliado is null && arquivos.Length == 0)
        {
            formulario.Add(new StringContent("x"), "outro"); // um multipart sem nenhuma parte é "malformado" (400 do corpo); aqui falta só a parte respostas
        }

        if (respostas is not null)
        {
            formulario.Add(new StringContent(respostas, Encoding.UTF8), "respostas");
        }

        if (avaliado is { } id)
        {
            formulario.Add(new StringContent(id.ToString(System.Globalization.CultureInfo.InvariantCulture)), "avaliadoId");
        }

        foreach (var (nome, bytes) in arquivos)
        {
            formulario.Add(new ByteArrayContent(bytes), "arquivos", nome);
        }

        return formulario;
    }

    private static Task<HttpResponseMessage> ResponderAsync(HttpClient cliente, int formularioId, HttpContent envio) => cliente.PostAsync($"/api/eu/formularios/{formularioId}/respostas", envio);

    private static async Task<JsonElement> ErrosAsync(HttpResponseMessage resposta) => (await CorpoAsync(resposta)).GetProperty("errors");

    private static byte[] Docx(bool comODocumento = true)
    {
        using var memoria = new MemoryStream();
        using (var zip = new ZipArchive(memoria, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entrada = zip.CreateEntry(comODocumento ? "word/document.xml" : "outro/arquivo.txt");
            using var escritor = new StreamWriter(entrada.Open());
            escritor.Write("<w:document/>");
        }

        return memoria.ToArray();
    }

    private static byte[] Doc() => [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[200]];

    private static async Task<long> LinhasAsync(ApiDeAcesso api, int formularioId) => await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {formularioId}");

    private static async Task<long> AuditoriaAsync(ApiDeAcesso api) => await ContarAsync(api, "SELECT (SELECT COUNT(*) FROM AUDITORIA_EVENTOS) + (SELECT COUNT(*) FROM AUDITORIA_MUDANCAS)");

    // ---------------------------------------------------------------- gravar

    [Fact]
    public async Task Responder_grava_uma_linha_por_pergunta_respondida_com_o_mesmo_instante_sem_o_opcional_em_branco_e_sem_auditoria()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp1", eu);
        var auditoriaAntes = await AuditoriaAsync(api);

        var resposta = await ResponderAsync(cliente, p.Id, Envio(Json(VC(p, "  Ana Souza  "), new { perguntaId = p.Longa, texto = "   " }, VM(p), VE(p))));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("responderFormulario");
        var recibo = await CorpoAsync(resposta);
        Assert.Equal((p.Id, JsonValueKind.Null, 0), (recibo.GetProperty("formularioId").GetInt32(), recibo.GetProperty("participacaoId").ValueKind, recibo.GetProperty("restantes").GetInt32()));
        Assert.Equal(3, await LinhasAsync(api, p.Id)); // a longa opcional em branco não gera linha
        Assert.Equal("Ana Souza", await TextoAsync(api, $"SELECT RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {p.Curta}"));
        Assert.Equal("Sim", await TextoAsync(api, $"SELECT RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {p.Multipla}"));
        Assert.Equal(p.Sim.ToString(), await TextoAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {p.Multipla}"));
        Assert.Equal("4", await TextoAsync(api, $"SELECT RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {p.Escala}"));
        Assert.Equal($"1,{eu}", await TextoAsync(api, $"SELECT CONCAT(COUNT(DISTINCT ID_FUNCIONARIO), ',', MIN(ID_FUNCIONARIO)) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {p.Id}")); // todas as linhas são da pessoa logada
        Assert.Equal("0", await TextoAsync(api, $"SELECT SUM(ANONIMO) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {p.Id}"));
        Assert.Equal("1", await TextoAsync(api, $"SELECT COUNT(DISTINCT DATA_RESPOSTA) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {p.Id}"));
        Assert.Equal(Instante, await TextoAsync(api, $"SELECT DATE_FORMAT(MIN(DATA_RESPOSTA), '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {p.Id}"));
        Assert.Equal(auditoriaAntes, await AuditoriaAsync(api)); // responder com nome não grava evento nem mudança
    }

    [Fact]
    public async Task Cada_erro_de_campo_vira_um_400_com_a_chave_certa_e_nada_e_gravado_nem_guardado()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp2", eu);
        var outra = await PesquisaAsync(api, "Zresp2 outra", eu);
        var longo = new string('a', 201);
        var muitoLongo = new string('b', 4001);
        var guardadosAntes = api.Api.Armazenamento.Gravados.Count;
        (string Nome, string? Respostas, int? Avaliado, string Chave)[] casos =
        [
            ("curta com 201 caracteres", Json(VC(p, longo), VM(p), VE(p)), null, "respostas[0].texto"),
            ("longa com 4001 caracteres", Json(new { perguntaId = p.Longa, texto = muitoLongo }, VC(p), VM(p), VE(p)), null, "respostas[0].texto"),
            ("texto que não é texto", Json(new { perguntaId = p.Curta, texto = 12 }, VM(p), VE(p)), null, "respostas[0].texto"),
            ("escala 6", Json(VE(p, 6), VC(p), VM(p)), null, "respostas[0].valorEscala"),
            ("escala 0", Json(VE(p, 0), VC(p), VM(p)), null, "respostas[0].valorEscala"),
            ("escala em texto", Json(new { perguntaId = p.Escala, valorEscala = "4" }, VC(p), VM(p)), null, "respostas[0].valorEscala"),
            ("alternativa que não existe", Json(new { perguntaId = p.Multipla, alternativaIds = new[] { 99999999 } }, VC(p), VE(p)), null, "respostas[0].alternativaIds"),
            ("duas alternativas", Json(new { perguntaId = p.Multipla, alternativaIds = new[] { p.Sim, p.Nao } }, VC(p), VE(p)), null, "respostas[0].alternativaIds"),
            ("campo de outro tipo na curta", Json(new { perguntaId = p.Curta, texto = "Ana", valorEscala = 3 }, VM(p), VE(p)), null, "respostas[0].valorEscala"),
            ("arquivoIndice numa pergunta de texto", Json(new { perguntaId = p.Curta, texto = "Ana", arquivoIndice = 0 }, VM(p), VE(p)), null, "respostas[0].arquivoIndice"),
            ("pergunta de outro formulário", Json(new { perguntaId = outra.Curta, texto = "Ana" }, VC(p), VM(p), VE(p)), null, "respostas[0].perguntaId"),
            ("pergunta sem id", Json(new { texto = "Ana" }, VC(p), VM(p), VE(p)), null, "respostas[0].perguntaId"),
            ("a mesma pergunta duas vezes", Json(VC(p), VC(p, "Outro"), VM(p), VE(p)), null, "respostas[1].perguntaId"),
            ("obrigatória fora da lista", Json(VC(p), VM(p)), null, "respostas"),
            ("obrigatória em branco", Json(VC(p, "   "), VM(p), VE(p)), null, "respostas[0].texto"),
            ("lista vazia", "[]", null, "respostas"),
            ("JSON quebrado", "isto não é json", null, "respostas"),
            ("objeto em vez de lista", "{}", null, "respostas"),
            ("sem a parte respostas", null, null, "respostas"),
            ("avaliadoId em formulário que não é avaliação", Json(VC(p), VM(p), VE(p)), 5, "avaliadoId"),
        ];

        var falhas = new List<string>();
        foreach (var (nome, respostas, avaliado, chave) in casos)
        {
            var resposta = await ResponderAsync(cliente, p.Id, Envio(respostas, avaliado));
            if ((int)resposta.StatusCode != 400 || !(await ErrosAsync(resposta)).TryGetProperty(chave, out _))
            {
                falhas.Add($"{nome}: {(int)resposta.StatusCode} {await resposta.Content.ReadAsStringAsync()}");
            }
        }

        var arquivoSolto = await ResponderAsync(cliente, p.Id, Envio(Json(VC(p), VM(p), VE(p)), null, ("solto.pdf", ArquivosDeTeste.Pdf())));
        if ((int)arquivoSolto.StatusCode != 400 || !(await ErrosAsync(arquivoSolto)).TryGetProperty("arquivos", out _))
        {
            falhas.Add("arquivo que nenhuma resposta usa");
        }

        Assert.Empty(falhas);
        Assert.Equal(0, await LinhasAsync(api, p.Id));
        Assert.Equal(guardadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    [Fact]
    public async Task Os_erros_de_campo_vem_juntos_num_400_so()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp3", eu);

        var resposta = await ResponderAsync(cliente, p.Id, Envio(Json(VE(p, 9), new { perguntaId = p.Curta, texto = new string('a', 300) })));
        var erros = await ErrosAsync(resposta);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True(erros.TryGetProperty("respostas[0].valorEscala", out _));
        Assert.True(erros.TryGetProperty("respostas[1].texto", out _));
        Assert.True(erros.TryGetProperty("respostas", out _)); // a múltipla escolha obrigatória que faltou
    }

    [Fact]
    public async Task Responder_de_novo_e_409_JA_RESPONDIDO_e_dois_envios_ao_mesmo_tempo_dao_um_201_e_um_409_sem_linha_repetida()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp4", eu);
        var corpo = Json(VC(p), VM(p), VE(p));

        var juntas = await Task.WhenAll(ResponderAsync(cliente, p.Id, Envio(corpo)), ResponderAsync(cliente, p.Id, Envio(corpo)));
        var depois = await ResponderAsync(cliente, p.Id, Envio(corpo));

        Assert.Equal([201, 409], juntas.Select(r => (int)r.StatusCode).Order());
        var conflito = await CorpoAsync(juntas.Single(r => (int)r.StatusCode == 409));
        Assert.Equal(("JA_RESPONDIDO", "Você já respondeu este formulário."), (Codigo(conflito), conflito.GetProperty("detail").GetString()));
        Assert.Equal(409, (int)depois.StatusCode);
        Assert.Equal(3, await LinhasAsync(api, p.Id)); // uma resposta só: três perguntas, três linhas
    }

    [Fact]
    public async Task Cada_pessoa_responde_a_sua_vez_e_o_formulario_de_outra_pessoa_e_404()
    {
        var api = await ApiAsync();
        var (clienteA, a) = await PessoaAsync(api);
        var (clienteB, b) = await PessoaAsync(api);
        var (clienteC, _) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp5", a, b);
        var corpo = Json(VC(p), VM(p), VE(p));

        var ra = await ResponderAsync(clienteA, p.Id, Envio(corpo));
        var rb = await ResponderAsync(clienteB, p.Id, Envio(corpo));
        var rc = await ResponderAsync(clienteC, p.Id, Envio(corpo));

        Assert.Equal((201, 201, 404), ((int)ra.StatusCode, (int)rb.StatusCode, (int)rc.StatusCode));
        Assert.Equal(6, await LinhasAsync(api, p.Id));
    }

    [Fact]
    public async Task Qualquer_perfil_responde_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var p = await PesquisaAsync(api, "Zresp6", api.Elenco.Gestor.FuncionarioId, api.Elenco.Rh.FuncionarioId);
        var corpo = Json(VC(p), VM(p), VE(p));

        var gestor = await ResponderAsync(api.Api.ClienteComo(api.Elenco.Gestor), p.Id, Envio(corpo));
        var rh = await ResponderAsync(api.Api.ClienteComo(api.Elenco.Rh), p.Id, Envio(corpo));
        var anonimo = await ResponderAsync(api.Api.ClienteAnonimo(), p.Id, Envio(corpo));

        Assert.Equal((201, 201, 401), ((int)gestor.StatusCode, (int)rh.StatusCode, (int)anonimo.StatusCode));
    }

    [Fact]
    public async Task Pessoa_inativa_com_o_token_ainda_valido_e_404_se_nunca_respondeu_e_422_FUNCIONARIO_INATIVO_se_ja_tinha_resposta_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var (nova, novaId) = await PessoaAsync(api);
        var (antiga, antigaId) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp6b", novaId, antigaId);
        await FormulariosDeTeste.ResponderAsync(api, p.Id, p.Curta, antigaId); // a pessoa antiga já tinha respondido (direto no banco)
        await AdvertenciasDeTeste.InativarAsync(api, novaId);
        await AdvertenciasDeTeste.InativarAsync(api, antigaId);
        var corpo = Json(VC(p), VM(p), VE(p));

        var daNova = await ResponderAsync(nova, p.Id, Envio(corpo));
        var daAntiga = await ResponderAsync(antiga, p.Id, Envio(corpo));

        Assert.Equal(404, (int)daNova.StatusCode); // quem está inativo deixou de ser do público: não há formulário para ela
        Assert.Equal(422, (int)daAntiga.StatusCode);
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(daAntiga)));
        Assert.Equal(1, await LinhasAsync(api, p.Id)); // só a linha que já existia
    }

    // ---------------------------------------------------------------- situação do formulário

    [Fact]
    public async Task Formulario_que_nao_existe_rascunho_por_link_ou_de_outra_pessoa_e_404_e_agendado_e_encerrado_sao_422()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (_, outro) = await PessoaAsync(api);
        async Task<int> NovoAsync(string situacao, string? alvo = "PESSOAS", string? token = null, string? prazo = null, params int[] pessoas)
        {
            var id = await SemearAsync(api, Titulo("Zresp7"), situacao, alvo: alvo, token: token, prazo: prazo);
            await PerguntaAsync(api, id, "Seu nome?");
            if (pessoas.Length > 0)
            {
                await EscolherPessoasAsync(api, id, pessoas);
            }

            return id;
        }

        var rascunho = await NovoAsync("RASCUNHO", pessoas: eu);
        var porLink = await NovoAsync("PUBLICADO", "LINK", new string('q', 43));
        var deOutro = await NovoAsync("PUBLICADO", pessoas: outro);
        var agendado = await NovoAsync("AGENDADO", pessoas: eu);
        var encerrado = await NovoAsync("ENCERRADO", pessoas: eu);
        var vencido = await NovoAsync("PUBLICADO", prazo: "2026-10-06", pessoas: eu);
        async Task<HttpResponseMessage> EnviarAsync(int id) => await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {id}"), texto = "Ana" })));

        var inexistente = await ResponderAsync(cliente, 99999999, Envio(Json(new { perguntaId = 1, texto = "Ana" })));
        var situacoes = new Dictionary<string, HttpResponseMessage>
        {
            ["rascunho"] = await EnviarAsync(rascunho),
            ["por link"] = await EnviarAsync(porLink),
            ["de outra pessoa"] = await EnviarAsync(deOutro),
            ["agendado"] = await EnviarAsync(agendado),
            ["encerrado"] = await EnviarAsync(encerrado),
            ["prazo vencido com a tarefa parada"] = await EnviarAsync(vencido),
        };

        Assert.Equal(404, (int)inexistente.StatusCode);
        Assert.Equal([404, 404, 404, 422, 422, 422], situacoes.Values.Select(r => (int)r.StatusCode));
        Assert.Equal("FORMULARIO_AINDA_NAO_ABERTO", Codigo(await CorpoAsync(situacoes["agendado"])));
        Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(situacoes["encerrado"])));
        Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(situacoes["prazo vencido com a tarefa parada"])));
        foreach (var id in new[] { rascunho, porLink, deOutro, agendado, encerrado, vencido })
        {
            Assert.Equal(0, await LinhasAsync(api, id));
        }

    }

    [Fact]
    public async Task Se_o_formulario_e_encerrado_na_hora_de_responder_e_422_nada_e_gravado_e_o_arquivo_guardado_e_apagado()
    {
        var api = await ApiAsync();
        var rh = api.Elenco.Rh.FuncionarioId;
        var id = await SemearAsync(api, Titulo("Zresp8"), "PUBLICADO", alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "Seu nome?", "RESPOSTA_CURTA", 1, true);
        var anexo = await PerguntaAsync(api, id, "Comprovante", "ARQUIVO", 2);
        await EscolherPessoasAsync(api, id, rh);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {id}"));

        var resposta = await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = curta, texto = "Ana" }, new { perguntaId = anexo, arquivoIndice = 0 }), null, ("comprovante.pdf", ArquivosDeTeste.Pdf())));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal(0, await LinhasAsync(api, id));
        var guardados = api.Api.Armazenamento.Gravados.Skip(gravadosAntes).ToList();
        var caminho = Assert.Single(guardados); // o arquivo FOI guardado (a receita grava antes da transação)...
        Assert.False(api.Api.Armazenamento.Existe(caminho)); // ...e foi apagado quando a gravação recusou
        Assert.Contains(caminho, api.Api.Armazenamento.Apagados);
    }

    // ---------------------------------------------------------------- avaliação e Sugestão

    [Fact]
    public async Task Na_avaliacao_da_equipe_o_avaliado_e_obrigatorio_tem_de_ser_da_equipe_e_cada_um_e_avaliado_uma_vez_com_o_restantes_certo()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var estranho = await AdvertenciasDeTeste.NovoMembroAsync(api, await AdvertenciasDeTeste.NovoSetorAsync(api));
        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("Zresp9 avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        var corpo = Json(new { perguntaId = nota, valorEscala = 5 });

        var semAvaliado = await ResponderAsync(gestor, id, Envio(corpo));
        var foraDaEquipe = await ResponderAsync(gestor, id, Envio(corpo, estranho.Id));
        var primeira = await ResponderAsync(gestor, id, Envio(corpo, m1.Id));
        var repetida = await ResponderAsync(gestor, id, Envio(corpo, m1.Id));
        var segunda = await ResponderAsync(gestor, id, Envio(corpo, m2.Id));

        Assert.Equal((400, 422, 201, 409, 201), ((int)semAvaliado.StatusCode, (int)foraDaEquipe.StatusCode, (int)primeira.StatusCode, (int)repetida.StatusCode, (int)segunda.StatusCode));
        Assert.True((await ErrosAsync(semAvaliado)).TryGetProperty("avaliadoId", out _));
        Assert.Equal(("AVALIADO_INVALIDO", "Você não deve avaliar esta pessoa neste formulário."), (Codigo(await CorpoAsync(foraDaEquipe)), (await CorpoAsync(foraDaEquipe)).GetProperty("detail").GetString()));
        Assert.Equal(1, (await CorpoAsync(primeira)).GetProperty("restantes").GetInt32());
        Assert.Equal(0, (await CorpoAsync(segunda)).GetProperty("restantes").GetInt32());
        Assert.Equal(2, await LinhasAsync(api, id));
        Assert.Equal($"{m1.Id},{m2.Id}", await TextoAsync(api, $"SELECT GROUP_CONCAT(ID_AVALIADO ORDER BY ID_AVALIADO) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Na_avaliacao_do_gestor_cada_pessoa_da_equipe_avalia_o_gestor_uma_vez()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, membro.Id);
        var id = await SemearAsync(api, Titulo("Zresp10 avaliação do gestor"), "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor.SetorId})");
        var corpo = Json(new { perguntaId = nota, valorEscala = 3 });

        var primeira = await ResponderAsync(cliente, id, Envio(corpo, setor.GestorFuncionarioId));
        var repetida = await ResponderAsync(cliente, id, Envio(corpo, setor.GestorFuncionarioId));

        Assert.Equal((201, 409), ((int)primeira.StatusCode, (int)repetida.StatusCode));
        Assert.Equal(0, (await CorpoAsync(primeira)).GetProperty("restantes").GetInt32());
        Assert.Equal(setor.GestorFuncionarioId.ToString(), await TextoAsync(api, $"SELECT ID_AVALIADO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task A_Sugestao_aceita_varios_envios_da_mesma_pessoa_ate_no_mesmo_segundo_cada_um_com_o_seu_instante()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var id = await SemearAsync(api, Titulo("Zresp11 sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var ideia = await PerguntaAsync(api, id, "Sua ideia", "RESPOSTA_LONGA", 1, true);
        await EscolherPessoasAsync(api, id, eu);

        var envios = new List<HttpResponseMessage>();
        for (var i = 1; i <= 3; i++)
        {
            envios.Add(await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = ideia, texto = $"Ideia {i}" }))));
        }

        Assert.All(envios, r => Assert.Equal(201, (int)r.StatusCode));
        Assert.Equal(3, await LinhasAsync(api, id));
        Assert.Equal("2026-10-07 09:42:00,2026-10-07 09:42:01,2026-10-07 09:42:02", await TextoAsync(api, $"SELECT GROUP_CONCAT(DATE_FORMAT(DATA_RESPOSTA, '%Y-%m-%d %H:%i:%s') ORDER BY DATA_RESPOSTA) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"));
    }

    // ---------------------------------------------------------------- arquivos

    private static async Task<(int Id, int Nome, int Anexo, int Outro)> ComArquivosAsync(ApiDeAcesso api, params int[] pessoas)
    {
        var id = await SemearAsync(api, Titulo("Zresp12 admissão"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var nome = await PerguntaAsync(api, id, "Seu nome?", "RESPOSTA_CURTA", 1, true);
        var anexo = await PerguntaAsync(api, id, "Comprovante de residência", "ARQUIVO", 2);
        var outro = await PerguntaAsync(api, id, "Outro documento", "ARQUIVO", 3);
        await EscolherPessoasAsync(api, id, pessoas);
        return (id, nome, anexo, outro);
    }

    [Fact]
    public async Task Pergunta_de_arquivo_guarda_o_blob_na_pasta_formularios_e_a_linha_leva_so_o_nome_limpo_aceitando_PDF_docx_e_doc()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (id, nome, anexo, outro) = await ComArquivosAsync(api, eu);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        var comTransacaoAntes = api.Api.Armazenamento.GravacoesComTransacaoAberta;
        var pdf = ArquivosDeTeste.Pdf(2048);

        var resposta = await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = nome, texto = "Ana" }, new { perguntaId = anexo, arquivoIndice = 1 }, new { perguntaId = outro, arquivoIndice = 0 }), null, ("../../etc/outro.docx", Docx()), ("meu comprovante.pdf", pdf)));

        Assert.Equal(201, (int)resposta.StatusCode);
        var novos = api.Api.Armazenamento.Gravados.Skip(gravadosAntes).ToList();
        Assert.Equal(2, novos.Count);
        Assert.All(novos, c => Assert.True(CaminhosDeArquivo.EhValido(c) && c.StartsWith("formularios/", StringComparison.Ordinal), c));
        var caminhoDoAnexo = await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {anexo}");
        var caminhoDoOutro = await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {outro}");
        Assert.EndsWith(".pdf", caminhoDoAnexo, StringComparison.Ordinal);
        Assert.EndsWith(".docx", caminhoDoOutro, StringComparison.Ordinal);
        Assert.Equal(pdf, api.Api.Armazenamento.Conteudo(caminhoDoAnexo!));
        Assert.Equal("application/pdf", api.Api.Armazenamento.TipoDeConteudo(caminhoDoAnexo!));
        Assert.Equal("meu comprovante.pdf", await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {anexo}"));
        Assert.Equal("meu comprovante.pdf", await TextoAsync(api, $"SELECT RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {anexo}"));
        Assert.Equal("outro.docx", await TextoAsync(api, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {outro}")); // sem o diretório do nome
        Assert.Equal(comTransacaoAntes, api.Api.Armazenamento.GravacoesComTransacaoAberta); // o blob é gravado FORA de qualquer transação
    }

    [Fact]
    public async Task Arquivo_Word_antigo_doc_e_aceito_pelo_conteudo()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (id, nome, anexo, _) = await ComArquivosAsync(api, eu);

        var resposta = await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = nome, texto = "Ana" }, new { perguntaId = anexo, arquivoIndice = 0 }), null, ("ficha.doc", Doc())));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.EndsWith(".doc", await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {anexo}"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Arquivo_grande_de_mais_de_outro_tipo_zip_renomeado_ou_vazio_e_400_em_arquivos_sem_gravar_nada()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (id, nome, anexo, _) = await ComArquivosAsync(api, eu);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;
        (string Nome, string NomeDoArquivo, byte[] Bytes)[] casos =
        [
            ("6 MB", "grande.pdf", ArquivosDeTeste.Pdf((5 * 1024 * 1024) + 1)),
            ("PNG", "foto.png", ArquivosDeTeste.Png()),
            ("JPEG", "foto.jpg", ArquivosDeTeste.Jpeg()),
            ("executável chamado .docx", "virus.docx", ArquivosDeTeste.Exe()),
            ("zip qualquer renomeado .docx", "planilha.docx", Docx(comODocumento: false)),
            ("vazio", "vazio.pdf", []),
            ("texto chamado .pdf", "texto.pdf", ArquivosDeTeste.Texto()),
        ];

        var falhas = new List<string>();
        foreach (var (rotulo, nomeDoArquivo, bytes) in casos)
        {
            var resposta = await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = nome, texto = "Ana" }, new { perguntaId = anexo, arquivoIndice = 0 }), null, (nomeDoArquivo, bytes)));
            if ((int)resposta.StatusCode != 400 || !(await ErrosAsync(resposta)).TryGetProperty("arquivos", out _))
            {
                falhas.Add($"{rotulo}: {(int)resposta.StatusCode}");
            }
        }

        Assert.Empty(falhas);
        Assert.Equal(0, await LinhasAsync(api, id));
        Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    // ---------------------------------------------------------------- minhas respostas

    [Fact]
    public async Task Minhas_respostas_devolve_toda_pergunta_em_ordem_com_o_campo_do_tipo_o_opcional_em_branco_vazio_e_o_arquivo_so_com_o_nome()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var id = await SemearAsync(api, Titulo("Zresp13 admissão"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "Seu nome?", "RESPOSTA_CURTA", 1, true);
        var longa = await PerguntaAsync(api, id, "Comentários", "RESPOSTA_LONGA", 2);
        var multipla = await PerguntaAsync(api, id, "Estado civil", "MULTIPLA_ESCOLHA", 3, true, "Solteiro", "Casado");
        var escala = await PerguntaAsync(api, id, "Nota", "ESCALA", 4, true);
        var anexo = await PerguntaAsync(api, id, "Comprovante", "ARQUIVO", 5);
        await EscolherPessoasAsync(api, id, eu);
        var casado = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {multipla} AND ORDEM = 2");
        await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = curta, texto = "Ana" }, new { perguntaId = multipla, alternativaIds = new[] { casado } }, new { perguntaId = escala, valorEscala = 5 }, new { perguntaId = anexo, arquivoIndice = 0 }), null, ("residencia.pdf", ArquivosDeTeste.Pdf())));

        var resposta = await cliente.GetAsync($"/api/eu/formularios/{id}/minhas-respostas");
        var corpo = await CorpoAsync(resposta);
        var itens = corpo.GetProperty("respostas").EnumerateArray().ToList();

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterMinhasRespostas");
        Assert.Equal((id, JsonValueKind.Null, "2026-10-07T12:42:00Z"), (corpo.GetProperty("formularioId").GetInt32(), corpo.GetProperty("avaliado").ValueKind, corpo.GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal([curta, longa, multipla, escala, anexo], itens.Select(i => i.GetProperty("perguntaId").GetInt32()));
        Assert.Equal("Ana", itens[0].GetProperty("texto").GetString());
        Assert.Equal((JsonValueKind.Null, 0, JsonValueKind.Null, JsonValueKind.Null), (itens[1].GetProperty("texto").ValueKind, itens[1].GetProperty("alternativas").GetArrayLength(), itens[1].GetProperty("valorEscala").ValueKind, itens[1].GetProperty("arquivo").ValueKind)); // a longa opcional em branco
        Assert.Equal([("Casado", 2)], itens[2].GetProperty("alternativas").EnumerateArray().Select(a => (a.GetProperty("texto").GetString()!, a.GetProperty("ordem").GetInt32())));
        Assert.Equal(5, itens[3].GetProperty("valorEscala").GetInt32());
        Assert.Equal("residencia.pdf", itens[4].GetProperty("arquivo").GetProperty("nome").GetString());
        Assert.DoesNotContain("formularios/", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal); // o caminho nunca sai da API
    }

    [Fact]
    public async Task Minhas_respostas_de_uma_avaliacao_pede_o_avaliado_e_devolve_o_resumo_dele()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("Zresp14 avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        await ResponderAsync(gestor, id, Envio(Json(new { perguntaId = nota, valorEscala = 2 }), m1.Id));
        await ResponderAsync(gestor, id, Envio(Json(new { perguntaId = nota, valorEscala = 5 }), m2.Id));

        var doPrimeiro = await CorpoAsync(await gestor.GetAsync($"/api/eu/formularios/{id}/minhas-respostas?avaliadoId={m1.Id}"));
        var semAvaliado = await gestor.GetAsync($"/api/eu/formularios/{id}/minhas-respostas");
        var forasDaEquipe = await gestor.GetAsync($"/api/eu/formularios/{id}/minhas-respostas?avaliadoId={setor.GestorFuncionarioId}");

        Assert.Equal((m1.Id, m1.Nome), (doPrimeiro.GetProperty("avaliado").GetProperty("id").GetInt32(), doPrimeiro.GetProperty("avaliado").GetProperty("nome").GetString()));
        Assert.Equal(2, doPrimeiro.GetProperty("respostas")[0].GetProperty("valorEscala").GetInt32());
        Assert.Equal(400, (int)semAvaliado.StatusCode);
        Assert.True((await ErrosAsync(semAvaliado)).TryGetProperty("avaliadoId", out _));
        Assert.Equal(404, (int)forasDaEquipe.StatusCode); // não há avaliação dele
    }

    [Fact]
    public async Task Minhas_respostas_e_404_para_quem_nao_respondeu_422_no_anonimo_e_na_Sugestao_e_400_com_avaliado_fora_da_avaliacao()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var p = await PesquisaAsync(api, "Zresp15", eu);
        var anonimo = await SemearAsync(api, Titulo("Zresp15 anônimo"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        await PerguntaAsync(api, anonimo, "Como foi?");
        await EscolherPessoasAsync(api, anonimo, eu);
        var sugestao = await SemearAsync(api, Titulo("Zresp15 sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await PerguntaAsync(api, sugestao, "Sua ideia");
        await EscolherPessoasAsync(api, sugestao, eu);
        var rascunho = await SemearAsync(api, Titulo("Zresp15 rascunho"), alvo: "PESSOAS");

        var semResposta = await cliente.GetAsync($"/api/eu/formularios/{p.Id}/minhas-respostas");
        var comAvaliado = await cliente.GetAsync($"/api/eu/formularios/{p.Id}/minhas-respostas?avaliadoId=5");
        var doAnonimo = await cliente.GetAsync($"/api/eu/formularios/{anonimo}/minhas-respostas");
        var daSugestao = await cliente.GetAsync($"/api/eu/formularios/{sugestao}/minhas-respostas");
        var doRascunho = await cliente.GetAsync($"/api/eu/formularios/{rascunho}/minhas-respostas");
        var inexistente = await cliente.GetAsync("/api/eu/formularios/99999999/minhas-respostas");
        var semLogin = await api.Api.ClienteAnonimo().GetAsync($"/api/eu/formularios/{p.Id}/minhas-respostas");

        Assert.Equal([404, 400, 422, 422, 404, 404, 401], new[] { semResposta, comAvaliado, doAnonimo, daSugestao, doRascunho, inexistente, semLogin }.Select(r => (int)r.StatusCode));
        Assert.Equal("FORMULARIO_ANONIMO", Codigo(await CorpoAsync(doAnonimo)));
        Assert.Equal("FORMULARIO_SUGESTAO", Codigo(await CorpoAsync(daSugestao)));
    }

    // ---------------------------------------------------------------- a limpeza de sobras não apaga o que o banco referencia

    [Fact]
    public async Task A_fonte_de_referencias_do_prefixo_formularios_reconhece_o_arquivo_de_resposta_e_o_curriculo_e_nao_o_que_sobrou()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var (id, nome, anexo, _) = await ComArquivosAsync(api, eu);
        await ResponderAsync(cliente, id, Envio(Json(new { perguntaId = nome, texto = "Ana" }, new { perguntaId = anexo, arquivoIndice = 0 }), null, ("a.pdf", ArquivosDeTeste.Pdf())));
        var guardado = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {anexo}"))!;
        var sobra = CaminhosDeArquivo.Novo("formularios", "pdf");
        using var escopo = api.Api.Services.CreateScope();

        var fontes = escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>().Where(f => f.Prefixo == "formularios/").ToList();
        var referenciados = await Assert.Single(fontes).ReferenciadosAsync([guardado, sobra], CancellationToken.None);

        Assert.IsType<FonteDeReferenciasDosFormularios>(fontes[0]);
        Assert.Equal([guardado], referenciados);
    }
}

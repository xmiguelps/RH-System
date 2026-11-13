using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.CandidaturasDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// As telas do RH sobre os candidatos de um formulário por link, no banco REAL com o relógio em 2026-10-07T12:42Z (<c>listarCandidatosFormulario</c>, <c>obterRespostasDeCandidato</c>, <c>baixarCurriculoDeCandidato</c> e a exportação do formulário por link): só o RH
/// (gestor e funcionário 403, sem login 401); só formulário por link (422 <c>FORMULARIO_SEM_LINK</c> nos outros); candidato de OUTRO formulário é 404; mais recentes primeiro; a busca olha nome e e-mail; o currículo baixa como anexo
/// <c>curriculo-&lt;nome&gt;.&lt;ext&gt;</c> e cada link pedido grava o evento <c>BAIXOU</c> sem nome nem e-mail; o caminho do armazenamento nunca sai.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CandidatosDoRhTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(CandidatosDoRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        return api;
    }

    private static string RotaDaLista(int formulario, string consulta = "") => $"/api/rh/formularios/{formulario}/candidatos{(consulta.Length == 0 ? "" : "?" + consulta)}";

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, int formulario, string consulta = "") => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync(RotaDaLista(formulario, consulta)));

    private static string[] Nomes(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("nome").GetString()!)];

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_vem_com_os_mais_recentes_primeiro_e_os_dados_de_cada_candidato()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl1");
        await CandidatarAsync(api, e, "ZCl1 Caio Almeida", "caio.almeida@email.com.br", telefone: null);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddMinutes(30));
        var mariana = await CandidatarAsync(api, e, "ZCl1 Mariana Teixeira", "mariana.teixeira@email.com.br", "(11) 98877-1204", ("cv.pdf", ArquivosDeTeste.Pdf()));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddMinutes(60));
        var zilda = await CandidatarAsync(api, e, "ZCl1 Zilda Prado", "zilda@email.com.br");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(RotaDaLista(e.Id));
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarCandidatosFormulario");
        Assert.Equal(["ZCl1 Zilda Prado", "ZCl1 Mariana Teixeira", "ZCl1 Caio Almeida"], Nomes(lista));
        Assert.Equal((1, 20, 3, 1), (lista.GetProperty("pagina").GetInt32(), lista.GetProperty("tamanhoPagina").GetInt32(), lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32()));
        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal([zilda, mariana], itens.Take(2).Select(i => i.GetProperty("id").GetInt32()));
        Assert.Equal(("mariana.teixeira@email.com.br", "(11) 98877-1204", true, "2026-10-07T13:12:00Z"), (itens[1].GetProperty("email").GetString(), itens[1].GetProperty("telefone").GetString(), itens[1].GetProperty("temCurriculo").GetBoolean(), itens[1].GetProperty("respondidoEmUtc").GetString()));
        Assert.Equal((JsonValueKind.Null, false), (itens[2].GetProperty("telefone").ValueKind, itens[2].GetProperty("temCurriculo").GetBoolean())); // sem telefone e sem currículo
        Assert.DoesNotContain(itens, i => i.TryGetProperty("caminhoCurriculo", out _) || i.EnumerateObject().Any(p => p.Value.ToString().StartsWith("formularios/", StringComparison.Ordinal))); // o caminho do armazenamento nunca sai
    }

    [Fact]
    public async Task A_lista_pagina_e_a_busca_acha_pelo_nome_ou_pelo_email_sem_diferenciar_caixa_e_trata_o_percentual_como_texto()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl2");
        foreach (var (nome, email) in new[] { ("ZCl2 Ana Beatriz", "ana@alfa.com.br"), ("ZCl2 Bruno Alves", "bruno@beta.com.br"), ("ZCl2 Carla Dias", "carla.alfa@gama.com.br") })
        {
            api.Api.Relogio.Fixar(api.Api.Relogio.AgoraUtc.AddMinutes(1));
            await CandidatarAsync(api, e, nome, email);
        }

        var primeira = await ListarAsync(api, e.Id, "tamanhoPagina=2");
        var segunda = await ListarAsync(api, e.Id, "tamanhoPagina=2&pagina=2");
        var alem = await ListarAsync(api, e.Id, "tamanhoPagina=2&pagina=9");
        var porNome = await ListarAsync(api, e.Id, "busca=BRUNO");
        var porEmail = await ListarAsync(api, e.Id, "busca=ALFA");
        var percentual = await ListarAsync(api, e.Id, "busca=%25");
        var semAchar = await ListarAsync(api, e.Id, "busca=zzzz");

        Assert.Equal(["ZCl2 Carla Dias", "ZCl2 Bruno Alves"], Nomes(primeira));
        Assert.Equal((3, 2), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(["ZCl2 Ana Beatriz"], Nomes(segunda));
        Assert.Empty(alem.GetProperty("itens").EnumerateArray());
        Assert.Equal(["ZCl2 Bruno Alves"], Nomes(porNome));
        Assert.Equal(["ZCl2 Carla Dias", "ZCl2 Ana Beatriz"], Nomes(porEmail)); // "alfa" está no e-mail de dois deles
        Assert.Equal(0, percentual.GetProperty("total").GetInt32()); // o % não vira curinga
        Assert.Equal(0, semAchar.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_lista_e_so_do_formulario_por_link_e_recusa_o_que_nao_e_com_422_ou_404_e_400_nos_parametros()
    {
        var api = await ApiAsync();
        var porLink = await EntrevistaAsync(api, "ZCl3");
        var dePessoas = await SemearAsync(api, Titulo("ZCl3 pessoas"), "PUBLICADO", alvo: "PESSOAS");
        var rh = await api.ClienteDoRhAgoraAsync();

        var vazia = await rh.GetAsync(RotaDaLista(porLink.Id));
        var semLink = await rh.GetAsync(RotaDaLista(dePessoas));
        var inexistente = await rh.GetAsync(RotaDaLista(99999999));
        var paginaZero = await rh.GetAsync(RotaDaLista(porLink.Id, "pagina=0&tamanhoPagina=101"));

        Assert.Equal([200, 422, 404, 400], new[] { vazia, semLink, inexistente, paginaZero }.Select(r => (int)r.StatusCode));
        Assert.Equal(0, (await CorpoAsync(vazia)).GetProperty("total").GetInt32());
        var recusa = await CorpoAsync(semLink);
        Assert.Equal(("FORMULARIO_SEM_LINK", "Este formulário não é respondido por link. Veja a lista de funcionários."), (Codigo(recusa), recusa.GetProperty("detail").GetString()));
        var erros = (await CorpoAsync(paginaZero)).GetProperty("errors");
        Assert.True(erros.TryGetProperty("pagina", out _) && erros.TryGetProperty("tamanhoPagina", out _));
    }

    // ---------------------------------------------------------------- as respostas de um candidato

    [Fact]
    public async Task O_RH_ve_as_respostas_do_candidato_pelas_perguntas_do_formulario_com_o_arquivo_so_por_id_e_nome()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl4");
        var candidato = await CandidatarAsync(api, e, "ZCl4 Mariana Teixeira", "mariana.teixeira@email.com.br", "(11) 98877-1204", ("cv.pdf", ArquivosDeTeste.Pdf()), true, ("../comprovante.pdf", ArquivosDeTeste.Pdf(1024)));
        var arquivoDaResposta = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_PERGUNTA = {e.Anexo}");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{e.Id}/respostas/candidatos/{candidato}");
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterRespostasDeCandidato");
        Assert.Equal((e.Id, "ZCl4 Mariana Teixeira"), (corpo.GetProperty("formularioId").GetInt32(), corpo.GetProperty("candidato").GetProperty("nome").GetString()));
        var quem = corpo.GetProperty("candidato");
        Assert.Equal((candidato, "mariana.teixeira@email.com.br", "(11) 98877-1204", true, "2026-10-07T12:42:00Z"), (quem.GetProperty("id").GetInt32(), quem.GetProperty("email").GetString(), quem.GetProperty("telefone").GetString(), quem.GetProperty("temCurriculo").GetBoolean(), quem.GetProperty("respondidoEmUtc").GetString()));
        var respostas = corpo.GetProperty("respostas").EnumerateArray().ToList();
        Assert.Equal([e.Multipla, e.Longa, e.Anexo], respostas.Select(r => r.GetProperty("perguntaId").GetInt32()));
        Assert.Equal(e.Sim, respostas[0].GetProperty("alternativas")[0].GetProperty("id").GetInt32());
        Assert.Equal("Reorganizei um estoque em dois dias.", respostas[1].GetProperty("texto").GetString());
        var arquivo = respostas[2].GetProperty("arquivo");
        Assert.Equal((arquivoDaResposta, "comprovante.pdf"), (arquivo.GetProperty("respostaId").GetInt32(), arquivo.GetProperty("nome").GetString()));
        Assert.Equal(["nome", "respostaId"], arquivo.EnumerateObject().Select(p => p.Name).Order()); // o caminho não é um campo
    }

    [Fact]
    public async Task A_pergunta_opcional_em_branco_vem_vazia_e_a_pergunta_nova_tambem()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl5");
        var candidato = await CandidatarAsync(api, e, "ZCl5 Caio Almeida", "caio@email.com.br");
        var nova = await PerguntaAsync(api, e.Id, "Pergunta acrescentada depois", "RESPOSTA_CURTA", 4);

        var corpo = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{e.Id}/respostas/candidatos/{candidato}"));

        var respostas = corpo.GetProperty("respostas").EnumerateArray().ToList();
        Assert.Equal([e.Multipla, e.Longa, e.Anexo, nova], respostas.Select(r => r.GetProperty("perguntaId").GetInt32()));
        Assert.All(respostas.Skip(2), r => Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, 0), (r.GetProperty("texto").ValueKind, r.GetProperty("arquivo").ValueKind, r.GetProperty("alternativas").GetArrayLength())));
    }

    [Fact]
    public async Task Candidato_de_outro_formulario_inexistente_ou_formulario_que_nao_e_por_link_nao_dao_respostas_nem_curriculo()
    {
        var api = await ApiAsync();
        var a = await EntrevistaAsync(api, "ZCl6 a");
        var b = await EntrevistaAsync(api, "ZCl6 b");
        var dePessoas = await SemearAsync(api, Titulo("ZCl6 pessoas"), "PUBLICADO", alvo: "PESSOAS");
        var doA = await CandidatarAsync(api, a, "ZCl6 Do A", "doa@email.com.br", curriculo: ("cv.pdf", ArquivosDeTeste.Pdf()));
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostasNoOutro = await rh.GetAsync($"/api/rh/formularios/{b.Id}/respostas/candidatos/{doA}");
        var curriculoNoOutro = await rh.GetAsync($"/api/rh/formularios/{b.Id}/candidatos/{doA}/curriculo/download");
        var respostasInexistente = await rh.GetAsync($"/api/rh/formularios/{a.Id}/respostas/candidatos/99999999");
        var curriculoInexistente = await rh.GetAsync($"/api/rh/formularios/{a.Id}/candidatos/99999999/curriculo/download");
        var formularioInexistente = await rh.GetAsync($"/api/rh/formularios/99999999/respostas/candidatos/{doA}");
        var semLink = await rh.GetAsync($"/api/rh/formularios/{dePessoas}/respostas/candidatos/{doA}");

        Assert.Equal([404, 404, 404, 404, 404, 422], new[] { respostasNoOutro, curriculoNoOutro, respostasInexistente, curriculoInexistente, formularioInexistente, semLink }.Select(r => (int)r.StatusCode));
        Assert.Equal("FORMULARIO_SEM_LINK", Codigo(await CorpoAsync(semLink)));
        Assert.Empty(await EventosAsync(api, b.Id)); // nem o 404 do currículo grava evento
    }

    // ---------------------------------------------------------------- o currículo

    [Fact]
    public async Task O_RH_baixa_o_curriculo_por_um_link_temporario_como_anexo_com_nome_gerado_e_o_evento_BAIXOU_sem_dado_pessoal()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl7");
        var bytes = ArquivosDeTeste.Pdf(3000);
        var candidato = await CandidatarAsync(api, e, "ZCl7 Mariana /Teixeira\\Souza", "mariana.teixeira@email.com.br", curriculo: ("meu curriculo final.pdf", bytes));
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_CURRICULO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO_CANDIDATO = {candidato}"))!;
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync($"/api/rh/formularios/{e.Id}/candidatos/{candidato}/curriculo/download");
        var link = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("baixarCurriculoDeCandidato");
        Assert.Equal(["expiraEmUtc", "url"], link.EnumerateObject().Select(p => p.Name).Order());
        var url = link.GetProperty("url").GetString()!;
        Assert.Contains("?nome=" + Uri.EscapeDataString("curriculo-ZCl7 Mariana -Teixeira-Souza.pdf"), url, StringComparison.Ordinal); // sem barra no nome: sobra só texto
        Assert.Equal(bytes, api.Api.Armazenamento.Conteudo(caminho));
        var evento = Assert.Single(await EventosAsync(api, e.Id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Baixou), (evento.Area, evento.Acao));
        Assert.All(new[] { "Mariana", "Teixeira", "email.com.br", "curriculo" }, parte => Assert.DoesNotContain(parte, evento.Descricao, StringComparison.OrdinalIgnoreCase));

        await rh.GetAsync($"/api/rh/formularios/{e.Id}/candidatos/{candidato}/curriculo/download");
        Assert.Equal(2, (await EventosAsync(api, e.Id)).Count); // cada link pedido é um evento
    }

    [Fact]
    public async Task O_nome_do_curriculo_usa_a_extensao_do_que_foi_guardado_e_o_Word_baixa_como_docx()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl8");
        var candidato = await CandidatarAsync(api, e, "ZCl8 Caio", "caio@email.com.br", curriculo: ("qualquer-nome-sem-extensao", Docx()));

        var link = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{e.Id}/candidatos/{candidato}/curriculo/download"));

        Assert.Contains("?nome=" + Uri.EscapeDataString("curriculo-ZCl8 Caio.docx"), link.GetProperty("url").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Candidato_sem_curriculo_e_404_e_o_arquivo_de_uma_pergunta_do_candidato_baixa_pelo_download_de_arquivo()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl9");
        var sem = await CandidatarAsync(api, e, "ZCl9 Sem Curriculo", "sem@email.com.br");
        var com = await CandidatarAsync(api, e, "ZCl9 Com Arquivo", "com@email.com.br", comArquivo: true, arquivos: ("comprovante.pdf", ArquivosDeTeste.Pdf()));
        var arquivoDaResposta = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_RESPOSTA FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_CANDIDATO = {com} AND ID_FORMULARIO_PERGUNTA = {e.Anexo}");
        var rh = await api.ClienteDoRhAgoraAsync();

        var semCurriculo = await rh.GetAsync($"/api/rh/formularios/{e.Id}/candidatos/{sem}/curriculo/download");
        var arquivo = await rh.GetAsync($"/api/rh/formularios/{e.Id}/arquivos/{arquivoDaResposta}/download");

        Assert.Equal([404, 200], new[] { semCurriculo, arquivo }.Select(r => (int)r.StatusCode));
        Assert.Contains("?nome=comprovante.pdf", (await CorpoAsync(arquivo)).GetProperty("url").GetString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- quem pode ver (dado pessoal só no RH)

    [Fact]
    public async Task So_o_RH_ve_os_candidatos_o_gestor_e_o_funcionario_tem_403_e_sem_login_401()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl10");
        var candidato = await CandidatarAsync(api, e, "ZCl10 Mariana", "mariana@email.com.br", curriculo: ("cv.pdf", ArquivosDeTeste.Pdf()));
        string[] rotas =
        [
            RotaDaLista(e.Id),
            $"/api/rh/formularios/{e.Id}/respostas/candidatos/{candidato}",
            $"/api/rh/formularios/{e.Id}/candidatos/{candidato}/curriculo/download",
        ];
        HttpClient[] quem = [api.Api.ClienteComo(api.Elenco.Gestor), api.Api.ClienteComo(api.Elenco.MembroA), api.Api.ClienteAnonimo()];

        var status = new List<int>();
        foreach (var rota in rotas)
        {
            foreach (var cliente in quem)
            {
                status.Add((int)(await cliente.GetAsync(rota)).StatusCode);
            }
        }

        Assert.Equal([403, 403, 401, 403, 403, 401, 403, 403, 401], status);
        Assert.Empty(await EventosAsync(api, e.Id)); // quem foi barrado não gera evento de download
    }

    // ---------------------------------------------------------------- a exportação do formulário por link

    [Fact]
    public async Task A_exportacao_do_formulario_por_link_tem_uma_linha_por_resposta_com_o_candidato_sem_setor_e_neutraliza_formula()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl11");
        await CandidatarAsync(api, e, "=HYPERLINK(\"http://x\")", "formula@email.com.br");
        await CandidatarAsync(api, e, "ZCl11 Ana", "ana@email.com.br", comArquivo: true, arquivos: ("anexo.pdf", ArquivosDeTeste.Pdf()));

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{e.Id}/exportacao");
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        var linhas = Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3).TrimEnd('\r', '\n').Split("\r\n");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]);
        Assert.Equal("formulario;respondente;email;setor;avaliado;pergunta;resposta;respondido_em", linhas[0]);
        Assert.Equal(1 + 3 + 2, linhas.Length); // a primeira candidatura respondeu 2 perguntas e a segunda 3 (com o arquivo)
        Assert.Contains(linhas, l => l.Contains(";ZCl11 Ana;ana@email.com.br;;;Envie um comprovante.;anexo.pdf;2026-10-07 09:42", StringComparison.Ordinal)); // candidato: setor vazio; arquivo: o nome
        Assert.All(linhas.Skip(1).Where(l => l.Contains("formula@email.com.br", StringComparison.Ordinal)), l => Assert.Contains("'=HYPERLINK", l, StringComparison.Ordinal)); // a fórmula virou texto
        Assert.DoesNotContain(linhas, l => l.Contains("formularios/", StringComparison.Ordinal)); // nenhum caminho de armazenamento
    }

    [Fact]
    public async Task As_respostas_de_um_candidato_nao_trazem_as_de_outro_do_mesmo_formulario()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZCl12");
        Task<HttpResponseMessage> Enviar(string nome, string email, string texto) => EnviarAsync(api.Api.ClienteAnonimo(), e.Token, Candidatura(Json(new { perguntaId = e.Multipla, alternativaIds = new[] { e.Sim } }, new { perguntaId = e.Longa, texto }), nome, email));
        await Enviar("ZCl12 Primeiro", "primeiro@email.com.br", "Texto do primeiro candidato.");
        await Enviar("ZCl12 Segundo", "segundo@email.com.br", "Texto do segundo candidato.");
        var segundo = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_CANDIDATO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id} AND EMAIL = 'segundo@email.com.br'");

        var corpo = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{e.Id}/respostas/candidatos/{segundo}"));

        Assert.Equal("Texto do segundo candidato.", corpo.GetProperty("respostas")[1].GetProperty("texto").GetString()); // o primeiro candidato respondeu antes: as linhas dele não entram
    }
}

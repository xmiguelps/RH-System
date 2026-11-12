using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// A lista do RH (<c>listarFormulariosRh</c>) e a exclusão física (<c>excluirFormulario</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. A lista: a ordem padrão é a dos mais recentes, <c>contagemPorTipo</c> respeita a busca e a situação e IGNORA o tipo, a situação é a calculada (o prazo
/// vencido vale encerrado), e as colunas "quem responde" e "respostas" saem por alvo e anonimato (por link e Sugestão sem esperados; anônimo aberto só com esperados; taxa inteira com 5 de 8 = 63%). A exclusão apaga tudo em uma transação, na ordem das chaves, e depois os arquivos;
/// cancela as tarefas e não deixa nada rodar depois; o anônimo aberto não tem contagem no evento.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ListaEExclusaoDeFormulariosTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ListaEExclusaoDeFormulariosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, string consulta) => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?{consulta}"));

    private static List<string> Titulos(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("titulo").GetString()!)];

    private static JsonElement Item(JsonElement lista, string titulo) => lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("titulo").GetString() == titulo);

    private static string Nulo(JsonElement e, string campo) => e.GetProperty(campo).ValueKind == JsonValueKind.Null ? "null" : e.GetProperty(campo).ToString();

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, ativo: ativa);

    private static async Task<int[]> PessoasAsync(ApiDeAcesso api, int quantas)
    {
        var ids = new int[quantas];
        for (var i = 0; i < quantas; i++)
        {
            ids[i] = await PessoaAsync(api, $"Pessoa {i}");
        }

        return ids;
    }

    private static Task EscolherPessoasAsync(ApiDeAcesso api, int formularioId, IEnumerable<int> pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({formularioId}, {p})"))}");

    private static Task CriadoEmAsync(ApiDeAcesso api, int formularioId, string local) => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET DATA_CRIACAO = '{local}' WHERE ID_FORMULARIO = {formularioId}");

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_ordem_padrao_e_dos_mais_recentes_e_o_item_traz_os_campos_do_contrato()
    {
        var api = await ApiAsync();
        var a = await SemearAsync(api, "Zlalfa antigo", "PUBLICADO", alvo: "PESSOAS", prazo: "2026-11-15");
        var b = await SemearAsync(api, "Zlalfa novo", tipo: "ENTREVISTA", alvo: "LINK");
        await CriadoEmAsync(api, a, "2026-10-01 10:00:00");
        await CriadoEmAsync(api, b, "2026-10-05 12:20:00");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?busca=Zlalfa");
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarFormulariosRh");
        Assert.Equal(["Zlalfa novo", "Zlalfa antigo"], Titulos(lista));
        var novo = Item(lista, "Zlalfa novo");
        Assert.Equal((b, "ENTREVISTA", "RASCUNHO", "LINK", "Candidatos, por link", "2026-10-05T15:20:00Z"), (novo.GetProperty("id").GetInt32(), novo.GetProperty("tipo").GetString(), novo.GetProperty("situacao").GetString(), novo.GetProperty("alvo").GetString(), novo.GetProperty("quemRespondeResumo").GetString(), novo.GetProperty("criadoEmUtc").GetString()));
        Assert.Equal(("null", "null", "null", "false"), (Nulo(novo, "esperados"), Nulo(novo, "taxaDeRespostaPercentual"), Nulo(novo, "sentidoAvaliacao"), novo.GetProperty("anonimo").GetBoolean().ToString().ToLowerInvariant()));
        var antigo = Item(lista, "Zlalfa antigo");
        Assert.Equal(("2026-11-15", "null", "PUBLICADO"), (antigo.GetProperty("prazo").GetString(), Nulo(antigo, "encerradoEmUtc"), antigo.GetProperty("situacao").GetString()));
        Assert.Equal((1, 20, 2, 1), (lista.GetProperty("pagina").GetInt32(), lista.GetProperty("tamanhoPagina").GetInt32(), lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32()));
    }

    [Fact]
    public async Task Ordenar_por_titulo_prazo_e_criadoEm_nas_duas_direcoes_com_o_padrao_de_cada_caso()
    {
        var api = await ApiAsync();
        var c = await SemearAsync(api, "Zlbeta C", prazo: "2026-10-30");
        var a = await SemearAsync(api, "Zlbeta A", prazo: "2026-12-01");
        var b = await SemearAsync(api, "Zlbeta B", prazo: "2026-11-01");
        await CriadoEmAsync(api, c, "2026-10-01 08:00:00");
        await CriadoEmAsync(api, a, "2026-10-02 08:00:00");
        await CriadoEmAsync(api, b, "2026-10-03 08:00:00");

        var padrao = await ListarAsync(api, "busca=Zlbeta");
        var tituloAsc = await ListarAsync(api, "busca=Zlbeta&ordenarPor=titulo");
        var tituloDesc = await ListarAsync(api, "busca=Zlbeta&ordenarPor=titulo&direcao=DESC");
        var prazoAsc = await ListarAsync(api, "busca=Zlbeta&ordenarPor=prazo&direcao=ASC");
        var criadoAsc = await ListarAsync(api, "busca=Zlbeta&ordenarPor=criadoEm&direcao=ASC");
        var soDirecao = await ListarAsync(api, "busca=Zlbeta&direcao=ASC");

        Assert.Equal(["Zlbeta B", "Zlbeta A", "Zlbeta C"], Titulos(padrao)); // criadoEm decrescente
        Assert.Equal(["Zlbeta A", "Zlbeta B", "Zlbeta C"], Titulos(tituloAsc));
        Assert.Equal(["Zlbeta C", "Zlbeta B", "Zlbeta A"], Titulos(tituloDesc));
        Assert.Equal(["Zlbeta C", "Zlbeta B", "Zlbeta A"], Titulos(prazoAsc));
        Assert.Equal(["Zlbeta C", "Zlbeta A", "Zlbeta B"], Titulos(criadoAsc));
        Assert.Equal(["Zlbeta C", "Zlbeta A", "Zlbeta B"], Titulos(soDirecao)); // só a direção: a coluna padrão (criadoEm) naquela direção
    }

    [Fact]
    public async Task Filtrar_por_tipo_situacao_e_busca_e_a_contagem_por_tipo_respeita_busca_e_situacao_mas_ignora_o_tipo()
    {
        var api = await ApiAsync();
        await SemearAsync(api, "Zlgama entrevista", tipo: "ENTREVISTA");
        await SemearAsync(api, "Zlgama admissão", tipo: "ADMISSAO");
        await SemearAsync(api, "Zlgama avaliação A", tipo: "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE");
        await SemearAsync(api, "Zlgama avaliação B", tipo: "AVALIACAO", sentido: "EQUIPE_AVALIA_GESTOR");
        await SemearAsync(api, "Zlgama pesquisa", "PUBLICADO", alvo: "PESSOAS");
        await SemearAsync(api, "Zlgama sugestões", "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await SemearAsync(api, "Outra coisa Zlgamax", tipo: "PESQUISA");

        var todos = await ListarAsync(api, "busca=Zlgama");
        var avaliacoes = await ListarAsync(api, "busca=Zlgama&tipo=AVALIACAO");
        var publicados = await ListarAsync(api, "busca=Zlgama&situacao=PUBLICADO");
        var pesquisasPublicadas = await ListarAsync(api, "busca=Zlgama&situacao=PUBLICADO&tipo=PESQUISA");

        Assert.Equal(7, todos.GetProperty("total").GetInt32());
        var c = todos.GetProperty("contagemPorTipo");
        Assert.Equal((7, 1, 1, 2, 2, 1), (c.GetProperty("todos").GetInt32(), c.GetProperty("entrevistas").GetInt32(), c.GetProperty("admissoes").GetInt32(), c.GetProperty("avaliacoes").GetInt32(), c.GetProperty("pesquisas").GetInt32(), c.GetProperty("sugestoes").GetInt32()));
        Assert.Equal(["Zlgama avaliação A", "Zlgama avaliação B"], Titulos(avaliacoes).Order()); // as duas direções juntas
        Assert.Equal(2, avaliacoes.GetProperty("total").GetInt32());
        Assert.Equal(7, avaliacoes.GetProperty("contagemPorTipo").GetProperty("todos").GetInt32()); // a contagem ignora o tipo: não muda ao trocar de aba
        var cp = publicados.GetProperty("contagemPorTipo");
        Assert.Equal((2, 0, 0, 0, 1, 1), (cp.GetProperty("todos").GetInt32(), cp.GetProperty("entrevistas").GetInt32(), cp.GetProperty("admissoes").GetInt32(), cp.GetProperty("avaliacoes").GetInt32(), cp.GetProperty("pesquisas").GetInt32(), cp.GetProperty("sugestoes").GetInt32()));
        Assert.Equal(["Zlgama pesquisa"], Titulos(pesquisasPublicadas));
        Assert.Equal(2, pesquisasPublicadas.GetProperty("contagemPorTipo").GetProperty("todos").GetInt32());
    }

    [Fact]
    public async Task A_situacao_do_filtro_e_a_calculada_o_prazo_vencido_com_ATIVO_ligado_e_encerrado_e_nao_publicado()
    {
        var api = await ApiAsync();
        await SemearAsync(api, "Zldelta rascunho");
        await SemearAsync(api, "Zldelta agendado", "AGENDADO", alvo: "PESSOAS");
        await SemearAsync(api, "Zldelta no prazo", "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-07");
        await SemearAsync(api, "Zldelta sem prazo", "PUBLICADO", alvo: "PESSOAS");
        await SemearAsync(api, "Zldelta vencido", "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");
        await SemearAsync(api, "Zldelta encerrado", "ENCERRADO", alvo: "PESSOAS");

        var rascunhos = await ListarAsync(api, "busca=Zldelta&situacao=RASCUNHO");
        var agendados = await ListarAsync(api, "busca=Zldelta&situacao=AGENDADO");
        var publicados = await ListarAsync(api, "busca=Zldelta&situacao=PUBLICADO");
        var encerrados = await ListarAsync(api, "busca=Zldelta&situacao=ENCERRADO");

        Assert.Equal(["Zldelta rascunho"], Titulos(rascunhos));
        Assert.Equal(["Zldelta agendado"], Titulos(agendados));
        Assert.Equal(["Zldelta no prazo", "Zldelta sem prazo"], Titulos(publicados).Order());
        Assert.Equal(["Zldelta encerrado", "Zldelta vencido"], Titulos(encerrados).Order());
        Assert.Equal(("ENCERRADO", "2026-10-07T03:00:00Z"), (Item(encerrados, "Zldelta vencido").GetProperty("situacao").GetString(), Item(encerrados, "Zldelta vencido").GetProperty("encerradoEmUtc").GetString())); // o encerramento nominal enquanto a tarefa não rodou
    }

    [Fact]
    public async Task Cinco_de_oito_e_63_por_cento_o_resumo_de_quem_responde_e_o_numero_de_respondidos_so_conta_quem_ainda_deve_responder()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 8);
        var id = await SemearAsync(api, "Zlepsilon clima", "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, pessoas);
        foreach (var p in pessoas.Take(5))
        {
            await ResponderAsync(api, id, pergunta, p);
        }

        var saiu = await PessoaAsync(api, "Respondeu e saiu");
        await ResponderAsync(api, id, pergunta, saiu); // não é destinatária: não entra na conta

        var item = Item(await ListarAsync(api, "busca=Zlepsilon"), "Zlepsilon clima");

        Assert.Equal((5, 8, 63, "8 pessoas escolhidas"), (item.GetProperty("respondidos").GetInt32(), item.GetProperty("esperados").GetInt32(), item.GetProperty("taxaDeRespostaPercentual").GetInt32(), item.GetProperty("quemRespondeResumo").GetString()));
    }

    [Fact]
    public async Task O_resumo_de_setores_conta_as_pessoas_e_todos_os_setores_so_quando_a_escolha_cobre_o_cadastro()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, "Zlzeta setor", "PUBLICADO", alvo: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor.SetorId})");

        var item = Item(await ListarAsync(api, "busca=Zlzeta"), "Zlzeta setor");

        Assert.Equal(("1 setor (2 pessoas)", 2, 0), (item.GetProperty("quemRespondeResumo").GetString(), item.GetProperty("esperados").GetInt32(), item.GetProperty("respondidos").GetInt32()));
    }

    [Fact]
    public async Task O_anonimo_aberto_mostra_so_os_esperados_e_encerrado_mostra_os_tres_exatos()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var aberto = await SemearAsync(api, "Zleta anônimo aberto", "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, "Zleta anônimo encerrado", "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var rascunho = await SemearAsync(api, "Zleta anônimo rascunho", anonimo: true, alvo: "PESSOAS");
        foreach (var id in new[] { aberto, encerrado })
        {
            await EscolherPessoasAsync(api, id, pessoas);
            foreach (var p in pessoas.Take(3))
            {
                await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid():D}', {id}, {p})");
            }
        }

        await EscolherPessoasAsync(api, rascunho, pessoas);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 18:00:00' WHERE ID_FORMULARIO = {encerrado}");

        var lista = await ListarAsync(api, "busca=Zleta");

        var a = Item(lista, "Zleta anônimo aberto");
        var e = Item(lista, "Zleta anônimo encerrado");
        var r = Item(lista, "Zleta anônimo rascunho");
        Assert.Equal(("null", 5, "null"), (Nulo(a, "respondidos"), a.GetProperty("esperados").GetInt32(), Nulo(a, "taxaDeRespostaPercentual")));
        Assert.Equal((3, 5, 60), (e.GetProperty("respondidos").GetInt32(), e.GetProperty("esperados").GetInt32(), e.GetProperty("taxaDeRespostaPercentual").GetInt32()));
        Assert.Equal(("null", 5, "null"), (Nulo(r, "respondidos"), r.GetProperty("esperados").GetInt32(), Nulo(r, "taxaDeRespostaPercentual")));
    }

    [Fact]
    public async Task Por_link_os_respondidos_sao_candidatos_e_a_sugestao_conta_envios_ambos_sem_esperados_nem_taxa()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Quem sugere");
        var link = await SemearAsync(api, "Zlteta entrevista", "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('l', 43));
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_CANDIDATOS (ID_FORMULARIO, NOME, EMAIL, DATA_CADASTRO) VALUES ({link}, 'Ana', 'ana.zlista8@exemplo.com', '2026-10-05 10:00:00'), ({link}, 'Bia', 'bia.zlista8@exemplo.com', '2026-10-05 11:00:00')");
        var sugestao = await SemearAsync(api, "Zlteta sugestões", "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, sugestao, [pessoa]);
        var p1 = await PerguntaAsync(api, sugestao, "Sugestão", ordem: 1);
        var p2 = await PerguntaAsync(api, sugestao, "Mais uma", ordem: 2);
        // dois envios da mesma pessoa: as duas linhas do primeiro têm a mesma data e a do segundo outra
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({p1}, {sugestao}, {pessoa}, 'a', '2026-10-05 10:00:00', 0), ({p2}, {sugestao}, {pessoa}, 'b', '2026-10-05 10:00:00', 0), ({p1}, {sugestao}, {pessoa}, 'c', '2026-10-06 09:00:00', 0)");

        var lista = await ListarAsync(api, "busca=Zlteta");

        var a = Item(lista, "Zlteta entrevista");
        var b = Item(lista, "Zlteta sugestões");
        Assert.Equal((2, "null", "null"), (a.GetProperty("respondidos").GetInt32(), Nulo(a, "esperados"), Nulo(a, "taxaDeRespostaPercentual")));
        Assert.Equal((2, "null", "null"), (b.GetProperty("respondidos").GetInt32(), Nulo(b, "esperados"), Nulo(b, "taxaDeRespostaPercentual")));
    }

    [Fact]
    public async Task Na_avaliacao_a_unidade_e_a_avaliacao_e_o_resumo_fala_em_gestores_e_avaliacoes()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, "Zliota gestor avalia", "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var pergunta = await PerguntaAsync(api, id, "Nota");
        await EscolherPessoasAsync(api, id, [setor.GestorFuncionarioId]);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, {setor.GestorFuncionarioId}, {m1.Id}, '4', '2026-10-05 10:00:00', 0)");

        var item = Item(await ListarAsync(api, "busca=Zliota"), "Zliota gestor avalia");

        Assert.Equal(("1 gestor (3 avaliações)", 1, 3, 33, "GESTOR_AVALIA_EQUIPE"), (item.GetProperty("quemRespondeResumo").GetString(), item.GetProperty("respondidos").GetInt32(), item.GetProperty("esperados").GetInt32(), item.GetProperty("taxaDeRespostaPercentual").GetInt32(), item.GetProperty("sentidoAvaliacao").GetString()));
    }

    [Fact]
    public async Task A_paginacao_devolve_o_total_e_as_paginas_e_os_parametros_invalidos_viram_400()
    {
        var api = await ApiAsync();
        for (var i = 1; i <= 5; i++)
        {
            await SemearAsync(api, $"Zlkapa item {i}");
        }

        var segunda = await ListarAsync(api, "busca=Zlkapa&tamanhoPagina=2&pagina=2&ordenarPor=titulo");
        var cliente = await api.ClienteDoRhAgoraAsync();
        var invalidos = new[] { "tipo=QUIZ", "situacao=ABERTO", "ordenarPor=autor", "pagina=0", "tamanhoPagina=101", "direcao=CIMA" };
        var respostas = new List<int>();
        foreach (var consulta in invalidos)
        {
            respostas.Add((int)(await cliente.GetAsync($"{Rota}?{consulta}")).StatusCode);
        }

        Assert.Equal(["Zlkapa item 3", "Zlkapa item 4"], Titulos(segunda));
        Assert.Equal((5, 3, 2, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32(), segunda.GetProperty("tamanhoPagina").GetInt32()));
        Assert.All(respostas, status => Assert.Equal(400, status));
    }

    [Fact]
    public async Task Quem_nao_e_do_RH_nao_lista_nem_exclui()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, "Zlambda só RH");
        var comum = await PessoaAsync(api, "Comum");
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal((403, 403), ((int)(await cliente.GetAsync(Rota)).StatusCode, (int)(await cliente.DeleteAsync($"{Rota}/{id}")).StatusCode));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    // ---------------------------------------------------------------- a exclusão

    private static async Task<HttpResponseMessage> ExcluirAsync(ApiDeAcesso api, int id, bool? confirmar = null) =>
        await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{id}" + (confirmar is null ? string.Empty : $"?confirmar={(confirmar.Value ? "true" : "false")}"));

    [Fact]
    public async Task Excluir_rascunho_e_agendado_nao_pede_confirmacao_e_apaga_o_formulario_com_as_perguntas_e_o_evento_diz_quantas_respostas()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho a excluir"));
        await PerguntaAsync(api, rascunho, "A", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");
        var agendado = await SemearAsync(api, Titulo("Agendado a excluir"), "AGENDADO", alvo: "PESSOAS");
        var tituloRascunho = await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {rascunho}");

        var a = await ExcluirAsync(api, rascunho);
        var b = await ExcluirAsync(api, agendado);

        Assert.Equal((204, 204), ((int)a.StatusCode, (int)b.StatusCode));
        await a.AfirmarConformeAoContratoAsync("excluirFormulario");
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO IN ({rascunho}, {agendado})"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {rascunho}"));
        var evento = Assert.Single(await EventosAsync(api, rascunho));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Excluiu, $"Excluiu o formulário \"{tituloRascunho}\" com 0 respostas."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.Equal(404, (int)(await ExcluirAsync(api, rascunho)).StatusCode);
    }

    [Fact]
    public async Task Excluir_publicado_com_pendentes_pede_confirmacao_e_sem_pendentes_apaga_direto()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 3);
        var pendente = await SemearAsync(api, Titulo("Com pendentes"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, pendente, "Como foi?");
        await EscolherPessoasAsync(api, pendente, pessoas);
        await ResponderAsync(api, pendente, pergunta, pessoas[0]);
        var completo = await SemearAsync(api, Titulo("Todos responderam"), "PUBLICADO", alvo: "PESSOAS");
        var perguntaCompleta = await PerguntaAsync(api, completo, "Como foi?");
        await EscolherPessoasAsync(api, completo, pessoas.Take(1));
        await ResponderAsync(api, completo, perguntaCompleta, pessoas[0]);

        var pedida = await ExcluirAsync(api, pendente);
        var problema = await CorpoAsync(pedida);
        var direta = await ExcluirAsync(api, completo);
        var confirmada = await ExcluirAsync(api, pendente, confirmar: true);

        Assert.Equal((409, "CONFIRMACAO_NECESSARIA", 2), ((int)pedida.StatusCode, Codigo(problema), problema.GetProperty("pendentes").GetInt32()));
        await pedida.AfirmarConformeAoContratoAsync("excluirFormulario");
        Assert.Equal((204, 204), ((int)direta.StatusCode, (int)confirmada.StatusCode));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO IN ({pendente}, {completo})")); // as respostas vão junto
    }

    [Fact]
    public async Task O_anonimo_publicado_pede_confirmacao_sempre_sem_numero_e_o_evento_nao_conta_respostas_enquanto_aberto()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var id = await SemearAsync(api, Titulo("Anônimo aberto"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        await EscolherPessoasAsync(api, id, pessoas);
        foreach (var p in pessoas)
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid():D}', {id}, {p})"); // todos já participaram
        }

        var pedida = await ExcluirAsync(api, id);
        var problema = await CorpoAsync(pedida);
        var feita = await ExcluirAsync(api, id, confirmar: true);

        Assert.Equal((409, "CONFIRMACAO_NECESSARIA"), ((int)pedida.StatusCode, Codigo(problema)));
        Assert.Equal(JsonValueKind.Null, problema.GetProperty("pendentes").ValueKind);
        Assert.Equal(204, (int)feita.StatusCode);
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.EndsWith("\".", evento.Descricao, StringComparison.Ordinal); // sem a contagem: "Excluiu o formulário "X"."
        Assert.DoesNotContain("respostas", evento.Descricao, StringComparison.Ordinal);
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task O_anonimo_encerrado_se_exclui_sem_confirmar_e_o_evento_conta_as_participacoes()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var id = await SemearAsync(api, Titulo("Anônimo encerrado"), "ENCERRADO", anonimo: true, alvo: "PESSOAS");
        await EscolherPessoasAsync(api, id, pessoas);
        foreach (var p in pessoas.Take(4))
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid():D}', {id}, {p})");
        }

        var resposta = await ExcluirAsync(api, id);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Contains("com 4 respostas.", Assert.Single(await EventosAsync(api, id)).Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_exclusao_apaga_respostas_participacoes_destinatarios_candidatos_alternativas_perguntas_e_os_arquivos_e_nada_sobra()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Respondente");
        var id = await SemearAsync(api, Titulo("Tudo junto"), "PUBLICADO", alvo: "PESSOAS");
        var escolha = await PerguntaAsync(api, id, "Escolha", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");
        var arquivoPergunta = await PerguntaAsync(api, id, "Envie", "ARQUIVO", 2);
        await EscolherPessoasAsync(api, id, [pessoa]);
        var curriculo = CaminhosDeArquivo.Novo("formularios", "pdf");
        var respostaArquivo = CaminhosDeArquivo.Novo("formularios", "pdf");
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            var armazenamento = escopo.ServiceProvider.GetRequiredService<IArmazenamentoDeArquivos>();
            await armazenamento.GravarAsync(curriculo, new MemoryStream(ArquivosDeTeste.Pdf()), "application/pdf", CancellationToken.None);
            await armazenamento.GravarAsync(respostaArquivo, new MemoryStream(ArquivosDeTeste.Pdf()), "application/pdf", CancellationToken.None);
        }

        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_CANDIDATOS (ID_FORMULARIO, NOME, EMAIL, CAMINHO_CURRICULO, DATA_CADASTRO) VALUES ({id}, 'Ana', 'ana.tudo@exemplo.com', '{curriculo}', '2026-10-05 10:00:00')");
        var candidato = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_CANDIDATO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {id}");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_ALTERNATIVA, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({escolha}, {id}, {pessoa}, (SELECT MIN(ID_ALTERNATIVA) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {escolha}), 'Sim', '2026-10-05 10:00:00', 0)");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FORMULARIO_CANDIDATO, RESPOSTA, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL, DATA_RESPOSTA, ANONIMO) VALUES ({arquivoPergunta}, {id}, {candidato}, 'portfolio.pdf', '{respostaArquivo}', 'portfolio.pdf', '2026-10-05 11:00:00', 0)");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid():D}', {id}, {pessoa})");

        var resposta = await ExcluirAsync(api, id, confirmar: true);

        Assert.Equal(204, (int)resposta.StatusCode);
        foreach (var tabela in new[] { "FORMULARIOS_RESPOSTAS", "FORMULARIOS_PARTICIPACOES", "FORMULARIOS_DESTINATARIOS", "FORMULARIOS_CANDIDATOS", "FORMULARIOS_PERGUNTAS" })
        {
            Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM {tabela} WHERE ID_FORMULARIO = {id}"));
        }

        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {escolha}"));
        Assert.Equal((false, false), (api.Api.Armazenamento.Existe(curriculo), api.Api.Armazenamento.Existe(respostaArquivo))); // os arquivos do currículo e da resposta saem do armazenamento
        Assert.Contains("com 2 respostas.", Assert.Single(await EventosAsync(api, id)).Descricao, StringComparison.Ordinal); // a pessoa respondeu uma vez e o candidato uma
    }

    [Fact]
    public async Task Excluir_cancela_as_tarefas_pendentes_e_nenhuma_roda_depois()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Com tarefas"), "AGENDADO", alvo: "PESSOAS", prazo: "2026-11-17");
        using (var escopo = api.Api.Services.CreateScope())
        {
            var agendamento = escopo.ServiceProvider.GetRequiredService<RHSystem.Application.Formularios.Construtor.IAgendamentoDoFormulario>();
            await agendamento.AgendarPublicacaoAsync(id, new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None);
            await agendamento.AgendarEncerramentoAsync(id, new DateTime(2026, 11, 18, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        }

        var resposta = await ExcluirAsync(api, id);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal((0L, 2L), (await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {id} AND TIPO IN ('PUBLICAR_FORMULARIO', 'ENCERRAR_FORMULARIO') AND SITUACAO = 'PENDENTE'"), await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {id} AND TIPO IN ('PUBLICAR_FORMULARIO', 'ENCERRAR_FORMULARIO') AND SITUACAO = 'CANCELADA'")));
        await using var escopoDaTarefa = api.Api.Services.CreateAsyncScope();
        foreach (var (tipo, instante) in new[] { ("PUBLICAR_FORMULARIO", "2026-10-20T03:00:00Z"), ("ENCERRAR_FORMULARIO", "2026-11-18T03:00:00Z") })
        {
            await escopoDaTarefa.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).ExecutarAsync(new TarefaParaExecutar(0, $"{tipo}:formulario={id}:em={instante}", id, null, null, 0), CancellationToken.None); // a tarefa que rodasse não acha o formulário e não faz nada
        }

        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Duas_abas_excluindo_ao_mesmo_tempo_dao_um_204_e_um_404_nunca_500()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas abas excluindo"));
        await PerguntaAsync(api, id, "A");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => cliente.DeleteAsync($"{Rota}/{id}")));

        Assert.Equal([204, 404], respostas.Select(r => (int)r.StatusCode).Order().ToArray());
        Assert.Single(await EventosAsync(api, id));
    }
}

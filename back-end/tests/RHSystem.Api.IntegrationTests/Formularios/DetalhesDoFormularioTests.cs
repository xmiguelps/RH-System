using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os detalhes do formulário pelo RH (<c>criarFormulario</c>, <c>obterFormularioRh</c> e <c>editarFormulario</c>), no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 em Brasília). O formulário nasce pela API (o caminho de verdade) ou por SQL quando o teste só precisa de um
/// estado (as datas do banco são locais, -03:00; as da API, UTC). A matriz de edição por situação e anonimato é a do contrato; o anônimo publicado recebe o MESMO veredito com e sem resposta. Nenhum evento de auditoria leva o conteúdo das perguntas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DetalhesDoFormularioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(DetalhesDoFormularioTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Novo(string titulo, string tipo = "PESQUISA", bool anonimo = false, string? descricao = null, string? sentido = null) => new { titulo, tipo, anonimo, descricao, sentidoAvaliacao = sentido };

    private static async Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota, corpo);

    private static async Task<int> CriarComSucessoAsync(ApiDeAcesso api, object corpo)
    {
        var resposta = await CriarAsync(api, corpo);
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> ObterAsync(ApiDeAcesso api, int id) => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}"));

    private static async Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}", corpo);

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_devolve_o_rascunho_com_o_autor_a_data_do_relogio_o_lembrete_ligado_e_o_banco_com_ATIVO_zero()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Pesquisa de clima");

        var resposta = await CriarAsync(api, Novo(titulo, descricao: "Conte como foi o trimestre."));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("criarFormulario");
        var id = formulario.GetProperty("id").GetInt32();
        Assert.Equal((titulo, "Conte como foi o trimestre.", "PESQUISA", "RASCUNHO", false, "2026-10-07T12:42:00Z"), (formulario.GetProperty("titulo").GetString(), formulario.GetProperty("descricao").GetString(), formulario.GetProperty("tipo").GetString(), formulario.GetProperty("situacao").GetString(), formulario.GetProperty("anonimo").GetBoolean(), formulario.GetProperty("criadoEmUtc").GetString()));
        Assert.Equal((api.Elenco.Rh.FuncionarioId, JsonValueKind.Null, false, 0), (formulario.GetProperty("autor").GetProperty("id").GetInt32(), formulario.GetProperty("sentidoAvaliacao").ValueKind, formulario.GetProperty("temRespostas").GetBoolean(), formulario.GetProperty("perguntas").GetArrayLength()));
        var publicacao = formulario.GetProperty("publicacao");
        Assert.Equal((JsonValueKind.Null, 0, 0, true, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (publicacao.GetProperty("alvo").ValueKind, publicacao.GetProperty("setores").GetArrayLength(), publicacao.GetProperty("pessoas").GetArrayLength(), publicacao.GetProperty("lembreteAutomatico").GetBoolean(), publicacao.GetProperty("prazo").ValueKind, publicacao.GetProperty("publicadoEmUtc").ValueKind, publicacao.GetProperty("tokenDoLink").ValueKind, publicacao.GetProperty("recorrencia").ValueKind));
        // o padrão do banco é ATIVO = TRUE: o rascunho só nasce inativo porque a API grava o valor
        Assert.Equal(("False", "True", "2026-10-07 09:42:00", api.Elenco.Rh.FuncionarioId.ToString(System.Globalization.CultureInfo.InvariantCulture)), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT LEMBRETE_AUTOMATICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CRIACAO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ID_FUNCIONARIO_AUTOR FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        Assert.Null(await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Criar_titulo_e_descricao_saem_aparados_e_descricao_em_branco_volta_nula()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Aparado");

        var formulario = await CorpoAsync(await CriarAsync(api, Novo($"  {titulo}  ", descricao: "   ")));

        Assert.Equal((titulo, JsonValueKind.Null), (formulario.GetProperty("titulo").GetString(), formulario.GetProperty("descricao").ValueKind));
    }

    [Fact]
    public async Task Criar_anonimo_devolve_temRespostas_nulo_e_a_avaliacao_da_equipe_sobre_o_gestor_pode_ser_anonima()
    {
        var api = await ApiAsync();

        var pesquisa = await CorpoAsync(await CriarAsync(api, Novo(Titulo("Clima anônimo"), anonimo: true)));
        var doGestor = await CorpoAsync(await CriarAsync(api, Novo(Titulo("Avaliação do gestor"), "AVALIACAO", anonimo: true, sentido: "EQUIPE_AVALIA_GESTOR")));

        Assert.Equal((true, JsonValueKind.Null), (pesquisa.GetProperty("anonimo").GetBoolean(), pesquisa.GetProperty("temRespostas").ValueKind));
        Assert.Equal(("AVALIACAO", "EQUIPE_AVALIA_GESTOR", true), (doGestor.GetProperty("tipo").GetString(), doGestor.GetProperty("sentidoAvaliacao").GetString(), doGestor.GetProperty("anonimo").GetBoolean()));
    }

    [Fact]
    public async Task Criar_avaliacao_exige_o_sentido_e_os_outros_tipos_nao_o_aceitam()
    {
        var api = await ApiAsync();

        var semSentido = await CriarAsync(api, Novo(Titulo("Avaliação"), "AVALIACAO"));
        var sentidoDemais = await CriarAsync(api, Novo(Titulo("Pesquisa"), "PESQUISA", sentido: "GESTOR_AVALIA_EQUIPE"));
        var certa = await CriarAsync(api, Novo(Titulo("Avaliação certa"), "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE"));

        Assert.Equal((400, 400, 201), ((int)semSentido.StatusCode, (int)sentidoDemais.StatusCode, (int)certa.StatusCode));
        Assert.Equal(["Informe quem avalia quem: GESTOR_AVALIA_EQUIPE ou EQUIPE_AVALIA_GESTOR."], (await CorpoAsync(semSentido)).GetProperty("errors").GetProperty("sentidoAvaliacao").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["O sentido só vale para a avaliação."], (await CorpoAsync(sentidoDemais)).GetProperty("errors").GetProperty("sentidoAvaliacao").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Criar_sugestao_anonima_e_avaliacao_do_gestor_sobre_a_equipe_anonima_sao_400_em_anonimo_e_os_erros_vem_juntos()
    {
        var api = await ApiAsync();

        var sugestao = await CriarAsync(api, Novo(Titulo("Sugestões"), "OUTRO", anonimo: true));
        var daEquipe = await CriarAsync(api, Novo(Titulo("Avaliação da equipe"), "AVALIACAO", anonimo: true, sentido: "GESTOR_AVALIA_EQUIPE"));
        var juntos = await CriarAsync(api, Novo("", "OUTRO", anonimo: true, descricao: new string('d', 1001)));

        Assert.Equal((400, 400, 400), ((int)sugestao.StatusCode, (int)daEquipe.StatusCode, (int)juntos.StatusCode));
        Assert.Equal(["A caixa de sugestões não pode ser anônima."], (await CorpoAsync(sugestao)).GetProperty("errors").GetProperty("anonimo").EnumerateArray().Select(e => e.GetString()));
        Assert.Contains("não pode ser anônima", (await CorpoAsync(daEquipe)).GetProperty("errors").GetProperty("anonimo")[0].GetString(), StringComparison.Ordinal);
        Assert.Equal(["anonimo", "descricao", "titulo"], (await CorpoAsync(juntos)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Criar_sem_o_tipo_ou_sem_o_anonimato_no_corpo_e_400_e_nao_vira_o_primeiro_valor()
    {
        var api = await ApiAsync();
        var cliente = await api.ClienteDoRhAgoraAsync();
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM FORMULARIOS");

        var semTipo = await cliente.PostAsJsonAsync(Rota, new { titulo = "Sem tipo", anonimo = false });
        var semAnonimo = await cliente.PostAsJsonAsync(Rota, new { titulo = "Sem anônimo", tipo = "PESQUISA" });
        var tipoInventado = await cliente.PostAsJsonAsync(Rota, new { titulo = "Tipo inventado", tipo = "QUIZ", anonimo = false });

        Assert.Equal((400, 400, 400), ((int)semTipo.StatusCode, (int)semAnonimo.StatusCode, (int)tipoInventado.StatusCode));
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM FORMULARIOS"));
    }

    [Fact]
    public async Task Criar_grava_so_o_evento_CRIOU_com_o_titulo_e_nenhum_dado_de_pergunta()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Auditado");

        var id = await CriarComSucessoAsync(api, Novo(titulo, anonimo: true));

        var eventos = await EventosAsync(api, id);
        var evento = Assert.Single(eventos);
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Criou, $"Criou o formulário \"{titulo}\".", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Quem_nao_e_do_RH_nao_cria_nem_ve_formulario()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Só do RH"));
        var comum = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Funcionário comum"), Perfil.Funcionario);
        var cliente = await Comunicacao.AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal((403, 403, 403), ((int)(await cliente.PostAsJsonAsync(Rota, Novo("Intruso"))).StatusCode, (int)(await cliente.GetAsync($"{Rota}/{id}")).StatusCode, (int)(await cliente.PutAsJsonAsync($"{Rota}/{id}", Novo("Intruso"))).StatusCode));
        Assert.Equal(401, (int)(await api.Api.CreateClient().GetAsync($"{Rota}/{id}")).StatusCode);
    }

    // ---------------------------------------------------------------- obter

    [Fact]
    public async Task Obter_devolve_o_formulario_inteiro_com_perguntas_em_ordem_alternativas_e_a_publicacao_por_pessoas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Completo"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30", descricao: "Texto.");
        await PerguntaAsync(api, id, "Segunda", "MULTIPLA_ESCOLHA", 2, true, "Ótimo", "Bom");
        await PerguntaAsync(api, id, "Primeira", "ESCALA", 1);
        var ana = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Ana Escolhida"), Perfil.Funcionario);
        var bia = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Bia Escolhida"), Perfil.Funcionario);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({id}, {bia}), ({id}, {ana})");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}");
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFormularioRh");
        Assert.Equal(["Primeira", "Segunda"], formulario.GetProperty("perguntas").EnumerateArray().Select(p => p.GetProperty("texto").GetString()));
        var segunda = formulario.GetProperty("perguntas")[1];
        Assert.Equal((2, "MULTIPLA_ESCOLHA", true), (segunda.GetProperty("ordem").GetInt32(), segunda.GetProperty("tipo").GetString(), segunda.GetProperty("obrigatoria").GetBoolean()));
        Assert.Equal(["Ótimo", "Bom"], segunda.GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("texto").GetString()));
        Assert.Equal([1, 2], segunda.GetProperty("alternativas").EnumerateArray().Select(a => a.GetProperty("ordem").GetInt32()));
        Assert.Equal(0, formulario.GetProperty("perguntas")[0].GetProperty("alternativas").GetArrayLength());
        var publicacao = formulario.GetProperty("publicacao");
        Assert.Equal(("PESSOAS", "2026-10-30", "2026-10-01T11:00:00Z", 0), (publicacao.GetProperty("alvo").GetString(), publicacao.GetProperty("prazo").GetString(), publicacao.GetProperty("publicadoEmUtc").GetString(), publicacao.GetProperty("setores").GetArrayLength()));
        Assert.Equal([ana, bia], publicacao.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("id").GetInt32())); // pelo nome: "Ana Escolhida" antes de "Bia Escolhida"
        Assert.All(publicacao.GetProperty("pessoas").EnumerateArray(), p => Assert.True(p.GetProperty("ativo").GetBoolean()));
    }

    [Fact]
    public async Task Obter_por_setores_traz_os_setores_pelo_nome_e_nenhuma_pessoa()
    {
        var api = await ApiAsync();
        var gestor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Gestor do setor"), Perfil.Gestor);
        var setorB = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor Beta"), gestor);
        var setorA = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor Alfa"), gestor);
        var id = await SemearAsync(api, Titulo("Por setores"), "AGENDADO", alvo: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setorB}), ({id}, {setorA})");

        var publicacao = (await ObterAsync(api, id)).GetProperty("publicacao");

        Assert.Equal([setorA, setorB], publicacao.GetProperty("setores").EnumerateArray().Select(s => s.GetProperty("id").GetInt32()));
        Assert.All(publicacao.GetProperty("setores").EnumerateArray(), s => Assert.StartsWith("Setor ", s.GetProperty("nome").GetString(), StringComparison.Ordinal));
        Assert.Equal(("SETORES", 0), (publicacao.GetProperty("alvo").GetString(), publicacao.GetProperty("pessoas").GetArrayLength()));
    }

    [Fact]
    public async Task Obter_inexistente_e_404_com_o_corpo_do_contrato()
    {
        var api = await ApiAsync();

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/2147483000");

        Assert.Equal(404, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFormularioRh");
    }

    [Fact]
    public async Task A_situacao_sai_das_colunas_e_o_prazo_vencido_vale_como_encerrado_mesmo_com_ATIVO_ligado()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho"));
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var publicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-07");
        var vencido = await SemearAsync(api, Titulo("Prazo vencido"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");

        var situacoes = new List<string?>();
        foreach (var id in new[] { rascunho, agendado, publicado, vencido, encerrado })
        {
            situacoes.Add((await ObterAsync(api, id)).GetProperty("situacao").GetString());
        }

        Assert.Equal(["RASCUNHO", "AGENDADO", "PUBLICADO", "ENCERRADO", "ENCERRADO"], situacoes); // o prazo de hoje ainda vale (até 23:59) e o de ontem não, apesar de ATIVO = 1
        // até a tarefa de encerrar rodar, o encerramento que se mostra é o nominal: o começo do dia seguinte ao prazo (prazo 06/10 -> 07/10 00:00 -03:00)
        Assert.Equal(("2026-10-07T03:00:00Z", JsonValueKind.Null), ((await ObterAsync(api, vencido)).GetProperty("publicacao").GetProperty("encerradoEmUtc").GetString(), (await ObterAsync(api, publicado)).GetProperty("publicacao").GetProperty("encerradoEmUtc").ValueKind));
    }

    [Fact]
    public async Task O_dia_agendado_so_aparece_enquanto_agendado_e_as_datas_saem_em_utc()
    {
        var api = await ApiAsync();
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var publicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");

        var a = (await ObterAsync(api, agendado)).GetProperty("publicacao");
        var p = (await ObterAsync(api, publicado)).GetProperty("publicacao");
        var e = (await ObterAsync(api, encerrado)).GetProperty("publicacao");

        Assert.Equal(("2026-10-20", JsonValueKind.Null, JsonValueKind.Null), (a.GetProperty("agendarPara").GetString(), a.GetProperty("publicadoEmUtc").ValueKind, a.GetProperty("encerradoEmUtc").ValueKind));
        Assert.Equal((JsonValueKind.Null, "2026-10-01T11:00:00Z", JsonValueKind.Null), (p.GetProperty("agendarPara").ValueKind, p.GetProperty("publicadoEmUtc").GetString(), p.GetProperty("encerradoEmUtc").ValueKind));
        Assert.Equal((JsonValueKind.Null, "2026-09-01T11:00:00Z", "2026-10-01T03:00:00Z"), (e.GetProperty("agendarPara").ValueKind, e.GetProperty("publicadoEmUtc").GetString(), e.GetProperty("encerradoEmUtc").GetString()));
    }

    [Fact]
    public async Task A_recorrencia_o_lembrete_e_a_proxima_reabertura_saem_da_publicacao()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Recorrente"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 3, RECORRENCIA_UNIDADE = 'SEMANA', PROXIMA_REABERTURA = '2026-11-01 00:00:00', RECORRENCIA_ATE = '2027-01-31', LEMBRETE_AUTOMATICO = FALSE WHERE ID_FORMULARIO = {id}");

        var publicacao = (await ObterAsync(api, id)).GetProperty("publicacao");

        var recorrencia = publicacao.GetProperty("recorrencia");
        Assert.Equal((3, "SEMANA", "2027-01-31"), (recorrencia.GetProperty("intervalo").GetInt32(), recorrencia.GetProperty("unidade").GetString(), recorrencia.GetProperty("ate").GetString()));
        Assert.Equal(("2026-11-01T03:00:00Z", false), (publicacao.GetProperty("proximaReaberturaEmUtc").GetString(), publicacao.GetProperty("lembreteAutomatico").GetBoolean()));
    }

    [Fact]
    public async Task O_token_do_link_so_vem_quando_o_formulario_por_link_ja_tem_o_codigo()
    {
        var api = await ApiAsync();
        var token = new string('k', 43);
        var rascunhoPorLink = await SemearAsync(api, Titulo("Entrevista rascunho"), tipo: "ENTREVISTA", alvo: "LINK");
        var publicadoPorLink = await SemearAsync(api, Titulo("Entrevista publicada"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: token);
        var porPessoas = await SemearAsync(api, Titulo("Por pessoas"), "PUBLICADO", alvo: "PESSOAS");

        Assert.Equal(JsonValueKind.Null, (await ObterAsync(api, rascunhoPorLink)).GetProperty("publicacao").GetProperty("tokenDoLink").ValueKind);
        Assert.Equal(token, (await ObterAsync(api, publicadoPorLink)).GetProperty("publicacao").GetProperty("tokenDoLink").GetString());
        Assert.Equal(JsonValueKind.Null, (await ObterAsync(api, porPessoas)).GetProperty("publicacao").GetProperty("tokenDoLink").ValueKind);
    }

    [Fact]
    public async Task O_temRespostas_e_nulo_no_anonimo_em_qualquer_situacao_e_um_booleano_com_nome()
    {
        var api = await ApiAsync();
        var anonimoRascunho = await SemearAsync(api, Titulo("Anônimo rascunho"), anonimo: true);
        var anonimoRespondido = await PublicadoRespondidoAsync(api, Titulo("Anônimo respondido"), anonimo: true);
        var anonimoEncerrado = await SemearAsync(api, Titulo("Anônimo encerrado"), "ENCERRADO", anonimo: true, alvo: "PESSOAS");
        var comNomeVazio = await SemearAsync(api, Titulo("Com nome vazio"), "PUBLICADO", alvo: "PESSOAS");
        var comNomeRespondido = await PublicadoRespondidoAsync(api, Titulo("Com nome respondido"), anonimo: false);

        var valores = new List<JsonValueKind>();
        foreach (var id in new[] { anonimoRascunho, anonimoRespondido, anonimoEncerrado, comNomeVazio, comNomeRespondido })
        {
            valores.Add((await ObterAsync(api, id)).GetProperty("temRespostas").ValueKind);
        }

        Assert.Equal([JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.False, JsonValueKind.True], valores);
    }

    [Fact]
    public async Task O_autor_apagado_volta_nulo_e_o_autor_inativo_continua_aparecendo()
    {
        var api = await ApiAsync();
        var semAutor = await SemearAsync(api, Titulo("Sem autor"));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ID_FUNCIONARIO_AUTOR = NULL WHERE ID_FORMULARIO = {semAutor}");
        var ex = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Autor desligado"), Perfil.Funcionario);
        var doEx = await SemearAsync(api, Titulo("Do ex"));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ID_FUNCIONARIO_AUTOR = {ex} WHERE ID_FORMULARIO = {doEx}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {ex}");

        var a = await ObterAsync(api, semAutor);
        var b = await ObterAsync(api, doEx);

        Assert.Equal(JsonValueKind.Null, a.GetProperty("autor").ValueKind);
        Assert.Equal((ex, false), (b.GetProperty("autor").GetProperty("id").GetInt32(), b.GetProperty("autor").GetProperty("ativo").GetBoolean()));
    }

    // ---------------------------------------------------------------- editar: rascunho e agendado mudam tudo

    [Fact]
    public async Task Editar_rascunho_muda_titulo_descricao_tipo_sentido_e_o_anonimato_e_o_banco_confere()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Antes"), descricao: "Antiga."));
        var depois = Titulo("Depois");

        var resposta = await EditarAsync(api, id, Novo(depois, "AVALIACAO", anonimo: true, descricao: "Nova.", sentido: "EQUIPE_AVALIA_GESTOR"));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("editarFormulario");
        Assert.Equal((depois, "Nova.", "AVALIACAO", "EQUIPE_AVALIA_GESTOR", true, "RASCUNHO"), (formulario.GetProperty("titulo").GetString(), formulario.GetProperty("descricao").GetString(), formulario.GetProperty("tipo").GetString(), formulario.GetProperty("sentidoAvaliacao").GetString(), formulario.GetProperty("anonimo").GetBoolean(), formulario.GetProperty("situacao").GetString()));
        Assert.Equal((depois, "AVALIACAO", "EQUIPE_AVALIA_GESTOR", "True", "False"), (await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT SENTIDO_AVALIACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ANONIMO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
    }

    [Fact]
    public async Task Editar_agendado_tambem_muda_tudo_e_mudar_o_tipo_para_outro_que_nao_avaliacao_anula_o_sentido()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Agendada"), "AGENDADO", tipo: "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE", alvo: "PESSOAS");

        var resposta = await EditarAsync(api, id, Novo(Titulo("Agora pesquisa"), "PESQUISA", anonimo: true));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("PESQUISA", (string?)null, "True", "AGENDADO"), (await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT SENTIDO_AVALIACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ANONIMO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), (await ObterAsync(api, id)).GetProperty("situacao").GetString()));
    }

    [Fact]
    public async Task Passar_a_anonimo_com_pergunta_de_arquivo_e_422_ANONIMO_SEM_ARQUIVO_e_nada_muda()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Com arquivo"));
        await PerguntaAsync(api, id, "Envie o comprovante", "ARQUIVO");

        var resposta = await EditarAsync(api, id, Novo("Virou anônimo", anonimo: true));
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "ANONIMO_SEM_ARQUIVO", "Formulário anônimo não pode ter pergunta de envio de arquivo."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("editarFormulario");
        Assert.Equal(("False", false), (await TextoAsync(api, $"SELECT ANONIMO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), (await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")) == "Virou anônimo"));
    }

    [Fact]
    public async Task Passar_a_anonimo_com_o_alvo_por_link_e_422_ANONIMO_SEM_LINK_e_mudar_o_tipo_de_um_por_link_e_422_LINK_SO_PARA_ENTREVISTA()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Entrevista por link"), tipo: "ENTREVISTA", alvo: "LINK");

        var anonimo = await EditarAsync(api, id, Novo("Anônimo", "ENTREVISTA", anonimo: true));
        var pesquisa = await EditarAsync(api, id, Novo("Pesquisa", "PESQUISA"));
        var anonimoEPesquisa = await EditarAsync(api, id, Novo("Os dois", "PESQUISA", anonimo: true)); // a primeira recusa é a do anonimato

        Assert.Equal(("ANONIMO_SEM_LINK", "LINK_SO_PARA_ENTREVISTA", "ANONIMO_SEM_LINK"), (Codigo(await CorpoAsync(anonimo)), Codigo(await CorpoAsync(pesquisa)), Codigo(await CorpoAsync(anonimoEPesquisa))));
        Assert.Equal((422, 422, 422), ((int)anonimo.StatusCode, (int)pesquisa.StatusCode, (int)anonimoEPesquisa.StatusCode));
        Assert.Equal("ENTREVISTA", await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Editar_para_sugestao_limpa_o_prazo_e_a_recorrencia_guardados_porque_a_sugestao_e_sempre_aberta()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Vai virar sugestão"), alvo: "PESSOAS", prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 1, RECORRENCIA_UNIDADE = 'MES' WHERE ID_FORMULARIO = {id}");

        var resposta = await EditarAsync(api, id, Novo(Titulo("Sugestões"), "OUTRO"));

        Assert.Equal(200, (int)resposta.StatusCode);
        var publicacao = (await CorpoAsync(resposta)).GetProperty("publicacao");
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, "PESSOAS"), (publicacao.GetProperty("prazo").ValueKind, publicacao.GetProperty("recorrencia").ValueKind, publicacao.GetProperty("alvo").GetString()));
        Assert.Equal((null, null, null), (await TextoAsync(api, $"SELECT DATA_PRAZO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT RECORRENCIA_INTERVALO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT RECORRENCIA_UNIDADE FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
    }

    // ---------------------------------------------------------------- editar: publicado

    [Fact]
    public async Task Anonimo_publicado_so_muda_titulo_e_descricao_e_o_409_e_o_mesmo_com_e_sem_resposta()
    {
        var api = await ApiAsync();
        var semResposta = await SemearAsync(api, Titulo("Anônimo sem resposta"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var comResposta = await PublicadoRespondidoAsync(api, Titulo("Anônimo com resposta"), anonimo: true);
        var veredito = new List<(int, string)>();

        foreach (var id in new[] { semResposta, comResposta })
        {
            var tipo = await EditarAsync(api, id, Novo("Outro tipo", "ENTREVISTA", anonimo: true));
            var anonimato = await EditarAsync(api, id, Novo("Identificado", anonimo: false));
            veredito.Add(((int)tipo.StatusCode, Codigo(await CorpoAsync(tipo))));
            veredito.Add(((int)anonimato.StatusCode, Codigo(await CorpoAsync(anonimato))));
        }

        Assert.All(veredito, v => Assert.Equal((409, "ANONIMATO_FIXO_APOS_PUBLICAR"), v));
        Assert.Equal(4, veredito.Count);
        var titulo = Titulo("Só o título");
        var ok = await EditarAsync(api, comResposta, Novo(titulo, anonimo: true, descricao: "Nova descrição."));
        Assert.Equal(200, (int)ok.StatusCode);
        Assert.Equal((titulo, "Nova descrição.", JsonValueKind.Null), ((await CorpoAsync(ok)).GetProperty("titulo").GetString(), (await CorpoAsync(ok)).GetProperty("descricao").GetString(), (await CorpoAsync(ok)).GetProperty("temRespostas").ValueKind));
    }

    [Fact]
    public async Task Com_nome_publicado_o_anonimato_nunca_muda_com_ou_sem_resposta_e_o_banco_nao_chega_a_recusar()
    {
        var api = await ApiAsync();
        var semResposta = await SemearAsync(api, Titulo("Nome sem resposta"), "PUBLICADO", alvo: "PESSOAS");
        var comResposta = await PublicadoRespondidoAsync(api, Titulo("Nome com resposta"), anonimo: false);

        var a = await EditarAsync(api, semResposta, Novo("Anônimo agora", anonimo: true));
        var b = await EditarAsync(api, comResposta, Novo("Anônimo agora", anonimo: true));

        Assert.All(new[] { a, b }, r => Assert.Equal(409, (int)r.StatusCode));
        Assert.Equal(("ANONIMATO_FIXO_APOS_PUBLICAR", "ANONIMATO_FIXO_APOS_PUBLICAR"), (Codigo(await CorpoAsync(a)), Codigo(await CorpoAsync(b)))); // nunca o 1451 da chave estrangeira (que viraria 500)
        Assert.Equal("False", await TextoAsync(api, $"SELECT ANONIMO FROM FORMULARIOS WHERE ID_FORMULARIO = {comResposta}"));
    }

    [Fact]
    public async Task Com_nome_publicado_sem_resposta_muda_o_tipo_e_com_resposta_so_titulo_e_descricao_mudam()
    {
        var api = await ApiAsync();
        var semResposta = await SemearAsync(api, Titulo("Sem resposta ainda"), "PUBLICADO", alvo: "PESSOAS");
        var comResposta = await PublicadoRespondidoAsync(api, Titulo("Já respondido"), anonimo: false);

        var muda = await EditarAsync(api, semResposta, Novo(Titulo("Virou avaliação"), "AVALIACAO", sentido: "GESTOR_AVALIA_EQUIPE"));
        var recusa = await EditarAsync(api, comResposta, Novo(Titulo("Tentou virar outro"), "OUTRO"));
        var titulo = Titulo("Só o texto");
        var soTexto = await EditarAsync(api, comResposta, Novo(titulo, descricao: "Descrição nova."));

        Assert.Equal((200, "AVALIACAO"), ((int)muda.StatusCode, (await CorpoAsync(muda)).GetProperty("tipo").GetString()));
        var problema = await CorpoAsync(recusa);
        Assert.Equal((422, "FORMULARIO_JA_RESPONDIDO", "Este formulário já tem respostas. Só o título e a descrição podem mudar."), ((int)recusa.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        await recusa.AfirmarConformeAoContratoAsync("editarFormulario");
        Assert.Equal((200, titulo, true), ((int)soTexto.StatusCode, (await CorpoAsync(soTexto)).GetProperty("titulo").GetString(), (await CorpoAsync(soTexto)).GetProperty("temRespostas").GetBoolean()));
        Assert.Equal("PESQUISA", await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {comResposta}"));
    }

    [Fact]
    public async Task O_formulario_por_link_publicado_sem_resposta_nao_sai_de_entrevista()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Entrevista no ar"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('m', 43));

        var recusa = await EditarAsync(api, id, Novo("Pesquisa", "PESQUISA"));
        var mantem = await EditarAsync(api, id, Novo(Titulo("Entrevista com outro título"), "ENTREVISTA"));

        Assert.Equal((422, "LINK_SO_PARA_ENTREVISTA", 200), ((int)recusa.StatusCode, Codigo(await CorpoAsync(recusa)), (int)mantem.StatusCode));
    }

    [Fact]
    public async Task Encerrado_nao_muda_nada_nem_com_nome_nem_anonimo()
    {
        var api = await ApiAsync();
        var comNome = await SemearAsync(api, Titulo("Encerrado com nome"), "ENCERRADO", alvo: "PESSOAS");
        var anonimo = await SemearAsync(api, Titulo("Encerrado anônimo"), "ENCERRADO", anonimo: true, alvo: "PESSOAS");

        var a = await EditarAsync(api, comNome, Novo("Mudou"));
        var b = await EditarAsync(api, anonimo, Novo("Mudou", anonimo: true));

        Assert.All(new[] { a, b }, r => Assert.Equal(422, (int)r.StatusCode));
        Assert.Equal(("FORMULARIO_ENCERRADO", "FORMULARIO_ENCERRADO"), (Codigo(await CorpoAsync(a)), Codigo(await CorpoAsync(b))));
        Assert.Equal("O formulário está encerrado e não pode mais ser alterado.", (await CorpoAsync(a)).GetProperty("detail").GetString());
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO IN ({comNome}, {anonimo}) AND TITULO = 'Mudou'"));
    }

    [Fact]
    public async Task O_prazo_vencido_com_a_tarefa_de_encerrar_parada_ja_nao_deixa_editar()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Venceu ontem"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");

        var resposta = await EditarAsync(api, id, Novo("Tarde demais"));

        Assert.Equal((422, "FORMULARIO_ENCERRADO"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
    }

    [Fact]
    public async Task Editar_inexistente_e_404_e_corpo_invalido_e_400_com_todos_os_erros_juntos()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Para editar"));

        var naoExiste = await EditarAsync(api, 2147483000, Novo("Qualquer"));
        var invalido = await EditarAsync(api, id, Novo("", "OUTRO", anonimo: true, descricao: new string('d', 1001)));

        Assert.Equal((404, 400), ((int)naoExiste.StatusCode, (int)invalido.StatusCode));
        Assert.Equal(["anonimo", "descricao", "titulo"], (await CorpoAsync(invalido)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Editar_grava_so_o_evento_ALTEROU_com_o_titulo_e_sem_nada_do_conteudo_das_perguntas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo auditado"), anonimo: true);
        await PerguntaAsync(api, id, "Pergunta secreta do anônimo");
        var novo = Titulo("Novo título");

        await EditarAsync(api, id, Novo(novo, anonimo: true));

        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Alterou o formulário \"{novo}\"."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.DoesNotContain("secreta", evento.Descricao, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- a corrida entre a leitura e a gravação

    [Fact]
    public async Task Editar_enquanto_outra_aba_publica_e_recebe_resposta_relê_e_da_o_erro_certo_sem_gravar_por_cima()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas abas"));
        var cliente = await ClienteComOutraAbaAsync(api, async () =>
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE, PUBLICO = 'PESSOAS', DATA_PUBLICACAO = '2026-10-07 09:41:00' WHERE ID_FORMULARIO = {id}");
            var pergunta = await PerguntaAsync(api, id, "Como foi?");
            await ResponderAsync(api, id, pergunta, api.Elenco.Rh.FuncionarioId);
        });

        // lido como rascunho (muda tudo), gravado já publicado e respondido: o tipo não pode mais mudar
        var resposta = await cliente.PutAsJsonAsync($"{Rota}/{id}", Novo(Titulo("Virou outro"), "OUTRO"));

        Assert.Equal((422, "FORMULARIO_JA_RESPONDIDO"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
        Assert.Equal("PESQUISA", await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }
}

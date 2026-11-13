using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// A publicação guardada (<c>salvarPublicacaoFormulario</c>) e a verificação (<c>verificarFormulario</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. Guardar só guarda: o formulário continua rascunho (ou agendado), não manda e-mail, não gera link, não roda a verificação
/// nem o mínimo de 5 do anônimo. O <c>agendarPara</c> de um rascunho é aceito e NÃO é guardado; o de um agendado muda a data e reagenda a tarefa. A verificação lê o público já guardado e devolve os itens do contrato na ordem.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PublicacaoGuardadaTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PublicacaoGuardadaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Corpo(string alvo, int[]? setores = null, int[]? pessoas = null, string? agendarPara = null, string? prazo = null, object? recorrencia = null, bool lembrete = true) =>
        new { alvo, setorIds = setores ?? [], funcionarioIds = pessoas ?? [], agendarPara, prazo, recorrencia, lembreteAutomatico = lembrete };

    private static async Task<HttpResponseMessage> SalvarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/publicacao", corpo);

    private static async Task<HttpResponseMessage> VerificarAsync(ApiDeAcesso api, int id) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}/verificacao");

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, ativo: ativa);

    private static async Task<string?> DestinatariosAsync(ApiDeAcesso api, int formularioId) =>
        await TextoAsync(api, $"SELECT GROUP_CONCAT(CONCAT(COALESCE(ID_FUNCIONARIO, 0), ':', COALESCE(ID_SETOR, 0)) ORDER BY ID_FORMULARIO_DESTINATARIO SEPARATOR ',') FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {formularioId}");

    private static Task<long> TarefasAsync(ApiDeAcesso api, int formularioId, string situacao, string? instanteUtc = null) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_FORMULARIO' AND REFERENCIA_ID = {formularioId} AND SITUACAO = '{situacao}'" + (instanteUtc is null ? string.Empty : $" AND CHAVE_IDEMPOTENCIA = 'PUBLICAR_FORMULARIO:formulario={formularioId}:em={instanteUtc}'"));

    // ---------------------------------------------------------------- guardar a publicação

    [Fact]
    public async Task Guardar_pessoas_prazo_recorrencia_e_lembrete_nao_publica_e_o_banco_confere()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Rascunho"));
        var ana = await PessoaAsync(api, "Ana Guardada");
        var bia = await PessoaAsync(api, "Bia Guardada");

        var resposta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [bia, ana, bia], prazo: "2026-10-31", recorrencia: new { intervalo = 2, unidade = "SEMANA", ate = "2027-03-31" }, lembrete: false));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("salvarPublicacaoFormulario");
        var publicacao = formulario.GetProperty("publicacao");
        Assert.Equal(("RASCUNHO", "PESSOAS", "2026-10-31", false, JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (formulario.GetProperty("situacao").GetString(), publicacao.GetProperty("alvo").GetString(), publicacao.GetProperty("prazo").GetString(), publicacao.GetProperty("lembreteAutomatico").GetBoolean(), publicacao.GetProperty("publicadoEmUtc").ValueKind, publicacao.GetProperty("tokenDoLink").ValueKind, publicacao.GetProperty("agendarPara").ValueKind));
        Assert.Equal([ana, bia], publicacao.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Order());
        Assert.Equal((2, "SEMANA", "2027-03-31"), (publicacao.GetProperty("recorrencia").GetProperty("intervalo").GetInt32(), publicacao.GetProperty("recorrencia").GetProperty("unidade").GetString(), publicacao.GetProperty("recorrencia").GetProperty("ate").GetString()));
        Assert.Equal(("PESSOAS", "False", "False", "2"), (await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT LEMBRETE_AUTOMATICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT RECORRENCIA_INTERVALO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        Assert.Equal((null, null, 2), (await TextoAsync(api, $"SELECT DATA_PUBLICACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}")));
        Assert.Equal(0, await TarefasAsync(api, id, "PENDENTE")); // só guarda: nenhuma tarefa
    }

    [Fact]
    public async Task Guardar_de_novo_substitui_o_publico_pessoas_depois_setores_depois_link_e_a_recorrencia_pode_sair()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Muda de público"), tipo: "ENTREVISTA");
        var ana = await PessoaAsync(api, "Ana");
        var gestor = await PessoaAsync(api, "Gestor do setor");
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor"), gestor);

        await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], prazo: "2026-10-31", recorrencia: new { intervalo = 1, unidade = "MES" }));
        var comPessoas = await DestinatariosAsync(api, id);
        await SalvarAsync(api, id, Corpo("SETORES", setores: [setor], prazo: "2026-10-31"));
        var comSetores = await DestinatariosAsync(api, id);
        var porLink = await SalvarAsync(api, id, Corpo("LINK", prazo: "2026-10-31"));

        Assert.Equal((ana + ":0", "0:" + setor, 200, null), (comPessoas, comSetores, (int)porLink.StatusCode, await DestinatariosAsync(api, id)));
        Assert.Equal((null, "LINK", null), (await TextoAsync(api, $"SELECT RECORRENCIA_INTERVALO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"))); // por link só guarda: o código nasce ao publicar
    }

    [Fact]
    public async Task O_agendarPara_de_um_rascunho_e_aceito_e_nao_guardado_o_formulario_continua_rascunho()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Rascunho com data"));
        var ana = await PessoaAsync(api, "Ana");

        var resposta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], agendarPara: "2026-10-20", prazo: "2026-10-30"));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("RASCUNHO", JsonValueKind.Null), (formulario.GetProperty("situacao").GetString(), formulario.GetProperty("publicacao").GetProperty("agendarPara").ValueKind));
        Assert.Null(await TextoAsync(api, $"SELECT DATA_PUBLICACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal(0, await TarefasAsync(api, id, "PENDENTE"));
        Assert.Equal("RASCUNHO", (await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}"))).GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task Agendado_com_agendarPara_muda_a_data_e_reagenda_a_tarefa_e_sem_a_data_mantem_tudo_como_esta()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana");
        using (var escopo = api.Api.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IAgendamentoDoFormulario>().AgendarPublicacaoAsync(id, new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None);
        }

        var semData = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], prazo: "2026-10-30"));
        var antes = ((await CorpoAsync(semData)).GetProperty("publicacao").GetProperty("agendarPara").GetString(), await TarefasAsync(api, id, "PENDENTE", "2026-10-20T03:00:00Z"));
        var comData = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], agendarPara: "2026-10-25", prazo: "2026-10-30"));
        var formulario = await CorpoAsync(comData);

        Assert.Equal(("2026-10-20", 1L), antes); // sem a data, a guardada continua e a tarefa não muda
        Assert.Equal(200, (int)comData.StatusCode);
        Assert.Equal(("AGENDADO", "2026-10-25"), (formulario.GetProperty("situacao").GetString(), formulario.GetProperty("publicacao").GetProperty("agendarPara").GetString()));
        Assert.Equal("2026-10-25 00:00:00", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal((0L, 1L, 1L), (await TarefasAsync(api, id, "PENDENTE", "2026-10-20T03:00:00Z"), await TarefasAsync(api, id, "CANCELADA", "2026-10-20T03:00:00Z"), await TarefasAsync(api, id, "PENDENTE", "2026-10-25T03:00:00Z")));
        var volta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], agendarPara: "2026-10-20", prazo: "2026-10-30")); // a data volta a um instante já usado: a tarefa cancelada é reativada
        Assert.Equal((200, 1L, 1L), ((int)volta.StatusCode, await TarefasAsync(api, id, "PENDENTE", "2026-10-20T03:00:00Z"), await TarefasAsync(api, id, "CANCELADA", "2026-10-25T03:00:00Z")));
    }

    [Fact]
    public async Task O_prazo_de_um_agendado_sem_agendarPara_nao_pode_ser_anterior_ao_dia_ja_agendado()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Agendado para 20/10"), "AGENDADO", alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana");

        var resposta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana], prazo: "2026-10-15"));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal(["O prazo não pode ser anterior ao dia agendado."], (await CorpoAsync(resposta)).GetProperty("errors").GetProperty("prazo").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task Agendado_por_link_que_troca_o_alvo_anula_o_codigo_no_mesmo_update()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Entrevista agendada"), "AGENDADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('t', 43));
        var ana = await PessoaAsync(api, "Ana");

        var resposta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana]));
        var mantem = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana]));

        Assert.Equal((200, 200), ((int)resposta.StatusCode, (int)mantem.StatusCode)); // o CHECK do banco recusaria código com outro alvo: o UPDATE anula os dois juntos
        Assert.Equal(("PESSOAS", null), (await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
    }

    [Fact]
    public async Task Agendado_por_link_que_continua_por_link_guarda_o_mesmo_codigo()
    {
        var api = await ApiAsync();
        var token = new string('u', 43);
        var id = await SemearAsync(api, Titulo("Entrevista agendada"), "AGENDADO", tipo: "ENTREVISTA", alvo: "LINK", token: token);

        var resposta = await SalvarAsync(api, id, Corpo("LINK", prazo: "2026-10-31"));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(token, (await CorpoAsync(resposta)).GetProperty("publicacao").GetProperty("tokenDoLink").GetString());
    }

    [Fact]
    public async Task Publicado_e_encerrado_nao_aceitam_guardar_a_publicacao_e_nada_muda()
    {
        var api = await ApiAsync();
        var publicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana");

        var a = await SalvarAsync(api, publicado, Corpo("PESSOAS", pessoas: [ana]));
        var b = await SalvarAsync(api, encerrado, Corpo("PESSOAS", pessoas: [ana]));

        Assert.Equal((422, "FORMULARIO_JA_PUBLICADO", 422, "FORMULARIO_ENCERRADO"), ((int)a.StatusCode, Codigo(await CorpoAsync(a)), (int)b.StatusCode, Codigo(await CorpoAsync(b))));
        Assert.Equal("Este formulário já está publicado ou agendado.", (await CorpoAsync(a)).GetProperty("detail").GetString());
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO IN ({publicado}, {encerrado})"));
    }

    [Fact]
    public async Task Pessoa_inativa_e_422_FUNCIONARIO_INATIVO_e_ninguem_entra_e_id_que_nao_existe_e_400()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Com inativa"));
        var ativa = await PessoaAsync(api, "Ativa");
        var inativa = await PessoaAsync(api, "Inativa", ativa: false);
        var gestor = await PessoaAsync(api, "Gestor");
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor"), gestor);
        await SalvarAsync(api, id, Corpo("SETORES", setores: [setor])); // o que já estava guardado não some quando a nova tentativa é recusada

        var comInativa = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ativa, inativa]));
        var pessoaInexistente = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ativa, 2147483000]));
        var setorInexistente = await SalvarAsync(api, id, Corpo("SETORES", setores: [setor, 2147483000]));

        var problema = await CorpoAsync(comInativa);
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Uma das pessoas escolhidas está inativa e não pode receber o formulário."), ((int)comInativa.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal((400, 400), ((int)pessoaInexistente.StatusCode, (int)setorInexistente.StatusCode));
        Assert.Equal(["Uma das pessoas escolhidas não existe."], (await CorpoAsync(pessoaInexistente)).GetProperty("errors").GetProperty("funcionarioIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Um dos setores escolhidos não existe."], (await CorpoAsync(setorInexistente)).GetProperty("errors").GetProperty("setorIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal($"0:{setor}", await DestinatariosAsync(api, id));
        Assert.Equal("SETORES", await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task As_regras_de_campo_viram_400_com_os_erros_juntos_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Campos"));

        var juntos = await SalvarAsync(api, id, Corpo("SETORES", pessoas: [1], agendarPara: "2026-10-01", prazo: "2026-09-30"));
        var linkComListas = await SalvarAsync(api, id, Corpo("LINK", setores: [1]));
        var sugestao = await SalvarAsync(api, await SemearAsync(api, Titulo("Sugestões"), tipo: "OUTRO"), Corpo("PESSOAS", pessoas: [api.Elenco.Rh.FuncionarioId], prazo: "2026-10-30"));
        var recorrenteSemPrazo = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [api.Elenco.Rh.FuncionarioId], recorrencia: new { intervalo = 1, unidade = "DIA" }));
        var semAlvo = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/publicacao", new { setorIds = new[] { 1 } });

        Assert.All(new[] { juntos, linkComListas, sugestao, recorrenteSemPrazo, semAlvo }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.Equal(["agendarPara", "funcionarioIds", "prazo", "setorIds"], (await CorpoAsync(juntos)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(["prazo"], (await CorpoAsync(sugestao)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Null(await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        await juntos.AfirmarConformeAoContratoAsync("salvarPublicacaoFormulario");
    }

    [Fact]
    public async Task Anonimo_por_link_e_422_ANONIMO_SEM_LINK_e_o_que_nao_e_entrevista_por_link_e_422_LINK_SO_PARA_ENTREVISTA()
    {
        var api = await ApiAsync();
        var anonimo = await SemearAsync(api, Titulo("Anônimo"), anonimo: true, tipo: "ENTREVISTA");
        var pesquisa = await SemearAsync(api, Titulo("Pesquisa"));

        var a = await SalvarAsync(api, anonimo, Corpo("LINK"));
        var b = await SalvarAsync(api, pesquisa, Corpo("LINK"));

        Assert.Equal((422, "ANONIMO_SEM_LINK", 422, "LINK_SO_PARA_ENTREVISTA"), ((int)a.StatusCode, Codigo(await CorpoAsync(a)), (int)b.StatusCode, Codigo(await CorpoAsync(b))));
        Assert.Null(await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO IN ({anonimo}, {pesquisa}) AND PUBLICO IS NOT NULL LIMIT 1"));
    }

    [Fact]
    public async Task O_anonimo_para_poucas_pessoas_pode_ser_guardado_porque_o_minimo_de_5_so_vale_ao_publicar_e_a_verificacao_nao_roda()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo pequeno"), anonimo: true);

        var resposta = await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [api.Elenco.Rh.FuncionarioId])); // sem nenhuma pergunta e para 1 pessoa: um publicar recusaria

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("PESSOAS", await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Guardar_grava_so_o_evento_ALTEROU_sem_a_lista_de_pessoas_nem_o_publico()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Auditoria da publicação");
        var id = await SemearAsync(api, titulo, anonimo: true);
        var ana = await PessoaAsync(api, "Ana Auditada");

        await SalvarAsync(api, id, Corpo("PESSOAS", pessoas: [ana]));

        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Guardou a publicação do formulário \"{titulo}\"."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.DoesNotContain("Ana", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Guardar_enquanto_outra_aba_publica_relê_e_da_422_sem_gravar_por_cima()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas abas"));
        var ana = await PessoaAsync(api, "Ana");
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE, PUBLICO = 'PESSOAS', DATA_PUBLICACAO = '2026-10-07 09:41:00' WHERE ID_FORMULARIO = {id}"));

        var resposta = await cliente.PutAsJsonAsync($"{Rota}/{id}/publicacao", Corpo("PESSOAS", pessoas: [ana]));

        Assert.Equal((422, "FORMULARIO_JA_PUBLICADO"), ((int)resposta.StatusCode, Codigo(await CorpoAsync(resposta))));
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Formulario_inexistente_e_404_e_quem_nao_e_do_RH_recebe_403_na_publicacao_e_na_verificacao()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Só do RH"));
        var comum = await PessoaAsync(api, "Comum");
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal((404, 404), ((int)(await SalvarAsync(api, 2147483000, Corpo("PESSOAS", pessoas: [comum]))).StatusCode, (int)(await VerificarAsync(api, 2147483000)).StatusCode));
        Assert.Equal((403, 403), ((int)(await cliente.PutAsJsonAsync($"{Rota}/{id}/publicacao", Corpo("PESSOAS", pessoas: [comum]))).StatusCode, (int)(await cliente.GetAsync($"{Rota}/{id}/verificacao")).StatusCode));
    }

    // ---------------------------------------------------------------- a verificação

    [Fact]
    public async Task A_verificacao_de_um_formulario_certo_devolve_os_quatro_itens_do_exemplo_do_contrato()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Tudo certo"), descricao: "Conte como foi.");
        await PerguntaAsync(api, id, "Nome", ordem: 1, obrigatoria: true);
        await PerguntaAsync(api, id, "Escolha", "MULTIPLA_ESCOLHA", 2, true, "Sim", "Não");
        await PerguntaAsync(api, id, "Nota", "ESCALA", 3, true);
        await PerguntaAsync(api, id, "Comentário", "RESPOSTA_LONGA", 4);

        var resposta = await VerificarAsync(api, id);
        var verificacao = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("verificarFormulario");
        Assert.True(verificacao.GetProperty("podePublicar").GetBoolean());
        Assert.Equal(
            ["TITULO_E_DESCRICAO:OK:Título e descrição preenchidos.:", "PERGUNTAS:OK:4 perguntas, 3 obrigatórias.:", "ALTERNATIVAS:OK:Toda pergunta de múltipla escolha tem pelo menos 2 alternativas.:", "DADO_SENSIVEL:OK:Nenhuma pergunta pede dado sensível, como saúde, religião ou orientação sexual.:"],
            verificacao.GetProperty("itens").EnumerateArray().Select(i => $"{i.GetProperty("codigo").GetString()}:{i.GetProperty("situacao").GetString()}:{i.GetProperty("mensagem").GetString()}:{(i.GetProperty("perguntaId").ValueKind == JsonValueKind.Null ? string.Empty : i.GetProperty("perguntaId").ToString())}"));
    }

    [Fact]
    public async Task A_verificacao_de_um_anonimo_com_avisos_e_dois_bloqueios_sai_como_o_exemplo_do_contrato_e_usa_o_publico_ja_guardado()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo com problemas"), anonimo: true, alvo: "PESSOAS");
        var multipla = await PerguntaAsync(api, id, "Escolha uma", "MULTIPLA_ESCOLHA", 1, true, "Só uma");
        var saude = await PerguntaAsync(api, id, "Você tem algum problema de saúde?", ordem: 2);
        var quatro = new[] { await PessoaAsync(api, "P1"), await PessoaAsync(api, "P2"), await PessoaAsync(api, "P3"), await PessoaAsync(api, "P4") };
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", quatro.Select(p => $"({id}, {p})"))}");

        var resposta = await VerificarAsync(api, id);
        var verificacao = await CorpoAsync(resposta);

        await resposta.AfirmarConformeAoContratoAsync("verificarFormulario");
        Assert.False(verificacao.GetProperty("podePublicar").GetBoolean());
        var itens = verificacao.GetProperty("itens").EnumerateArray().Select(i => (i.GetProperty("codigo").GetString(), i.GetProperty("situacao").GetString(), i.GetProperty("mensagem").GetString(), i.GetProperty("perguntaId").ValueKind == JsonValueKind.Null ? (int?)null : i.GetProperty("perguntaId").GetInt32())).ToList();
        Assert.Equal(
            [
                ("TITULO_E_DESCRICAO", "AVISO", "O formulário não tem descrição.", (int?)null),
                ("PERGUNTAS", "OK", "2 perguntas, 1 obrigatória.", null),
                ("ALTERNATIVAS", "BLOQUEIO", "A pergunta 1 é de múltipla escolha e precisa de pelo menos 2 alternativas.", multipla),
                ("ANONIMO_RESULTADO_MINIMO", "AVISO", "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais.", null),
                ("ANONIMO_PUBLICO_MINIMO", "BLOQUEIO", "O formulário anônimo está para 4 pessoas e precisa de pelo menos 5.", null),
                ("DADO_SENSIVEL", "AVISO", "A pergunta 2 parece pedir dado de saúde. Confira se ela é necessária.", saude),
            ],
            itens);
    }

    [Fact]
    public async Task O_publico_do_anonimo_conta_so_pessoas_ativas_e_ainda_nao_escolhido_e_aviso()
    {
        var api = await ApiAsync();
        var semPublico = await SemearAsync(api, Titulo("Sem público"), anonimo: true);
        var cinco = await SemearAsync(api, Titulo("Cinco pessoas"), anonimo: true, alvo: "PESSOAS");
        var seisComUmInativo = await SemearAsync(api, Titulo("Seis com um inativo"), anonimo: true, alvo: "PESSOAS");
        var pessoas = new List<int>();
        for (var i = 0; i < 6; i++)
        {
            pessoas.Add(await PessoaAsync(api, $"Pessoa {i}", ativa: i != 5));
        }

        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Take(5).Select(p => $"({cinco}, {p})"))}");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({seisComUmInativo}, {p})"))}");

        async Task<(string, string)> ItemAsync(int id)
        {
            var itens = (await CorpoAsync(await VerificarAsync(api, id))).GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("codigo").GetString() == "ANONIMO_PUBLICO_MINIMO");
            return (itens.GetProperty("situacao").GetString()!, itens.GetProperty("mensagem").GetString()!);
        }

        Assert.Equal(("AVISO", "Escolha pelo menos 5 pessoas na etapa Publicação."), await ItemAsync(semPublico));
        Assert.Equal(("OK", "5 pessoas."), await ItemAsync(cinco));
        Assert.Equal(("OK", "5 pessoas."), await ItemAsync(seisComUmInativo)); // o sexto está inativo: não conta
    }

    [Fact]
    public async Task A_verificacao_sem_pergunta_bloqueia_e_vale_em_qualquer_situacao()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Vazio"));
        var encerrado = await SemearAsync(api, Titulo("Encerrado vazio"), "ENCERRADO", alvo: "PESSOAS");

        var a = await CorpoAsync(await VerificarAsync(api, rascunho));
        var b = await VerificarAsync(api, encerrado);

        Assert.False(a.GetProperty("podePublicar").GetBoolean());
        Assert.Equal("O formulário não tem nenhuma pergunta.", a.GetProperty("itens")[1].GetProperty("mensagem").GetString());
        Assert.Equal(200, (int)b.StatusCode);
    }

    [Fact]
    public async Task Agendado_que_vira_por_link_ganha_o_codigo_do_link_e_o_mantem_ao_guardar_de_novo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Entrevista que virou link"), "AGENDADO", tipo: "ENTREVISTA", alvo: "PESSOAS");

        var primeira = await SalvarAsync(api, id, Corpo("LINK", prazo: "2026-10-31"));
        var codigo = await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");
        var segunda = await SalvarAsync(api, id, Corpo("LINK", prazo: "2026-10-31"));

        Assert.Equal((200, 200), ((int)primeira.StatusCode, (int)segunda.StatusCode));
        Assert.Equal(43, codigo?.Length); // sem o código a tarefa publicaria uma entrevista por link que ninguém consegue abrir
        Assert.Equal(codigo, (await CorpoAsync(primeira)).GetProperty("publicacao").GetProperty("tokenDoLink").GetString());
        Assert.Equal(codigo, await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")); // guardar de novo não troca o código
    }
}

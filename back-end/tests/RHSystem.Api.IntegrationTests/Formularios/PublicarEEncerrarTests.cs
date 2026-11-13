using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Publicar, agendar e encerrar o formulário (<c>publicarFormulario</c> e <c>encerrarFormulario</c>) e as duas tarefas (<c>PUBLICAR_FORMULARIO</c> e <c>ENCERRAR_FORMULARIO</c>), no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 em Brasília). Ordem do publicar: o mínimo de 5 do
/// anônimo ANTES da verificação, depois os bloqueios, depois publica agora (<c>ATIVO</c> e a data do relógio) ou agenda (a tarefa no começo do dia -03:00). Sem e-mail em nenhum dos dois. As tarefas têm chave que leva o instante e RELÊEM o formulário antes de agir.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PublicarEEncerrarTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PublicarEEncerrarTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Corpo(string alvo, int[]? setores = null, int[]? pessoas = null, string? agendarPara = null, string? prazo = null, object? recorrencia = null, bool lembrete = true) =>
        new { alvo, setorIds = setores ?? [], funcionarioIds = pessoas ?? [], agendarPara, prazo, recorrencia, lembreteAutomatico = lembrete };

    private static async Task<HttpResponseMessage> PublicarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/publicar", corpo);

    private static async Task<HttpResponseMessage> EncerrarAsync(ApiDeAcesso api, int id, bool? confirmar = null) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsync($"{Rota}/{id}/encerrar" + (confirmar is null ? string.Empty : $"?confirmar={(confirmar.Value ? "true" : "false")}"), content: null);

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

    /// <summary>Um formulário de rascunho com uma pergunta e a descrição (a verificação passa), pronto para publicar.</summary>
    private static async Task<int> ProntoAsync(ApiDeAcesso api, string titulo, bool anonimo = false, string tipo = "PESQUISA", string? sentido = null)
    {
        var id = await SemearAsync(api, titulo, anonimo: anonimo, tipo: tipo, sentido: sentido, descricao: "Conte como foi.");
        await PerguntaAsync(api, id, "Como foi?", obrigatoria: true);
        return id;
    }

    private static Task<long> TarefasAsync(ApiDeAcesso api, string tipo, int formularioId, string situacao, string? instanteUtc = null) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{tipo}' AND REFERENCIA_ID = {formularioId} AND SITUACAO = '{situacao}'" + (instanteUtc is null ? string.Empty : $" AND CHAVE_IDEMPOTENCIA = '{tipo}:formulario={formularioId}:em={instanteUtc}'"));

    private static async Task ExecutarTarefaAsync(ApiDeAcesso api, string tipo, int formularioId, string instanteUtc)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var tarefa = new TarefaParaExecutar(0, $"{tipo}:formulario={formularioId}:em={instanteUtc}", formularioId, null, null, 0);
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).ExecutarAsync(tarefa, Ct);
    }

    private static async Task RecuperarAsync(ApiDeAcesso api, string tipo)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).RecuperarAsync(Ct);
    }

    // ---------------------------------------------------------------- publicar agora

    [Fact]
    public async Task Publicar_agora_por_pessoas_vira_publicado_com_a_data_do_relogio_agenda_o_encerramento_pelo_prazo_e_nao_manda_e_mail()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Pesquisa de clima"));
        var pessoas = await PessoasAsync(api, 3);
        var emailsAntes = await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, prazo: "2026-10-31"));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("publicarFormulario");
        var publicacao = formulario.GetProperty("publicacao");
        Assert.Equal(("PUBLICADO", "PESSOAS", "2026-10-07T12:42:00Z", "2026-10-31", JsonValueKind.Null, JsonValueKind.Null), (formulario.GetProperty("situacao").GetString(), publicacao.GetProperty("alvo").GetString(), publicacao.GetProperty("publicadoEmUtc").GetString(), publicacao.GetProperty("prazo").GetString(), publicacao.GetProperty("tokenDoLink").ValueKind, publicacao.GetProperty("agendarPara").ValueKind));
        Assert.Equal(pessoas.Order(), publicacao.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()).Order());
        Assert.Equal(("True", "2026-10-07 09:42:00", pessoas.Length), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), (int)await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}")));
        Assert.Equal((0L, 1L), (await TarefasAsync(api, "PUBLICAR_FORMULARIO", id, "PENDENTE"), await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, "PENDENTE", "2026-11-01T03:00:00Z"))); // o prazo 31/10 encerra no começo do dia 01/11 (-03:00)
        Assert.Equal(emailsAntes, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS")); // a publicação NÃO manda e-mail
    }

    [Fact]
    public async Task Publicar_sem_prazo_nao_agenda_encerramento_e_publicar_por_setores_guarda_os_setores()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Sem prazo"));
        var gestor = await PessoaAsync(api, "Gestor");
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor"), gestor);

        var resposta = await PublicarAsync(api, id, Corpo("SETORES", setores: [setor]));
        var publicacao = (await CorpoAsync(resposta)).GetProperty("publicacao");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(setor, publicacao.GetProperty("setores")[0].GetProperty("id").GetInt32());
        Assert.Equal((0L, 0L, "0:" + setor), (await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, "PENDENTE"), await TarefasAsync(api, "PUBLICAR_FORMULARIO", id, "PENDENTE"), await TextoAsync(api, $"SELECT GROUP_CONCAT(CONCAT(COALESCE(ID_FUNCIONARIO, 0), ':', COALESCE(ID_SETOR, 0))) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}")));
    }

    [Fact]
    public async Task Publicar_por_link_agora_gera_o_codigo_de_43_caracteres_base64url_diferente_a_cada_formulario()
    {
        var api = await ApiAsync();
        var a = await ProntoAsync(api, Titulo("Entrevista A"), tipo: "ENTREVISTA");
        var b = await ProntoAsync(api, Titulo("Entrevista B"), tipo: "ENTREVISTA");

        var ra = await PublicarAsync(api, a, Corpo("LINK", prazo: "2026-10-31"));
        var rb = await PublicarAsync(api, b, Corpo("LINK"));
        var tokenA = (await CorpoAsync(ra)).GetProperty("publicacao").GetProperty("tokenDoLink").GetString()!;
        var tokenB = (await CorpoAsync(rb)).GetProperty("publicacao").GetProperty("tokenDoLink").GetString()!;

        Assert.Equal((200, 200), ((int)ra.StatusCode, (int)rb.StatusCode));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", tokenA);
        Assert.NotEqual(tokenA, tokenB);
        Assert.Equal(tokenA, await TextoAsync(api, $"SELECT LINK_PUBLICO_CODIGO FROM FORMULARIOS WHERE ID_FORMULARIO = {a}"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO IN ({a}, {b})")); // por link não há destinatários
    }

    [Fact]
    public async Task A_recorrencia_guarda_a_proxima_reabertura_e_sem_proxima_se_o_limite_vem_antes()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 2);
        var recorrente = await ProntoAsync(api, Titulo("Recorrente"));
        var limitado = await ProntoAsync(api, Titulo("Recorrente limitado"));

        await PublicarAsync(api, recorrente, Corpo("PESSOAS", pessoas: pessoas, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES" }));
        await PublicarAsync(api, limitado, Corpo("PESSOAS", pessoas: pessoas, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES", ate = "2026-10-31" }));

        var a = (await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{recorrente}"))).GetProperty("publicacao");
        var b = (await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{limitado}"))).GetProperty("publicacao");
        Assert.Equal(("2026-11-07T03:00:00Z", 1), (a.GetProperty("proximaReaberturaEmUtc").GetString(), a.GetProperty("recorrencia").GetProperty("intervalo").GetInt32())); // 07/10 + 1 mês = 07/11 às 00:00 -03:00
        Assert.Equal((JsonValueKind.Null, 1), (b.GetProperty("proximaReaberturaEmUtc").ValueKind, b.GetProperty("recorrencia").GetProperty("intervalo").GetInt32())); // 07/11 passa do limite 31/10
    }

    // ---------------------------------------------------------------- agendar

    [Fact]
    public async Task Agendar_para_uma_data_futura_vira_agendado_gera_o_codigo_por_link_e_agenda_a_tarefa_no_comeco_do_dia()
    {
        var api = await ApiAsync();
        var porLink = await ProntoAsync(api, Titulo("Entrevista agendada"), tipo: "ENTREVISTA");
        var porPessoas = await ProntoAsync(api, Titulo("Pesquisa agendada"));
        var pessoas = await PessoasAsync(api, 2);

        var a = await PublicarAsync(api, porLink, Corpo("LINK", agendarPara: "2026-11-03", prazo: "2026-11-17"));
        var b = await PublicarAsync(api, porPessoas, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03", prazo: "2026-11-17"));
        var pa = (await CorpoAsync(a)).GetProperty("publicacao");
        var pb = (await CorpoAsync(b)).GetProperty("publicacao");

        Assert.Equal((200, 200), ((int)a.StatusCode, (int)b.StatusCode));
        await a.AfirmarConformeAoContratoAsync("publicarFormulario");
        Assert.Equal(("AGENDADO", "2026-11-03", JsonValueKind.Null), ((await CorpoAsync(b)).GetProperty("situacao").GetString(), pb.GetProperty("agendarPara").GetString(), pb.GetProperty("publicadoEmUtc").ValueKind));
        Assert.Matches("^[A-Za-z0-9_-]{43}$", pa.GetProperty("tokenDoLink").GetString()!); // o código nasce mesmo agendando
        Assert.Equal(("False", "2026-11-03 00:00:00"), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {porPessoas}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {porPessoas}")));
        Assert.Equal((1L, 1L), (await TarefasAsync(api, "PUBLICAR_FORMULARIO", porPessoas, "PENDENTE", "2026-11-03T03:00:00Z"), await TarefasAsync(api, "ENCERRAR_FORMULARIO", porPessoas, "PENDENTE", "2026-11-18T03:00:00Z")));
    }

    [Fact]
    public async Task Agendar_para_hoje_publica_agora_e_agendar_para_o_passado_e_400()
    {
        var api = await ApiAsync();
        var hoje = await ProntoAsync(api, Titulo("Para hoje"));
        var passado = await ProntoAsync(api, Titulo("Para ontem"));
        var pessoas = await PessoasAsync(api, 1);

        var agora = await PublicarAsync(api, hoje, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-10-07"));
        var antes = await PublicarAsync(api, passado, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-10-06"));

        Assert.Equal((200, "PUBLICADO", 400), ((int)agora.StatusCode, (await CorpoAsync(agora)).GetProperty("situacao").GetString(), (int)antes.StatusCode));
        Assert.Equal(0L, await TarefasAsync(api, "PUBLICAR_FORMULARIO", hoje, "PENDENTE"));
    }

    // ---------------------------------------------------------------- as recusas do publicar

    [Fact]
    public async Task Publicar_anonimo_para_menos_de_5_e_422_FORMULARIO_ANONIMO_PUBLICO_PEQUENO_antes_da_verificacao()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Anônimo sem pergunta"), anonimo: true); // a verificação também bloquearia (nenhuma pergunta)
        var quatro = await PessoasAsync(api, 4);

        var resposta = await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: quatro));
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "FORMULARIO_ANONIMO_PUBLICO_PEQUENO", "Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para 4."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("publicarFormulario");
        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Publicar_com_bloqueio_na_verificacao_e_422_VERIFICACAO_COM_BLOQUEIO_e_nada_muda()
    {
        var api = await ApiAsync();
        var semPergunta = await SemearAsync(api, Titulo("Sem pergunta"));
        var pessoas = await PessoasAsync(api, 2);

        var resposta = await PublicarAsync(api, semPergunta, Corpo("PESSOAS", pessoas: pessoas));
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "VERIFICACAO_COM_BLOQUEIO", "O formulário tem itens a corrigir antes de publicar. Veja a etapa Verificação."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(("False", 0L, null), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {semPergunta}"), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {semPergunta}"), await TextoAsync(api, $"SELECT PUBLICO FROM FORMULARIOS WHERE ID_FORMULARIO = {semPergunta}")));
    }

    [Fact]
    public async Task Publicar_o_que_ja_esta_publicado_ou_agendado_e_422_FORMULARIO_JA_PUBLICADO_e_encerrado_e_422_FORMULARIO_ENCERRADO()
    {
        var api = await ApiAsync();
        var publicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var pessoas = await PessoasAsync(api, 1);

        var a = await PublicarAsync(api, publicado, Corpo("PESSOAS", pessoas: pessoas));
        var b = await PublicarAsync(api, agendado, Corpo("PESSOAS", pessoas: pessoas));
        var c = await PublicarAsync(api, encerrado, Corpo("PESSOAS", pessoas: pessoas));

        Assert.Equal(("FORMULARIO_JA_PUBLICADO", "FORMULARIO_JA_PUBLICADO", "FORMULARIO_ENCERRADO"), (Codigo(await CorpoAsync(a)), Codigo(await CorpoAsync(b)), Codigo(await CorpoAsync(c))));
        Assert.All(new[] { a, b, c }, r => Assert.Equal(422, (int)r.StatusCode));
    }

    [Fact]
    public async Task O_corpo_do_publicar_segue_as_mesmas_regras_do_guardar_400_pessoa_inativa_422_anonimo_por_link_422_e_404()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Regras"));
        var ativa = await PessoaAsync(api, "Ativa");
        var inativa = await PessoaAsync(api, "Inativa", ativa: false);
        var anonimo = await ProntoAsync(api, Titulo("Anônimo"), anonimo: true, tipo: "ENTREVISTA");

        var campos = await PublicarAsync(api, id, Corpo("SETORES", pessoas: [ativa]));
        var comInativa = await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: [ativa, inativa]));
        var anonimoPorLink = await PublicarAsync(api, anonimo, Corpo("LINK"));
        var inexistente = await PublicarAsync(api, 2147483000, Corpo("PESSOAS", pessoas: [ativa]));

        Assert.Equal((400, 422, 422, 404), ((int)campos.StatusCode, (int)comInativa.StatusCode, (int)anonimoPorLink.StatusCode, (int)inexistente.StatusCode));
        Assert.Equal(("FUNCIONARIO_INATIVO", "ANONIMO_SEM_LINK"), (Codigo(await CorpoAsync(comInativa)), Codigo(await CorpoAsync(anonimoPorLink))));
        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Quem_nao_e_do_RH_nao_publica_nem_encerra()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Só o RH"));
        var comum = await PessoaAsync(api, "Comum");
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal((403, 403), ((int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/publicar", Corpo("PESSOAS", pessoas: [comum]))).StatusCode, (int)(await cliente.PostAsync($"{Rota}/{id}/encerrar", content: null)).StatusCode));
    }

    // ---------------------------------------------------------------- o anônimo: a fotografia

    [Fact]
    public async Task Publicar_anonimo_por_setor_tira_a_fotografia_uma_linha_por_pessoa_e_mantem_a_linha_do_setor()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        for (var i = 0; i < 5; i++)
        {
            await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        }

        var id = await ProntoAsync(api, Titulo("Anônimo por setor"), anonimo: true);

        var resposta = await PublicarAsync(api, id, Corpo("SETORES", setores: [setor.SetorId]));
        var formulario = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((JsonValueKind.Null, "PUBLICADO"), (formulario.GetProperty("temRespostas").ValueKind, formulario.GetProperty("situacao").GetString()));
        Assert.Equal((6L, 1L), (await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_FUNCIONARIO IS NOT NULL"), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_SETOR = {setor.SetorId}"))); // 5 membros + o gestor do setor
    }

    // ---------------------------------------------------------------- auditoria

    [Fact]
    public async Task Publicar_e_agendar_gravam_o_evento_PUBLICOU_sem_contagem_de_destinatarios()
    {
        var api = await ApiAsync();
        var agora = await ProntoAsync(api, Titulo("Publicado agora"), anonimo: true);
        var depois = await ProntoAsync(api, Titulo("Agendado"));
        var cinco = await PessoasAsync(api, 5);

        await PublicarAsync(api, agora, Corpo("PESSOAS", pessoas: cinco));
        await PublicarAsync(api, depois, Corpo("PESSOAS", pessoas: cinco, agendarPara: "2026-11-03"));

        var tituloAgora = await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {agora}");
        var tituloDepois = await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {depois}");
        var a = Assert.Single(await EventosAsync(api, agora));
        var b = Assert.Single(await EventosAsync(api, depois));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Publicou, $"Publicou o formulário \"{tituloAgora}\"."), (a.Area, a.Acao, a.Descricao));
        Assert.Equal((AuditoriaAcao.Publicou, $"Agendou o formulário \"{tituloDepois}\" para 03/11/2026."), (b.Acao, b.Descricao));
    }

    // ---------------------------------------------------------------- a corrida do publicar

    [Fact]
    public async Task Duas_abas_publicando_ao_mesmo_tempo_dao_um_200_e_um_422_nunca_500_e_so_uma_publicacao()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Duas abas publicando"));
        var pessoas = await PessoasAsync(api, 2);
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => cliente.PostAsJsonAsync($"{Rota}/{id}/publicar", Corpo("PESSOAS", pessoas: pessoas, prazo: "2026-10-31"))));

        var status = respostas.Select(r => (int)r.StatusCode).Order().ToArray();
        Assert.Equal([200, 422], status);
        Assert.Equal(1L, await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, "PENDENTE"));
        Assert.Single(await EventosAsync(api, id));
    }

    // ---------------------------------------------------------------- a tarefa de publicar

    [Fact]
    public async Task A_tarefa_publica_o_agendado_uma_vez_e_rodar_de_novo_nao_faz_nada()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Agendado para 03/11"));
        var pessoas = await PessoasAsync(api, 2);
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03", prazo: "2026-11-17", recorrencia: new { intervalo = 7, unidade = "DIA" }));
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");
        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");

        Assert.Equal(("True", "2026-11-03 00:00:00", "2026-11-10 00:00:00"), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(PROXIMA_REABERTURA, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        var eventos = (await EventosAsync(api, id)).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, eventos.Count); // o "agendou" do RH e uma só publicação do sistema
        Assert.Equal((TipoDeAtor.Sistema, AuditoriaAcao.Publicou), (eventos[1].AtorTipo, eventos[1].Acao));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_do_anonimo_agendado_tira_a_fotografia_so_na_hora_de_publicar()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        for (var i = 0; i < 5; i++)
        {
            await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        }

        var id = await ProntoAsync(api, Titulo("Anônimo agendado"), anonimo: true);
        await PublicarAsync(api, id, Corpo("SETORES", setores: [setor.SetorId], agendarPara: "2026-11-03"));
        var linhasAntes = await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_FUNCIONARIO IS NOT NULL");
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor); // entra no setor depois de agendar e antes do dia: a fotografia é do dia da publicação
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");

        Assert.Equal((0L, 7L, "True"), (linhasAntes, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_FUNCIONARIO IS NOT NULL"), await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_nao_publica_o_anonimo_que_caiu_abaixo_de_5_e_o_formulario_continua_agendado()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var id = await ProntoAsync(api, Titulo("Anônimo que encolheu"), anonimo: true);
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03"));
        await AdvertenciasDeTeste.InativarAsync(api, pessoas[0]);
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");

        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        var verificacao = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}/verificacao"));
        Assert.False(verificacao.GetProperty("podePublicar").GetBoolean()); // a verificação mostra o bloqueio: 4 pessoas
        Assert.Single(await EventosAsync(api, id)); // só o "agendou" do RH: nada foi publicado
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_publica_o_anonimo_agendado_que_tem_exatamente_5_pessoas()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var id = await ProntoAsync(api, Titulo("Anônimo com 5"), anonimo: true);
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03"));
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");

        Assert.Equal("True", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")); // 5 é o mínimo: publica
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_antiga_de_um_dia_que_o_RH_trocou_nao_publica_nada()
    {
        var api = await ApiAsync();
        var id = await ProntoAsync(api, Titulo("Reagendado"));
        var pessoas = await PessoasAsync(api, 2);
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03"));
        await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/publicacao", Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-10"));
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z"); // a tarefa do dia 03 já foi cancelada, mas se rodasse nada acontece: o formulário é do dia 10

        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_de_um_formulario_apagado_ou_ja_publicado_a_mao_nao_faz_nada_e_a_recuperacao_agenda_o_agendado_sem_tarefa()
    {
        var api = await ApiAsync();
        var semTarefa = await SemearAsync(api, Titulo("Agendado sem tarefa"), "AGENDADO", alvo: "PESSOAS");
        var jaPublicado = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");

        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", 2147483000, "2026-11-03T03:00:00Z");
        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", jaPublicado, "2026-10-01T11:00:00Z");
        await RecuperarAsync(api, "PUBLICAR_FORMULARIO");
        await RecuperarAsync(api, "PUBLICAR_FORMULARIO"); // idempotente

        Assert.Equal((1L, 0L), (await TarefasAsync(api, "PUBLICAR_FORMULARIO", semTarefa, "PENDENTE", "2026-10-20T03:00:00Z"), await TarefasAsync(api, "PUBLICAR_FORMULARIO", jaPublicado, "PENDENTE")));
    }

    // ---------------------------------------------------------------- encerrar

    [Fact]
    public async Task Encerrar_com_pendentes_pede_confirmacao_com_o_numero_e_com_confirmar_encerra_na_hora_do_relogio()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Com pendentes"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        var pessoas = await PessoasAsync(api, 3);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({id}, {p})"))}");
        await ResponderAsync(api, id, pergunta, pessoas[0]);

        var pedida = await EncerrarAsync(api, id);
        var problema = await CorpoAsync(pedida);
        var recusada = await EncerrarAsync(api, id, confirmar: false);
        var feita = await EncerrarAsync(api, id, confirmar: true);

        Assert.Equal((409, "CONFIRMACAO_NECESSARIA", "Confirmação necessária.", 2), ((int)pedida.StatusCode, Codigo(problema), problema.GetProperty("title").GetString(), problema.GetProperty("pendentes").GetInt32()));
        Assert.Equal("2 pessoas ainda não responderam. Confirme para continuar.", problema.GetProperty("detail").GetString());
        await pedida.AfirmarConformeAoContratoAsync("encerrarFormulario");
        Assert.Equal((409, 200), ((int)recusada.StatusCode, (int)feita.StatusCode));
        Assert.Equal(("ENCERRADO", "2026-10-07T12:42:00Z"), ((await CorpoAsync(feita)).GetProperty("situacao").GetString(), (await CorpoAsync(feita)).GetProperty("publicacao").GetProperty("encerradoEmUtc").GetString()));
        Assert.Equal(("False", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ENCERRAMENTO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
    }

    [Fact]
    public async Task Encerrar_sem_pendentes_encerra_direto_formulario_por_link_e_sugestao_tambem()
    {
        var api = await ApiAsync();
        var todos = await SemearAsync(api, Titulo("Todos responderam"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, todos, "Como foi?");
        var pessoa = await PessoaAsync(api, "Respondente");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({todos}, {pessoa})");
        await ResponderAsync(api, todos, pergunta, pessoa);
        var porLink = await SemearAsync(api, Titulo("Entrevista"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('e', 43));
        var sugestao = await SemearAsync(api, Titulo("Sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({sugestao}, {pessoa})");

        var respostas = new[] { await EncerrarAsync(api, todos), await EncerrarAsync(api, porLink), await EncerrarAsync(api, sugestao) };

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode));
        foreach (var r in respostas)
        {
            Assert.Equal("ENCERRADO", (await CorpoAsync(r)).GetProperty("situacao").GetString());
        }
    }

    [Fact]
    public async Task Encerrar_o_anonimo_pede_confirmacao_sempre_com_pendentes_nulo_mesmo_com_todos_ja_tendo_participado()
    {
        var api = await ApiAsync();
        var cinco = await PessoasAsync(api, 5);
        var id = await SemearAsync(api, Titulo("Anônimo aberto"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", cinco.Select(p => $"({id}, {p})"))}");

        var pedida = await EncerrarAsync(api, id);
        var problema = await CorpoAsync(pedida);
        var feita = await EncerrarAsync(api, id, confirmar: true);

        Assert.Equal((409, "CONFIRMACAO_NECESSARIA", "Num formulário anônimo o sistema não diz quantas pessoas ainda não responderam. Confirme para continuar."), ((int)pedida.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.True(problema.TryGetProperty("pendentes", out var pendentes));
        Assert.Equal(JsonValueKind.Null, pendentes.ValueKind);
        Assert.Equal(200, (int)feita.StatusCode);
    }

    [Fact]
    public async Task Na_avaliacao_os_pendentes_sao_as_avaliacoes_que_faltam()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("Gestor avalia"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var pergunta = await PerguntaAsync(api, id, "Nota");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({id}, {setor.GestorFuncionarioId})");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, {setor.GestorFuncionarioId}, {m1.Id}, '4', '2026-10-05 10:00:00', 0)");

        var problema = await CorpoAsync(await EncerrarAsync(api, id));

        Assert.Equal(1, problema.GetProperty("pendentes").GetInt32()); // duas avaliações a fazer, uma feita
    }

    [Fact]
    public async Task So_o_publicado_se_encerra_os_outros_sao_422_FORMULARIO_NAO_PUBLICADO()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho"));
        var agendado = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var vencido = await SemearAsync(api, Titulo("Prazo vencido"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");

        var respostas = new[] { await EncerrarAsync(api, rascunho), await EncerrarAsync(api, agendado), await EncerrarAsync(api, encerrado), await EncerrarAsync(api, vencido, confirmar: true) };

        Assert.All(respostas, r => Assert.Equal(422, (int)r.StatusCode));
        foreach (var r in respostas)
        {
            Assert.Equal("FORMULARIO_NAO_PUBLICADO", Codigo(await CorpoAsync(r)));
        }

        Assert.Equal(404, (int)(await EncerrarAsync(api, 2147483000)).StatusCode);
    }

    [Fact]
    public async Task Encerrar_a_mao_cancela_as_proximas_reaberturas_mas_a_recorrencia_continua_descrita()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Recorrente"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 1, RECORRENCIA_UNIDADE = 'MES', PROXIMA_REABERTURA = '2026-11-07 00:00:00' WHERE ID_FORMULARIO = {id}");
        using (var escopo = api.Api.Services.CreateScope())
        {
            await escopo.ServiceProvider.GetRequiredService<RHSystem.Application.Formularios.Construtor.IAgendamentoDoFormulario>().AgendarEncerramentoAsync(id, new DateTime(2026, 10, 31, 3, 0, 0, DateTimeKind.Utc), Ct);
        }

        var resposta = await EncerrarAsync(api, id, confirmar: true);
        var publicacao = (await CorpoAsync(resposta)).GetProperty("publicacao");

        Assert.Equal((JsonValueKind.Null, 1), (publicacao.GetProperty("proximaReaberturaEmUtc").ValueKind, publicacao.GetProperty("recorrencia").GetProperty("intervalo").GetInt32()));
        Assert.Equal((null, 0L, 1L), (await TextoAsync(api, $"SELECT PROXIMA_REABERTURA FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, "PENDENTE"), await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, "CANCELADA")));
    }

    [Fact]
    public async Task Duas_abas_encerrando_ao_mesmo_tempo_dao_um_200_e_um_422_e_um_so_evento()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas abas encerrando"), "PUBLICADO", alvo: "PESSOAS");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => cliente.PostAsync($"{Rota}/{id}/encerrar?confirmar=true", content: null)));

        Assert.Equal([200, 422], respostas.Select(r => (int)r.StatusCode).Order().ToArray());
        Assert.Single(await EventosAsync(api, id));
    }

    [Fact]
    public async Task Encerrar_grava_so_o_evento_ALTEROU_sem_contagem_de_respostas_nem_de_pendentes()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Auditoria do encerramento");
        var id = await SemearAsync(api, titulo, "PUBLICADO", anonimo: true, alvo: "PESSOAS");

        await EncerrarAsync(api, id, confirmar: true);

        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Encerrou o formulário \"{titulo}\"."), (evento.Area, evento.Acao, evento.Descricao));
    }

    // ---------------------------------------------------------------- a tarefa de encerrar

    [Fact]
    public async Task A_tarefa_encerra_pelo_prazo_gravando_o_encerramento_nominal_e_mantem_a_proxima_reabertura()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 2);
        var id = await ProntoAsync(api, Titulo("Prazo 30/09"));
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, prazo: "2026-10-20", recorrencia: new { intervalo = 1, unidade = "MES" }));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 21, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", id, "2026-10-21T03:00:00Z");
        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", id, "2026-10-21T03:00:00Z");

        Assert.Equal(("False", "2026-10-21 00:00:00", "2026-11-07 00:00:00"), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ENCERRAMENTO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(PROXIMA_REABERTURA, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
        var eventos = (await EventosAsync(api, id)).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, eventos.Count); // a publicação e UM encerramento do sistema
        Assert.Equal((TipoDeAtor.Sistema, AuditoriaAcao.Alterou), (eventos[1].AtorTipo, eventos[1].Acao));
        Assert.Contains("pelo prazo", eventos[1].Descricao, StringComparison.Ordinal);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_de_encerrar_nao_faz_nada_se_o_prazo_mudou_ou_sumiu_ou_o_formulario_ja_foi_encerrado()
    {
        var api = await ApiAsync();
        var mudou = await SemearAsync(api, Titulo("Prazo mudou"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-25");
        var sumiu = await SemearAsync(api, Titulo("Sem prazo"), "PUBLICADO", alvo: "PESSOAS");
        var jaEncerrado = await SemearAsync(api, Titulo("Já encerrado"), "ENCERRADO", alvo: "PESSOAS", prazo: "2026-10-20");
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 21, 3, 0, 5, DateTimeKind.Utc));

        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", mudou, "2026-10-21T03:00:00Z"); // a chave é do prazo 20/10; o formulário agora tem 25/10
        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", sumiu, "2026-10-21T03:00:00Z");
        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", jaEncerrado, "2026-10-21T03:00:00Z");
        await ExecutarTarefaAsync(api, "ENCERRAR_FORMULARIO", 2147483000, "2026-10-21T03:00:00Z");

        Assert.Equal(("True", "True", "2026-10-01 00:00:00"), (await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {mudou}"), await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {sumiu}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ENCERRAMENTO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {jaEncerrado}"))); // o encerramento que já existia não é regravado
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_recuperacao_agenda_o_encerramento_dos_publicados_com_prazo_que_ficaram_sem_tarefa()
    {
        var api = await ApiAsync();
        var semTarefa = await SemearAsync(api, Titulo("Publicado com prazo"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30");
        var semPrazo = await SemearAsync(api, Titulo("Publicado sem prazo"), "PUBLICADO", alvo: "PESSOAS");

        await RecuperarAsync(api, "ENCERRAR_FORMULARIO");
        await RecuperarAsync(api, "ENCERRAR_FORMULARIO");

        Assert.Equal((1L, 0L), (await TarefasAsync(api, "ENCERRAR_FORMULARIO", semTarefa, "PENDENTE", "2026-10-31T03:00:00Z"), await TarefasAsync(api, "ENCERRAR_FORMULARIO", semPrazo, "PENDENTE")));
    }

    private static async Task ExecutarTarefaComChaveAsync(ApiDeAcesso api, string tipo, int formularioId, string chave)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == tipo).ExecutarAsync(new TarefaParaExecutar(0, chave, formularioId, null, null, 0), Ct);
    }

    private static Task<string?> NovaTentativaAsync(ApiDeAcesso api, int formularioId) =>
        TextoAsync(api, $"SELECT MIN(CHAVE_IDEMPOTENCIA) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_FORMULARIO' AND REFERENCIA_ID = {formularioId} AND SITUACAO = 'PENDENTE' AND CHAVE_IDEMPOTENCIA LIKE '%:r=%'");

    [Fact]
    public async Task O_anonimo_agendado_barrado_pela_tarefa_e_tentado_de_novo_pela_recuperacao_e_publica_quando_o_publico_volta_a_ter_5()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 5);
        var id = await ProntoAsync(api, Titulo("Anônimo que voltou"), anonimo: true);
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03"));
        await AdvertenciasDeTeste.InativarAsync(api, pessoas[0]);
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));
        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z"); // barrado: 4 pessoas ativas
        var barrado = await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");
        var nova = await PessoaAsync(api, "Pessoa que entrou");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({id}, {nova})"); // o RH corrige o público: volta a ter 5

        await RecuperarAsync(api, "PUBLICAR_FORMULARIO");
        var chave = await NovaTentativaAsync(api, id);
        await ExecutarTarefaComChaveAsync(api, "PUBLICAR_FORMULARIO", id, chave!);

        Assert.Equal("False", barrado);
        Assert.NotNull(chave); // a recuperação horária cria uma nova tentativa para o agendado que já passou do dia
        Assert.Equal("True", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task O_agendado_que_ficou_sem_pergunta_nao_e_publicado_e_publica_quando_o_RH_poe_uma_pergunta()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, 2);
        var id = await ProntoAsync(api, Titulo("Agendado sem pergunta"));
        await PublicarAsync(api, id, Corpo("PESSOAS", pessoas: pessoas, agendarPara: "2026-11-03"));
        await Sql.ExecutarAsync(api.Conexao, $"DELETE FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {id}"); // o RH apagou todas as perguntas enquanto estava agendado
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));
        await ExecutarTarefaAsync(api, "PUBLICAR_FORMULARIO", id, "2026-11-03T03:00:00Z");
        var semPergunta = await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");

        await PerguntaAsync(api, id, "Como foi?", obrigatoria: true);
        await RecuperarAsync(api, "PUBLICAR_FORMULARIO");
        await ExecutarTarefaComChaveAsync(api, "PUBLICAR_FORMULARIO", id, (await NovaTentativaAsync(api, id))!);

        Assert.Equal("False", semPergunta); // sem pergunta nenhum envio seria aceito: não publica
        Assert.Equal("True", await TextoAsync(api, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }
}

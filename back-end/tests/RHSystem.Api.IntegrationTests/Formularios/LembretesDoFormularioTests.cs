using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>lembrarQuemFaltaFormulario</c> (o botão do RH) e a tarefa <c>LEMBRETE_FORMULARIO</c> (o automático 7 dias antes do prazo e o envio do manual do anônimo), no banco REAL com o relógio em 2026-10-07T12:42Z. Com nome: um e-mail por pessoa que falta, no máximo um por pessoa, formulário e dia (a repetida só entra em
/// <c>ignorados</c>), com <c>enviados</c> e <c>ignorados</c>. ANÔNIMO: a resposta é SEMPRE a mesma (<c>anonimo: true</c>, números nulos), a rota só marca o dia e agenda a tarefa, que enfileira e-mails SIGILOSOS (sem pessoa na linha nem na chave, apagados ao enviar, sem evento por e-mail); o evento da rota sai sempre, sem número nem destinatário.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LembretesDoFormularioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";
    private const string Tarefa = "LEMBRETE_FORMULARIO";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(LembretesDoFormularioTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, ativo: ativa);

    private static async Task<int[]> PessoasAsync(ApiDeAcesso api, string prefixo, int quantas)
    {
        var ids = new int[quantas];
        for (var i = 0; i < quantas; i++)
        {
            ids[i] = await PessoaAsync(api, $"{prefixo} {i}");
        }

        return ids;
    }

    /// <summary>Um formulário publicado para as pessoas (com uma pergunta).</summary>
    private static async Task<(int Id, int Pergunta)> ComNomeAsync(ApiDeAcesso api, string prefixo, int[] pessoas, string? prazo = null, string tipo = "PESQUISA", string situacao = "PUBLICADO", bool anonimo = false)
    {
        var id = await SemearAsync(api, Titulo(prefixo), situacao, anonimo: anonimo, tipo: tipo, alvo: "PESSOAS", prazo: prazo);
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, pessoas);
        return (id, pergunta);
    }

    private static async Task<HttpResponseMessage> LembrarAsync(ApiDeAcesso api, int formulario, params int[] pessoas) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{formulario}/lembretes", new { funcionarioIds = pessoas });

    private static async Task<(int?, int?)> NumerosAsync(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        static int? Inteiro(JsonElement e, string campo) => e.GetProperty(campo).ValueKind == JsonValueKind.Null ? null : e.GetProperty(campo).GetInt32();
        return (Inteiro(corpo, "enviados"), Inteiro(corpo, "ignorados"));
    }

    private static Task<long> EmailsAsync(ApiDeAcesso api, int formulario, string extra = "") => ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {formulario} {extra}");

    private static async Task<string?> EmailDaPessoaAsync(ApiDeAcesso api, int pessoa) => await TextoAsync(api, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}");

    private static async Task ExecutarTarefaAsync(ApiDeAcesso api, string chave, int formulario, DateOnly? dia = null)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).ExecutarAsync(new TarefaParaExecutar(0, chave, formulario, dia, null, 0), CancellationToken.None);
    }

    // ---------------------------------------------------------------- com nome

    [Fact]
    public async Task Lembrar_todos_que_faltam_enfileira_um_email_por_pessoa_com_a_chave_do_dia_e_o_evento_diz_quantos()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem1", 3);
        var (id, pergunta) = await ComNomeAsync(api, "ZLem1", pessoas, prazo: "2026-10-30");
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoas[0]);

        var resposta = await LembrarAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("lembrarQuemFaltaFormulario");
        Assert.Equal((false, 2, 1), ((await CorpoAsync(resposta)).GetProperty("anonimo").GetBoolean(), (await NumerosAsync(resposta)).Item1, (await NumerosAsync(resposta)).Item2)); // 2 enviados; a que já respondeu é "ignorada"
        Assert.Equal(2L, await EmailsAsync(api, id));
        Assert.Equal(
            [$"FORMULARIO_LEMBRETE:form={id}:func={pessoas[1]}:dia=2026-10-07", $"FORMULARIO_LEMBRETE:form={id}:func={pessoas[2]}:dia=2026-10-07"],
            await Sql.ColunaAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id} ORDER BY ID_FUNCIONARIO"));
        Assert.Equal(("0", "PENDENTE"), (await TextoAsync(api, $"SELECT SUM(SIGILOSO) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id}"), await TextoAsync(api, $"SELECT MIN(SITUACAO) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id}")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.EnviouEmail), (evento.Area, evento.Acao));
        Assert.Contains("para 2 pessoa(s)", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Lembrar_pessoas_escolhidas_conta_como_ignorado_quem_ja_respondeu_quem_nao_deve_responder_e_o_repetido()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem2", 3);
        var estranha = await PessoaAsync(api, "ZLem2 estranha");
        var (id, pergunta) = await ComNomeAsync(api, "ZLem2", pessoas);
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoas[0]);

        var mistura = await LembrarAsync(api, id, pessoas[1], pessoas[0], estranha, pessoas[1]);
        var outra = await LembrarAsync(api, id, pessoas[2]);

        Assert.Equal((1, 2), await NumerosAsync(mistura)); // enviou à [1]; a [0] já respondeu e a estranha não deve responder (o repetido conta uma vez só)
        Assert.Equal((1, 0), await NumerosAsync(outra));
        Assert.Equal(2L, await EmailsAsync(api, id));
    }

    [Fact]
    public async Task No_maximo_um_lembrete_por_pessoa_por_formulario_e_por_dia_e_no_dia_seguinte_sai_de_novo()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem3", 2);
        var (id, _) = await ComNomeAsync(api, "ZLem3", pessoas);

        var primeira = await LembrarAsync(api, id);
        var segunda = await LembrarAsync(api, id);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(1));
        var amanha = await LembrarAsync(api, id);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);

        Assert.Equal([(2, 0), (0, 2), (2, 0)], new[] { await NumerosAsync(primeira), await NumerosAsync(segunda), await NumerosAsync(amanha) });
        Assert.Equal(4L, await EmailsAsync(api, id));
        Assert.Equal(2, (await EventosAsync(api, id)).Count); // a segunda chamada não enviou nada e não grava evento
    }

    [Fact]
    public async Task Pessoa_sem_email_corporativo_e_pessoa_inativa_nao_recebem_e_contam_como_ignoradas()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem4", 3);
        var (id, _) = await ComNomeAsync(api, "ZLem4", pessoas);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {pessoas[0]}");
        await AdvertenciasDeTeste.InativarAsync(api, pessoas[1]);

        var todos = await LembrarAsync(api, id);

        Assert.Equal((1, 1), await NumerosAsync(todos)); // só a [2]; a [0] não tem e-mail e a [1] saiu do público
        Assert.Equal(1L, await EmailsAsync(api, id));
    }

    [Fact]
    public async Task Na_avaliacao_quem_tem_varias_avaliacoes_pendentes_recebe_um_email_so()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("ZLem5 avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);

        var resposta = await LembrarAsync(api, id);

        Assert.Equal((1, 0), await NumerosAsync(resposta));
        Assert.Equal(1L, await EmailsAsync(api, id));
    }

    [Fact]
    public async Task O_email_sai_com_o_titulo_o_prazo_e_a_auditoria_do_envio_nao_leva_a_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZLem6");
        var (id, _) = await ComNomeAsync(api, "ZLem6 <b>", [pessoa], prazo: "2026-10-30");
        await LembrarAsync(api, id);
        api.Api.Enviador.Limpar();

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
        }

        var mensagem = Assert.Single(api.Api.Enviador.Enviadas, m => m.Assunto.StartsWith("Lembrete: ZLem6", StringComparison.Ordinal));
        Assert.Contains("O prazo para responder é 30/10/2026.", mensagem.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", mensagem.Html, StringComparison.Ordinal); // o título passa por HtmlEncode
        Assert.Equal(await EmailDaPessoaAsync(api, pessoa), mensagem.Para);
        var registro = await TextoAsync(api, "SELECT REGISTRO_AFETADO FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'E-mail de lembrete do formulário enviado' ORDER BY ID_AUDITORIA_EVENTO DESC LIMIT 1");
        Assert.StartsWith("Formulário \"ZLem6", registro, StringComparison.Ordinal); // o evento do envio diz o formulário e nunca a pessoa
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task So_o_publicado_recebe_lembrete_e_nao_vale_para_link_Sugestao_nem_prazo_vencido()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZLem7");
        var rascunho = await SemearAsync(api, Titulo("ZLem7 rascunho"), alvo: "PESSOAS");
        var (agendado, _) = await ComNomeAsync(api, "ZLem7 agendado", [pessoa], situacao: "AGENDADO");
        var (encerrado, _) = await ComNomeAsync(api, "ZLem7 encerrado", [pessoa], situacao: "ENCERRADO");
        var (vencido, _) = await ComNomeAsync(api, "ZLem7 vencido", [pessoa], prazo: "2026-10-06");
        var porLink = await SemearAsync(api, Titulo("ZLem7 link"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('l', 43));
        var (sugestao, _) = await ComNomeAsync(api, "ZLem7 sugestão", [pessoa], tipo: "OUTRO");

        var respostas = new Dictionary<string, HttpResponseMessage>
        {
            ["rascunho"] = await LembrarAsync(api, rascunho),
            ["agendado"] = await LembrarAsync(api, agendado),
            ["encerrado"] = await LembrarAsync(api, encerrado),
            ["vencido"] = await LembrarAsync(api, vencido),
            ["link"] = await LembrarAsync(api, porLink),
            ["sugestão"] = await LembrarAsync(api, sugestao),
        };

        Assert.All(respostas, r => Assert.Equal(422, (int)r.Value.StatusCode));
        Assert.Equal(["FORMULARIO_NAO_PUBLICADO", "FORMULARIO_NAO_PUBLICADO", "FORMULARIO_NAO_PUBLICADO", "FORMULARIO_NAO_PUBLICADO", "FORMULARIO_POR_LINK", "FORMULARIO_SUGESTAO"], await Task.WhenAll(respostas.Values.Select(async r => Codigo(await CorpoAsync(r)))));
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID IN (" + string.Join(",", new[] { rascunho, agendado, encerrado, vencido, porLink, sugestao }) + ")"));
    }

    [Fact]
    public async Task O_corpo_o_formulario_e_o_perfil_sao_conferidos_antes_de_qualquer_envio()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZLem8");
        var (id, _) = await ComNomeAsync(api, "ZLem8", [pessoa]);
        var rh = await api.ClienteDoRhAgoraAsync();

        var inexistente = await LembrarAsync(api, 99999999);
        var semLista = await rh.PostAsJsonAsync($"{Rota}/{id}/lembretes", new { });
        var demais = await LembrarAsync(api, id, [.. Enumerable.Repeat(pessoa, 201)]);
        var zero = await LembrarAsync(api, id, 0);
        var duzentas = await LembrarAsync(api, id, [.. Enumerable.Repeat(pessoa, 200)]);
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).PostAsJsonAsync($"{Rota}/{id}/lembretes", new { funcionarioIds = Array.Empty<int>() });
        var semLogin = await api.Api.ClienteAnonimo().PostAsJsonAsync($"{Rota}/{id}/lembretes", new { funcionarioIds = Array.Empty<int>() });

        Assert.Equal([404, 400, 400, 400, 200, 403, 401], new[] { inexistente, semLista, demais, zero, duzentas, gestor, semLogin }.Select(r => (int)r.StatusCode));
        Assert.True((await CorpoAsync(demais)).GetProperty("errors").TryGetProperty("funcionarioIds", out _));
    }

    // ---------------------------------------------------------------- anônimo

    private static async Task<(int Id, int[] Pessoas, int Pergunta)> AnonimoAsync(ApiDeAcesso api, string prefixo, int pessoas = 5)
    {
        var ids = await PessoasAsync(api, prefixo, pessoas);
        var (id, pergunta) = await ComNomeAsync(api, prefixo, ids, prazo: "2026-10-30", anonimo: true);
        return (id, ids, pergunta);
    }

    private static Task ParticiparAsync(ApiDeAcesso api, int formulario, int pessoa) => Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa})");

    [Fact]
    public async Task No_anonimo_a_resposta_e_sempre_a_mesma_a_rota_so_marca_e_agenda_e_o_evento_sai_sempre_sem_numero()
    {
        var api = await ApiAsync();
        var muitos = await AnonimoAsync(api, "ZLem9a", 8);
        var poucos = await AnonimoAsync(api, "ZLem9b", 5);
        foreach (var pessoa in poucos.Pessoas.Take(4))
        {
            await ParticiparAsync(api, poucos.Id, pessoa);
        }

        var todosParticiparam = await AnonimoAsync(api, "ZLem9c", 5);
        foreach (var pessoa in todosParticiparam.Pessoas)
        {
            await ParticiparAsync(api, todosParticiparam.Id, pessoa);
        }

        var respostas = new List<string>();
        foreach (var (formulario, _, _) in new[] { muitos, poucos, todosParticiparam })
        {
            respostas.Add(await (await LembrarAsync(api, formulario)).Content.ReadAsStringAsync());
        }

        respostas.Add(await (await LembrarAsync(api, muitos.Id)).Content.ReadAsStringAsync()); // o de hoje já tinha saído

        Assert.All(respostas, r => Assert.Equal("""{"anonimo":true,"enviados":null,"ignorados":null}""", r));
        Assert.Equal(0L, await EmailsAsync(api, muitos.Id)); // a rota não enfileira nada: quem enfileira é a tarefa
        Assert.Equal(("1", "1", "PENDENTE"), (
            await TextoAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={muitos.Id}:dia=2026-10-07:alcance=TODOS' AND SITUACAO = 'CONCLUIDA'"),
            await TextoAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {muitos.Id}"),
            await TextoAsync(api, $"SELECT SITUACAO FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={muitos.Id}:dia=2026-10-07:alcance=TODOS:envio=1'")));
        var eventos = await EventosAsync(api, muitos.Id);
        Assert.Equal(2, eventos.Count); // um por clique, mesmo com o de hoje já enviado
        Assert.All(eventos, e => Assert.Equal(($"Mandou o lembrete do formulário \"{e.Descricao.Split('"')[1]}\".", AuditoriaAcao.EnviouEmail), (e.Descricao, e.Acao)));
    }

    [Fact]
    public async Task A_tarefa_do_anonimo_enfileira_so_para_quem_nao_participou_em_linhas_sigilosas_sem_pessoa_e_apaga_ao_enviar()
    {
        var api = await ApiAsync();
        var (id, pessoas, _) = await AnonimoAsync(api, "ZLem10", 6);
        foreach (var pessoa in pessoas.Take(2))
        {
            await ParticiparAsync(api, id, pessoa);
        }

        await LembrarAsync(api, id);
        await ExecutarTarefaAsync(api, $"LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=TODOS:envio=1", id, new DateOnly(2026, 10, 7));

        Assert.Equal((4L, 4L, 0L, 0L), (
            await EmailsAsync(api, id, "AND SIGILOSO = 1"),
            await EmailsAsync(api, id, "AND ID_FUNCIONARIO IS NULL"),
            await EmailsAsync(api, id, "AND ID_FUNCIONARIO IS NOT NULL"),
            await EmailsAsync(api, id, "AND SIGILOSO = 0")));
        var chaves = await Sql.ColunaAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id}");
        Assert.All(chaves, chave => Assert.Matches($"^FORMULARIO_LEMBRETE:form={id}:dia=2026-10-07:r=[0-9a-f]{{32}}$", chave)); // sem a pessoa na chave
        var destinatarios = await Sql.ColunaAsync(api.Conexao, $"SELECT DESTINATARIO_EMAIL FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID = {id}");
        var esperados = new List<string>();
        foreach (var pessoa in pessoas.Skip(2))
        {
            esperados.Add((await EmailDaPessoaAsync(api, pessoa))!);
        }

        Assert.Equal(esperados.Order(), destinatarios.Order());

        api.Api.Enviador.Limpar();
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetRequiredService<IProcessadorDeEmails>().ProcessarCicloAsync(CancellationToken.None);
        }

        Assert.Equal(4, api.Api.Enviador.Enviadas.Count(m => m.Assunto.StartsWith("Lembrete: ZLem10", StringComparison.Ordinal)));
        Assert.Equal(0L, await EmailsAsync(api, id)); // as linhas sigilosas foram APAGADAS ao enviar (nunca ficam ENVIADO)
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'E-mail de lembrete do formulário enviado' AND REGISTRO_AFETADO LIKE '%ZLem10%'")); // e nenhum ENVIOU_EMAIL por mensagem sigilosa
    }

    [Fact]
    public async Task No_anonimo_lembrar_uma_pessoa_especifica_e_422_e_nada_e_marcado()
    {
        var api = await ApiAsync();
        var (id, pessoas, _) = await AnonimoAsync(api, "ZLem11");

        var resposta = await LembrarAsync(api, id, pessoas[0]);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FORMULARIO_ANONIMO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {id}"));
        Assert.Empty(await EventosAsync(api, id));
    }

    // ---------------------------------------------------------------- o automático

    private static async Task<int> PublicarAsync(ApiDeAcesso api, string prefixo, string prazo, bool lembrete = true, string tipo = "PESQUISA", string? alvo = "PESSOAS", string? agendarPara = null)
    {
        var pessoas = await PessoasAsync(api, prefixo, 2);
        var id = await SemearAsync(api, Titulo(prefixo), tipo: tipo, descricao: "Conte como foi.");
        await PerguntaAsync(api, id, "Como foi?", obrigatoria: true);
        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/publicar", new { alvo, setorIds = Array.Empty<int>(), funcionarioIds = pessoas, agendarPara, prazo = tipo == "OUTRO" ? null : prazo, lembreteAutomatico = lembrete });
        Assert.Equal(200, (int)resposta.StatusCode);
        return id;
    }

    private static async Task<(string?, string?, string?)> AutomaticoAsync(ApiDeAcesso api, int id) => (
        await TextoAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {id}"),
        await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {id}"),
        await TextoAsync(api, $"SELECT DATE_FORMAT(EXECUTAR_APOS, '%Y-%m-%d %H:%i:%s') FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {id}"));

    [Fact]
    public async Task Publicar_com_prazo_a_mais_de_7_dias_agenda_o_lembrete_automatico_para_7_dias_antes_e_a_7_dias_ou_menos_nao_agenda()
    {
        var api = await ApiAsync();
        var a8 = await PublicarAsync(api, "ZLem12 a 8 dias", "2026-10-15");
        var a7 = await PublicarAsync(api, "ZLem12 a 7 dias", "2026-10-14");
        var a6 = await PublicarAsync(api, "ZLem12 a 6 dias", "2026-10-13");
        var longe = await PublicarAsync(api, "ZLem12 longe", "2026-10-31");

        Assert.Equal(("1", $"LEMBRETE_FORMULARIO:form={a8}:dia=2026-10-08:alcance=TODOS", "2026-10-08 03:00:00"), await AutomaticoAsync(api, a8));
        Assert.Equal("0", (await AutomaticoAsync(api, a7)).Item1);
        Assert.Equal("0", (await AutomaticoAsync(api, a6)).Item1);
        Assert.Equal(("1", $"LEMBRETE_FORMULARIO:form={longe}:dia=2026-10-24:alcance=TODOS", "2026-10-24 03:00:00"), await AutomaticoAsync(api, longe));
    }

    [Fact]
    public async Task Nao_agenda_o_automatico_com_a_caixa_desligada_na_Sugestao_nem_por_link()
    {
        var api = await ApiAsync();
        var desligado = await PublicarAsync(api, "ZLem13 desligado", "2026-10-31", lembrete: false);
        var sugestao = await PublicarAsync(api, "ZLem13 sugestão", "2026-10-31", tipo: "OUTRO");

        Assert.Equal(("0", "0"), ((await AutomaticoAsync(api, desligado)).Item1, (await AutomaticoAsync(api, sugestao)).Item1));
    }

    [Fact]
    public async Task O_agendado_conta_os_7_dias_a_partir_do_dia_da_publicacao()
    {
        var api = await ApiAsync();
        var perto = await PublicarAsync(api, "ZLem14 perto", "2026-11-09", agendarPara: "2026-11-03"); // publica 03/11 e vence 09/11: 6 dias
        var longe = await PublicarAsync(api, "ZLem14 longe", "2026-11-12", agendarPara: "2026-11-03"); // 9 dias: o lembrete é 05/11

        Assert.Equal("0", (await AutomaticoAsync(api, perto)).Item1);
        Assert.Equal(("1", $"LEMBRETE_FORMULARIO:form={longe}:dia=2026-11-05:alcance=TODOS", "2026-11-05 03:00:00"), await AutomaticoAsync(api, longe));
    }

    [Fact]
    public async Task A_tarefa_do_automatico_manda_so_para_quem_falta_e_o_manual_do_dia_antes_impede_o_automatico_do_dia()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem15", 3);
        var (id, pergunta) = await ComNomeAsync(api, "ZLem15", pessoas, prazo: "2026-10-30");
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoas[0]);
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 23, 3, 0, 10, DateTimeKind.Utc));
        var chave = $"LEMBRETE_FORMULARIO:form={id}:dia=2026-10-23:alcance=TODOS";
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, REFERENCIA_ID, DIA_REFERENCIA, EXECUTAR_APOS, SITUACAO) VALUES ('{Tarefa}', '{chave}', {id}, '2026-10-23', '2026-10-23 03:00:00', 'PENDENTE')");

        await ExecutarTarefaAsync(api, chave, id, new DateOnly(2026, 10, 23));
        await ExecutarTarefaAsync(api, chave, id, new DateOnly(2026, 10, 23)); // roda de novo: a chave do e-mail de cada pessoa já existe

        Assert.Equal(2L, await EmailsAsync(api, id));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((TipoDeAtor.Sistema, AuditoriaAcao.EnviouEmail), (evento.AtorTipo, evento.Acao));
        Assert.Contains("lembrete automático", evento.Descricao, StringComparison.Ordinal);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_nao_faz_nada_se_o_formulario_sumiu_encerrou_a_caixa_foi_desligada_ou_o_dia_nao_e_o_do_prazo()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem16", 2);
        var (encerrado, _) = await ComNomeAsync(api, "ZLem16 encerrado", pessoas, prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-10 00:00:00' WHERE ID_FORMULARIO = {encerrado}");
        var (desligado, _) = await ComNomeAsync(api, "ZLem16 desligado", pessoas, prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET LEMBRETE_AUTOMATICO = FALSE WHERE ID_FORMULARIO = {desligado}");
        var (outroDia, _) = await ComNomeAsync(api, "ZLem16 outro dia", pessoas, prazo: "2026-10-30");

        await ExecutarTarefaAsync(api, $"LEMBRETE_FORMULARIO:form={encerrado}:dia=2026-10-23:alcance=TODOS", encerrado, new DateOnly(2026, 10, 23));
        await ExecutarTarefaAsync(api, $"LEMBRETE_FORMULARIO:form={desligado}:dia=2026-10-23:alcance=TODOS", desligado, new DateOnly(2026, 10, 23));
        await ExecutarTarefaAsync(api, $"LEMBRETE_FORMULARIO:form={outroDia}:dia=2026-10-20:alcance=TODOS", outroDia, new DateOnly(2026, 10, 20)); // o prazo é 30: o dia certo é 23
        await ExecutarTarefaAsync(api, "LEMBRETE_FORMULARIO:form=99999999:dia=2026-10-23:alcance=TODOS", 99999999, new DateOnly(2026, 10, 23));

        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'FORMULARIO_LEMBRETE' AND REFERENCIA_ID IN ({encerrado}, {desligado}, {outroDia})"));
    }

    [Fact]
    public async Task A_recuperacao_agenda_o_automatico_que_ficou_para_tras_so_de_hoje_em_diante()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem17", 2);
        var (futuro, _) = await ComNomeAsync(api, "ZLem17 futuro", pessoas, prazo: "2026-10-31");
        var (passado, _) = await ComNomeAsync(api, "ZLem17 passado", pessoas, prazo: "2026-10-12"); // o dia do lembrete (05/10) já passou
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).RecuperarAsync(CancellationToken.None);
        }

        Assert.Equal(($"LEMBRETE_FORMULARIO:form={futuro}:dia=2026-10-24:alcance=TODOS", "0"), (await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {futuro}"), await TextoAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND REFERENCIA_ID = {passado}")));
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_o_formulario_cancela_os_lembretes_pendentes_com_nome_apaga_os_sigilosos_e_nao_toca_nos_de_outro()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZLem18", 2);
        var (a, _) = await ComNomeAsync(api, "ZLem18 a", pessoas);
        var (b, _) = await ComNomeAsync(api, "ZLem18 b", pessoas);
        var anonimo = await AnonimoAsync(api, "ZLem18 anon", 5);
        await LembrarAsync(api, a);
        await LembrarAsync(api, b);
        await LembrarAsync(api, anonimo.Id);
        await ExecutarTarefaAsync(api, $"LEMBRETE_FORMULARIO:form={anonimo.Id}:dia=2026-10-07:alcance=TODOS:envio=1", anonimo.Id, new DateOnly(2026, 10, 7));

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{a}?confirmar=true");
        var respostaDoAnonimo = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{anonimo.Id}?confirmar=true");

        Assert.Equal((204, 204), ((int)resposta.StatusCode, (int)respostaDoAnonimo.StatusCode));
        Assert.Equal((0L, 2L, 2L, 0L), (await EmailsAsync(api, a, "AND SITUACAO = 'PENDENTE'"), await EmailsAsync(api, a, "AND SITUACAO = 'CANCELADO' AND CARGA IS NULL"), await EmailsAsync(api, b, "AND SITUACAO = 'PENDENTE'"), await EmailsAsync(api, anonimo.Id))); // os do a ficam CANCELADOS (sem carga), os do b continuam na fila e os sigilosos do anônimo foram APAGADOS
    }
}

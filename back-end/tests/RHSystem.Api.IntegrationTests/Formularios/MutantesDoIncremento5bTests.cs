using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os mutantes do incremento 5b (T11 a T18) que sobreviveram e mudam o comportamento: a fronteira dos números de 1 em diante (o 1 vale, o 0 é 400); duas pessoas e dois formulários nunca se misturam ao responder, na lista e no lembrete; a Sugestão de pessoas e formulários diferentes no mesmo
/// segundo não se empurra; o rascunho é sempre 404 nas minhas respostas; o aberto de uma avaliação multiplica os minutos pelos avaliados; o lembrete do gestor leva o setor na tarefa; o do dia de hoje é agendado na recuperação; quem entra hoje já é da equipe e a participação da equipe é só do formulário.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MutantesDoIncremento5bTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MutantesDoIncremento5bTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, int formulario, int pergunta) => ResponderAsync(cliente, formulario, Envio(Json(new { perguntaId = pergunta, texto = "ok" })));

    // ---------------------------------------------------------------- fronteira dos números

    [Fact]
    public async Task O_numero_1_vale_e_o_0_e_400_no_avaliado_no_setor_e_nos_ids_do_lembrete()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var pessoa = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId); // o gestor avalia a equipe: só assim ele tem pares
        var avaliacao = await SemearAsync(api, Titulo("ZMut1 avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, avaliacao, "Nota", "ESCALA", 1, true);
        await EscolherPessoasAsync(api, avaliacao, setor.GestorFuncionarioId);
        var pesquisa = await SemearAsync(api, Titulo("ZMut1 pesquisa"), "PUBLICADO", alvo: "PESSOAS");
        await PerguntaAsync(api, pesquisa, "Como foi?");
        var rh = await api.ClienteDoRhAgoraAsync();
        var corpo = Json(new { perguntaId = nota, valorEscala = 3 });

        var responder0 = await ResponderAsync(pessoa, avaliacao, Envio(corpo, 0));
        var responder1 = await ResponderAsync(pessoa, avaliacao, Envio(corpo, 1));
        var minhas0 = await pessoa.GetAsync($"/api/eu/formularios/{avaliacao}/minhas-respostas?avaliadoId=0");
        var minhas1 = await pessoa.GetAsync($"/api/eu/formularios/{avaliacao}/minhas-respostas?avaliadoId=1");
        var resultado0 = await rh.GetAsync($"/api/rh/formularios/{avaliacao}/resultado?avaliadoId=0");
        var resultado1 = await rh.GetAsync($"/api/rh/formularios/{avaliacao}/resultado?avaliadoId=1");
        var setor0 = await rh.GetAsync($"/api/rh/formularios/{pesquisa}/respondentes?setorId=0");
        var setor1 = await rh.GetAsync($"/api/rh/formularios/{pesquisa}/respondentes?setorId=1");
        var lembrar0 = await rh.PostAsJsonAsync($"/api/rh/formularios/{pesquisa}/lembretes", new { funcionarioIds = new[] { 0 } });
        var lembrar1 = await rh.PostAsJsonAsync($"/api/rh/formularios/{pesquisa}/lembretes", new { funcionarioIds = new[] { 1 } });

        Assert.Equal([400, 400, 400, 400, 400], new[] { responder0, minhas0, resultado0, setor0, lembrar0 }.Select(r => (int)r.StatusCode));
        Assert.DoesNotContain(400, new[] { responder1, minhas1, resultado1, setor1, lembrar1 }.Select(r => (int)r.StatusCode)); // o 1 passa da conferência (depois vem 404, 422 ou 200)
        Assert.True((await ErrosAsync(responder0)).TryGetProperty("avaliadoId", out _));
        Assert.Equal("AVALIADO_INVALIDO", Codigo(await CorpoAsync(responder1))); // o avaliado 1 é um número válido, só não é de quem avalia
    }

    // ---------------------------------------------------------------- minhas respostas e abrir

    [Fact]
    public async Task Minhas_respostas_de_um_rascunho_e_sempre_404_inclusive_o_anonimo_e_a_Sugestao()
    {
        var api = await ApiAsync();
        var (pessoa, _) = await PessoaAsync(api);
        var anonimo = await SemearAsync(api, Titulo("ZMut2 anônimo"), "RASCUNHO", anonimo: true, alvo: "PESSOAS");
        var sugestao = await SemearAsync(api, Titulo("ZMut2 sugestão"), "RASCUNHO", tipo: "OUTRO", alvo: "PESSOAS");

        var doAnonimo = await pessoa.GetAsync($"/api/eu/formularios/{anonimo}/minhas-respostas");
        var daSugestao = await pessoa.GetAsync($"/api/eu/formularios/{sugestao}/minhas-respostas");

        Assert.Equal([404, 404], new[] { doAnonimo, daSugestao }.Select(r => (int)r.StatusCode)); // rascunho não existe para ninguém: nem 422 FORMULARIO_ANONIMO, nem 422 FORMULARIO_SUGESTAO
    }

    [Fact]
    public async Task Abrir_uma_avaliacao_multiplica_os_minutos_pelos_avaliados()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        for (var i = 0; i < 3; i++)
        {
            await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        }

        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("ZMut3"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        await PerguntaAsync(api, id, "Conte", "RESPOSTA_LONGA", 1);
        await PerguntaAsync(api, id, "Mais", "RESPOSTA_LONGA", 2);
        await EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);

        var aberto = await CorpoAsync(await gestor.GetAsync($"/api/eu/formularios/{id}"));

        Assert.Equal((3, 9), (aberto.GetProperty("avaliados").GetArrayLength(), aberto.GetProperty("minutosEstimados").GetInt32())); // 2 respostas longas (3 minutos) vezes 3 avaliados
    }

    // ---------------------------------------------------------------- duas pessoas, dois formulários

    [Fact]
    public async Task Duas_pessoas_e_dois_formularios_nao_se_misturam_ao_responder_nem_na_lista()
    {
        var api = await ApiAsync();
        var (p1, id1) = await PessoaAsync(api);
        var (p2, id2) = await PessoaAsync(api);
        var nomeado = await SemearAsync(api, Titulo("ZMut4 nomeado"), "PUBLICADO", alvo: "PESSOAS");
        var perguntaNomeado = await PerguntaAsync(api, nomeado, "Como foi?");
        var anonimo1 = await SemearAsync(api, Titulo("ZMut4 anônimo 1"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var perguntaAnonimo1 = await PerguntaAsync(api, anonimo1, "Como foi?");
        var anonimo2 = await SemearAsync(api, Titulo("ZMut4 anônimo 2"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var perguntaAnonimo2 = await PerguntaAsync(api, anonimo2, "Como foi?");
        foreach (var f in new[] { nomeado, anonimo1, anonimo2 })
        {
            await EscolherPessoasAsync(api, f, id1, id2);
        }

        var p1Nomeado = await EnviarAsync(p1, nomeado, perguntaNomeado);
        var p1Anonimo1 = await EnviarAsync(p1, anonimo1, perguntaAnonimo1);
        var listaDaP2 = await CorpoAsync(await p2.GetAsync("/api/eu/formularios?busca=ZMut4"));
        var listaDaP1 = await CorpoAsync(await p1.GetAsync("/api/eu/formularios?busca=ZMut4"));
        var p2Nomeado = await EnviarAsync(p2, nomeado, perguntaNomeado); // a resposta da outra pessoa no mesmo formulário não conta para esta
        var p2Anonimo1 = await EnviarAsync(p2, anonimo1, perguntaAnonimo1);
        var p1Anonimo2 = await EnviarAsync(p1, anonimo2, perguntaAnonimo2); // a participação em outro formulário anônimo não conta
        var p2Anonimo2 = await EnviarAsync(p2, anonimo2, perguntaAnonimo2);

        Assert.Equal([201, 201, 201, 201, 201, 201], new[] { p1Nomeado, p1Anonimo1, p2Nomeado, p2Anonimo1, p1Anonimo2, p2Anonimo2 }.Select(r => (int)r.StatusCode));
        Assert.Equal((3, 0), (listaDaP2.GetProperty("itens").GetArrayLength(), listaDaP2.GetProperty("itens").EnumerateArray().Count(i => i.GetProperty("situacao").GetString() == "RESPONDIDO"))); // para a P2 nada foi respondido ainda
        var daP1 = listaDaP1.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([anonimo2], daP1); // a P1 já respondeu os outros dois: só falta o anônimo 2
    }

    [Fact]
    public async Task A_Sugestao_de_pessoas_e_formularios_diferentes_no_mesmo_segundo_nao_se_empurra()
    {
        var api = await ApiAsync();
        var (p1, id1) = await PessoaAsync(api);
        var (p2, id2) = await PessoaAsync(api);
        var s1 = await SemearAsync(api, Titulo("ZMut5 a"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var perguntaS1 = await PerguntaAsync(api, s1, "Ideia", "RESPOSTA_LONGA", 1, true);
        var s2 = await SemearAsync(api, Titulo("ZMut5 b"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        var perguntaS2 = await PerguntaAsync(api, s2, "Ideia", "RESPOSTA_LONGA", 1, true);
        foreach (var f in new[] { s1, s2 })
        {
            await EscolherPessoasAsync(api, f, id1, id2);
        }

        var corpo = Json(new { perguntaId = perguntaS1, texto = "ideia" });
        await ResponderAsync(p1, s1, Envio(corpo));
        await ResponderAsync(p2, s1, Envio(corpo)); // outra pessoa, mesmo formulário, mesmo segundo
        await ResponderAsync(p1, s2, Envio(Json(new { perguntaId = perguntaS2, texto = "ideia" }))); // mesma pessoa, outro formulário, mesmo segundo

        Assert.Equal("2026-10-07 09:42:00", await TextoAsync(api, $"SELECT CONCAT(MIN(DATA_RESPOSTA), '') FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO IN ({s1}, {s2})"));
        Assert.Equal("2026-10-07 09:42:00", await TextoAsync(api, $"SELECT CONCAT(MAX(DATA_RESPOSTA), '') FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO IN ({s1}, {s2})")); // nenhum foi empurrado um segundo para a frente
    }

    // ---------------------------------------------------------------- avaliados e resultado de outro formulário

    [Fact]
    public async Task O_mesmo_avaliado_em_duas_avaliacoes_anonimas_nao_soma_as_participacoes()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membros = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            membros.Add((await AdvertenciasDeTeste.NovoMembroAsync(api, setor)).Id);
        }

        var ids = new List<int>();
        foreach (var rodada in new[] { "a", "b" })
        {
            var id = await SemearAsync(api, Titulo("ZMut6 " + rodada), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
            var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor.SetorId})");
            foreach (var membro in membros.Take(2)) // 2 participações em cada uma: 4 no total, mas só 2 em cada
            {
                await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ({id}, {membro}, {setor.GestorFuncionarioId})");
                await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ('{Guid.NewGuid()}', {id}, {membro}, {setor.GestorFuncionarioId})");
                await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ID_AVALIADO, ANONIMO) VALUES ({nota}, {id}, '4', '2026-10-01 00:00:00', {setor.GestorFuncionarioId}, 1)");
            }

            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {id}");
            ids.Add(id);
        }

        var rh = await api.ClienteDoRhAgoraAsync();
        var lista = await CorpoAsync(await rh.GetAsync($"/api/rh/formularios/{ids[0]}/avaliados"));
        var resultado = await rh.GetAsync($"/api/rh/formularios/{ids[0]}/resultado?avaliadoId={setor.GestorFuncionarioId}");

        var avaliado = Assert.Single(lista.GetProperty("itens").EnumerateArray());
        Assert.Equal((JsonValueKind.Null, false), (avaliado.GetProperty("totalRespostas").ValueKind, avaliado.GetProperty("resultadoLiberado").GetBoolean())); // 2 participações aqui: "menos de 3 respostas"
        Assert.Equal(422, (int)resultado.StatusCode);
    }

    // ---------------------------------------------------------------- lembretes

    [Fact]
    public async Task O_lembrete_geral_nao_conta_a_resposta_da_pessoa_em_outro_formulario()
    {
        var api = await ApiAsync();
        var pessoas = new int[3];
        for (var i = 0; i < 3; i++)
        {
            pessoas[i] = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut7"), Perfil.Funcionario);
        }

        var alvo = await SemearAsync(api, Titulo("ZMut7 alvo"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30");
        var perguntaAlvo = await PerguntaAsync(api, alvo, "Como foi?");
        var outro = await SemearAsync(api, Titulo("ZMut7 outro"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-30");
        var perguntaOutro = await PerguntaAsync(api, outro, "Como foi?");
        await EscolherPessoasAsync(api, alvo, pessoas);
        await EscolherPessoasAsync(api, outro, pessoas);
        await FormulariosDeTeste.ResponderAsync(api, alvo, perguntaAlvo, pessoas[0]);
        await FormulariosDeTeste.ResponderAsync(api, outro, perguntaOutro, pessoas[1]); // respondeu OUTRO formulário, não este

        var resposta = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"/api/rh/formularios/{alvo}/lembretes", new { funcionarioIds = Array.Empty<int>() }));

        Assert.Equal((2, 1), (resposta.GetProperty("enviados").GetInt32(), resposta.GetProperty("ignorados").GetInt32())); // a pessoa 2 e a 3 faltam; só a 1 respondeu ESTE
    }

    [Fact]
    public async Task O_lembrete_anonimo_do_gestor_leva_o_setor_na_tarefa_e_o_do_RH_nao_leva_nada()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membros = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            membros.Add((await AdvertenciasDeTeste.NovoMembroAsync(api, setor)).Id);
        }

        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("ZMut8"), "PUBLICADO", anonimo: true, alvo: "PESSOAS", prazo: "2026-10-30");
        await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, [.. membros]);

        await gestor.PostAsync($"/api/gestor/formularios/{id}/lembretes", null);
        await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"/api/rh/formularios/{id}/lembretes", new { funcionarioIds = Array.Empty<int>() });

        Assert.Equal($$"""{"setorId":{{setor.SetorId}}}""", (await TextoAsync(api, $"SELECT PARAMETROS FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=SETOR_{setor.SetorId}:envio=1'"))!.Replace(" ", string.Empty, StringComparison.Ordinal));
        Assert.Null(await TextoAsync(api, $"SELECT PARAMETROS FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=TODOS:envio=1'"));
    }

    [Fact]
    public async Task A_recuperacao_agenda_o_lembrete_automatico_que_cai_hoje()
    {
        var api = await ApiAsync();
        var pessoas = new int[2];
        for (var i = 0; i < 2; i++)
        {
            pessoas[i] = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut9"), Perfil.Funcionario);
        }

        var id = await SemearAsync(api, Titulo("ZMut9"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-14"); // o dia do lembrete é 7 dias antes: hoje
        await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, pessoas);

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == "LEMBRETE_FORMULARIO").RecuperarAsync(CancellationToken.None);
        }

        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'LEMBRETE_FORMULARIO' AND CHAVE_IDEMPOTENCIA = 'LEMBRETE_FORMULARIO:form={id}:dia=2026-10-07:alcance=TODOS'"));
    }

    // ---------------------------------------------------------------- equipe do gestor

    [Fact]
    public async Task Quem_entra_hoje_ja_e_da_equipe_do_gestor()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var novo = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut10 entra hoje"), Perfil.Funcionario, setor.SetorId, true, new DateOnly(2026, 10, 7));
        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("ZMut10"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-11-15");
        await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, novo);

        var lista = await CorpoAsync(await gestor.GetAsync("/api/gestor/formularios"));

        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray(), i => i.GetProperty("id").GetInt32() == id);
        Assert.Equal(1, item.GetProperty("esperados").GetInt32());
    }

    [Fact]
    public async Task As_participacoes_da_equipe_num_anonimo_encerrado_sao_so_as_do_formulario()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membros = new List<int>();
        for (var i = 0; i < 3; i++)
        {
            membros.Add((await AdvertenciasDeTeste.NovoMembroAsync(api, setor)).Id);
        }

        var gestor = await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId);
        var alvo = await SemearAsync(api, Titulo("ZMut11 alvo"), "PUBLICADO", anonimo: true, alvo: "PESSOAS", prazo: "2026-11-15");
        var outro = await SemearAsync(api, Titulo("ZMut11 outro"), "PUBLICADO", anonimo: true, alvo: "PESSOAS", prazo: "2026-11-15");
        foreach (var f in new[] { alvo, outro })
        {
            await PerguntaAsync(api, f, "Como foi?");
            await EscolherPessoasAsync(api, f, [.. membros]);
        }

        async Task ParticiparAsync(int formulario, int pessoa) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa})");
        await ParticiparAsync(alvo, membros[0]);
        await ParticiparAsync(outro, membros[1]);
        await ParticiparAsync(outro, membros[2]);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {alvo}");

        var corpo = await CorpoAsync(await gestor.GetAsync($"/api/gestor/formularios/{alvo}/respondentes"));

        Assert.Equal((1, 3), (corpo.GetProperty("respondidos").GetInt32(), corpo.GetProperty("esperados").GetInt32())); // só a participação deste formulário
    }
}

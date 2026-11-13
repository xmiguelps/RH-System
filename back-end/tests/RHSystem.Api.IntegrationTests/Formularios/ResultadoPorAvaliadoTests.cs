using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>listarAvaliadosFormulario</c> (a escolha de "quem" na tela de resultado das avaliações), no banco REAL com o relógio em 2026-10-07T12:42Z. Só avaliação (422 <c>FORMULARIO_NAO_E_AVALIACAO</c>); lista curta, sem paginação, por nome; <c>esperadas</c> exato (quantas pessoas deviam avaliar); <b>com nome</b> a contagem aparece sempre e o
/// resultado libera com uma resposta; <b>anônimo</b> só depois de ENCERRADO DE VERDADE (409 antes), o avaliado com menos de 3 participações vem com <c>totalRespostas</c> nulo e <c>resultadoLiberado</c> falso, e o resultado só é liberado com 3 participações E uma pergunta com 3 respostas: a lista concorda com <c>obterResultadoFormulario</c>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ResultadoPorAvaliadoTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ResultadoPorAvaliadoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Rota(int formulario) => $"/api/rh/formularios/{formulario}/avaliados";

    private static async Task<HttpResponseMessage> ListarAsync(ApiDeAcesso api, int formulario) => await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(formulario));

    private static async Task<HttpResponseMessage> ResultadoAsync(ApiDeAcesso api, int formulario, int avaliado) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{formulario}/resultado?avaliadoId={avaliado}");

    private static Task EncerrarAsync(ApiDeAcesso api, int formulario) => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {formulario}");

    private static Task AnonimaAsync(ApiDeAcesso api, int formulario, int pergunta, string texto, int avaliado) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ID_AVALIADO, ANONIMO) VALUES ({pergunta}, {formulario}, {Valor(texto)}, '2026-10-01 00:00:00', {avaliado}, 1)");

    /// <summary>Um avaliador do formulário anônimo: a participação, a linha da fotografia (quem devia avaliar quem) e a resposta à pergunta, se dada.</summary>
    private static async Task AvaliadorAsync(ApiDeAcesso api, int formulario, int pessoa, int avaliado, int? pergunta = null, string? texto = null)
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ({formulario}, {pessoa}, {avaliado})");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa}, {avaliado})");
        if (pergunta is { } p && texto is not null)
        {
            await AnonimaAsync(api, formulario, p, texto, avaliado);
        }
    }

    private static Task NomeadaAsync(ApiDeAcesso api, int formulario, int pergunta, int pessoa, string texto, int avaliado) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {formulario}, {pessoa}, {avaliado}, {Valor(texto)}, '2026-10-02 11:10:00', 0)");

    private static JsonElement Do(JsonElement lista, int avaliado) => lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == avaliado);

    // ---------------------------------------------------------------- anônima

    [Fact]
    public async Task Na_avaliacao_anonima_encerrada_so_quem_tem_3_participacoes_mostra_o_total_e_o_resultado_so_libera_com_uma_pergunta_de_3_respostas()
    {
        var api = await ApiAsync();
        var setorA = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var setorB = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var setorC = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("ZAva1"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        var comentario = await PerguntaAsync(api, id, "Comentário", "RESPOSTA_CURTA", 2);
        for (var i = 0; i < 3; i++) // A: 3 participações e a nota com 3 respostas
        {
            await AvaliadorAsync(api, id, (await AdvertenciasDeTeste.NovoMembroAsync(api, setorA)).Id, setorA.GestorFuncionarioId, nota, (i + 3).ToString());
        }

        for (var i = 0; i < 2; i++) // B: só 2 participações
        {
            await AvaliadorAsync(api, id, (await AdvertenciasDeTeste.NovoMembroAsync(api, setorB)).Id, setorB.GestorFuncionarioId, nota, "1");
        }

        for (var i = 0; i < 3; i++) // C: 3 participações, mas a única pergunta respondida (opcional) tem só 2 respostas
        {
            await AvaliadorAsync(api, id, (await AdvertenciasDeTeste.NovoMembroAsync(api, setorC)).Id, setorC.GestorFuncionarioId, i < 2 ? comentario : null, i < 2 ? "ok" : null);
        }

        await EncerrarAsync(api, id);

        var resposta = await ListarAsync(api, id);
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarAvaliadosFormulario");
        Assert.Equal(3, lista.GetProperty("minimoPorAvaliado").GetInt32());
        var nomes = lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!).ToList();
        Assert.Equal([.. nomes.Order(StringComparer.CurrentCultureIgnoreCase)], nomes); // por ordem de nome
        var a = Do(lista, setorA.GestorFuncionarioId);
        var b = Do(lista, setorB.GestorFuncionarioId);
        var c = Do(lista, setorC.GestorFuncionarioId);
        Assert.Equal((3, 3, true), (a.GetProperty("totalRespostas").GetInt32(), a.GetProperty("esperadas").GetInt32(), a.GetProperty("resultadoLiberado").GetBoolean()));
        Assert.Equal((JsonValueKind.Null, 2, false), (b.GetProperty("totalRespostas").ValueKind, b.GetProperty("esperadas").GetInt32(), b.GetProperty("resultadoLiberado").GetBoolean())); // "menos de 3 respostas", sem dizer quantas
        Assert.Equal((3, 3, false), (c.GetProperty("totalRespostas").GetInt32(), c.GetProperty("esperadas").GetInt32(), c.GetProperty("resultadoLiberado").GetBoolean()));
        Assert.Equal(3, lista.GetProperty("itens").GetArrayLength()); // só as pessoas avaliadas
        // a lista concorda com o resultado: liberado = 200 com pergunta; abaixo do mínimo = 422; 3 participações sem pergunta de 3 respostas = 200 com tudo oculto
        var resultadoA = await ResultadoAsync(api, id, setorA.GestorFuncionarioId);
        var resultadoB = await ResultadoAsync(api, id, setorB.GestorFuncionarioId);
        var resultadoC = await CorpoAsync(await ResultadoAsync(api, id, setorC.GestorFuncionarioId));
        Assert.Equal((200, 422), ((int)resultadoA.StatusCode, (int)resultadoB.StatusCode));
        Assert.Equal((0, 2), (resultadoC.GetProperty("perguntas").GetArrayLength(), resultadoC.GetProperty("perguntasOcultas").GetInt32())); // as duas perguntas ficam de fora (nota sem resposta e comentário com 2)
    }

    [Fact]
    public async Task Na_avaliacao_anonima_ainda_aberta_a_lista_e_409_mesmo_com_respostas_e_sem_dizer_quantas()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("ZAva2"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        for (var i = 0; i < 3; i++)
        {
            await AvaliadorAsync(api, id, (await AdvertenciasDeTeste.NovoMembroAsync(api, setor)).Id, setor.GestorFuncionarioId, nota, "4");
        }

        var resposta = await ListarAsync(api, id);

        Assert.Equal(409, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("RESULTADO_ANONIMO_INDISPONIVEL", "O resultado de um formulário anônimo só aparece depois que ele é encerrado."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
        Assert.DoesNotContain("totalRespostas", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- com nome

    [Fact]
    public async Task Com_nome_a_lista_mostra_a_contagem_sempre_inclusive_de_quem_saiu_e_concorda_com_o_resultado()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var feito = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZAva3 Feito");
        var faltando = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZAva3 Faltando");
        var saiu = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZAva3 Saiu");
        var id = await SemearAsync(api, Titulo("ZAva3"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({id}, {setor.GestorFuncionarioId})");
        await NomeadaAsync(api, id, nota, setor.GestorFuncionarioId, "5", feito.Id);
        await NomeadaAsync(api, id, nota, setor.GestorFuncionarioId, "3", saiu.Id);
        await AdvertenciasDeTeste.InativarAsync(api, saiu.Id);

        var resposta = await ListarAsync(api, id);
        var lista = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarAvaliadosFormulario");
        Assert.Equal(JsonValueKind.Null, lista.GetProperty("minimoPorAvaliado").ValueKind);
        var nomes = lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!).ToList();
        Assert.Equal([.. nomes.Order(StringComparer.CurrentCultureIgnoreCase)], nomes);
        var f = Do(lista, feito.Id);
        var p = Do(lista, faltando.Id);
        var s = Do(lista, saiu.Id);
        Assert.Equal((1, 1, true), (f.GetProperty("totalRespostas").GetInt32(), f.GetProperty("esperadas").GetInt32(), f.GetProperty("resultadoLiberado").GetBoolean()));
        Assert.Equal((0, 1, false), (p.GetProperty("totalRespostas").GetInt32(), p.GetProperty("esperadas").GetInt32(), p.GetProperty("resultadoLiberado").GetBoolean())); // a contagem aparece mesmo sem resultado liberado
        Assert.Equal((1, 0, true, false), (s.GetProperty("totalRespostas").GetInt32(), s.GetProperty("esperadas").GetInt32(), s.GetProperty("resultadoLiberado").GetBoolean(), s.GetProperty("funcionario").GetProperty("ativo").GetBoolean())); // quem saiu e foi avaliado continua na lista
        Assert.Equal(3, lista.GetProperty("itens").GetArrayLength());
        foreach (var (avaliado, total) in new[] { (feito.Id, 1), (faltando.Id, 0), (saiu.Id, 1) })
        {
            Assert.Equal(total, (await CorpoAsync(await ResultadoAsync(api, id, avaliado))).GetProperty("totalRespostas").GetInt32()); // o mesmo número da tela de resultado
        }
    }

    [Fact]
    public async Task Avaliacao_sem_ninguem_para_avaliar_devolve_a_lista_vazia()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZAva4"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");

        var lista = await CorpoAsync(await ListarAsync(api, id));

        Assert.Equal((0, JsonValueKind.Null), (lista.GetProperty("itens").GetArrayLength(), lista.GetProperty("minimoPorAvaliado").ValueKind));
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task Formulario_que_nao_e_avaliacao_e_422_o_inexistente_404_e_so_o_RH_ve()
    {
        var api = await ApiAsync();
        var pesquisa = await SemearAsync(api, Titulo("ZAva5"), "PUBLICADO", alvo: "PESSOAS");
        var avaliacao = await SemearAsync(api, Titulo("ZAva5 avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");

        var naoEAvaliacao = await ListarAsync(api, pesquisa);
        var inexistente = await ListarAsync(api, 99999999);
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(avaliacao));
        var membro = await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync(Rota(avaliacao));
        var semLogin = await api.Api.ClienteAnonimo().GetAsync(Rota(avaliacao));

        Assert.Equal([422, 404, 403, 403, 401], new[] { naoEAvaliacao, inexistente, gestor, membro, semLogin }.Select(r => (int)r.StatusCode));
        var recusa = await CorpoAsync(naoEAvaliacao);
        Assert.Equal(("FORMULARIO_NAO_E_AVALIACAO", "Este formulário não é uma avaliação."), (Codigo(recusa), recusa.GetProperty("detail").GetString()));
    }
}

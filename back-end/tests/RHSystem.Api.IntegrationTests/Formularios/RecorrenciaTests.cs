using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// A tarefa <c>REABRIR_FORMULARIO</c> (o formulário recorrente), no banco REAL. Só reabre o ENCERRADO (inclusive o prazo vencido com a tarefa de encerrar parada); ainda publicado, nada se apaga e a data avança; reabrir apaga as respostas, as participações e os ARQUIVOS da rodada anterior, religa o <c>ATIVO</c> e limpa o encerramento juntos, grava a nova publicação,
/// avança o prazo o mesmo número de dias, recalcula a próxima reabertura (até <c>ate</c>) e agenda o encerramento, o lembrete e a reabertura seguinte; o anônimo tira a fotografia de novo e só reabre com 5 ou mais pessoas, senão fica encerrado e a recorrência acaba (sem apagar nada); a tarefa rodada duas vezes reabre uma vez; encerrar à mão cancela a recorrência.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class RecorrenciaTests(MySqlFixture mysql)
{
    private const string Tarefa = "REABRIR_FORMULARIO";
    private static readonly DateTime NoDia = new(2026, 10, 8, 3, 0, 10, DateTimeKind.Utc); // 08/10 00:00:10 no Brasil

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(RecorrenciaTests));
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

    /// <summary>Um recorrente (a cada semana) publicado em 01/10 com prazo em 05/10, já ENCERRADO, com a reabertura marcada para 08/10 00:00 (-03:00).</summary>
    private static async Task<(int Id, int Pergunta)> EncerradoAsync(ApiDeAcesso api, string prefixo, bool anonimo, int[] pessoas, string? ate = null, string prazo = "2026-10-05", bool encerrar = true)
    {
        var id = await SemearAsync(api, Titulo(prefixo), "PUBLICADO", anonimo: anonimo, alvo: "PESSOAS", prazo: prazo);
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        await EscolherPessoasAsync(api, id, pessoas);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 1, RECORRENCIA_UNIDADE = 'SEMANA', PROXIMA_REABERTURA = '2026-10-08 00:00:00', RECORRENCIA_ATE = {Valor(ate)} WHERE ID_FORMULARIO = {id}");
        return (id, pergunta);
    }

    private static Task EncerrarNoBancoAsync(ApiDeAcesso api, int id) => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {id}");

    private static async Task ReabrirAsync(ApiDeAcesso api, int id, string instante = "2026-10-08T03:00:00Z")
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).ExecutarAsync(new TarefaParaExecutar(0, $"{Tarefa}:formulario={id}:em={instante}", id, null, null, 0), CancellationToken.None);
    }

    private static Task<long> TarefasAsync(ApiDeAcesso api, string tipo, int id, string situacao = "PENDENTE", string? chave = null) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{tipo}' AND REFERENCIA_ID = {id} AND SITUACAO = '{situacao}'" + (chave is null ? string.Empty : $" AND CHAVE_IDEMPOTENCIA = '{chave}'"));

    private static async Task<string?> CampoAsync(ApiDeAcesso api, int id, string coluna) => await TextoAsync(api, $"SELECT {coluna} FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");

    private static Task ParticiparAsync(ApiDeAcesso api, int formulario, int pessoa) => Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa})");

    // ---------------------------------------------------------------- com nome

    [Fact]
    public async Task A_reabertura_do_formulario_com_nome_apaga_a_rodada_anterior_inclusive_o_arquivo_e_agenda_a_rodada_nova()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZRec1", 2);
        var (id, pergunta) = await EncerradoAsync(api, "ZRec1", false, pessoas, prazo: "2026-10-05");
        var anexo = await PerguntaAsync(api, id, "Comprovante", "ARQUIVO", 2);
        var caminho = RHSystem.Application.Arquivos.CaminhosDeArquivo.Novo("formularios", "pdf");
        await api.Api.Armazenamento.GravarAsync(caminho, new MemoryStream(ArquivosDeTeste.Pdf()), "application/pdf", CancellationToken.None);
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoas[0]);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL) VALUES ({anexo}, {id}, {pessoas[0]}, 'a.pdf', '2026-10-05 10:00:00', 0, '{caminho}', 'a.pdf')");
        await EncerrarNoBancoAsync(api, id);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);

        Assert.Equal(("True", null, "2026-10-08 00:00:00", "2026-10-12", "2026-10-15 00:00:00"), (
            await CampoAsync(api, id, "ATIVO"),
            await CampoAsync(api, id, "DATA_ENCERRAMENTO"),
            await CampoAsync(api, id, "DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s')"),
            await CampoAsync(api, id, "DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d')"), // a rodada anterior durou 4 dias (de 01/10 a 05/10): a nova, que abre em 08/10, vence em 12/10
            await CampoAsync(api, id, "DATE_FORMAT(PROXIMA_REABERTURA, '%Y-%m-%d %H:%i:%s')")));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"));
        Assert.False(api.Api.Armazenamento.Existe(caminho)); // o arquivo da rodada anterior sai do armazenamento
        Assert.Equal((1L, 1L), (await TarefasAsync(api, "ENCERRAR_FORMULARIO", id, chave: "ENCERRAR_FORMULARIO:formulario=" + id + ":em=2026-10-13T03:00:00Z"), await TarefasAsync(api, Tarefa, id, chave: $"{Tarefa}:formulario={id}:em=2026-10-15T03:00:00Z")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((TipoDeAtor.Sistema, AuditoriaAcao.Alterou, $"Reabriu o formulário \"{await CampoAsync(api, id, "TITULO")}\" para uma nova rodada."), (evento.AtorTipo, evento.Acao, evento.Descricao));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task Depois_de_reabrir_a_pergunta_volta_a_ser_editavel_e_a_pessoa_pode_responder_de_novo()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZRec2");
        var (id, pergunta) = await EncerradoAsync(api, "ZRec2", false, [pessoa]);
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoa);
        await EncerrarNoBancoAsync(api, id);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);
        var editar = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"/api/rh/formularios/{id}/perguntas/{pergunta}", new { texto = "Pergunta nova", tipo = "RESPOSTA_CURTA", obrigatoria = false, alternativas = Array.Empty<object>() });
        var responder = await ResponderAsync(await AdvertenciasDeTeste.ClienteDeAsync(api, pessoa), id, Envio(Json(new { perguntaId = pergunta, texto = "De novo." })));

        Assert.Equal((200, 201), ((int)editar.StatusCode, (int)responder.StatusCode));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task Ainda_publicado_na_data_nao_apaga_nada_e_a_data_avanca_para_a_proxima()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZRec3");
        var (id, pergunta) = await EncerradoAsync(api, "ZRec3", false, [pessoa], prazo: "2026-10-30");
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoa);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);

        Assert.Equal(("2026-10-15 00:00:00", "True", 1L, 1L), (
            await CampoAsync(api, id, "DATE_FORMAT(PROXIMA_REABERTURA, '%Y-%m-%d %H:%i:%s')"),
            await CampoAsync(api, id, "ATIVO"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"), // nada foi apagado
            await TarefasAsync(api, Tarefa, id, chave: $"{Tarefa}:formulario={id}:em=2026-10-15T03:00:00Z")));
        Assert.Empty(await EventosAsync(api, id));
        Assert.Equal("2026-10-30", await CampoAsync(api, id, "DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d')")); // o prazo também não mudou
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task O_prazo_vencido_com_a_tarefa_de_encerrar_parada_ja_conta_como_encerrado_e_reabre()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZRec4");
        var (id, pergunta) = await EncerradoAsync(api, "ZRec4", false, [pessoa], prazo: "2026-10-05"); // ATIVO continua ligado: a tarefa de encerrar nunca rodou
        await FormulariosDeTeste.ResponderAsync(api, id, pergunta, pessoa);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);

        Assert.Equal((0L, "2026-10-12"), (await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"), await CampoAsync(api, id, "DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d')")));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task A_tarefa_rodada_duas_vezes_reabre_uma_vez_so_e_a_antiga_depois_de_encerrar_a_mao_nao_reabre()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZRec5");
        var (reaberto, pergunta) = await EncerradoAsync(api, "ZRec5 reaberto", false, [pessoa]);
        await FormulariosDeTeste.ResponderAsync(api, reaberto, pergunta, pessoa);
        await EncerrarNoBancoAsync(api, reaberto);
        var (cancelado, perguntaDoCancelado) = await EncerradoAsync(api, "ZRec5 à mão", false, [pessoa], prazo: "2026-10-30"); // ainda publicado: dá para encerrar à mão
        await FormulariosDeTeste.ResponderAsync(api, cancelado, perguntaDoCancelado, pessoa);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 1 WHERE ID_FORMULARIO = {cancelado}");
        var encerrar = await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/formularios/{cancelado}/encerrar?confirmar=true", content: null);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, reaberto);
        await ReabrirAsync(api, reaberto); // a segunda acha a data já avançada e não faz nada
        await ReabrirAsync(api, cancelado);

        Assert.Equal(200, (int)encerrar.StatusCode);
        Assert.Equal((1, null, 1L, "False"), (
            (await EventosAsync(api, reaberto)).Count,
            await CampoAsync(api, cancelado, "PROXIMA_REABERTURA"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {cancelado}"), // as respostas do encerrado à mão continuam
            await CampoAsync(api, cancelado, "ATIVO")));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task Ao_passar_do_limite_ate_a_proxima_reabertura_some_e_o_lembrete_da_rodada_nova_e_agendado()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZRec6");
        var (ultimo, _) = await EncerradoAsync(api, "ZRec6 último", false, [pessoa], ate: "2026-10-10");
        var (comLembrete, _) = await EncerradoAsync(api, "ZRec6 lembrete", false, [pessoa], prazo: "2026-10-01");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET DATA_PUBLICACAO = '2026-09-20 00:00:00', RECORRENCIA_INTERVALO = 2, RECORRENCIA_UNIDADE = 'MES' WHERE ID_FORMULARIO = {comLembrete}"); // a rodada anterior durou 11 dias: a nova vence em 19/10 e o lembrete é em 12/10
        await EncerrarNoBancoAsync(api, ultimo);
        await EncerrarNoBancoAsync(api, comLembrete);
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, ultimo);
        await ReabrirAsync(api, comLembrete);

        Assert.Equal((null, "True"), (await CampoAsync(api, ultimo, "PROXIMA_REABERTURA"), await CampoAsync(api, ultimo, "ATIVO"))); // 15/10 passa do limite de 10/10: não há próxima
        Assert.Equal(0L, await TarefasAsync(api, Tarefa, ultimo));
        Assert.Equal(("2026-10-19", 1L), (await CampoAsync(api, comLembrete, "DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d')"), await TarefasAsync(api, "LEMBRETE_FORMULARIO", comLembrete, chave: $"LEMBRETE_FORMULARIO:form={comLembrete}:dia=2026-10-12:alcance=TODOS")));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    // ---------------------------------------------------------------- anônimo

    [Fact]
    public async Task O_anonimo_reabre_com_a_fotografia_nova_e_apaga_respostas_e_participacoes()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZRec7", 6);
        var (id, pergunta) = await EncerradoAsync(api, "ZRec7", true, pessoas);
        foreach (var pessoa in pessoas.Take(3))
        {
            await ParticiparAsync(api, id, pessoa);
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, 'ok', '2026-10-01 00:00:00', 1)");
        }

        await EncerrarNoBancoAsync(api, id);
        await AdvertenciasDeTeste.InativarAsync(api, pessoas[5]); // sai antes da nova rodada: a fotografia nova tem 5
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);

        Assert.Equal(("True", 0L, 0L, 5L), (
            await CampoAsync(api, id, "ATIVO"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {id}"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_FUNCIONARIO IS NOT NULL")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.DoesNotMatch("[0-9]", evento.Descricao.Replace("\"" + await CampoAsync(api, id, "TITULO") + "\"", string.Empty)); // sem contagem nenhuma
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task O_anonimo_que_ficou_abaixo_de_5_pessoas_continua_encerrado_sem_recorrencia_e_nada_e_apagado()
    {
        var api = await ApiAsync();
        var pessoas = await PessoasAsync(api, "ZRec8", 5);
        var (id, pergunta) = await EncerradoAsync(api, "ZRec8", true, pessoas);
        await ParticiparAsync(api, id, pessoas[0]);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {id}, 'ok', '2026-10-01 00:00:00', 1)");
        await EncerrarNoBancoAsync(api, id);
        await AdvertenciasDeTeste.InativarAsync(api, pessoas[4]); // sobram 4
        api.Api.Relogio.Fixar(NoDia);

        await ReabrirAsync(api, id);

        Assert.Equal(("False", null, 1L, 1L), (
            await CampoAsync(api, id, "ATIVO"),
            await CampoAsync(api, id, "PROXIMA_REABERTURA"),
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"), // a reabertura inteira foi desfeita
            await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {id}")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Contains("Não reabriu o formulário", evento.Descricao, StringComparison.Ordinal);
        Assert.NotNull(await CampoAsync(api, id, "DATA_ENCERRAMENTO"));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    // ---------------------------------------------------------------- agendar

    [Fact]
    public async Task Publicar_um_recorrente_agenda_a_primeira_reabertura_e_o_agendado_a_agenda_ao_ser_publicado_pela_tarefa()
    {
        var api = await ApiAsync();
        async Task<int> NovoAsync(string prefixo, string? agendarPara)
        {
            var pessoas = await PessoasAsync(api, prefixo, 2);
            var id = await SemearAsync(api, Titulo(prefixo), descricao: "Conte como foi.");
            await PerguntaAsync(api, id, "Como foi?", obrigatoria: true);
            var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"/api/rh/formularios/{id}/publicar", new { alvo = "PESSOAS", setorIds = Array.Empty<int>(), funcionarioIds = pessoas, agendarPara, prazo = agendarPara is null ? "2026-10-20" : "2026-11-20", recorrencia = new { intervalo = 2, unidade = "SEMANA" }, lembreteAutomatico = true });
            Assert.Equal(200, (int)resposta.StatusCode);
            return id;
        }

        var agora = await NovoAsync("ZRec9 agora", null);
        var depois = await NovoAsync("ZRec9 depois", "2026-11-03");
        var antes = await TarefasAsync(api, Tarefa, depois);
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 3, 3, 0, 5, DateTimeKind.Utc));
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == "PUBLICAR_FORMULARIO").ExecutarAsync(new TarefaParaExecutar(0, $"PUBLICAR_FORMULARIO:formulario={depois}:em=2026-11-03T03:00:00Z", depois, null, null, 0), CancellationToken.None);
        }

        Assert.Equal(1L, await TarefasAsync(api, Tarefa, agora, chave: $"{Tarefa}:formulario={agora}:em=2026-10-21T03:00:00Z")); // 07/10 mais 2 semanas
        Assert.Equal((0L, 1L), (antes, await TarefasAsync(api, Tarefa, depois, chave: $"{Tarefa}:formulario={depois}:em=2026-11-17T03:00:00Z"))); // só agenda quando a tarefa de publicar ativa o formulário: 03/11 mais 2 semanas
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
    }

    [Fact]
    public async Task Formulario_por_link_nao_pode_ser_recorrente_e_a_recuperacao_agenda_a_reabertura_que_ficou_sem_tarefa()
    {
        var api = await ApiAsync();
        var porLink = await SemearAsync(api, Titulo("ZRec10 link"), tipo: "ENTREVISTA", descricao: "Conte como foi.");
        await PerguntaAsync(api, porLink, "Como foi?", obrigatoria: true);
        var link = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"/api/rh/formularios/{porLink}/publicar", new { alvo = "LINK", setorIds = Array.Empty<int>(), funcionarioIds = Array.Empty<int>(), prazo = "2026-10-20", recorrencia = new { intervalo = 1, unidade = "SEMANA" }, lembreteAutomatico = false });
        var pessoa = await PessoaAsync(api, "ZRec10");
        var (semTarefa, _) = await EncerradoAsync(api, "ZRec10 sem tarefa", false, [pessoa]);

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).RecuperarAsync(CancellationToken.None);
        }

        Assert.Equal(400, (int)link.StatusCode);
        Assert.True((await CorpoAsync(link)).GetProperty("errors").TryGetProperty("recorrencia", out _));
        Assert.Equal(1L, await TarefasAsync(api, Tarefa, semTarefa, chave: $"{Tarefa}:formulario={semTarefa}:em=2026-10-08T03:00:00Z"));
    }
}

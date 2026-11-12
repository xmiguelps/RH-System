using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// A gestão dos avisos pelo RH (<c>/api/rh/avisos</c>: listar, criar, resumo, obter, editar, excluir, publicar e encerrar), no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 em Brasília). O aviso nasce pela API (o caminho de verdade) ou por SQL
/// quando o teste só precisa de um estado. As datas do banco são locais (-03:00); as da API, UTC. O e-mail da publicação é da T5: aqui só se confere a tarefa agendada (<c>PUBLICAR_AVISO</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AvisosRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/avisos";
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AvisosRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    private static object Novo(string titulo, string tipo = "SIMPLES", string conteudo = "Texto do aviso.", bool fixado = false, bool publicar = true, string? publicarEmUtc = null, string? expiraEm = null) =>
        new { tipo, titulo, conteudo, fixado, publicar, publicarEmUtc, expiraEm };

    private static async Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota, corpo);

    private static async Task<int> CriarComSucessoAsync(ApiDeAcesso api, object corpo)
    {
        var resposta = await CriarAsync(api, corpo);
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static async Task<JsonElement> ObterAsync(ApiDeAcesso api, int id) => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}"));

    private static async Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}", corpo);

    private static async Task<HttpResponseMessage> AcaoAsync(ApiDeAcesso api, int id, string acao) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/{acao}", new { });

    private static async Task<HttpResponseMessage> ListarAsync(ApiDeAcesso api, string consulta = "") => await (await api.ClienteDoRhAgoraAsync()).GetAsync(consulta.Length == 0 ? Rota : $"{Rota}?{consulta}");

    private static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int avisoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "AVISOS", avisoId);

    private static Task<long> TarefasAsync(ApiDeAcesso api, int avisoId, string situacao, string? instanteUtc = null) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO' AND REFERENCIA_ID = {avisoId} AND SITUACAO = '{situacao}'" + (instanteUtc is null ? string.Empty : $" AND CHAVE_IDEMPOTENCIA = 'PUBLICAR_AVISO:aviso={avisoId}:em={instanteUtc}'"));

    /// <summary>Um aviso direto no banco (datas LOCAIS, -03:00). O trigger de INSERT põe o NOW() real onde a data vem nula; o rascunho sem data volta a NULL.</summary>
    private static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, bool enviado, string? publicacaoLocal, string? expiracaoLocal, string tipo = "SIMPLES", bool fixado = false, string conteudo = "Conteúdo semeado.")
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, DATA_EXPIRACAO, TIPO_AVISO, FIXADO, ENVIADO) VALUES ({api.Elenco.Rh.FuncionarioId}, '{titulo}', '{conteudo}', {Data(publicacaoLocal ?? "2000-01-01 00:00:00")}, {Data(expiracaoLocal)}, '{tipo}', {(fixado ? 1 : 0)}, {(enviado ? 1 : 0)})");
        var id = (int)await ContarAsync(api, $"SELECT ID_AVISO FROM AVISOS WHERE TITULO = '{titulo}'");
        if (publicacaoLocal is null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS SET DATA_PUBLICACAO = NULL WHERE ID_AVISO = {id}");
        }

        return id;
    }

    private static string Data(string? local) => local is null ? "NULL" : $"'{local}'";

    private static Task LerAsync(ApiDeAcesso api, int avisoId, int quantidade) =>
        Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS_VISUALIZACOES SET VISUALIZADO = TRUE, DATA_VISUALIZACAO = '2026-10-06 10:00:00' WHERE ID_AVISO = {avisoId} AND ID_AVISO_VISUALIZACAO IN (SELECT ID FROM (SELECT ID_AVISO_VISUALIZACAO AS ID FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {avisoId} ORDER BY ID_AVISO_VISUALIZACAO LIMIT {quantidade}) t)");

    // ---------------------------------------------------------------- criar

    [Fact]
    public async Task Criar_publicando_agora_devolve_o_aviso_ativo_com_o_autor_a_previa_e_a_leitura_e_agenda_a_tarefa_do_e_mail()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Reunião geral");

        var resposta = await CriarAsync(api, Novo(titulo, conteudo: "Reunião de todos amanhã às 9h.", fixado: true));
        var aviso = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = aviso.GetProperty("id").GetInt32();
        Assert.Equal(("SIMPLES", titulo, "Reunião de todos amanhã às 9h.", "Reunião de todos amanhã às 9h.", "ATIVO", true, "2026-10-07T12:42:00Z", JsonValueKind.Null, "Ana Souza"), (aviso.GetProperty("tipo").GetString(), aviso.GetProperty("titulo").GetString(), aviso.GetProperty("conteudo").GetString(), aviso.GetProperty("previa").GetString(), aviso.GetProperty("situacao").GetString(), aviso.GetProperty("fixado").GetBoolean(), aviso.GetProperty("publicacaoEmUtc").GetString(), aviso.GetProperty("expiraEmUtc").ValueKind, aviso.GetProperty("autorNome").GetString()));
        Assert.Equal((0, await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES v JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO WHERE v.ID_AVISO = {id} AND f.ATIVO = TRUE")), (aviso.GetProperty("leitura").GetProperty("viram").GetInt32(), (long)aviso.GetProperty("leitura").GetProperty("total").GetInt32()));
        Assert.Equal(("True", "True", "2026-10-07 09:42:00"), (await TextoAsync(api, $"SELECT ENVIADO FROM AVISOS WHERE ID_AVISO = {id}"), await TextoAsync(api, $"SELECT FIXADO FROM AVISOS WHERE ID_AVISO = {id}"), Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_PUBLICACAO FROM AVISOS WHERE ID_AVISO = {id}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(1, await TarefasAsync(api, id, "PENDENTE", "2026-10-07T12:42:00Z"));
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Publicou], (await EventosAsync(api, id)).Select(e => e.Acao));
        Assert.Equal(AuditoriaArea.Avisos, (await EventosAsync(api, id))[0].Area);
    }

    [Fact]
    public async Task Criar_rascunho_sem_data_volta_com_publicacao_nula_e_com_data_prevista_a_guarda_e_nenhum_dos_dois_agenda_tarefa()
    {
        var api = await ApiAsync();

        var semData = await CriarAsync(api, Novo(Titulo("Rascunho sem data"), publicar: false));
        var comData = await CriarAsync(api, Novo(Titulo("Rascunho com data"), publicar: false, publicarEmUtc: "2026-10-20T11:00:00Z"));
        var rascunho = await CorpoAsync(semData);
        var prevista = await CorpoAsync(comData);

        Assert.Equal(("RASCUNHO", JsonValueKind.Null, JsonValueKind.Null), (rascunho.GetProperty("situacao").GetString(), rascunho.GetProperty("publicacaoEmUtc").ValueKind, rascunho.GetProperty("leitura").ValueKind));
        Assert.Equal(("RASCUNHO", "2026-10-20T11:00:00Z", JsonValueKind.Null), (prevista.GetProperty("situacao").GetString(), prevista.GetProperty("publicacaoEmUtc").GetString(), prevista.GetProperty("leitura").ValueKind));
        Assert.Equal(("False", (string?)null, "2026-10-20 08:00:00"), (await TextoAsync(api, $"SELECT ENVIADO FROM AVISOS WHERE ID_AVISO = {rascunho.GetProperty("id").GetInt32()}"), await TextoAsync(api, $"SELECT DATA_PUBLICACAO FROM AVISOS WHERE ID_AVISO = {rascunho.GetProperty("id").GetInt32()}"), Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_PUBLICACAO FROM AVISOS WHERE ID_AVISO = {prevista.GetProperty("id").GetInt32()}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)));
        Assert.Equal(0, await TarefasAsync(api, rascunho.GetProperty("id").GetInt32(), "PENDENTE") + await TarefasAsync(api, prevista.GetProperty("id").GetInt32(), "PENDENTE"));
        Assert.Equal([AuditoriaAcao.Criou], (await EventosAsync(api, rascunho.GetProperty("id").GetInt32())).Select(e => e.Acao));
    }

    [Fact]
    public async Task Criar_com_data_futura_agenda_o_aviso_e_a_tarefa_e_data_passada_publica_agora()
    {
        var api = await ApiAsync();

        var agendado = await CorpoAsync(await CriarAsync(api, Novo(Titulo("Agendado"), publicarEmUtc: "2026-10-09T11:00:00Z", expiraEm: "2026-10-16")));
        var passado = await CorpoAsync(await CriarAsync(api, Novo(Titulo("Data passada"), publicarEmUtc: "2026-10-01T11:00:00Z")));

        Assert.Equal(("AGENDADO", "2026-10-09T11:00:00Z", "2026-10-17T02:59:59Z", JsonValueKind.Null), (agendado.GetProperty("situacao").GetString(), agendado.GetProperty("publicacaoEmUtc").GetString(), agendado.GetProperty("expiraEmUtc").GetString(), agendado.GetProperty("leitura").ValueKind));
        Assert.Equal(("ATIVO", "2026-10-07T12:42:00Z"), (passado.GetProperty("situacao").GetString(), passado.GetProperty("publicacaoEmUtc").GetString()));
        Assert.Equal(1, await TarefasAsync(api, agendado.GetProperty("id").GetInt32(), "PENDENTE", "2026-10-09T11:00:00Z"));
        Assert.Equal("2026-10-16 23:59:59", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_EXPIRACAO FROM AVISOS WHERE ID_AVISO = {agendado.GetProperty("id").GetInt32()}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task Criar_com_campos_invalidos_devolve_todos_os_erros_juntos_um_por_campo_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM AVISOS");

        var resposta = await CriarAsync(api, Novo(" ", "SIMPLES", new string('c', 281), expiraEm: "2026-10-06"));
        var problema = await CorpoAsync(resposta);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal(["conteudo", "expiraEm", "titulo"], problema.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("O aviso simples tem no máximo 280 caracteres.", problema.GetProperty("errors").GetProperty("conteudo")[0].GetString());
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM AVISOS"));
    }

    [Fact]
    public async Task Criar_com_tipo_invalido_ou_corpo_vazio_e_400_e_o_detalhado_aceita_o_que_o_simples_recusa()
    {
        var api = await ApiAsync();

        var tipoInvalido = await CriarAsync(api, Novo(Titulo("Tipo"), tipo: "OUTRO"));
        var detalhado = await CriarAsync(api, Novo(Titulo("Detalhado"), tipo: "DETALHADO", conteudo: "## Título\n" + new string('c', 400)));

        Assert.Equal(400, (int)tipoInvalido.StatusCode);
        Assert.Equal(201, (int)detalhado.StatusCode);
        var corpo = await CorpoAsync(detalhado);
        Assert.Equal(200, corpo.GetProperty("previa").GetString()!.Length); // só o início, em texto puro e sem o título
    }

    [Fact]
    public async Task Criar_publicando_no_dia_da_expiracao_vale_e_expiracao_antes_do_dia_da_publicacao_e_400_em_expiraEm()
    {
        var api = await ApiAsync();

        var mesmoDia = await CriarAsync(api, Novo(Titulo("Expira hoje"), expiraEm: "2026-10-07"));
        var antes = await CriarAsync(api, Novo(Titulo("Expira antes"), publicarEmUtc: "2026-10-12T11:00:00Z", expiraEm: "2026-10-11"));

        Assert.Equal(201, (int)mesmoDia.StatusCode);
        Assert.Equal("2026-10-08T02:59:59Z", (await CorpoAsync(mesmoDia)).GetProperty("expiraEmUtc").GetString());
        Assert.Equal(400, (int)antes.StatusCode);
        Assert.Contains("expiraEm", (await CorpoAsync(antes)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
    }

    // ---------------------------------------------------------------- obter

    [Fact]
    public async Task Obter_traz_o_conteudo_de_qualquer_situacao_e_a_leitura_so_no_ativo_e_no_expirado()
    {
        var api = await ApiAsync();
        var ativo = await SemearAsync(api, Titulo("Ativo obter"), true, "2026-10-01 08:00:00", null);
        var expirado = await SemearAsync(api, Titulo("Expirado obter"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendado = await SemearAsync(api, Titulo("Agendado obter"), true, "2026-10-09 08:00:00", null);
        var rascunho = await SemearAsync(api, Titulo("Rascunho obter"), false, null, null);
        await LerAsync(api, ativo, 2);

        var doAtivo = await ObterAsync(api, ativo);
        var doExpirado = await ObterAsync(api, expirado);

        Assert.Equal(("ATIVO", "Conteúdo semeado.", 2), (doAtivo.GetProperty("situacao").GetString(), doAtivo.GetProperty("conteudo").GetString(), doAtivo.GetProperty("leitura").GetProperty("viram").GetInt32()));
        Assert.Equal(("EXPIRADO", "2026-10-01T02:59:59Z"), (doExpirado.GetProperty("situacao").GetString(), doExpirado.GetProperty("expiraEmUtc").GetString()));
        Assert.Equal(JsonValueKind.Object, doExpirado.GetProperty("leitura").ValueKind);
        Assert.Equal(("AGENDADO", JsonValueKind.Null), ((await ObterAsync(api, agendado)).GetProperty("situacao").GetString(), (await ObterAsync(api, agendado)).GetProperty("leitura").ValueKind));
        Assert.Equal(("RASCUNHO", JsonValueKind.Null, JsonValueKind.Null), ((await ObterAsync(api, rascunho)).GetProperty("situacao").GetString(), (await ObterAsync(api, rascunho)).GetProperty("publicacaoEmUtc").ValueKind, (await ObterAsync(api, rascunho)).GetProperty("leitura").ValueKind));
        Assert.Equal(404, (int)(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/2147483647")).StatusCode);
    }

    // ---------------------------------------------------------------- editar

    [Fact]
    public async Task Editar_rascunho_muda_tudo_e_data_nula_apaga_a_data_prevista()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Rascunho a editar"), publicar: false, publicarEmUtc: "2026-10-20T11:00:00Z"));

        var resposta = await EditarAsync(api, id, new { titulo = "Rascunho editado", conteudo = "Novo texto.", fixado = true, publicarEmUtc = (string?)null, expiraEm = "2026-11-30" });
        var aviso = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("Rascunho editado", "Novo texto.", true, "RASCUNHO", JsonValueKind.Null, "2026-12-01T02:59:59Z"), (aviso.GetProperty("titulo").GetString(), aviso.GetProperty("conteudo").GetString(), aviso.GetProperty("fixado").GetBoolean(), aviso.GetProperty("situacao").GetString(), aviso.GetProperty("publicacaoEmUtc").ValueKind, aviso.GetProperty("expiraEmUtc").GetString()));
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], (await EventosAsync(api, id)).Select(e => e.Acao));
    }

    [Fact]
    public async Task Editar_agendado_com_nova_data_cancela_a_tarefa_antiga_e_agenda_outra_e_data_nula_mantem()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Agendado a editar"), publicarEmUtc: "2026-10-09T11:00:00Z"));

        var nova = await EditarAsync(api, id, new { titulo = "Agendado editado", conteudo = "Texto.", fixado = false, publicarEmUtc = "2026-10-10T12:00:00Z", expiraEm = (string?)null });
        var mantem = await EditarAsync(api, id, new { titulo = "Agendado editado", conteudo = "Texto 2.", fixado = false, publicarEmUtc = (string?)null, expiraEm = (string?)null });

        Assert.Equal(200, (int)nova.StatusCode);
        Assert.Equal("2026-10-10T12:00:00Z", (await CorpoAsync(nova)).GetProperty("publicacaoEmUtc").GetString());
        Assert.Equal("2026-10-10T12:00:00Z", (await CorpoAsync(mantem)).GetProperty("publicacaoEmUtc").GetString());
        Assert.Equal((1, 1), (await TarefasAsync(api, id, "CANCELADA", "2026-10-09T11:00:00Z"), await TarefasAsync(api, id, "PENDENTE", "2026-10-10T12:00:00Z")));
        Assert.Equal(1, await TarefasAsync(api, id, "PENDENTE"));
    }

    [Fact]
    public async Task Editar_ativo_nao_muda_a_data_de_publicacao_mas_aceita_vazio_ou_a_mesma_e_a_expiracao_passada_o_expira()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Ativo a editar")));

        var outraData = await EditarAsync(api, id, new { titulo = "T", conteudo = "C", fixado = false, publicarEmUtc = "2026-10-08T12:00:00Z", expiraEm = (string?)null });
        var mesmaData = await EditarAsync(api, id, new { titulo = "T2", conteudo = "C", fixado = false, publicarEmUtc = "2026-10-07T12:42:00Z", expiraEm = (string?)null });
        var vazia = await EditarAsync(api, id, new { titulo = "T3", conteudo = "C", fixado = true, publicarEmUtc = (string?)null, expiraEm = (string?)null });

        Assert.Equal(422, (int)outraData.StatusCode);
        Assert.Equal(("AVISO_PUBLICACAO_NAO_EDITAVEL", "Este aviso já foi publicado; a data de publicação não pode mudar."), (Codigo(await CorpoAsync(outraData)), (await CorpoAsync(outraData)).GetProperty("detail").GetString()));
        Assert.Equal(200, (int)mesmaData.StatusCode);
        Assert.Equal(("T3", true, "ATIVO"), ((await CorpoAsync(vazia)).GetProperty("titulo").GetString(), (await CorpoAsync(vazia)).GetProperty("fixado").GetBoolean(), (await CorpoAsync(vazia)).GetProperty("situacao").GetString()));
        Assert.Equal("2026-10-07T12:42:00Z", (await ObterAsync(api, id)).GetProperty("publicacaoEmUtc").GetString());
    }

    [Fact]
    public async Task Editar_a_expiracao_do_expirado_para_o_futuro_volta_a_ativo_e_expiracao_vazia_tira_a_expiracao()
    {
        var api = await ApiAsync();
        var expirado = await SemearAsync(api, Titulo("Expirado a reviver"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");

        var reviveu = await CorpoAsync(await EditarAsync(api, expirado, new { titulo = "Reviveu", conteudo = "C", fixado = false, publicarEmUtc = (string?)null, expiraEm = "2026-10-31" }));
        var semExpiracao = await CorpoAsync(await EditarAsync(api, expirado, new { titulo = "Reviveu", conteudo = "C", fixado = false, publicarEmUtc = (string?)null, expiraEm = (string?)null }));
        var expiraNoPassado = await CorpoAsync(await EditarAsync(api, expirado, new { titulo = "Reviveu", conteudo = "C", fixado = false, publicarEmUtc = (string?)null, expiraEm = "2026-10-06" }));

        Assert.Equal(("ATIVO", "2026-11-01T02:59:59Z"), (reviveu.GetProperty("situacao").GetString(), reviveu.GetProperty("expiraEmUtc").GetString()));
        Assert.Equal(("ATIVO", JsonValueKind.Null), (semExpiracao.GetProperty("situacao").GetString(), semExpiracao.GetProperty("expiraEmUtc").ValueKind));
        Assert.Equal("EXPIRADO", expiraNoPassado.GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task Editar_confere_o_limite_pelo_tipo_do_aviso_que_nao_muda_404_se_nao_existe_e_400_com_todos_os_erros()
    {
        var api = await ApiAsync();
        var simples = await CriarComSucessoAsync(api, Novo(Titulo("Simples a editar")));

        var longo = await EditarAsync(api, simples, new { titulo = "T", conteudo = new string('c', 281), fixado = false, publicarEmUtc = (string?)null, expiraEm = (string?)null });
        var vazio = await EditarAsync(api, simples, new { titulo = "", conteudo = "", fixado = false, publicarEmUtc = (string?)null, expiraEm = "2026-10-01" });
        var inexistente = await EditarAsync(api, int.MaxValue, new { titulo = "T", conteudo = "C", fixado = false, publicarEmUtc = (string?)null, expiraEm = (string?)null });

        Assert.Equal(400, (int)longo.StatusCode);
        Assert.Equal(["conteudo", "expiraEm", "titulo"], (await CorpoAsync(vazio)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_rascunho_e_agendado_apaga_o_aviso_e_as_leituras_cancela_a_tarefa_e_audita()
    {
        var api = await ApiAsync();
        var agendado = await CriarComSucessoAsync(api, Novo(Titulo("Agendado a excluir"), publicarEmUtc: "2026-10-09T11:00:00Z"));
        var rascunho = await CriarComSucessoAsync(api, Novo(Titulo("Rascunho a excluir"), publicar: false));
        var leiturasAntes = await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO IN ({agendado}, {rascunho})");

        var doAgendado = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{agendado}");
        var doRascunho = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{rascunho}");

        Assert.True(leiturasAntes > 0); // o trigger criou as linhas na criação, inclusive no rascunho
        Assert.Equal((204, 204), ((int)doAgendado.StatusCode, (int)doRascunho.StatusCode));
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS WHERE ID_AVISO IN ({agendado}, {rascunho})") + await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO IN ({agendado}, {rascunho})"));
        Assert.Equal((1, 0), (await TarefasAsync(api, agendado, "CANCELADA"), await TarefasAsync(api, agendado, "PENDENTE")));
        Assert.Equal(AuditoriaAcao.Excluiu, (await EventosAsync(api, agendado)).Last().Acao);
        Assert.Equal(AuditoriaAcao.Excluiu, (await EventosAsync(api, rascunho)).Last().Acao);
    }

    [Fact]
    public async Task Excluir_aviso_ativo_ou_expirado_e_422_AVISO_NAO_PODE_SER_EXCLUIDO_e_nada_muda_e_o_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var ativo = await CriarComSucessoAsync(api, Novo(Titulo("Ativo a excluir")));
        var expirado = await SemearAsync(api, Titulo("Expirado a excluir"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");

        var doAtivo = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{ativo}");
        var doExpirado = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{expirado}");
        var inexistente = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/2147483647");

        Assert.Equal((422, 422, 404), ((int)doAtivo.StatusCode, (int)doExpirado.StatusCode, (int)inexistente.StatusCode));
        var problema = await CorpoAsync(doAtivo);
        Assert.Equal(("AVISO_NAO_PODE_SER_EXCLUIDO", "Só é possível excluir um aviso que ainda não foi publicado. Para tirar um aviso do ar, encerre-o."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(2, await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS WHERE ID_AVISO IN ({ativo}, {expirado})"));
    }

    // ---------------------------------------------------------------- publicar

    [Fact]
    public async Task Publicar_rascunho_grava_a_data_efetiva_agora_ou_a_prevista_se_for_futura_e_agenda_a_tarefa()
    {
        var api = await ApiAsync();
        var semData = await CriarComSucessoAsync(api, Novo(Titulo("Publicar sem data"), publicar: false));
        var dataPassada = await CriarComSucessoAsync(api, Novo(Titulo("Publicar data passada"), publicar: false, publicarEmUtc: "2026-10-05T11:00:00Z"));
        var dataFutura = await CriarComSucessoAsync(api, Novo(Titulo("Publicar data futura"), publicar: false, publicarEmUtc: "2026-10-12T11:00:00Z"));

        var a = await CorpoAsync(await AcaoAsync(api, semData, "publicar"));
        var b = await CorpoAsync(await AcaoAsync(api, dataPassada, "publicar"));
        var c = await CorpoAsync(await AcaoAsync(api, dataFutura, "publicar"));

        Assert.Equal([("ATIVO", "2026-10-07T12:42:00Z"), ("ATIVO", "2026-10-07T12:42:00Z"), ("AGENDADO", "2026-10-12T11:00:00Z")], new[] { a, b, c }.Select(x => (x.GetProperty("situacao").GetString()!, x.GetProperty("publicacaoEmUtc").GetString()!)));
        Assert.Equal((1, 1, 1), (await TarefasAsync(api, semData, "PENDENTE", "2026-10-07T12:42:00Z"), await TarefasAsync(api, dataPassada, "PENDENTE", "2026-10-07T12:42:00Z"), await TarefasAsync(api, dataFutura, "PENDENTE", "2026-10-12T11:00:00Z")));
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Publicou], (await EventosAsync(api, semData)).Select(e => e.Acao));
    }

    [Fact]
    public async Task Publicar_o_que_ja_foi_publicado_e_422_AVISO_JA_PUBLICADO_e_o_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var ativo = await CriarComSucessoAsync(api, Novo(Titulo("Ativo a publicar")));
        var agendado = await CriarComSucessoAsync(api, Novo(Titulo("Agendado a publicar"), publicarEmUtc: "2026-10-09T11:00:00Z"));

        var doAtivo = await AcaoAsync(api, ativo, "publicar");
        var doAgendado = await AcaoAsync(api, agendado, "publicar");
        var inexistente = await AcaoAsync(api, int.MaxValue, "publicar");

        Assert.Equal((422, 422, 404), ((int)doAtivo.StatusCode, (int)doAgendado.StatusCode, (int)inexistente.StatusCode));
        Assert.Equal(("AVISO_JA_PUBLICADO", "Este aviso já foi publicado."), (Codigo(await CorpoAsync(doAtivo)), (await CorpoAsync(doAtivo)).GetProperty("detail").GetString()));
        Assert.Equal("2026-10-09T11:00:00Z", (await ObterAsync(api, agendado)).GetProperty("publicacaoEmUtc").GetString()); // o segundo clique não mexe
    }

    [Fact]
    public async Task Publicar_com_expiracao_anterior_a_data_efetiva_e_400_em_expiraEm_e_o_rascunho_continua_rascunho()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Expira antes de publicar"), false, null, "2026-10-06 23:59:59");

        var resposta = await AcaoAsync(api, id, "publicar");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Contains("expiraEm", (await CorpoAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal("False", await TextoAsync(api, $"SELECT ENVIADO FROM AVISOS WHERE ID_AVISO = {id}"));
    }

    [Fact]
    public async Task Dois_cliques_em_publicar_ao_mesmo_tempo_publicam_uma_vez_so_e_o_outro_e_422()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Duplo clique"), publicar: false));
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cliente.PostAsJsonAsync($"{Rota}/{id}/publicar", new { })));

        Assert.Equal([200, 422, 422, 422], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(1, await TarefasAsync(api, id, "PENDENTE"));
        Assert.Single(await EventosAsync(api, id), e => e.Acao == AuditoriaAcao.Publicou);
    }

    // ---------------------------------------------------------------- encerrar

    [Fact]
    public async Task Encerrar_aviso_ativo_o_expira_agora_e_so_o_ativo_se_encerra()
    {
        var api = await ApiAsync();
        var ativo = await CriarComSucessoAsync(api, Novo(Titulo("Ativo a encerrar"), expiraEm: "2026-12-31"));
        var rascunho = await CriarComSucessoAsync(api, Novo(Titulo("Rascunho a encerrar"), publicar: false));
        var agendado = await CriarComSucessoAsync(api, Novo(Titulo("Agendado a encerrar"), publicarEmUtc: "2026-10-09T11:00:00Z"));
        var expirado = await SemearAsync(api, Titulo("Expirado a encerrar"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");

        var encerrado = await AcaoAsync(api, ativo, "encerrar");
        var segundo = await AcaoAsync(api, ativo, "encerrar");
        var recusados = new[] { await AcaoAsync(api, rascunho, "encerrar"), await AcaoAsync(api, agendado, "encerrar"), await AcaoAsync(api, expirado, "encerrar") };
        var inexistente = await AcaoAsync(api, int.MaxValue, "encerrar");

        var aviso = await CorpoAsync(encerrado);
        Assert.Equal(200, (int)encerrado.StatusCode);
        Assert.Equal(("EXPIRADO", "2026-10-07T12:42:00Z", JsonValueKind.Object), (aviso.GetProperty("situacao").GetString(), aviso.GetProperty("expiraEmUtc").GetString(), aviso.GetProperty("leitura").ValueKind));
        Assert.All(new[] { segundo }.Concat(recusados), r => Assert.Equal(422, (int)r.StatusCode));
        Assert.Equal(("AVISO_NAO_ESTA_ATIVO", "Só um aviso ativo pode ser encerrado."), (Codigo(await CorpoAsync(segundo)), (await CorpoAsync(segundo)).GetProperty("detail").GetString()));
        Assert.Equal(404, (int)inexistente.StatusCode);
        Assert.Equal(AuditoriaAcao.Alterou, (await EventosAsync(api, ativo)).Last().Acao);
    }

    // ---------------------------------------------------------------- listar

    [Fact]
    public async Task A_lista_padrao_e_a_dos_ativos_por_publicacao_decrescente_com_a_leitura_e_cada_aba_tem_a_sua_ordem()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS_VISUALIZACOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS");
        var antigo = await SemearAsync(api, "Ativo antigo", true, "2026-10-01 08:00:00", null);
        var novo = await SemearAsync(api, "Ativo novo", true, "2026-10-05 08:00:00", "2026-10-20 23:59:59");
        var expiradoA = await SemearAsync(api, "Expirado A", true, "2026-08-01 08:00:00", "2026-08-31 23:59:59");
        var expiradoB = await SemearAsync(api, "Expirado B", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendadoTarde = await SemearAsync(api, "Agendado tarde", true, "2026-10-20 08:00:00", null);
        var agendadoCedo = await SemearAsync(api, "Agendado cedo", true, "2026-10-09 08:00:00", null);
        var rascunhoA = await SemearAsync(api, "Rascunho A", false, null, null);
        var rascunhoB = await SemearAsync(api, "Rascunho B", false, null, null);
        await LerAsync(api, novo, 1);

        var ativos = await CorpoAsync(await ListarAsync(api));
        var expirados = await CorpoAsync(await ListarAsync(api, "situacao=EXPIRADO"));
        var agendados = await CorpoAsync(await ListarAsync(api, "situacao=AGENDADO"));
        var rascunhos = await CorpoAsync(await ListarAsync(api, "situacao=RASCUNHO"));

        Assert.Equal([novo, antigo], Ids(ativos)); // publicação decrescente
        Assert.Equal([expiradoB, expiradoA], Ids(expirados));
        Assert.Equal([agendadoCedo, agendadoTarde], Ids(agendados)); // quem publica primeiro
        Assert.Equal([rascunhoB, rascunhoA], Ids(rascunhos)); // o mais recentemente criado
        Assert.Equal((1, 20, 2, 1), (ativos.GetProperty("pagina").GetInt32(), ativos.GetProperty("tamanhoPagina").GetInt32(), ativos.GetProperty("total").GetInt32(), ativos.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(JsonValueKind.Object, ativos.GetProperty("itens")[0].GetProperty("leitura").ValueKind);
        Assert.Equal(1, ativos.GetProperty("itens")[0].GetProperty("leitura").GetProperty("viram").GetInt32());
        Assert.All(agendados.GetProperty("itens").EnumerateArray().Concat(rascunhos.GetProperty("itens").EnumerateArray()), a => Assert.Equal(JsonValueKind.Null, a.GetProperty("leitura").ValueKind));
        Assert.All(new[] { ativos, expirados, agendados, rascunhos }, lista => Assert.All(lista.GetProperty("itens").EnumerateArray(), a => Assert.Equal(lista.GetProperty("itens")[0].GetProperty("situacao").GetString(), a.GetProperty("situacao").GetString())));
    }

    [Fact]
    public async Task A_lista_filtra_por_tipo_busca_no_titulo_e_no_conteudo_e_pagina()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS_VISUALIZACOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS");
        var simples = await SemearAsync(api, "Café da manhã", true, "2026-10-01 08:00:00", null, "SIMPLES", conteudo: "Traga uma xícara.");
        var detalhado = await SemearAsync(api, "Política de viagens", true, "2026-10-02 08:00:00", null, "DETALHADO", conteudo: "## Regras\nReembolso de hospedagem em até 5 dias.");
        var terceiro = await SemearAsync(api, "Manutenção do sistema", true, "2026-10-03 08:00:00", null, "SIMPLES", conteudo: "O portal fica fora do ar.");

        var porTipo = await CorpoAsync(await ListarAsync(api, "tipo=DETALHADO"));
        var peloTitulo = await CorpoAsync(await ListarAsync(api, "busca=caf%C3%A9"));
        var peloConteudo = await CorpoAsync(await ListarAsync(api, "busca=hospedagem"));
        var primeiraPagina = await CorpoAsync(await ListarAsync(api, "pagina=1&tamanhoPagina=2"));
        var segundaPagina = await CorpoAsync(await ListarAsync(api, "pagina=2&tamanhoPagina=2"));

        Assert.Equal([detalhado], Ids(porTipo));
        Assert.Equal([simples], Ids(peloTitulo));
        Assert.Equal([detalhado], Ids(peloConteudo));
        Assert.Equal([terceiro, detalhado], Ids(primeiraPagina));
        Assert.Equal([simples], Ids(segundaPagina));
        Assert.Equal((3, 2), (primeiraPagina.GetProperty("total").GetInt32(), primeiraPagina.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal("Reembolso de hospedagem em até 5 dias.", porTipo.GetProperty("itens")[0].GetProperty("previa").GetString());
    }

    [Theory]
    [InlineData("situacao=ENCERRADO")]
    [InlineData("tipo=OUTRO")]
    [InlineData("pagina=0")]
    [InlineData("tamanhoPagina=101")]
    public async Task A_lista_com_filtro_invalido_e_400(string consulta)
    {
        var api = await ApiAsync();

        Assert.Equal(400, (int)(await ListarAsync(api, consulta)).StatusCode);
    }

    // ---------------------------------------------------------------- resumo

    [Fact]
    public async Task O_resumo_conta_cada_aba_a_media_de_leitura_dos_ativos_e_o_proximo_a_expirar_em_7_dias()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS_VISUALIZACOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS");
        var semAtivos = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/resumo"));
        var a = await SemearAsync(api, "Resumo A", true, "2026-10-01 08:00:00", "2026-10-12 23:59:59"); // expira em 5 dias
        var b = await SemearAsync(api, "Resumo B", true, "2026-10-02 08:00:00", "2026-10-10 23:59:59"); // expira em 3 dias: o próximo a expirar
        var c = await SemearAsync(api, "Resumo C", true, "2026-10-03 08:00:00", "2026-11-30 23:59:59"); // longe
        await SemearAsync(api, "Resumo D", true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        await SemearAsync(api, "Resumo E", true, "2026-10-20 08:00:00", null);
        await SemearAsync(api, "Resumo F", false, null, null);
        await SemearAsync(api, "Resumo G", false, null, null);
        var total = await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES v JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO WHERE v.ID_AVISO = {a} AND f.ATIVO = TRUE");
        await LerAsync(api, a, 1);
        await LerAsync(api, b, (int)total);

        var resumo = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/resumo"));

        Assert.Equal((JsonValueKind.Null, 0, JsonValueKind.Null), (semAtivos.GetProperty("leituraMediaDosAtivosPercentual").ValueKind, semAtivos.GetProperty("expiramNosProximos7Dias").GetInt32(), semAtivos.GetProperty("proximoAExpirar").ValueKind));
        Assert.Equal((3, 1, 1, 2), (resumo.GetProperty("contagens").GetProperty("ativos").GetInt32(), resumo.GetProperty("contagens").GetProperty("agendados").GetInt32(), resumo.GetProperty("contagens").GetProperty("expirados").GetInt32(), resumo.GetProperty("contagens").GetProperty("rascunhos").GetInt32()));
        var esperadoMedia = (int)Math.Round((((1d / total) + 1d + 0d) / 3d) * 100d, MidpointRounding.AwayFromZero);
        Assert.Equal((esperadoMedia, 2, b, "Resumo B", "2026-10-11T02:59:59Z"), (resumo.GetProperty("leituraMediaDosAtivosPercentual").GetInt32(), resumo.GetProperty("expiramNosProximos7Dias").GetInt32(), resumo.GetProperty("proximoAExpirar").GetProperty("avisoId").GetInt32(), resumo.GetProperty("proximoAExpirar").GetProperty("titulo").GetString(), resumo.GetProperty("proximoAExpirar").GetProperty("expiraEmUtc").GetString()));
        Assert.True(c > 0);
    }

    // ---------------------------------------------------------------- quem pode

    [Fact]
    public async Task Gestor_funcionario_e_sem_login_nao_gerem_avisos()
    {
        var api = await ApiAsync();
        var id = await CriarComSucessoAsync(api, Novo(Titulo("Só o RH mexe")));

        foreach (var pessoa in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
            Assert.Equal(403, (int)(await cliente.GetAsync(Rota)).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsJsonAsync(Rota, Novo("X"))).StatusCode);
            Assert.Equal(403, (int)(await cliente.GetAsync($"{Rota}/resumo")).StatusCode);
            Assert.Equal(403, (int)(await cliente.GetAsync($"{Rota}/{id}")).StatusCode);
            Assert.Equal(403, (int)(await cliente.DeleteAsync($"{Rota}/{id}")).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/publicar", new { })).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/encerrar", new { })).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.CreateClient().GetAsync(Rota)).StatusCode);
    }

    // ---------------------------------------------------------------- contrato

    [Fact]
    public async Task As_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        var cliente = await api.ClienteDoRhAgoraAsync();
        var criado = await CriarAsync(api, Novo(Titulo("Do contrato"), "DETALHADO", "## Seção\nTexto do contrato.", fixado: true, expiraEm: "2026-12-31"));
        var id = (await CorpoAsync(criado)).GetProperty("id").GetInt32();
        var rascunho = await CriarComSucessoAsync(api, Novo(Titulo("Rascunho do contrato"), publicar: false));
        var agendado = await CriarComSucessoAsync(api, Novo(Titulo("Agendado do contrato"), publicarEmUtc: "2026-10-09T11:00:00Z"));

        await criado.AfirmarConformeAoContratoAsync("criarAviso");
        await (await CriarAsync(api, Novo(" "))).AfirmarConformeAoContratoAsync("criarAviso"); // 400
        await (await ListarAsync(api)).AfirmarConformeAoContratoAsync("listarAvisos");
        await (await ListarAsync(api, "situacao=ENCERRADO")).AfirmarConformeAoContratoAsync("listarAvisos"); // 400
        await (await cliente.GetAsync($"{Rota}/resumo")).AfirmarConformeAoContratoAsync("obterResumoDosAvisos");
        await (await cliente.GetAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("obterAviso");
        await (await cliente.GetAsync($"{Rota}/2147483647")).AfirmarConformeAoContratoAsync("obterAviso"); // 404
        await (await EditarAsync(api, id, new { titulo = "Editado", conteudo = "Texto.", fixado = false, publicarEmUtc = (string?)null, expiraEm = (string?)null })).AfirmarConformeAoContratoAsync("editarAviso");
        await (await EditarAsync(api, id, new { titulo = "", conteudo = "", fixado = false })).AfirmarConformeAoContratoAsync("editarAviso"); // 400
        await (await EditarAsync(api, id, new { titulo = "T", conteudo = "C", fixado = false, publicarEmUtc = "2026-10-08T12:00:00Z" })).AfirmarConformeAoContratoAsync("editarAviso"); // 422
        await (await EditarAsync(api, int.MaxValue, new { titulo = "T", conteudo = "C", fixado = false })).AfirmarConformeAoContratoAsync("editarAviso"); // 404
        await (await AcaoAsync(api, rascunho, "publicar")).AfirmarConformeAoContratoAsync("publicarAviso");
        await (await AcaoAsync(api, rascunho, "publicar")).AfirmarConformeAoContratoAsync("publicarAviso"); // 422
        await (await AcaoAsync(api, int.MaxValue, "publicar")).AfirmarConformeAoContratoAsync("publicarAviso"); // 404
        await (await AcaoAsync(api, id, "encerrar")).AfirmarConformeAoContratoAsync("encerrarAviso");
        await (await AcaoAsync(api, id, "encerrar")).AfirmarConformeAoContratoAsync("encerrarAviso"); // 422
        await (await cliente.DeleteAsync($"{Rota}/{agendado}")).AfirmarConformeAoContratoAsync("excluirAviso"); // 204
        await (await cliente.DeleteAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("excluirAviso"); // 422
        await (await cliente.DeleteAsync($"{Rota}/2147483647")).AfirmarConformeAoContratoAsync("excluirAviso"); // 404
    }
}

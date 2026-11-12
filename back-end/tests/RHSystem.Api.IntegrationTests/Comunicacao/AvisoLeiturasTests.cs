using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// "Quem viu" (<c>GET /api/rh/avisos/{id}/leituras</c>) e "Lembrar quem não viu" (<c>POST /api/rh/avisos/{id}/lembretes</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. A conta é por linha de leitura de pessoa ATIVA hoje (o trigger
/// cria a linha dos ativos quando o aviso é criado; quem saiu fica fora; quem entrou depois e abriu entra). O lembrete enfileira um e-mail por pessoa ativa, admitida e com e-mail que ainda não abriu; a chave leva o minuto do clique, então o duplo clique não manda
/// em dobro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AvisoLeiturasTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/avisos";
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AvisoLeiturasTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    /// <summary>Um aviso direto no banco (datas LOCAIS). O trigger cria a linha de leitura de cada pessoa ativa agora.</summary>
    private static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, bool enviado, string? publicacaoLocal, string? expiracaoLocal = null)
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, DATA_EXPIRACAO, TIPO_AVISO, FIXADO, ENVIADO) VALUES ({api.Elenco.Rh.FuncionarioId}, '{titulo}', 'Conteúdo do aviso.', '{publicacaoLocal ?? "2000-01-01 00:00:00"}', {(expiracaoLocal is null ? "NULL" : $"'{expiracaoLocal}'")}, 'SIMPLES', 0, {(enviado ? 1 : 0)})");
        var id = (int)await ContarAsync(api, $"SELECT ID_AVISO FROM AVISOS WHERE TITULO = '{titulo}'");
        if (publicacaoLocal is null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS SET DATA_PUBLICACAO = NULL WHERE ID_AVISO = {id}");
        }

        return id;
    }

    private static Task LerAsync(ApiDeAcesso api, int avisoId, params int[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS_VISUALIZACOES SET VISUALIZADO = TRUE, DATA_VISUALIZACAO = '2026-10-06 10:00:00' WHERE ID_AVISO = {avisoId} AND ID_FUNCIONARIO IN ({string.Join(", ", pessoas)})");

    private static async Task<HttpResponseMessage> LeiturasAsync(ApiDeAcesso api, int id, string consulta = "") =>
        await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}/{id}/leituras" + (consulta.Length == 0 ? string.Empty : $"?{consulta}"));

    private static async Task<HttpResponseMessage> LembrarAsync(ApiDeAcesso api, int id) => await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/lembretes", new { });

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static async Task<List<int>> LembradosAsync(ApiDeAcesso api, int avisoId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.Database.SqlQuery<int>($"SELECT ID_FUNCIONARIO AS Value FROM FILA_EMAILS WHERE TIPO = 'AVISO_LEMBRETE' AND REFERENCIA_ID = {avisoId} ORDER BY ID_FUNCIONARIO, ID_EMAIL").ToListAsync();
    }

    private static async Task<List<int>> NaoLidosEsperadosAsync(ApiDeAcesso api, int avisoId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.Database.SqlQuery<int>($"SELECT f.ID_FUNCIONARIO AS Value FROM AVISOS_VISUALIZACOES v JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO WHERE v.ID_AVISO = {avisoId} AND v.VISUALIZADO = FALSE AND f.ATIVO = TRUE AND f.EMAIL_CORPORATIVO IS NOT NULL AND f.DATA_ADMISSAO <= '2026-10-07' ORDER BY f.ID_FUNCIONARIO").ToListAsync();
    }

    // ---------------------------------------------------------------- quem viu

    [Fact]
    public async Task Quem_viu_traz_os_totais_que_nao_mudam_com_o_filtro_a_lista_por_nome_e_o_filtro_LIDO_e_NAO_LIDO_com_pagina()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Quem viu"), true, "2026-10-01 08:00:00");
        var e = api.Elenco;
        await LerAsync(api, id, e.MembroA.FuncionarioId, e.Gestor.FuncionarioId);
        var total = (int)await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES v JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO WHERE v.ID_AVISO = {id} AND f.ATIVO = TRUE");

        var todos = await CorpoAsync(await LeiturasAsync(api, id, "tamanhoPagina=100"));
        var lidos = await CorpoAsync(await LeiturasAsync(api, id, "situacao=LIDO"));
        var naoLidos = await CorpoAsync(await LeiturasAsync(api, id, "situacao=NAO_LIDO&tamanhoPagina=2"));

        Assert.Equal((2, total - 2), (todos.GetProperty("viram").GetInt32(), todos.GetProperty("aindaNaoViram").GetInt32()));
        Assert.Equal((2, total - 2), (lidos.GetProperty("viram").GetInt32(), lidos.GetProperty("aindaNaoViram").GetInt32())); // os totais não mudam com o filtro
        Assert.Equal((total, 1, 1), (todos.GetProperty("total").GetInt32(), todos.GetProperty("pagina").GetInt32(), todos.GetProperty("totalPaginas").GetInt32()));
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var porNome = await db.Database.SqlQuery<int>($"SELECT f.ID_FUNCIONARIO AS Value FROM AVISOS_VISUALIZACOES v JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = v.ID_FUNCIONARIO WHERE v.ID_AVISO = {id} AND f.ATIVO = TRUE ORDER BY f.NOME, f.ID_FUNCIONARIO").ToListAsync();
            Assert.Equal(porNome, todos.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32())); // a ordem é a do nome, com a ordenação do banco
        }

        Assert.Equal(2, lidos.GetProperty("total").GetInt32());
        Assert.All(lidos.GetProperty("itens").EnumerateArray(), i => Assert.Equal(("2026-10-06T13:00:00Z", true), (i.GetProperty("lidoEmUtc").GetString(), i.GetProperty("lido").GetBoolean())));
        Assert.Equal((total - 2, 2, (total - 2 + 1) / 2), (naoLidos.GetProperty("total").GetInt32(), naoLidos.GetProperty("itens").GetArrayLength(), naoLidos.GetProperty("totalPaginas").GetInt32()));
        Assert.All(naoLidos.GetProperty("itens").EnumerateArray(), i => Assert.Equal((false, JsonValueKind.Null), (i.GetProperty("lido").GetBoolean(), i.GetProperty("lidoEmUtc").ValueKind)));
        var julia = todos.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == e.MembroB.FuncionarioId).GetProperty("funcionario");
        Assert.Equal((true, "Comercial"), (julia.GetProperty("ativo").GetBoolean(), julia.GetProperty("setor").GetString()));
    }

    [Fact]
    public async Task Quem_saiu_depois_fica_fora_da_conta_e_quem_entrou_depois_e_abriu_entra()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Quem saiu e quem entrou"), true, "2026-10-01 08:00:00");
        var saiu = api.Elenco.MembroB.FuncionarioId;
        var antes = await CorpoAsync(await LeiturasAsync(api, id, "tamanhoPagina=100"));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {saiu}");
        var emailNovo = $"entrou.leitura.{Interlocked.Increment(ref _sequencia)}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(emailNovo, nivel: "RH");
        var entrou = (int)await ContarAsync(api, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{emailNovo}'");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AVISOS_VISUALIZACOES (ID_AVISO, ID_FUNCIONARIO, VISUALIZADO, DATA_VISUALIZACAO) VALUES ({id}, {entrou}, TRUE, '2026-10-06 11:00:00')");
        try
        {
            var depois = await CorpoAsync(await LeiturasAsync(api, id, "tamanhoPagina=100"));

            var ids = depois.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()).ToList();
            Assert.DoesNotContain(saiu, ids);
            Assert.Contains(entrou, ids);
            Assert.Equal((antes.GetProperty("total").GetInt32(), antes.GetProperty("viram").GetInt32() + 1), (depois.GetProperty("total").GetInt32(), depois.GetProperty("viram").GetInt32())); // saiu um (não lido) e entrou um (lido)
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE WHERE ID_FUNCIONARIO = {saiu}");
        }
    }

    [Fact]
    public async Task Quem_viu_so_vale_para_aviso_publicado_o_expirado_vale_e_o_rascunho_e_o_agendado_sao_422()
    {
        var api = await ApiAsync();
        var ativo = await SemearAsync(api, Titulo("Leituras ativo"), true, "2026-10-01 08:00:00");
        var expirado = await SemearAsync(api, Titulo("Leituras expirado"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendado = await SemearAsync(api, Titulo("Leituras agendado"), true, "2026-10-09 08:00:00");
        var rascunho = await SemearAsync(api, Titulo("Leituras rascunho"), false, null);

        var respostas = new[] { await LeiturasAsync(api, ativo), await LeiturasAsync(api, expirado), await LeiturasAsync(api, agendado), await LeiturasAsync(api, rascunho), await LeiturasAsync(api, int.MaxValue) };

        Assert.Equal([200, 200, 422, 422, 404], respostas.Select(r => (int)r.StatusCode));
        var problema = await CorpoAsync(respostas[2]);
        Assert.Equal(("AVISO_AINDA_NAO_PUBLICADO", "Este aviso ainda não foi publicado."), (Codigo(problema), problema.GetProperty("detail").GetString()));
    }

    [Theory]
    [InlineData("situacao=TODOS")]
    [InlineData("pagina=0")]
    [InlineData("tamanhoPagina=101")]
    public async Task Quem_viu_com_filtro_invalido_e_400(string consulta)
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Leituras 400"), true, "2026-10-01 08:00:00");

        Assert.Equal(400, (int)(await LeiturasAsync(api, id, consulta)).StatusCode);
    }

    // ---------------------------------------------------------------- lembrar quem não viu

    [Fact]
    public async Task Lembrar_enfileira_um_e_mail_por_pessoa_ativa_admitida_e_com_e_mail_que_ainda_nao_abriu_e_responde_202_com_a_quantidade()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Lembrar"), true, "2026-10-01 08:00:00");
        var e = api.Elenco;
        await LerAsync(api, id, e.MembroA.FuncionarioId, e.Gestor.FuncionarioId);
        var esperados = await NaoLidosEsperadosAsync(api, id);

        var resposta = await LembrarAsync(api, id);
        var corpo = await CorpoAsync(resposta);
        var lembrados = await LembradosAsync(api, id);

        Assert.Equal(202, (int)resposta.StatusCode);
        Assert.Equal(esperados.Count, corpo.GetProperty("quantidadeDeEmails").GetInt32());
        Assert.Equal(esperados, lembrados);
        Assert.DoesNotContain(e.MembroA.FuncionarioId, lembrados);
        Assert.DoesNotContain(e.Gestor.FuncionarioId, lembrados);
        Assert.DoesNotContain(e.Inativo.FuncionarioId, lembrados);
        Assert.DoesNotContain(e.EmAdmissao.FuncionarioId, lembrados);
        Assert.Equal($"AVISO_LEMBRETE:aviso={id}:func={lembrados[0]}:em=2026-10-07T12:42:00Z", (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'AVISO_LEMBRETE' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {lembrados[0]}"))!);
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "AVISOS", id));
        Assert.Equal((AuditoriaArea.Avisos, AuditoriaAcao.EnviouEmail), (evento.Area, evento.Acao));
        Assert.Contains($"{esperados.Count} pessoa", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_duplo_clique_no_mesmo_minuto_nao_manda_em_dobro_e_um_minuto_depois_manda_de_novo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duplo clique"), true, "2026-10-01 08:00:00");

        var primeira = await CorpoAsync(await LembrarAsync(api, id));
        var segunda = await CorpoAsync(await LembrarAsync(api, id));
        var depoisDeUmClique = (await LembradosAsync(api, id)).Count;
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddMinutes(2));
        var terceira = await CorpoAsync(await LembrarAsync(api, id));

        Assert.Equal(primeira.GetProperty("quantidadeDeEmails").GetInt32(), segunda.GetProperty("quantidadeDeEmails").GetInt32()); // o mesmo resultado
        Assert.Equal(primeira.GetProperty("quantidadeDeEmails").GetInt32(), depoisDeUmClique); // e nenhum e-mail a mais
        Assert.Equal(depoisDeUmClique * 2, (await LembradosAsync(api, id)).Count); // sem limite de lembretes: outro minuto, outro lote
        Assert.True(terceira.GetProperty("quantidadeDeEmails").GetInt32() > 0);
    }

    [Fact]
    public async Task Um_segundo_clique_20_segundos_depois_ainda_e_o_mesmo_minuto_e_nao_manda_nem_audita_de_novo()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Mesmo minuto"), true, "2026-10-01 08:00:00");

        await LembrarAsync(api, id);
        var depoisDoPrimeiro = (await LembradosAsync(api, id)).Count;
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddSeconds(20));
        await LembrarAsync(api, id);

        Assert.Equal(depoisDoPrimeiro, (await LembradosAsync(api, id)).Count);
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "AVISOS", id)); // o segundo clique não enfileirou nada: não há evento novo
    }

    [Fact]
    public async Task O_lembrete_nao_vai_para_quem_saiu_da_empresa_nem_para_quem_ficou_sem_e_mail()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Sem saiu nem sem e-mail"), true, "2026-10-01 08:00:00");
        var saiu = api.Elenco.MembroB.FuncionarioId;
        var semEmail = api.Elenco.DeOutroSetor.FuncionarioId;
        var emailOriginal = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {semEmail}"))!;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {saiu}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {semEmail}");
        try
        {
            var resposta = await LembrarAsync(api, id);

            var lembrados = await LembradosAsync(api, id);
            Assert.Equal(202, (int)resposta.StatusCode);
            Assert.DoesNotContain(saiu, lembrados);
            Assert.DoesNotContain(semEmail, lembrados);
            Assert.Contains(api.Elenco.MembroA.FuncionarioId, lembrados);
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE WHERE ID_FUNCIONARIO = {saiu}");
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = '{emailOriginal}' WHERE ID_FUNCIONARIO = {semEmail} AND EMAIL_CORPORATIVO IS NULL");
        }
    }

    [Fact]
    public async Task Cliques_em_paralelo_no_mesmo_minuto_enfileiram_um_lote_so()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Paralelo"), true, "2026-10-01 08:00:00");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cliente.PostAsJsonAsync($"{Rota}/{id}/lembretes", new { })));

        Assert.All(respostas, r => Assert.Equal(202, (int)r.StatusCode));
        Assert.Equal((await NaoLidosEsperadosAsync(api, id)).Count, (await LembradosAsync(api, id)).Count);
    }

    [Fact]
    public async Task Quando_todos_ja_viram_a_quantidade_e_zero_e_a_resposta_ainda_e_202()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Todos viram"), true, "2026-10-01 08:00:00");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS_VISUALIZACOES SET VISUALIZADO = TRUE, DATA_VISUALIZACAO = '2026-10-06 10:00:00' WHERE ID_AVISO = {id}");

        var resposta = await LembrarAsync(api, id);

        Assert.Equal(202, (int)resposta.StatusCode);
        Assert.Equal(0, (await CorpoAsync(resposta)).GetProperty("quantidadeDeEmails").GetInt32());
        Assert.Empty(await LembradosAsync(api, id));
    }

    [Fact]
    public async Task Lembrar_so_vale_para_aviso_ativo_o_resto_e_422_AVISO_NAO_ESTA_ATIVO_e_o_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var expirado = await SemearAsync(api, Titulo("Lembrar expirado"), true, "2026-09-01 08:00:00", "2026-09-30 23:59:59");
        var agendado = await SemearAsync(api, Titulo("Lembrar agendado"), true, "2026-10-09 08:00:00");
        var rascunho = await SemearAsync(api, Titulo("Lembrar rascunho"), false, null);

        var respostas = new[] { await LembrarAsync(api, expirado), await LembrarAsync(api, agendado), await LembrarAsync(api, rascunho), await LembrarAsync(api, int.MaxValue) };

        Assert.Equal([422, 422, 422, 404], respostas.Select(r => (int)r.StatusCode));
        var problema = await CorpoAsync(respostas[0]);
        Assert.Equal(("AVISO_NAO_ESTA_ATIVO", "Só é possível lembrar sobre um aviso ativo."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Empty(await LembradosAsync(api, expirado));
    }

    // ---------------------------------------------------------------- quem pode e contrato

    [Fact]
    public async Task Gestor_funcionario_e_sem_login_nao_veem_quem_leu_nem_mandam_lembrete()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Perfis"), true, "2026-10-01 08:00:00");

        foreach (var pessoa in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
            Assert.Equal(403, (int)(await cliente.GetAsync($"{Rota}/{id}/leituras")).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/lembretes", new { })).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.CreateClient().GetAsync($"{Rota}/{id}/leituras")).StatusCode);
        Assert.Equal(401, (int)(await api.Api.CreateClient().PostAsJsonAsync($"{Rota}/{id}/lembretes", new { })).StatusCode);
    }

    [Fact]
    public async Task As_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        var ativo = await SemearAsync(api, Titulo("Contrato leituras"), true, "2026-10-01 08:00:00");
        var rascunho = await SemearAsync(api, Titulo("Contrato rascunho"), false, null);
        await LerAsync(api, ativo, api.Elenco.MembroA.FuncionarioId);

        await (await LeiturasAsync(api, ativo, "situacao=NAO_LIDO&tamanhoPagina=5")).AfirmarConformeAoContratoAsync("listarLeiturasDoAviso");
        await (await LeiturasAsync(api, ativo, "situacao=TODOS")).AfirmarConformeAoContratoAsync("listarLeiturasDoAviso"); // 400
        await (await LeiturasAsync(api, int.MaxValue)).AfirmarConformeAoContratoAsync("listarLeiturasDoAviso"); // 404
        await (await LeiturasAsync(api, rascunho)).AfirmarConformeAoContratoAsync("listarLeiturasDoAviso"); // 422
        await (await LembrarAsync(api, ativo)).AfirmarConformeAoContratoAsync("lembrarQuemNaoViuAviso");
        await (await LembrarAsync(api, rascunho)).AfirmarConformeAoContratoAsync("lembrarQuemNaoViuAviso"); // 422
        await (await LembrarAsync(api, int.MaxValue)).AfirmarConformeAoContratoAsync("lembrarQuemNaoViuAviso"); // 404
    }
}

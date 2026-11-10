using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Gestores;

/// <summary>
/// <c>PUT /api/rh/gestores/{gestorId}/ausencia</c> ("Gestor ausente"), só para o RH, no banco REAL. O relógio parte de 2026-10-07T12:42Z e cada teste que o move o devolve a esse instante. A ordem de avaliação é a do contrato: 404, 400,
/// 422 (o plano do gestor diz "404, 400, 409, 422"; esta rota não tem 409). A porta <see cref="IPedidosParadosNoGestor"/> da 1a é o composto vazio, ou o <see cref="PedidosParadosEspiao"/> nos testes que olham a chamada.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AusenciaDoGestorTests(MySqlFixture mysql)
{
    private const string TextoSemEquipe = " só decide pedidos de gestores e não tem equipe. Não há etapa do gestor para aprovar sozinha.";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(AusenciaDoGestorTests));

    private static string Rota(int gestorId) => $"/api/rh/gestores/{gestorId}/ausencia";

    private static async Task<HttpResponseMessage> ComoRhAsync(ApiDeAcesso api, int gestorId, bool ausente, string? ausenteAte) =>
        await (await api.Api.ClienteComTokenNovoAsync(api.Elenco.Rh)).PutAsJsonAsync(Rota(gestorId), new { ausente, ausenteAte }); // token novo: alguns testes andam o relógio

    private static Task<HttpResponseMessage> ComEspiaoAsync(ApiDeAcesso api, WebApplicationFactory<Program> fabrica, int gestorId, bool ausente, string? ausenteAte) =>
        api.Api.ClienteComo(fabrica, api.Elenco.Rh).PutAsJsonAsync(Rota(gestorId), new { ausente, ausenteAte });

    private static async Task<JsonElement> ListarLinhaAsync(ApiDeAcesso api, int gestorId)
    {
        var itens = await ItensAsync(await (await api.Api.ClienteComTokenNovoAsync(api.Elenco.Rh)).GetAsync("/api/rh/gestores"));
        return Assert.Single(itens, i => i.GetProperty("gestorId").GetInt32() == gestorId);
    }

    private static async Task<(long Ausente, string? Ate)> NoBancoAsync(ApiDeAcesso api, int gestorId) =>
        (await Sql.ContarAsync(api.Conexao, $"SELECT CAST(APROVACAO_AUTOMATICA AS SIGNED) FROM GESTORES WHERE ID_GESTOR = {gestorId}"),
            await Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(APROVACAO_AUTOMATICA_ATE, '%Y-%m-%d') FROM GESTORES WHERE ID_GESTOR = {gestorId}") as string);

    // ---------------------------------------------------------------- marcar e desmarcar

    [Fact]
    public async Task Marcar_com_volta_futura_devolve_200_ausenteHoje_true_e_pedidosAprovadosAgora_zeros_com_a_porta_padrao_vazia()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Marcar1", equipe: 2);

        var resposta = await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-19");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((gestor.GestorId, "Marcar1 Gestor", "Marcar1 Setor", false, 2), (corpo.GetProperty("gestorId").GetInt32(), corpo.GetProperty("funcionario").GetProperty("nome").GetString(), corpo.GetProperty("setor").GetProperty("nome").GetString(), corpo.GetProperty("soDecidePedidosDeGestores").GetBoolean(), corpo.GetProperty("quantidadeEquipe").GetInt32()));
        Assert.Equal((true, "2026-10-19", true), (corpo.GetProperty("ausente").GetBoolean(), corpo.GetProperty("ausenteAte").GetString(), corpo.GetProperty("ausenteHoje").GetBoolean()));
        var aprovados = corpo.GetProperty("pedidosAprovadosAgora");
        Assert.Equal((0, 0), (aprovados.GetProperty("ajustesDePonto").GetInt32(), aprovados.GetProperty("ferias").GetInt32()));
        Assert.Equal((1L, "2026-10-19"), await NoBancoAsync(api, gestor.GestorId));
    }

    [Fact]
    public async Task Marcar_sem_data_vale_ate_desmarcar_e_desmarcar_devolve_ausente_false_data_null_e_zeros()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Marcar2");

        var marcada = await CorpoAsync(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: null));
        Assert.Equal((true, JsonValueKind.Null, true), (marcada.GetProperty("ausente").GetBoolean(), marcada.GetProperty("ausenteAte").ValueKind, marcada.GetProperty("ausenteHoje").GetBoolean()));

        var desmarcada = await CorpoAsync(await ComoRhAsync(api, gestor.GestorId, ausente: false, ausenteAte: null));

        Assert.Equal((false, JsonValueKind.Null, false), (desmarcada.GetProperty("ausente").GetBoolean(), desmarcada.GetProperty("ausenteAte").ValueKind, desmarcada.GetProperty("ausenteHoje").GetBoolean()));
        var aprovados = desmarcada.GetProperty("pedidosAprovadosAgora");
        Assert.Equal((0, 0), (aprovados.GetProperty("ajustesDePonto").GetInt32(), aprovados.GetProperty("ferias").GetInt32()));
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, gestor.GestorId));
    }

    [Fact]
    public async Task ausenteHoje_vale_ate_o_dia_anterior_a_volta_e_depois_continua_ausente_true_com_ausenteHoje_false()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Volta3");
        Assert.Equal(200, (int)(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-19")).StatusCode);

        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 18, 15, 0, 0, DateTimeKind.Utc)); // véspera da volta: ainda ausente
            var vespera = await ListarLinhaAsync(api, gestor.GestorId);
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 15, 0, 0, DateTimeKind.Utc)); // o dia da volta: já voltou
            var diaDaVolta = await ListarLinhaAsync(api, gestor.GestorId);
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 25, 15, 0, 0, DateTimeKind.Utc)); // depois: a API não desliga a marcação sozinha
            var depois = await ListarLinhaAsync(api, gestor.GestorId);

            Assert.Equal((true, true), (vespera.GetProperty("ausente").GetBoolean(), vespera.GetProperty("ausenteHoje").GetBoolean()));
            Assert.Equal((true, false), (diaDaVolta.GetProperty("ausente").GetBoolean(), diaDaVolta.GetProperty("ausenteHoje").GetBoolean()));
            Assert.Equal((true, false, "2026-10-19"), (depois.GetProperty("ausente").GetBoolean(), depois.GetProperty("ausenteHoje").GetBoolean(), depois.GetProperty("ausenteAte").GetString()));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task ausenteHoje_na_virada_do_dia_usa_o_dia_de_Brasilia()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Virada4");
        Assert.Equal(200, (int)(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-19")).StatusCode);

        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 2, 30, 0, DateTimeKind.Utc)); // 18/10 23:30 em -03:00: ainda é véspera, mesmo já sendo dia 19 em UTC
            var antesDaMeiaNoite = await ListarLinhaAsync(api, gestor.GestorId);
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 19, 3, 0, 0, DateTimeKind.Utc)); // 19/10 00:00 em -03:00: o dia da volta
            var naMeiaNoite = await ListarLinhaAsync(api, gestor.GestorId);

            Assert.True(antesDaMeiaNoite.GetProperty("ausenteHoje").GetBoolean());
            Assert.False(naMeiaNoite.GetProperty("ausenteHoje").GetBoolean());
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    // ---------------------------------------------------------------- 400

    [Fact]
    public async Task Marcar_com_volta_hoje_ou_no_passado_devolve_400_em_ausenteAte_e_nao_grava()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Passado5");

        foreach (var data in new[] { "2026-10-07", "2026-10-06", "2020-01-01" }) // hoje (2026-10-07) e antes
        {
            Assert.True((await ErrosAsync(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: data))).TryGetProperty("ausenteAte", out _));
        }

        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, gestor.GestorId));
    }

    [Fact]
    public async Task Volta_no_dia_seguinte_de_Brasilia_e_aceita_mesmo_quando_em_UTC_ja_e_esse_dia()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Fuso6");

        try
        {
            api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc)); // 07/10 23:30 em -03:00: hoje, para a API, ainda é 07/10
            Assert.Equal(200, (int)(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-08")).StatusCode);
            Assert.True((await ErrosAsync(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-07"))).TryGetProperty("ausenteAte", out _));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Desmarcar_com_data_devolve_400_em_ausenteAte_e_nao_grava()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Desmarcar7");
        Assert.Equal(200, (int)(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: null)).StatusCode);

        Assert.True((await ErrosAsync(await ComoRhAsync(api, gestor.GestorId, ausente: false, ausenteAte: "2026-12-01"))).TryGetProperty("ausenteAte", out _));

        Assert.Equal((1L, (string?)null), await NoBancoAsync(api, gestor.GestorId)); // continua marcado
    }

    [Fact]
    public async Task Corpo_sem_ausente_ou_sem_ausenteAte_devolve_400()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Corpo8");
        Assert.Equal(200, (int)(await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: null)).StatusCode);
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);

        foreach (var corpo in new[] { "{}", "{\"ausente\":false}", "{\"ausenteAte\":null}", "{\"ausente\":\"talvez\",\"ausenteAte\":null}" })
        {
            Assert.Equal(400, (int)(await cliente.PutAsync(Rota(gestor.GestorId), Json(corpo))).StatusCode);
        }

        Assert.Equal(400, (int)(await cliente.PutAsync(Rota(gestor.GestorId), Json(""))).StatusCode); // corpo JSON vazio
        Assert.Equal((1L, (string?)null), await NoBancoAsync(api, gestor.GestorId)); // "{\"ausente\":false}" não valeu como desmarcar
    }

    // ---------------------------------------------------------------- 422 e 404

    [Fact]
    public async Task Marcar_o_RH_cadastrado_sem_setor_devolve_422_GESTOR_SEM_EQUIPE_com_o_nome_no_detail_e_desmarcar_e_permitido()
    {
        var api = await ApiAsync();
        var rh = await GestoresDeTeste.RhCadastradoAsync(api, "SemEquipe9");
        var espiao = new PedidosParadosEspiao();
        var fabrica = api.Api.ComPedidosParados(espiao);

        var marcar = await ComEspiaoAsync(api, fabrica, rh.GestorId, ausente: true, ausenteAte: "2026-10-19");

        Assert.Equal((422, "GESTOR_SEM_EQUIPE", "SemEquipe9 RH" + TextoSemEquipe), await ProblemaAsync(marcar));
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, rh.GestorId));
        Assert.Empty(espiao.Chamadas); // recusou antes de chamar a porta

        // A ordem é a do contrato: 400 antes de 422 (data de volta no passado).
        Assert.True((await ErrosAsync(await ComEspiaoAsync(api, fabrica, rh.GestorId, ausente: true, ausenteAte: "2026-10-01"))).TryGetProperty("ausenteAte", out _));

        // Desmarcar é permitido: limpa uma marca antiga que ficou (de quando a pessoa ainda gerenciava um setor, por exemplo).
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE, APROVACAO_AUTOMATICA_ATE = '2026-10-19' WHERE ID_GESTOR = {rh.GestorId}");
        var desmarcar = await ComEspiaoAsync(api, fabrica, rh.GestorId, ausente: false, ausenteAte: null);
        Assert.Equal(200, (int)desmarcar.StatusCode);
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, rh.GestorId));
        Assert.Empty(espiao.Chamadas); // desmarcar nunca chama a porta
    }

    [Fact]
    public async Task Gestor_inexistente_sem_setor_com_nivel_Gestor_rebaixado_ou_inativo_devolvem_404()
    {
        var api = await ApiAsync();
        var semSetor = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, api.Elenco.GestorSemSetor.FuncionarioId); // nível Gestor, linha em GESTORES, nenhum setor
        var rebaixado = await GestoresDeTeste.ExGestorRebaixadoAsync(api, "Quatrocentos10");
        var inativo = await GestoresDeTeste.GestorInativoAsync(api, "Quatrocentos10");
        var gestorDeVerdade = await GestoresDeTeste.GestorDeSetorAsync(api, "Quatrocentos10 verdadeiro"); // o controle: a mesma chamada, para quem é gestor, dá 200 (sem ele o 404 de uma rota que não existe passaria)
        var espiao = new PedidosParadosEspiao();
        var fabrica = api.Api.ComPedidosParados(espiao);

        Assert.Equal(200, (int)(await ComEspiaoAsync(api, fabrica, gestorDeVerdade.GestorId, ausente: true, ausenteAte: "2026-10-19")).StatusCode);
        var chamadasDoControle = espiao.Chamadas.Count; // a do controle (1); as dos 404 não podem acrescentar nenhuma
        foreach (var gestorId in new[] { int.MaxValue, semSetor, rebaixado.GestorId, inativo.GestorId })
        {
            Assert.Equal(404, (int)(await ComEspiaoAsync(api, fabrica, gestorId, ausente: true, ausenteAte: "2026-10-19")).StatusCode);
        }

        // O 404 vem antes do 400: um corpo inválido para um gestor que não existe também é 404.
        Assert.Equal(404, (int)(await ComEspiaoAsync(api, fabrica, int.MaxValue, ausente: false, ausenteAte: "2026-12-01")).StatusCode);
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, semSetor));
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, inativo.GestorId));
        Assert.Equal(chamadasDoControle, espiao.Chamadas.Count);
    }

    // ---------------------------------------------------------------- a porta dos pedidos parados

    [Fact]
    public async Task Marcar_chama_AprovarEtapaDoGestorAsync_com_o_gestorId_na_mesma_transacao()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Porta11");
        var espiao = new PedidosParadosEspiao();
        (long Ausente, string? Ate)? vistoPorOutraConexao = null;
        espiao.AoChamar = async (_, _) => vistoPorOutraConexao = await NoBancoAsync(api, gestor.GestorId);

        var resposta = await ComEspiaoAsync(api, api.Api.ComPedidosParados(espiao), gestor.GestorId, ausente: true, ausenteAte: "2026-10-19");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(new[] { (PedidosParadosEspiao.Aprovar, gestor.GestorId) }, espiao.Chamadas.ToArray());
        Assert.Equal((0L, (string?)null), vistoPorOutraConexao); // no instante da chamada a marca ainda não estava confirmada: a chamada roda dentro da transação que a grava
        Assert.Equal((1L, "2026-10-19"), await NoBancoAsync(api, gestor.GestorId)); // e depois da resposta está
    }

    [Fact]
    public async Task Marcar_repete_na_resposta_o_que_a_porta_devolveu_com_numeros_diferentes_de_zero()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Repete12");
        var espiao = new PedidosParadosEspiao { Devolve = new PedidosAprovados(3, 2) };
        var fabrica = api.Api.ComPedidosParados(espiao);

        var primeira = (await CorpoAsync(await ComEspiaoAsync(api, fabrica, gestor.GestorId, ausente: true, ausenteAte: null))).GetProperty("pedidosAprovadosAgora");
        espiao.Devolve = new PedidosAprovados(0, 4); // os campos trocados dariam { 4, 0 }
        var segunda = (await CorpoAsync(await ComEspiaoAsync(api, fabrica, gestor.GestorId, ausente: true, ausenteAte: null))).GetProperty("pedidosAprovadosAgora");

        Assert.Equal((3, 2), (primeira.GetProperty("ajustesDePonto").GetInt32(), primeira.GetProperty("ferias").GetInt32()));
        Assert.Equal((0, 4), (segunda.GetProperty("ajustesDePonto").GetInt32(), segunda.GetProperty("ferias").GetInt32()));
    }

    [Fact]
    public async Task Marcar_de_novo_chama_a_porta_de_novo_e_desmarcar_nao_chama()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Novo13");
        var espiao = new PedidosParadosEspiao { Devolve = new PedidosAprovados(1, 1) };
        var fabrica = api.Api.ComPedidosParados(espiao);

        await ComEspiaoAsync(api, fabrica, gestor.GestorId, ausente: true, ausenteAte: null);
        await ComEspiaoAsync(api, fabrica, gestor.GestorId, ausente: true, ausenteAte: null); // idempotente, e chama a porta de novo
        var desmarcada = await CorpoAsync(await ComEspiaoAsync(api, fabrica, gestor.GestorId, ausente: false, ausenteAte: null));

        Assert.Equal(new[] { (PedidosParadosEspiao.Aprovar, gestor.GestorId), (PedidosParadosEspiao.Aprovar, gestor.GestorId) }, espiao.Chamadas.ToArray());
        var aprovados = desmarcada.GetProperty("pedidosAprovadosAgora"); // ao desmarcar não aprovou nada, mesmo com a porta configurada para devolver (1, 1)
        Assert.Equal((0, 0), (aprovados.GetProperty("ajustesDePonto").GetInt32(), aprovados.GetProperty("ferias").GetInt32()));
    }

    [Fact]
    public async Task Porta_que_lanca_desfaz_a_marcacao_e_devolve_500_sem_vazar_detalhe()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Lanca14");
        var espiao = new PedidosParadosEspiao { Lanca = new InvalidOperationException("detalhe-interno-da-porta-777") };
        var eventosAntes = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "GESTORES", gestor.GestorId)).Count;

        var resposta = await ComEspiaoAsync(api, api.Api.ComPedidosParados(espiao), gestor.GestorId, ausente: true, ausenteAte: "2026-10-19");

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain("detalhe-interno-da-porta-777", await resposta.Content.ReadAsStringAsync());
        Assert.Equal(new[] { (PedidosParadosEspiao.Aprovar, gestor.GestorId) }, espiao.Chamadas.ToArray()); // a porta foi chamada e lançou
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, gestor.GestorId)); // a marcação foi desfeita
        Assert.Equal(eventosAntes, (await AuditoriaDeTeste.EventosAsync(api.Conexao, "GESTORES", gestor.GestorId)).Count); // e nenhum evento de auditoria ficou
    }

    // ---------------------------------------------------------------- auditoria, perfis e contrato

    [Fact]
    public async Task Marcar_e_desmarcar_gravam_Alterou_na_area_Cadastros_com_Gestor_ausente_e_Volta_em_antes_e_depois_e_o_nome_da_pessoa()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Auditoria15");

        await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-19");
        await ComoRhAsync(api, gestor.GestorId, ausente: false, ausenteAte: null);

        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "GESTORES", gestor.GestorId);
        Assert.Equal(2, eventos.Count);
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Alterou, "Gestor Auditoria15 Gestor", api.Elenco.Rh.FuncionarioId), (e.Area, e.Acao, e.RegistroAfetado, e.FuncionarioAtorId)));
        Assert.Contains(eventos[0].Mudancas, m => m is { Campo: "Gestor ausente", ValorAntes: "Não", ValorDepois: "Sim" });
        Assert.Contains(eventos[0].Mudancas, m => m.Campo == "Volta em" && m.ValorAntes is null && m.ValorDepois == "19/10/2026");
        Assert.Contains(eventos[1].Mudancas, m => m is { Campo: "Gestor ausente", ValorAntes: "Sim", ValorDepois: "Não" });
        Assert.Contains(eventos[1].Mudancas, m => m.Campo == "Volta em" && m.ValorAntes == "19/10/2026" && m.ValorDepois is null);
    }

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Permissao16");
        var corpo = new { ausente = true, ausenteAte = (string?)null };

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).PutAsJsonAsync(Rota(gestor.GestorId), corpo)).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PutAsJsonAsync(Rota(gestor.GestorId), corpo)).StatusCode);
        Assert.Equal((0L, (string?)null), await NoBancoAsync(api, gestor.GestorId));
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, "Schema17", equipe: 1);
        var rh = await GestoresDeTeste.RhCadastradoAsync(api, "Schema17");

        // O status de cada resposta é conferido antes do schema: o 404 de uma rota que não existe também "bate" com o 404 declarado.
        var (ok, passado, inexistente, semEquipe) = (
            await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2026-10-19"),
            await ComoRhAsync(api, gestor.GestorId, ausente: true, ausenteAte: "2020-01-01"),
            await ComoRhAsync(api, int.MaxValue, ausente: true, ausenteAte: null),
            await ComoRhAsync(api, rh.GestorId, ausente: true, ausenteAte: null));
        Assert.Equal([200, 400, 404, 422], [(int)ok.StatusCode, (int)passado.StatusCode, (int)inexistente.StatusCode, (int)semEquipe.StatusCode]);

        await ok.AfirmarConformeAoContratoAsync("configurarAusenciaDoGestor");
        await passado.AfirmarConformeAoContratoAsync("configurarAusenciaDoGestor");
        await inexistente.AfirmarConformeAoContratoAsync("configurarAusenciaDoGestor");
        await semEquipe.AfirmarConformeAoContratoAsync("configurarAusenciaDoGestor");
    }
}

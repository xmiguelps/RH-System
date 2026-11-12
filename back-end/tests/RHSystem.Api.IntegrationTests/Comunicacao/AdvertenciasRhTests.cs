using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Comunicacao.AdvertenciasDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// As advertências pelo RH (<c>/api/rh/advertencias</c> e <c>/api/rh/funcionarios/{id}/advertencias</c>), no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 no horário local). Registrar nasce PENDENTE, com o gestor da pessoa ou sem gestor (RH), e manda o e-mail SÓ à pessoa; as
/// listas são de um ano pela data do ocorrido; o resumo conta o ano (as canceladas só na aba); responder à contestação é um UPDATE condicional em CONTESTADA, com o texto só no e-mail e na auditoria. Cada teste usa o seu setor, então as listas não dependem dos outros.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AdvertenciasRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/advertencias";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AdvertenciasRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpClient> RhAsync(ApiDeAcesso api) => await api.ClienteDoRhAgoraAsync();

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, string consulta)
    {
        var resposta = await (await RhAsync(api)).GetAsync($"{Rota}?{consulta}");
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static object Registro(string data = "2026-10-05", string tipo = "ESCRITA", string aplicadaPor = "GESTOR_DO_COLABORADOR", string motivo = "Atrasos repetidos", string descricao = "Cinco atrasos na abertura da loja.") =>
        new { dataDoOcorrido = data, tipo, aplicadaPor, motivo, descricao };

    private static async Task<HttpResponseMessage> RegistrarAsync(ApiDeAcesso api, int pessoa, object corpo) => await PostAsync(await RhAsync(api), $"/api/rh/funcionarios/{pessoa}/advertencias", corpo);

    // ---------------------------------------------------------------- registrar

    [Fact]
    public async Task Registrar_em_nome_do_gestor_nasce_PENDENTE_com_o_gestor_da_pessoa_e_manda_so_o_e_mail_dela()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome) = await NovoMembroAsync(api, setor);

        var resposta = await RegistrarAsync(api, pessoa, Registro());

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(
            ("2026-10-05", "ESCRITA", "Atrasos repetidos", "PENDENTE", "Cinco atrasos na abertura da loja.", nome, "GESTOR", setor.GestorNome),
            (corpo.GetProperty("data").GetString(), corpo.GetProperty("tipo").GetString(), corpo.GetProperty("motivo").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("funcionario").GetProperty("nome").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("tipo").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("nome").GetString()));
        foreach (var vazio in new[] { "dataCienciaUtc", "textoContestacao", "dataContestacaoUtc", "desfechoDaContestacao" })
        {
            Assert.Equal(JsonValueKind.Null, corpo.GetProperty(vazio).ValueKind);
        }

        Assert.Equal((setor.GestorId.ToString(), "PENDENTE", "2026-10-05"), (await TextoAsync(api, $"SELECT ID_GESTOR FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"), await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_ADVERTENCIA, '%Y-%m-%d') FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}")));
        // só a pessoa recebe: nem o gestor nem o RH (decisão do dono)
        Assert.Equal(1, EmailsDa(api, "ADVERTENCIA_REGISTRADA", id));
        Assert.Equal(pessoa, (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id}"));
        Assert.Equal($"ADVERTENCIA_REGISTRADA:advertencia={id}:func={pessoa}", await TextoAsync(api, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id}"));
    }

    [Fact]
    public async Task Registrar_em_nome_do_RH_deixa_o_gestor_nulo_e_a_lista_mostra_RH_sem_nome()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);

        var resposta = await RegistrarAsync(api, pessoa, Registro(aplicadaPor: "RH", tipo: "VERBAL"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("RH", JsonValueKind.Null), (corpo.GetProperty("aplicadaPor").GetProperty("tipo").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("nome").ValueKind));
        Assert.Null(await TextoAsync(api, $"SELECT ID_GESTOR FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {corpo.GetProperty("id").GetInt32()}"));
        Assert.Equal(1, EmailsDa(api, "ADVERTENCIA_REGISTRADA", corpo.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task A_data_de_hoje_vale_no_horario_do_Brasil_e_a_de_amanha_e_400()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc)); // 22:00 de 07/10 no Brasil

        var hoje = await RegistrarAsync(api, pessoa, Registro(data: "2026-10-07"));
        var amanha = await RegistrarAsync(api, pessoa, Registro(data: "2026-10-08"));

        Assert.Equal(201, (int)hoje.StatusCode);
        Assert.Equal(400, (int)amanha.StatusCode);
        Assert.True((await CorpoAsync(amanha)).GetProperty("errors").TryGetProperty("dataDoOcorrido", out _));
    }

    [Fact]
    public async Task Pessoa_sem_gestor_ou_com_gestor_desligado_e_422_ADVERTENCIA_SEM_GESTOR_mas_o_RH_registra_em_nome_do_RH()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (semGestor, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = NULL WHERE ID_FUNCIONARIO = {semGestor}");
        var (gestorDesligado, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {setor.GestorFuncionarioId}");

        var a = await RegistrarAsync(api, semGestor, Registro());
        var b = await RegistrarAsync(api, gestorDesligado, Registro());
        var comoRh = await RegistrarAsync(api, semGestor, Registro(aplicadaPor: "RH"));

        foreach (var resposta in new[] { a, b })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            var erro = await CorpoAsync(resposta);
            Assert.Equal(("ADVERTENCIA_SEM_GESTOR", "Esta pessoa não tem gestor ativo. Escolha \"RH\" em \"Aplicada por\"."), (erro.GetProperty("codigo").GetString(), erro.GetProperty("detail").GetString()));
        }

        Assert.Equal(201, (int)comoRh.StatusCode);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO IN ({semGestor}, {gestorDesligado}) AND ID_GESTOR IS NOT NULL"));
    }

    [Fact]
    public async Task Pessoa_inativa_e_422_FUNCIONARIO_INATIVO_com_o_texto_do_trigger_em_regraDoBanco()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (inativo, _) = await NovoMembroAsync(api, setor, ativo: false);

        var resposta = await RegistrarAsync(api, inativo, Registro());

        Assert.Equal(422, (int)resposta.StatusCode);
        var erro = await CorpoAsync(resposta);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Não é possível registrar advertência para uma pessoa inativa.", "Nao e possivel registrar advertencia para funcionario inativo."), (erro.GetProperty("codigo").GetString(), erro.GetProperty("detail").GetString(), erro.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO = {inativo}"));
    }

    [Fact]
    public async Task Pessoa_que_nao_existe_e_404_e_nada_e_gravado()
    {
        var api = await ApiAsync();

        var resposta = await RegistrarAsync(api, int.MaxValue, Registro());

        Assert.Equal(404, (int)resposta.StatusCode);
    }

    [Theory]
    [InlineData("dataDoOcorrido", "0001-01-01")]
    [InlineData("motivo", "")]
    [InlineData("motivo", "   ")]
    [InlineData("descricao", "")]
    public async Task Campo_invalido_do_registro_e_400_com_o_erro_no_campo(string campo, string valor)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var corpo = new Dictionary<string, object?> { ["dataDoOcorrido"] = "2026-10-05", ["tipo"] = "VERBAL", ["aplicadaPor"] = "RH", ["motivo"] = "Motivo", ["descricao"] = "Descrição" };
        corpo[campo] = valor;

        var resposta = await RegistrarAsync(api, pessoa, corpo);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Os_tamanhos_do_motivo_e_da_descricao_valem_ate_255_e_5000_caracteres_e_os_erros_vem_juntos()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);

        var noLimite = await RegistrarAsync(api, pessoa, Registro(motivo: new string('m', 255), descricao: new string('d', 5000)));
        var passou = await RegistrarAsync(api, pessoa, Registro(motivo: new string('m', 256), descricao: new string('d', 5001), data: "2026-10-08"));

        Assert.Equal(201, (int)noLimite.StatusCode);
        Assert.Equal(400, (int)passou.StatusCode);
        var erros = (await CorpoAsync(passou)).GetProperty("errors");
        Assert.True(erros.TryGetProperty("motivo", out _) && erros.TryGetProperty("descricao", out _) && erros.TryGetProperty("dataDoOcorrido", out _));
    }

    [Fact]
    public async Task O_evento_de_auditoria_do_registro_nao_leva_o_motivo_nem_a_descricao()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome) = await NovoMembroAsync(api, setor);

        var resposta = await RegistrarAsync(api, pessoa, Registro(motivo: "motivo-sigiloso-1", descricao: "descricao-sigilosa-1"));

        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
        Assert.Equal((AuditoriaArea.Advertencias, AuditoriaAcao.Criou, $"Registrou uma advertência escrita de 05/10/2026 para {nome}.", $"Advertência de {nome}", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.DoesNotContain("sigilos", evento.Descricao, StringComparison.Ordinal);
        Assert.Empty(evento.Mudancas);
    }

    [Fact]
    public async Task Pessoa_sem_e_mail_corporativo_tem_a_advertencia_registrada_e_nenhum_e_mail()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {pessoa}");

        var resposta = await RegistrarAsync(api, pessoa, Registro());

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(0, EmailsDa(api, "ADVERTENCIA_REGISTRADA", (await CorpoAsync(resposta)).GetProperty("id").GetInt32()));
    }

    // ---------------------------------------------------------------- a lista do RH

    [Fact]
    public async Task A_lista_e_de_um_ano_pela_data_do_ocorrido_do_mais_recente_para_o_mais_antigo_e_depois_o_mais_recente_registrado()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var antiga = await SemearAsync(api, a, "2031-03-01", gestorId: setor.GestorId);
        var meio1 = await SemearAsync(api, a, "2031-06-10");
        var meio2 = await SemearAsync(api, a, "2031-06-10");
        var recente = await SemearAsync(api, a, "2031-09-30");
        var deOutroAno = await SemearAsync(api, a, "2032-01-02");

        var de2031 = await ListarAsync(api, $"ano=2031&setorId={setor.SetorId}");
        var de2032 = await ListarAsync(api, $"ano=2032&setorId={setor.SetorId}");

        Assert.Equal([recente, meio2, meio1, antiga], Ids(de2031));
        Assert.Equal([deOutroAno], Ids(de2032));
        Assert.Equal((1, 20, 4, 1), (de2031.GetProperty("pagina").GetInt32(), de2031.GetProperty("tamanhoPagina").GetInt32(), de2031.GetProperty("total").GetInt32(), de2031.GetProperty("totalPaginas").GetInt32()));
    }

    [Fact]
    public async Task Sem_ano_vale_o_ano_atual_no_horario_do_Brasil()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var de2026 = await SemearAsync(api, a, "2026-02-01");
        var de2027 = await SemearAsync(api, a, "2027-02-01");

        var hoje = await ListarAsync(api, $"setorId={setor.SetorId}");
        api.Api.Relogio.Fixar(new DateTime(2027, 1, 1, 3, 0, 0, DateTimeKind.Utc)); // 00:00 de 01/01/2027 no Brasil
        var viradaDoAno = await ListarAsync(api, $"setorId={setor.SetorId}");

        Assert.Equal([de2026], Ids(hoje));
        Assert.Equal([de2027], Ids(viradaDoAno));
    }

    [Fact]
    public async Task Os_filtros_situacao_tipo_setor_e_busca_valem_juntos_e_a_busca_acha_o_nome_e_o_motivo()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var outroSetor = await NovoSetorAsync(api);
        var (ana, nomeAna) = await NovoMembroAsync(api, setor, prefixo: "Ana Busca");
        var (bia, _) = await NovoMembroAsync(api, setor, prefixo: "Bia Busca");
        var (carla, _) = await NovoMembroAsync(api, outroSetor);
        var pendenteVerbal = await SemearAsync(api, ana, "2033-05-01", tipo: "VERBAL", motivo: "motivo-zebra", situacao: "PENDENTE");
        var aplicadaEscrita = await SemearAsync(api, ana, "2033-05-02", tipo: "ESCRITA", situacao: "APLICADA", cienciaLocal: "2033-05-03 10:00:00");
        var canceladaBia = await SemearAsync(api, bia, "2033-05-03", tipo: "ESCRITA", situacao: "CANCELADA", textoContestacao: "contestei isto aqui", contestacaoLocal: "2033-05-04 10:00:00");
        var deOutroSetor = await SemearAsync(api, carla, "2033-05-04");

        Assert.Equal([canceladaBia, aplicadaEscrita, pendenteVerbal], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}")));
        Assert.Equal([deOutroSetor], Ids(await ListarAsync(api, $"ano=2033&setorId={outroSetor.SetorId}")));
        Assert.Equal([pendenteVerbal], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&situacao=PENDENTE")));
        Assert.Equal([canceladaBia], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&situacao=CANCELADA")));
        Assert.Equal([canceladaBia, aplicadaEscrita], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&tipo=ESCRITA")));
        Assert.Equal([aplicadaEscrita, pendenteVerbal], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&busca={Uri.EscapeDataString(nomeAna)}"))); // o nome do colaborador
        Assert.Equal([pendenteVerbal], Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&busca=zebra"))); // o motivo
        Assert.Empty(Ids(await ListarAsync(api, $"ano=2033&setorId={setor.SetorId}&busca=inexistente-xyz")));
    }

    [Fact]
    public async Task O_ano_vai_de_1_de_janeiro_a_31_de_dezembro_na_lista_no_resumo_e_no_indicador_e_a_virada_do_ano_e_a_do_Brasil()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var primeiro = await SemearAsync(api, pessoa, "2043-01-01");
        var ultimo = await SemearAsync(api, pessoa, "2043-12-31");
        await SemearAsync(api, pessoa, "2042-12-31");
        await SemearAsync(api, pessoa, "2044-01-01");
        var (indicada, _) = await NovoMembroAsync(api, setor);
        await SemearAsync(api, indicada, "2026-01-01");
        await SemearAsync(api, indicada, "2026-12-31");
        await SemearAsync(api, indicada, "2025-12-31");
        await SemearAsync(api, indicada, "2027-01-01");

        var lista = await ListarAsync(api, $"ano=2043&setorId={setor.SetorId}");
        var resumo = await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/resumo?ano=2043"));
        api.Api.Relogio.Fixar(new DateTime(2027, 1, 1, 2, 59, 0, DateTimeKind.Utc)); // 23:59 de 31/12/2026 no Brasil
        var indicador = await CorpoAsync(await (await RhAsync(api)).GetAsync($"/api/rh/funcionarios/{indicada}/indicadores"));
        var resumoSemAno = await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/resumo"));
        var listaSemAno = await ListarAsync(api, $"setorId={setor.SetorId}");

        Assert.Equal([ultimo, primeiro], Ids(lista));
        Assert.Equal(2, resumo.GetProperty("contagens").GetProperty("todas").GetInt32());
        Assert.Equal(2, indicador.GetProperty("advertenciasQuantidade").GetInt32());
        Assert.Equal(2026, resumoSemAno.GetProperty("ano").GetInt32());
        Assert.Equal(2, listaSemAno.GetProperty("total").GetInt32()); // as duas de 2026 da pessoa indicada: o ano ainda é 2026 no Brasil
    }

    [Fact]
    public async Task O_filtro_de_setor_e_o_setor_ATUAL_da_pessoa_e_cada_item_traz_o_resumo_da_pessoa_e_quem_aplicou()
    {
        var api = await ApiAsync();
        var antes = await NovoSetorAsync(api);
        var depois = await NovoSetorAsync(api);
        var (pessoa, nome) = await NovoMembroAsync(api, antes);
        var id = await SemearAsync(api, pessoa, "2034-02-02", tipo: "ESCRITA", gestorId: antes.GestorId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {depois.SetorId} WHERE ID_FUNCIONARIO = {pessoa}");

        var noSetorAntigo = await ListarAsync(api, $"ano=2034&setorId={antes.SetorId}");
        var noSetorAtual = await ListarAsync(api, $"ano=2034&setorId={depois.SetorId}");

        Assert.Empty(Ids(noSetorAntigo));
        var item = Assert.Single(noSetorAtual.GetProperty("itens").EnumerateArray());
        Assert.Equal(
            (id, "2034-02-02", nome, depois.SetorNome, true, "ESCRITA", "Atraso na abertura", "PENDENTE", "GESTOR", antes.GestorNome),
            (item.GetProperty("id").GetInt32(), item.GetProperty("data").GetString(), item.GetProperty("funcionario").GetProperty("nome").GetString(), item.GetProperty("funcionario").GetProperty("setor").GetString(), item.GetProperty("funcionario").GetProperty("ativo").GetBoolean(), item.GetProperty("tipo").GetString(), item.GetProperty("motivo").GetString(), item.GetProperty("situacao").GetString(), item.GetProperty("aplicadaPor").GetProperty("tipo").GetString(), item.GetProperty("aplicadaPor").GetProperty("nome").GetString()));
        Assert.Equal(["id", "data", "funcionario", "tipo", "motivo", "aplicadaPor", "situacao"], [.. item.EnumerateObject().Select(p => p.Name)]); // sem a descrição: a lista é curta
    }

    [Fact]
    public async Task A_lista_pagina()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var ids = new List<int>();
        foreach (var dia in new[] { "2035-01-01", "2035-01-02", "2035-01-03" })
        {
            ids.Add(await SemearAsync(api, a, dia));
        }

        var primeira = await ListarAsync(api, $"ano=2035&setorId={setor.SetorId}&tamanhoPagina=2");
        var segunda = await ListarAsync(api, $"ano=2035&setorId={setor.SetorId}&tamanhoPagina=2&pagina=2");

        Assert.Equal([ids[2], ids[1]], Ids(primeira));
        Assert.Equal([ids[0]], Ids(segunda));
        Assert.Equal((3, 2), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32()));
    }

    [Theory]
    [InlineData("ano=1999", "ano")]
    [InlineData("ano=2101", "ano")]
    [InlineData("situacao=ABC", "situacao")]
    [InlineData("tipo=ABC", "tipo")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    public async Task Parametro_invalido_da_lista_e_400_com_o_erro_no_campo(string consulta, string campo)
    {
        var api = await ApiAsync();

        var resposta = await (await RhAsync(api)).GetAsync($"{Rota}?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    // ---------------------------------------------------------------- o resumo

    [Fact]
    public async Task O_resumo_conta_o_ano_com_as_canceladas_so_na_aba_e_o_setor_com_mais_nao_canceladas_com_empate_alfabetico()
    {
        var api = await ApiAsync();
        var alfa = await NovoSetorAsync(api, "Aaa Setor");
        var beta = await NovoSetorAsync(api, "Bbb Setor");
        var (a1, _) = await NovoMembroAsync(api, alfa);
        var (b1, _) = await NovoMembroAsync(api, beta);
        var (b2, _) = await NovoMembroAsync(api, beta);
        await SemearAsync(api, a1, "2036-01-10", situacao: "PENDENTE");
        await SemearAsync(api, a1, "2036-02-10", situacao: "APLICADA", cienciaLocal: "2036-02-11 10:00:00");
        await SemearAsync(api, b1, "2036-03-10", situacao: "CONTESTADA", textoContestacao: "Contestei esta advertência.", contestacaoLocal: "2036-03-11 10:00:00");
        await SemearAsync(api, b2, "2036-04-10", situacao: "APLICADA", cienciaLocal: "2036-04-11 10:00:00");
        await SemearAsync(api, b2, "2036-05-10", situacao: "CANCELADA", textoContestacao: "Contestei outra.", contestacaoLocal: "2036-05-11 10:00:00");
        await SemearAsync(api, b2, "2036-06-10", situacao: "CANCELADA", textoContestacao: "Contestei mais uma.", contestacaoLocal: "2036-06-11 10:00:00");
        await SemearAsync(api, a1, "2037-01-10"); // outro ano: fora

        var resposta = await (await RhAsync(api)).GetAsync($"{Rota}/resumo?ano=2036");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var c = corpo.GetProperty("contagens");
        Assert.Equal(2036, corpo.GetProperty("ano").GetInt32());
        Assert.Equal((6, 1, 2, 1, 2), (c.GetProperty("todas").GetInt32(), c.GetProperty("pendentes").GetInt32(), c.GetProperty("aplicadas").GetInt32(), c.GetProperty("contestadas").GetInt32(), c.GetProperty("canceladas").GetInt32()));
        // o resumo é global (de todos os setores): a conta dos setores deste teste só vale se o ano 2036 é só dele, e é.
        var setor = corpo.GetProperty("setorComMaisRegistros");
        Assert.Equal((alfa.SetorNome, 2), (setor.GetProperty("setor").GetString(), setor.GetProperty("quantidade").GetInt32())); // Aaa (2) empata com Bbb (2 não canceladas): o primeiro em ordem alfabética
    }

    [Fact]
    public async Task O_resumo_de_um_ano_sem_advertencia_nao_tem_setor_e_o_da_pessoa_sem_setor_nao_conta_para_nenhum()
    {
        var api = await ApiAsync();
        var semSetor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, NomeUnico("Sem Setor Adv"), Perfil.Funcionario);
        await SemearAsync(api, semSetor, "2038-01-10");

        var corpo = await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/resumo?ano=2038"));
        var vazio = await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/resumo?ano=2039"));

        Assert.Equal(1, corpo.GetProperty("contagens").GetProperty("todas").GetInt32());
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("setorComMaisRegistros").ValueKind);
        Assert.Equal((0, 0, 0, 0, 0), (vazio.GetProperty("contagens").GetProperty("todas").GetInt32(), vazio.GetProperty("contagens").GetProperty("pendentes").GetInt32(), vazio.GetProperty("contagens").GetProperty("aplicadas").GetInt32(), vazio.GetProperty("contagens").GetProperty("contestadas").GetInt32(), vazio.GetProperty("contagens").GetProperty("canceladas").GetInt32()));
        Assert.Equal(JsonValueKind.Null, vazio.GetProperty("setorComMaisRegistros").ValueKind);
    }

    [Fact]
    public async Task O_resumo_sem_ano_e_do_ano_atual_e_ano_invalido_e_400()
    {
        var api = await ApiAsync();

        var atual = await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/resumo"));
        var invalido = await (await RhAsync(api)).GetAsync($"{Rota}/resumo?ano=1999");

        Assert.Equal(2026, atual.GetProperty("ano").GetInt32());
        Assert.Equal(400, (int)invalido.StatusCode);
        Assert.True((await CorpoAsync(invalido)).GetProperty("errors").TryGetProperty("ano", out _));
    }

    // ---------------------------------------------------------------- o detalhe

    [Fact]
    public async Task O_detalhe_traz_a_contestacao_e_o_desfecho_derivado_da_situacao()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var pendente = await SemearAsync(api, pessoa, "2040-01-01", descricao: "Descrição completa.");
        var contestada = await SemearAsync(api, pessoa, "2040-01-02", situacao: "CONTESTADA", textoContestacao: "Texto da contestação.", contestacaoLocal: "2040-01-03 10:00:00");
        var mantida = await SemearAsync(api, pessoa, "2040-01-03", situacao: "APLICADA", textoContestacao: "Contestei e perdi.", contestacaoLocal: "2040-01-04 10:00:00");
        var acatada = await SemearAsync(api, pessoa, "2040-01-04", situacao: "CANCELADA", textoContestacao: "Contestei e ganhei.", contestacaoLocal: "2040-01-05 10:00:00");
        var ciente = await SemearAsync(api, pessoa, "2040-01-05", situacao: "APLICADA", cienciaLocal: "2040-01-06 09:00:00");

        async Task<JsonElement> ObterAsync(int id) => await CorpoAsync(await (await RhAsync(api)).GetAsync($"{Rota}/{id}"));
        var d = await ObterAsync(pendente);

        Assert.Equal("Descrição completa.", d.GetProperty("descricao").GetString());
        Assert.Equal(JsonValueKind.Null, d.GetProperty("desfechoDaContestacao").ValueKind);
        Assert.Equal(JsonValueKind.Null, (await ObterAsync(contestada)).GetProperty("desfechoDaContestacao").ValueKind);
        Assert.Equal("MANTIDA", (await ObterAsync(mantida)).GetProperty("desfechoDaContestacao").GetString());
        Assert.Equal("ACATADA", (await ObterAsync(acatada)).GetProperty("desfechoDaContestacao").GetString());
        Assert.Equal(JsonValueKind.Null, (await ObterAsync(ciente)).GetProperty("desfechoDaContestacao").ValueKind); // ciente sem contestação: sem desfecho
        var c = await ObterAsync(contestada);
        Assert.Equal(("Texto da contestação.", "2040-01-03T13:00:00Z", "CONTESTADA"), (c.GetProperty("textoContestacao").GetString(), c.GetProperty("dataContestacaoUtc").GetString(), c.GetProperty("situacao").GetString()));
        Assert.Equal("2040-01-06T12:00:00Z", (await ObterAsync(ciente)).GetProperty("dataCienciaUtc").GetString());
        Assert.Equal(404, (int)(await (await RhAsync(api)).GetAsync($"{Rota}/{int.MaxValue}")).StatusCode);
    }

    // ---------------------------------------------------------------- responder à contestação

    private static async Task<HttpResponseMessage> ResponderAsync(ApiDeAcesso api, int id, object corpo) => await PostAsync(await RhAsync(api), $"{Rota}/{id}/resposta", corpo);

    private static async Task<int> ContestadaAsync(ApiDeAcesso api, int pessoa, string data = "2026-09-15") =>
        await SemearAsync(api, pessoa, data, situacao: "CONTESTADA", textoContestacao: "Eu estava de folga.", contestacaoLocal: "2026-09-17 10:05:00");

    [Fact]
    public async Task Manter_volta_a_APLICADA_com_o_desfecho_MANTIDA_e_nada_e_gravado_da_resposta_no_banco()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);

        var resposta = await ResponderAsync(api, id, new { decisao = "MANTER", resposta = "Mantemos: o registro do gestor confirma a falta." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("APLICADA", "MANTIDA", "Eu estava de folga."), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("desfechoDaContestacao").GetString(), corpo.GetProperty("textoContestacao").GetString()));
        Assert.Equal("APLICADA", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
        Assert.Equal((AuditoriaArea.Advertencias, AuditoriaAcao.Alterou, $"Respondeu à contestação da advertência de {nome}: mantida.", $"Advertência de {nome}"), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado));
        Assert.Equal(["situacao: CONTESTADA -> APLICADA", "resposta:  -> Mantemos: o registro do gestor confirma a falta."], [.. evento.Mudancas.Select(m => $"{m.Campo}: {m.ValorAntes} -> {m.ValorDepois}")]);
        Assert.Equal(1, EmailsDa(api, "ADVERTENCIA_RESPONDIDA", id));
        var carga = JsonSerializer.Deserialize<JsonElement>((await TextoAsync(api, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_RESPONDIDA' AND REFERENCIA_ID = {id}"))!);
        Assert.Equal(("Mantemos: o registro do gestor confirma a falta.", "MANTER", nome), (carga.GetProperty("resposta").GetString(), carga.GetProperty("decisao").GetString(), carga.GetProperty("nome").GetString()));
        Assert.Equal(pessoa, (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_RESPONDIDA' AND REFERENCIA_ID = {id}"));
    }

    [Fact]
    public async Task Acatar_cancela_a_advertencia_com_o_desfecho_ACATADA()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);

        var resposta = await ResponderAsync(api, id, new { decisao = "ACATAR", resposta = "Conferimos: cancelamos a advertência." });

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("CANCELADA", "ACATADA"), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("desfechoDaContestacao").GetString()));
        Assert.Equal("CANCELADA", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        var mudanca = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id)).Single().Mudancas[0];
        Assert.Equal(("situacao", "CONTESTADA", "CANCELADA"), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois));
    }

    [Theory]
    [InlineData("PENDENTE", null)]
    [InlineData("APLICADA", null)]
    [InlineData("CANCELADA", "texto")]
    public async Task Responder_a_advertencia_que_nao_esta_contestada_e_422_e_nada_muda(string situacao, string? texto)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-09-15", situacao: situacao, cienciaLocal: situacao == "APLICADA" ? "2026-09-16 10:00:00" : null, textoContestacao: texto, contestacaoLocal: texto is null ? null : "2026-09-16 10:00:00");

        var resposta = await ResponderAsync(api, id, new { decisao = "MANTER", resposta = "Resposta." });

        Assert.Equal(422, (int)resposta.StatusCode);
        var erro = await CorpoAsync(resposta);
        Assert.Equal(("ADVERTENCIA_NAO_ESTA_CONTESTADA", "Só uma advertência contestada pode receber resposta."), (erro.GetProperty("codigo").GetString(), erro.GetProperty("detail").GetString()));
        Assert.Equal(situacao, await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        Assert.Equal(0, EmailsDa(api, "ADVERTENCIA_RESPONDIDA", id));
        Assert.Empty(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
    }

    [Fact]
    public async Task Responder_duas_vezes_em_paralelo_tem_um_efeito_so_e_a_outra_e_422()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);
        var cliente = await RhAsync(api);

        var respostas = await Task.WhenAll(
            PostAsync(cliente, $"{Rota}/{id}/resposta", new { decisao = "MANTER", resposta = "Primeira." }),
            PostAsync(cliente, $"{Rota}/{id}/resposta", new { decisao = "ACATAR", resposta = "Segunda." }));

        Assert.Equal([200, 422], respostas.Select(r => (int)r.StatusCode).Order().ToArray());
        Assert.Equal(1, EmailsDa(api, "ADVERTENCIA_RESPONDIDA", id));
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
    }

    [Theory]
    [InlineData("TALVEZ", "Resposta.")]
    [InlineData("MANTER", "")]
    [InlineData("MANTER", "   ")]
    public async Task Resposta_com_campo_invalido_e_400_e_a_advertencia_continua_contestada(string decisao, string resposta)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);

        var retorno = await ResponderAsync(api, id, new { decisao, resposta });

        Assert.Equal(400, (int)retorno.StatusCode);
        Assert.Equal("CONTESTADA", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
    }

    [Fact]
    public async Task A_resposta_aceita_2000_caracteres_e_recusa_2001_e_a_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);

        var longa = await ResponderAsync(api, id, new { decisao = "MANTER", resposta = new string('r', 2001) });
        var noLimite = await ResponderAsync(api, id, new { decisao = "MANTER", resposta = new string('r', 2000) });
        var inexistente = await ResponderAsync(api, int.MaxValue, new { decisao = "MANTER", resposta = "Resposta." });

        Assert.Equal((400, 200, 404), ((int)longa.StatusCode, (int)noLimite.StatusCode, (int)inexistente.StatusCode));
    }

    // ---------------------------------------------------------------- a lista da pessoa (ficha) e o indicador

    [Fact]
    public async Task A_lista_da_ficha_traz_todos_os_anos_com_as_canceladas_para_pessoa_inativa_tambem_e_404_se_nao_existe()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var a = await SemearAsync(api, pessoa, "2041-03-01");
        var b = await SemearAsync(api, pessoa, "2042-03-01", situacao: "CANCELADA", textoContestacao: "Contestei.", contestacaoLocal: "2042-03-02 10:00:00");
        var c = await SemearAsync(api, pessoa, "2042-03-01");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {pessoa}");

        var resposta = await (await RhAsync(api)).GetAsync($"/api/rh/funcionarios/{pessoa}/advertencias");
        var inexistente = await (await RhAsync(api)).GetAsync($"/api/rh/funcionarios/{int.MaxValue}/advertencias");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([c, b, a], Ids(await CorpoAsync(resposta))); // data desc e, no empate, o mais recente registrado
        Assert.Equal(404, (int)inexistente.StatusCode);
        Assert.Equal(400, (int)(await (await RhAsync(api)).GetAsync($"/api/rh/funcionarios/{pessoa}/advertencias?pagina=0")).StatusCode);
    }

    [Fact]
    public async Task O_indicador_da_ficha_conta_so_as_nao_canceladas_do_ano_corrente()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        await SemearAsync(api, pessoa, "2026-01-10");
        await SemearAsync(api, pessoa, "2026-02-10", situacao: "APLICADA", cienciaLocal: "2026-02-11 10:00:00");
        await SemearAsync(api, pessoa, "2026-03-10", situacao: "CANCELADA", textoContestacao: "Contestei.", contestacaoLocal: "2026-03-11 10:00:00");
        await SemearAsync(api, pessoa, "2025-03-10");

        var corpo = await CorpoAsync(await (await RhAsync(api)).GetAsync($"/api/rh/funcionarios/{pessoa}/indicadores"));

        Assert.Equal(2, corpo.GetProperty("advertenciasQuantidade").GetInt32());
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task As_rotas_do_RH_so_atendem_o_RH()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await ContestadaAsync(api, pessoa);

        foreach (var (metodo, rota) in new[] { ("GET", Rota), ("GET", $"{Rota}/resumo"), ("GET", $"{Rota}/{id}"), ("POST", $"{Rota}/{id}/resposta"), ("GET", $"/api/rh/funcionarios/{pessoa}/advertencias"), ("POST", $"/api/rh/funcionarios/{pessoa}/advertencias") })
        {
            foreach (var quem in new[] { api.Elenco.MembroA, api.Elenco.Gestor })
            {
                var cliente = await api.ClienteAgoraAsync(quem.UsuarioId);
                var resposta = metodo == "GET" ? await cliente.GetAsync(rota) : await PostAsync(cliente, rota, Registro());
                Assert.Equal(403, (int)resposta.StatusCode);
            }

            var anonimo = api.Api.ClienteAnonimo();
            Assert.Equal(401, (int)(metodo == "GET" ? await anonimo.GetAsync(rota) : await PostAsync(anonimo, rota, Registro())).StatusCode);
        }

        Assert.Equal("CONTESTADA", await TextoAsync(api, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
    }

    [Fact]
    public async Task As_seis_rotas_respondem_como_o_contrato_manda()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var (semGestor, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = NULL WHERE ID_FUNCIONARIO = {semGestor}");
        var (inativo, _) = await NovoMembroAsync(api, setor, ativo: false);
        var contestada = await ContestadaAsync(api, pessoa, "2026-09-10");
        var pendente = await SemearAsync(api, pessoa, "2026-09-11");
        var rh = await RhAsync(api);

        await (await rh.GetAsync($"{Rota}?ano=2026&setorId={setor.SetorId}")).AfirmarConformeAoContratoAsync("listarAdvertencias");
        await (await rh.GetAsync($"{Rota}?ano=1999")).AfirmarConformeAoContratoAsync("listarAdvertencias"); // 400
        await (await rh.GetAsync($"{Rota}/resumo")).AfirmarConformeAoContratoAsync("obterResumoDasAdvertencias");
        await (await rh.GetAsync($"{Rota}/resumo?ano=2101")).AfirmarConformeAoContratoAsync("obterResumoDasAdvertencias"); // 400
        await (await rh.GetAsync($"{Rota}/{contestada}")).AfirmarConformeAoContratoAsync("obterAdvertencia");
        await (await rh.GetAsync($"{Rota}/{int.MaxValue}")).AfirmarConformeAoContratoAsync("obterAdvertencia"); // 404
        await (await ResponderAsync(api, contestada, new { decisao = "ACATAR", resposta = "Acatada." })).AfirmarConformeAoContratoAsync("responderContestacaoDeAdvertencia");
        await (await ResponderAsync(api, pendente, new { decisao = "ACATAR", resposta = "Acatada." })).AfirmarConformeAoContratoAsync("responderContestacaoDeAdvertencia"); // 422
        await (await ResponderAsync(api, pendente, new { decisao = "TALVEZ", resposta = "" })).AfirmarConformeAoContratoAsync("responderContestacaoDeAdvertencia"); // 400
        await (await rh.GetAsync($"/api/rh/funcionarios/{pessoa}/advertencias")).AfirmarConformeAoContratoAsync("listarAdvertenciasDoFuncionario");
        await (await rh.GetAsync($"/api/rh/funcionarios/{int.MaxValue}/advertencias")).AfirmarConformeAoContratoAsync("listarAdvertenciasDoFuncionario"); // 404
        await (await RegistrarAsync(api, pessoa, Registro())).AfirmarConformeAoContratoAsync("registrarAdvertencia");
        await (await RegistrarAsync(api, inativo, Registro())).AfirmarConformeAoContratoAsync("registrarAdvertencia"); // 422
        await (await RegistrarAsync(api, semGestor, Registro())).AfirmarConformeAoContratoAsync("registrarAdvertencia"); // 422
        await (await RegistrarAsync(api, pessoa, Registro(motivo: ""))).AfirmarConformeAoContratoAsync("registrarAdvertencia"); // 400
        await (await RegistrarAsync(api, int.MaxValue, Registro())).AfirmarConformeAoContratoAsync("registrarAdvertencia"); // 404
    }
}

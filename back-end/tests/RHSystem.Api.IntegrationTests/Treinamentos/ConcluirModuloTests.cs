using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>concluirModuloDeTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 local). Concluir o último módulo fecha o treinamento (<c>CONCLUIDO</c>, data do relógio, sem nota), o trigger cria a linha do certificado e a aplicação troca NA MESMA transação o código (UUID v1) por um UUID versão 4 e a data de emissão pela do relógio; os dois últimos módulos ao mesmo tempo dão um só <c>CONCLUIDO</c>
/// e um só certificado; repetir não escreve nada (salvo o caso quebrado de todos concluídos com a linha em andamento); a primeira conclusão em <c>INSCRITO</c> também inicia; o encerrado só fecha a porta de quem não tinha começado.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConcluirModuloTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento, int modulo) => $"/api/eu/treinamentos/{treinamento}/modulos/{modulo}/concluir";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ConcluirModuloTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome, Perfil perfil = Perfil.Funcionario)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), perfil, null, true, new DateOnly(2026, 1, 2));
        return (id, await api.ClienteAgoraAsync(await AdvertenciasDeTeste.ComLoginAsync(api, id)));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    private static Task<string?> LinhaDaPessoaAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CONCAT(STATUS_TREINAMENTO, '|', IFNULL(DATE_FORMAT(DATA_INICIO, '%Y-%m-%d'), '-'), '|', IFNULL(DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d'), '-'), '|', IFNULL(NOTA, '-')) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static Task<string?> CertificadoAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CONCAT(CODIGO_VALIDACAO, '|', IFNULL(DATE_FORMAT(DATA_EMISSAO, '%Y-%m-%d'), '-')) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static Task<int> EmAndamentoAsync(ApiDeAcesso api, string titulo, string situacao = "EM_ANDAMENTO", bool obrigatorio = true, bool emite = true) => SemearAsync(api, titulo, situacao, obrigatorio, "TODOS", emite);

    private static async Task<List<int>> ModulosAsync(ApiDeAcesso api, int treinamento, int quantos)
    {
        var ids = new List<int>();
        for (var i = 1; i <= quantos; i++)
        {
            ids.Add(await ModuloAsync(api, treinamento, i, "TEXTO", duracao: 10));
        }

        return ids;
    }

    [Fact]
    public async Task A_primeira_conclusao_em_INSCRITO_inicia_o_treinamento_e_grava_o_instante_do_relogio_sem_concluir_nada()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc1 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCnc1"));
        var m = await ModulosAsync(api, t, 3);
        await LinhaAsync(api, t, eu, "INSCRITO");

        var resposta = await cliente.PostAsync(Rota(t, m[0]), null);
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("concluirModuloDeTreinamento");
        Assert.Equal((m[0], false), (corpo.GetProperty("moduloId").GetInt32(), corpo.GetProperty("treinamentoConcluido").GetBoolean()));
        var cartao = corpo.GetProperty("treinamento");
        Assert.Equal(("EM_ANDAMENTO", "2026-10-07", 1, 33, 2, JsonValueKind.Null), (cartao.GetProperty("situacao").GetString(), cartao.GetProperty("iniciadoEm").GetString(), cartao.GetProperty("modulosConcluidos").GetInt32(), cartao.GetProperty("progressoPercentual").GetInt32(), cartao.GetProperty("moduloAtual").GetInt32(), cartao.GetProperty("certificado").ValueKind));
        Assert.Equal("2026-10-07T12:42:00Z", cartao.GetProperty("modulos")[0].GetProperty("concluidoEmUtc").GetString());
        Assert.Equal("2026-10-07 09:42:00", await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {m[0]} AND ID_FUNCIONARIO = {eu}")); // o banco guarda -03:00
        Assert.Equal("EM_ANDAMENTO|2026-10-07|-|-", await LinhaDaPessoaAsync(api, t, eu));
        Assert.Empty(await EventosAsync(api, t)); // concluir módulo não gera evento; só a conclusão do treinamento
    }

    [Fact]
    public async Task Concluir_o_ultimo_modulo_conclui_o_treinamento_e_troca_o_codigo_do_certificado_para_v4_com_a_data_do_relogio()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc2 Ana");
        var titulo = Titulo("ZCnc2");
        var t = await EmAndamentoAsync(api, titulo);
        var m = await ModulosAsync(api, t, 2);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        _ = await cliente.PostAsync(Rota(t, m[0]), null);

        var resposta = await cliente.PostAsync(Rota(t, m[1]), null);
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("concluirModuloDeTreinamento");
        Assert.True(corpo.GetProperty("treinamentoConcluido").GetBoolean());
        var cartao = corpo.GetProperty("treinamento");
        Assert.Equal(("CONCLUIDO", 2, 100, JsonValueKind.Null, "2026-10-05", "2026-10-07T12:42:00Z"), (cartao.GetProperty("situacao").GetString(), cartao.GetProperty("modulosConcluidos").GetInt32(), cartao.GetProperty("progressoPercentual").GetInt32(), cartao.GetProperty("moduloAtual").ValueKind, cartao.GetProperty("iniciadoEm").GetString(), cartao.GetProperty("concluidoEmUtc").GetString()));
        var certificado = cartao.GetProperty("certificado");
        var codigo = certificado.GetProperty("codigoValidacao").GetString();
        var certificadoId = await ContarAsync(api, $"SELECT ID_CERTIFICADO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}");
        Assert.Equal(($"CERT-2026-{certificadoId:00000}", "2026-10-07", true), (certificado.GetProperty("numero").GetString(), certificado.GetProperty("emitidoEm").GetString(), CodigoDoCertificado.EhVersao4(codigo)));
        Assert.Equal($"{codigo}|2026-10-07", await CertificadoAsync(api, t, eu)); // a data NÃO é a do relógio real do banco e o código NÃO é o v1 do trigger
        Assert.Equal("CONCLUIDO|2026-10-05|2026-10-07|-", await LinhaDaPessoaAsync(api, t, eu)); // sem nota
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}"));
        var evento = Assert.Single(await EventosAsync(api, t));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain(codigo!, evento.Descricao, StringComparison.Ordinal); // o código é segredo de quem o tem
    }

    [Fact]
    public async Task Treinamento_de_um_modulo_em_INSCRITO_inicia_e_conclui_na_mesma_chamada_com_a_mesma_data()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc3 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCnc3"));
        var m = await ModulosAsync(api, t, 1);
        await LinhaAsync(api, t, eu, "INSCRITO");

        var resposta = await cliente.PostAsync(Rota(t, m[0]), null);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("treinamentoConcluido").GetBoolean());
        Assert.Equal("CONCLUIDO|2026-10-07|2026-10-07|-", await LinhaDaPessoaAsync(api, t, eu));
    }

    [Fact]
    public async Task Treinamento_que_nao_emite_conclui_sem_certificado_na_resposta_e_a_linha_do_trigger_continua_escondida()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc4 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCnc4"), emite: false);
        var m = await ModulosAsync(api, t, 1);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");

        var resposta = await cliente.PostAsync(Rota(t, m[0]), null);
        var lista = await CorpoAsync(await cliente.GetAsync("/api/eu/treinamentos?aba=CONCLUIDOS"));

        Assert.True((await CorpoAsync(resposta)).GetProperty("treinamentoConcluido").GetBoolean());
        Assert.Equal(JsonValueKind.Null, (await CorpoAsync(resposta)).GetProperty("treinamento").GetProperty("certificado").ValueKind);
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}")); // o trigger criou a linha; a API a trata como inexistente
        Assert.Equal(JsonValueKind.Null, lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == t).GetProperty("certificado").ValueKind);
    }

    [Fact]
    public async Task Os_dois_ultimos_modulos_concluidos_ao_mesmo_tempo_deixam_um_CONCLUIDO_um_certificado_e_um_unico_treinamentoConcluido()
    {
        var api = await ApiAsync();
        for (var rodada = 1; rodada <= 3; rodada++)
        {
            var (eu, cliente) = await PessoaAsync(api, $"ZCnc5 Ana {rodada}");
            var t = await EmAndamentoAsync(api, Titulo($"ZCnc5 {rodada}"));
            var m = await ModulosAsync(api, t, 2);
            await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");

            var respostas = await Task.WhenAll(cliente.PostAsync(Rota(t, m[0]), null), cliente.PostAsync(Rota(t, m[1]), null), cliente.PostAsync(Rota(t, m[1]), null));

            Assert.Equal([200, 200, 200], respostas.Select(r => (int)r.StatusCode));
            var fecharam = new List<bool>();
            foreach (var r in respostas)
            {
                fecharam.Add((await CorpoAsync(r)).GetProperty("treinamentoConcluido").GetBoolean());
            }

            Assert.Equal(1, fecharam.Count(f => f)); // só uma das chamadas fechou o treinamento
            Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_FUNCIONARIO = {eu} AND ID_MODULO IN ({m[0]}, {m[1]})"));
            Assert.Equal("CONCLUIDO|2026-10-05|2026-10-07|-", await LinhaDaPessoaAsync(api, t, eu));
            Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}"));
            Assert.True(CodigoDoCertificado.EhVersao4((await CertificadoAsync(api, t, eu))![..36]));
            Assert.Single(await EventosAsync(api, t));
        }
    }

    [Fact]
    public async Task Repetir_um_modulo_concluido_nao_escreve_nada_nem_depois_de_o_treinamento_estar_concluido()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc6 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCnc6"));
        var m = await ModulosAsync(api, t, 2);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        _ = await cliente.PostAsync(Rota(t, m[0]), null);
        var instante = await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {m[0]} AND ID_FUNCIONARIO = {eu}");
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(2));
        var cliente2 = await api.ClienteAgoraAsync((int)await ContarAsync(api, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {eu}"));

        var repetido = await cliente2.PostAsync(Rota(t, m[0]), null);
        var ultimo = await cliente2.PostAsync(Rota(t, m[1]), null);
        var certificadoAntes = await CertificadoAsync(api, t, eu);
        var repetidoDepois = await cliente2.PostAsync(Rota(t, m[1]), null);
        var repetidoPrimeiro = await cliente2.PostAsync(Rota(t, m[0]), null);

        Assert.Equal([200, 200, 200, 200], new[] { repetido, ultimo, repetidoDepois, repetidoPrimeiro }.Select(r => (int)r.StatusCode));
        var fecharam = new List<bool>();
        foreach (var r in new[] { repetido, ultimo, repetidoDepois, repetidoPrimeiro })
        {
            fecharam.Add((await CorpoAsync(r)).GetProperty("treinamentoConcluido").GetBoolean());
        }

        Assert.Equal([false, true, false, false], fecharam);
        Assert.Equal(instante, await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {m[0]} AND ID_FUNCIONARIO = {eu}")); // o instante do primeiro clique não mudou
        Assert.Equal(certificadoAntes, await CertificadoAsync(api, t, eu)); // o certificado continua o mesmo
        Assert.Equal("CONCLUIDO", (await CorpoAsync(repetidoDepois)).GetProperty("treinamento").GetProperty("situacao").GetString());
        Assert.Single(await EventosAsync(api, t));
    }

    [Fact]
    public async Task A_corrida_que_deixou_todos_os_modulos_concluidos_e_a_linha_em_andamento_fecha_na_chamada_seguinte()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc7 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCnc7"));
        var m = await ModulosAsync(api, t, 2);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        await ProgressoAsync(api, m[0], eu, "2026-10-06 10:00:00");
        await ProgressoAsync(api, m[1], eu, "2026-10-06 11:00:00"); // todos concluídos, mas a linha ficou em EM_ANDAMENTO

        var resposta = await cliente.PostAsync(Rota(t, m[1]), null);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("treinamentoConcluido").GetBoolean());
        Assert.Equal("CONCLUIDO|2026-10-05|2026-10-07|-", await LinhaDaPessoaAsync(api, t, eu));
        Assert.True(CodigoDoCertificado.EhVersao4((await CertificadoAsync(api, t, eu))![..36]));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_FUNCIONARIO = {eu} AND ID_MODULO IN ({m[0]}, {m[1]})")); // nenhum progresso novo
    }

    [Fact]
    public async Task Sem_acesso_404_ou_422_modulo_de_outro_treinamento_404_e_o_encerrado_so_fecha_a_porta_de_quem_nao_tinha_comecado()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCnc8 Ana");
        var opcional = await EmAndamentoAsync(api, Titulo("ZCnc8 opcional"), obrigatorio: false);
        var moduloOpcional = (await ModulosAsync(api, opcional, 1))[0];
        var obrigatorioSemLinha = await EmAndamentoAsync(api, Titulo("ZCnc8 obrigatorio"));
        var moduloObrigatorio = (await ModulosAsync(api, obrigatorioSemLinha, 1))[0];
        var rascunho = await SemearAsync(api, Titulo("ZCnc8 rascunho"), obrigatorio: true);
        var moduloRascunho = (await ModulosAsync(api, rascunho, 1))[0];
        await LinhaAsync(api, rascunho, eu, "INSCRITO");
        var reprovado = await EmAndamentoAsync(api, Titulo("ZCnc8 reprovado"));
        var moduloReprovado = (await ModulosAsync(api, reprovado, 1))[0];
        await LinhaAsync(api, reprovado, eu, "REPROVADO");
        var encerradoInscrito = await EmAndamentoAsync(api, Titulo("ZCnc8 encerrado inscrito"), "ENCERRADO");
        var moduloEncerradoInscrito = (await ModulosAsync(api, encerradoInscrito, 1))[0];
        await LinhaAsync(api, encerradoInscrito, eu, "INSCRITO");
        var encerradoAndamento = await EmAndamentoAsync(api, Titulo("ZCnc8 encerrado andamento"), "ENCERRADO");
        var moduloEncerradoAndamento = (await ModulosAsync(api, encerradoAndamento, 1))[0];
        await LinhaAsync(api, encerradoAndamento, eu, "EM_ANDAMENTO", "2026-09-10");
        var normal = await EmAndamentoAsync(api, Titulo("ZCnc8 normal"));
        var moduloNormal = (await ModulosAsync(api, normal, 1))[0];
        await LinhaAsync(api, normal, eu, "INSCRITO");

        var respostas = new[]
        {
            await cliente.PostAsync(Rota(opcional, moduloOpcional), null), await cliente.PostAsync(Rota(obrigatorioSemLinha, moduloObrigatorio), null), await cliente.PostAsync(Rota(rascunho, moduloRascunho), null),
            await cliente.PostAsync(Rota(reprovado, moduloReprovado), null), await cliente.PostAsync(Rota(encerradoInscrito, moduloEncerradoInscrito), null), await cliente.PostAsync(Rota(normal, moduloObrigatorio), null),
            await cliente.PostAsync(Rota(normal, 987654), null), await cliente.PostAsync(Rota(987654, moduloNormal), null), await cliente.PostAsync(Rota(encerradoAndamento, moduloEncerradoAndamento), null),
        };

        Assert.Equal([422, 404, 404, 404, 422, 404, 404, 404, 200], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("concluirModuloDeTreinamento");
        await respostas[4].AfirmarConformeAoContratoAsync("concluirModuloDeTreinamento");
        Assert.Equal(["INSCRICAO_PENDENTE", "TREINAMENTO_ENCERRADO"], [Codigo(await CorpoAsync(respostas[0])), Codigo(await CorpoAsync(respostas[4]))]);
        Assert.True((await CorpoAsync(respostas[8])).GetProperty("treinamentoConcluido").GetBoolean()); // quem já estava em andamento conclui normalmente no encerrado
        Assert.Equal(("CONCLUIDO", true), ((await CorpoAsync(respostas[8])).GetProperty("treinamento").GetProperty("situacao").GetString(), (await CorpoAsync(respostas[8])).GetProperty("treinamento").GetProperty("encerrado").GetBoolean()));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_FUNCIONARIO = {eu} AND ID_MODULO IN ({moduloOpcional}, {moduloObrigatorio}, {moduloRascunho}, {moduloReprovado}, {moduloEncerradoInscrito})"));
        Assert.Equal("INSCRITO|-|-|-", await LinhaDaPessoaAsync(api, encerradoInscrito, eu));
    }

    [Fact]
    public async Task Concluir_junto_com_excluir_ou_encerrar_nunca_devolve_erro_cru_nem_deadlock()
    {
        var api = await ApiAsync();
        for (var rodada = 1; rodada <= 3; rodada++)
        {
            var (eu, cliente) = await PessoaAsync(api, $"ZCnc9 Ana {rodada}");
            var comExclusao = await EmAndamentoAsync(api, Titulo($"ZCnc9 excluir {rodada}"), emite: false);
            var mExclusao = await ModulosAsync(api, comExclusao, 2);
            await LinhaAsync(api, comExclusao, eu, "EM_ANDAMENTO", "2026-10-05");
            var comEncerramento = await EmAndamentoAsync(api, Titulo($"ZCnc9 encerrar {rodada}"), emite: false);
            var mEncerramento = await ModulosAsync(api, comEncerramento, 2);
            await LinhaAsync(api, comEncerramento, eu, "EM_ANDAMENTO", "2026-10-05");
            var rh = await api.ClienteDoRhAgoraAsync();

            var concluirEExcluir = await Task.WhenAll(cliente.PostAsync(Rota(comExclusao, mExclusao[0]), null), cliente.PostAsync(Rota(comExclusao, mExclusao[1]), null), rh.DeleteAsync($"/api/rh/treinamentos/{comExclusao}?confirmar=true"));
            var concluirEEncerrar = await Task.WhenAll(cliente.PostAsync(Rota(comEncerramento, mEncerramento[0]), null), cliente.PostAsync(Rota(comEncerramento, mEncerramento[1]), null), rh.PostAsync($"/api/rh/treinamentos/{comEncerramento}/encerrar?confirmar=true", null));

            Assert.All(concluirEExcluir.Concat(concluirEEncerrar), r => Assert.DoesNotContain((int)r.StatusCode, new[] { 500, 502, 503 }));
            Assert.Equal(204, (int)concluirEExcluir[2].StatusCode); // a exclusão sempre ganha ou espera; nunca sobra filho sem pai
            Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO IN ({mExclusao[0]}, {mExclusao[1]})"));
            Assert.Equal(200, (int)concluirEEncerrar[2].StatusCode);
        }
    }

    [Fact]
    public async Task Gestor_conclui_o_proprio_treinamento_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var (gestor, clienteDoGestor) = await PessoaAsync(api, "ZCnc10 Gestor", Perfil.Gestor);
        var t = await EmAndamentoAsync(api, Titulo("ZCnc10"));
        var m = await ModulosAsync(api, t, 1);
        await LinhaAsync(api, t, gestor, "INSCRITO");

        var comoGestor = await clienteDoGestor.PostAsync(Rota(t, m[0]), null);
        var semLogin = await api.Api.ClienteAnonimo().PostAsync(Rota(t, m[0]), null);

        Assert.Equal([200, 401], new[] { comoGestor, semLogin }.Select(r => (int)r.StatusCode));
        Assert.Equal("CONCLUIDO|2026-10-07|2026-10-07|-", await LinhaDaPessoaAsync(api, t, gestor));
    }
}

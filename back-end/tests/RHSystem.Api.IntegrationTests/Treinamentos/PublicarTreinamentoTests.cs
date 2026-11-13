using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>publicarTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. Publicar exige título, data de início, público (com ao menos um setor ou uma pessoa) e ao menos um módulo, com TODOS os erros juntos num 400; publicar de novo, inclusive o encerrado, é 422 <c>TREINAMENTO_JA_PUBLICADO</c>; o <c>UPDATE</c> é condicional ao estado,
/// então duas chamadas ao mesmo tempo dão um 200 e um 422. O treinamento obrigatório ganha uma linha <c>INSCRITO</c> para cada pessoa ATIVA com a admissão já ocorrida do público, numa transação, e o opcional não cria linha. Nenhum e-mail sai; o evento <c>PUBLICOU</c> leva a quantidade e nenhum nome de pessoa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PublicarTreinamentoTests(MySqlFixture mysql)
{
    private static string Rota(int id) => $"/api/rh/treinamentos/{id}/publicar";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PublicarTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> PublicarAsync(ApiDeAcesso api, int id) => await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota(id), null);

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, bool ativa = true, DateOnly? admissao = null) =>
        ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, setor, ativa, admissao ?? new DateOnly(2026, 1, 2));

    /// <summary>Um rascunho completo (com data, público TODOS e um módulo) direto no banco; os parâmetros mudam o que o teste quer.</summary>
    private static async Task<int> RascunhoProntoAsync(ApiDeAcesso api, string prefixo, string? comeca = "2026-10-07", bool obrigatorio = false, string? publico = "TODOS", bool comModulo = true)
    {
        var id = await SemearAsync(api, Titulo(prefixo), obrigatorio: obrigatorio, publico: publico);
        if (comeca is not null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '{comeca}' WHERE ID_TREINAMENTO = {id}");
        }

        if (comModulo)
        {
            await ModuloAsync(api, id, 1);
        }

        return id;
    }

    private static Task<long> LinhasDosParticipantesAsync(ApiDeAcesso api, int id) => ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id}");

    [Fact]
    public async Task Publicar_com_a_data_de_hoje_libera_na_hora_com_data_futura_agenda_e_com_data_passada_libera()
    {
        var api = await ApiAsync();
        var hoje = await RascunhoProntoAsync(api, "ZPub1 hoje", "2026-10-07");
        var futuro = await RascunhoProntoAsync(api, "ZPub1 futuro", "2026-10-20");
        var passado = await RascunhoProntoAsync(api, "ZPub1 passado", "2026-09-01");

        var rHoje = await PublicarAsync(api, hoje);
        var rFuturo = await PublicarAsync(api, futuro);
        var rPassado = await PublicarAsync(api, passado);

        Assert.Equal([200, 200, 200], new[] { rHoje, rFuturo, rPassado }.Select(r => (int)r.StatusCode));
        await rHoje.AfirmarConformeAoContratoAsync("publicarTreinamento");
        var corpo = await CorpoAsync(rHoje);
        Assert.Equal((hoje, true, "EM_ANDAMENTO", "2026-10-07"), (corpo.GetProperty("id").GetInt32(), corpo.GetProperty("publicado").GetBoolean(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("comecaEm").GetString()));
        Assert.Equal("AGENDADO", (await CorpoAsync(rFuturo)).GetProperty("situacao").GetString());
        Assert.Equal("EM_ANDAMENTO", (await CorpoAsync(rPassado)).GetProperty("situacao").GetString());
        Assert.Equal("1,1,1", await TextoAsync(api, $"SELECT GROUP_CONCAT(PUBLICADO ORDER BY ID_TREINAMENTO SEPARATOR ',') FROM TREINAMENTOS WHERE ID_TREINAMENTO IN ({hoje}, {futuro}, {passado})"));
    }

    [Fact]
    public async Task Publicar_sem_modulo_sem_data_e_sem_publico_devolve_400_com_todos_os_campos_juntos_e_nao_publica()
    {
        var api = await ApiAsync();
        var vazio = await RascunhoProntoAsync(api, "ZPub2 vazio", comeca: null, publico: null, comModulo: false);
        var semSetor = await RascunhoProntoAsync(api, "ZPub2 sem setor", publico: "SETORES");
        var semPessoa = await RascunhoProntoAsync(api, "ZPub2 sem pessoa", publico: "PESSOAS");
        var tituloCurto = await SemearAsync(api, "ab");
        await ModuloAsync(api, tituloCurto, 1);

        var rVazio = await PublicarAsync(api, vazio);
        var rSemSetor = await PublicarAsync(api, semSetor);
        var rSemPessoa = await PublicarAsync(api, semPessoa);
        var rTitulo = await PublicarAsync(api, tituloCurto);

        Assert.Equal([400, 400, 400, 400], new[] { rVazio, rSemSetor, rSemPessoa, rTitulo }.Select(r => (int)r.StatusCode));
        await rVazio.AfirmarConformeAoContratoAsync("publicarTreinamento");
        var erros = (await CorpoAsync(rVazio)).GetProperty("errors");
        Assert.Equal(["comecaEm", "modulos", "publico"], erros.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal(("Informe a data em que o acesso é liberado.", "Inclua pelo menos um módulo.", "Escolha para quem é o treinamento."), (erros.GetProperty("comecaEm")[0].GetString(), erros.GetProperty("modulos")[0].GetString(), erros.GetProperty("publico")[0].GetString()));
        Assert.Equal("Escolha pelo menos um setor.", (await CorpoAsync(rSemSetor)).GetProperty("errors").GetProperty("publico")[0].GetString());
        Assert.Equal("Escolha pelo menos uma pessoa.", (await CorpoAsync(rSemPessoa)).GetProperty("errors").GetProperty("publico")[0].GetString());
        Assert.Equal(["comecaEm", "titulo"], (await CorpoAsync(rTitulo)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // o título ruim vem junto com a data que falta
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS WHERE PUBLICADO = TRUE AND ID_TREINAMENTO IN ({vazio}, {semSetor}, {semPessoa}, {tituloCurto})"));
        Assert.Empty(await EventosAsync(api, vazio));
    }

    [Fact]
    public async Task Obrigatorio_para_um_setor_cria_so_as_linhas_dos_ativos_admitidos_do_setor()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPub3 setor"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPub3 outro"));
        var ana = await PessoaAsync(api, "ZPub3 Ana", setor);
        var bia = await PessoaAsync(api, "ZPub3 Bia", setor, admissao: new DateOnly(2026, 10, 7)); // admitida hoje conta
        _ = await PessoaAsync(api, "ZPub3 Caio inativo", setor, ativa: false);
        _ = await PessoaAsync(api, "ZPub3 Dani em admissão", setor, admissao: new DateOnly(2026, 10, 8));
        _ = await PessoaAsync(api, "ZPub3 Edu de outro setor", outroSetor);
        var id = await RascunhoProntoAsync(api, "ZPub3 trein", obrigatorio: true, publico: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({id}, {setor})");

        var resposta = await PublicarAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal($"{ana},{bia}", await TextoAsync(api, $"SELECT GROUP_CONCAT(ID_FUNCIONARIO ORDER BY ID_FUNCIONARIO SEPARATOR ',') FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id}"));
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id} AND STATUS_TREINAMENTO = 'INSCRITO' AND DATA_INICIO IS NULL AND DATA_CONCLUSAO IS NULL AND NOTA IS NULL"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {id}"));
    }

    [Fact]
    public async Task Obrigatorio_para_pessoas_e_para_todos_cria_as_linhas_dos_ativos_admitidos_e_pula_inativo_e_em_admissao()
    {
        var api = await ApiAsync();
        var ana = await PessoaAsync(api, "ZPub4 Ana");
        var inativa = await PessoaAsync(api, "ZPub4 Bia inativa", ativa: false);
        var futura = await PessoaAsync(api, "ZPub4 Caio em admissão", admissao: new DateOnly(2026, 10, 8));
        var pessoas = await RascunhoProntoAsync(api, "ZPub4 pessoas", obrigatorio: true, publico: "PESSOAS");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({pessoas}, {ana}), ({pessoas}, {inativa}), ({pessoas}, {futura})");
        var todos = await RascunhoProntoAsync(api, "ZPub4 todos", obrigatorio: true, publico: "TODOS");

        var rPessoas = await PublicarAsync(api, pessoas);
        var rTodos = await PublicarAsync(api, todos);

        Assert.Equal([200, 200], new[] { rPessoas, rTodos }.Select(r => (int)r.StatusCode));
        Assert.Equal($"{ana}", await TextoAsync(api, $"SELECT GROUP_CONCAT(ID_FUNCIONARIO) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {pessoas}"));
        var alcancadas = await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO <= '2026-10-07'");
        Assert.Equal(alcancadas, await LinhasDosParticipantesAsync(api, todos));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {todos} AND ID_FUNCIONARIO IN ({inativa}, {futura})"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {todos}")); // TODOS não guarda linha no público
    }

    [Fact]
    public async Task Opcional_nao_cria_linha_nenhuma()
    {
        var api = await ApiAsync();
        _ = await PessoaAsync(api, "ZPub5 Ana");
        var id = await RascunhoProntoAsync(api, "ZPub5 opcional", obrigatorio: false, publico: "TODOS");

        var resposta = await PublicarAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(0L, await LinhasDosParticipantesAsync(api, id));
    }

    [Fact]
    public async Task Publicar_grava_um_evento_com_o_titulo_e_a_quantidade_e_sem_nome_de_pessoa_e_nao_envia_email()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPub6 setor"));
        var nomeDaPessoa = Titulo("ZPub6 Fulana");
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nomeDaPessoa, Perfil.Funcionario, setor, true, new DateOnly(2026, 1, 2));
        var titulo = Titulo("ZPub6 trein");
        var id = await SemearAsync(api, titulo, obrigatorio: true, publico: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '2026-10-07' WHERE ID_TREINAMENTO = {id}");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({id}, {setor})");
        await ModuloAsync(api, id, 1);
        var emailsAntes = await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await PublicarAsync(api, id);

        Assert.Equal(200, (int)resposta.StatusCode);
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Publicou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.Contains("1 pessoa", evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain(nomeDaPessoa, evento.Descricao, StringComparison.Ordinal);
        Assert.Equal(emailsAntes, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS")); // publicar não avisa ninguém
    }

    [Fact]
    public async Task Publicar_de_novo_inclusive_o_encerrado_e_422_com_o_codigo_proprio_e_nao_mexe_em_nada()
    {
        var api = await ApiAsync();
        var andamento = await SemearAsync(api, Titulo("ZPub7 andamento"), "EM_ANDAMENTO");
        var encerrado = await SemearAsync(api, Titulo("ZPub7 encerrado"), "ENCERRADO");
        var agendado = await SemearAsync(api, Titulo("ZPub7 agendado"), "AGENDADO");

        var respostas = new[] { await PublicarAsync(api, andamento), await PublicarAsync(api, encerrado), await PublicarAsync(api, agendado) };

        Assert.Equal([422, 422, 422], respostas.Select(r => (int)r.StatusCode));
        await respostas[1].AfirmarConformeAoContratoAsync("publicarTreinamento");
        Assert.Equal(["TREINAMENTO_JA_PUBLICADO", "TREINAMENTO_JA_PUBLICADO", "TREINAMENTO_JA_PUBLICADO"], [Codigo(await CorpoAsync(respostas[0])), Codigo(await CorpoAsync(respostas[1])), Codigo(await CorpoAsync(respostas[2]))]);
        Assert.Equal("Este treinamento já foi publicado.", (await CorpoAsync(respostas[0])).GetProperty("detail").GetString());
        Assert.Equal("2026-10-01 00:00:00", await TextoAsync(api, $"SELECT DATE_FORMAT(ENCERRADO_EM, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS WHERE ID_TREINAMENTO = {encerrado}")); // o encerrado continua encerrado
        Assert.Empty(await EventosAsync(api, andamento));
    }

    [Fact]
    public async Task Publicar_duas_vezes_ao_mesmo_tempo_da_um_200_e_um_422_sem_linha_duplicada_e_com_um_so_evento()
    {
        var api = await ApiAsync();
        _ = await PessoaAsync(api, "ZPub8 Ana");
        var id = await RascunhoProntoAsync(api, "ZPub8 paralelo", obrigatorio: true, publico: "TODOS");
        var alcancadas = await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO <= '2026-10-07'");

        var respostas = await Task.WhenAll(PublicarAsync(api, id), PublicarAsync(api, id), PublicarAsync(api, id));

        Assert.Equal([200, 422, 422], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(alcancadas, await LinhasDosParticipantesAsync(api, id));
        Assert.Single(await EventosAsync(api, id));
    }

    [Fact]
    public async Task Publicar_junto_com_a_exclusao_do_ultimo_modulo_nunca_deixa_publicado_sem_modulo()
    {
        var api = await ApiAsync();
        var id = await RascunhoProntoAsync(api, "ZPub9 corrida", comModulo: false);
        var modulo = await ModuloAsync(api, id, 1);
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(cliente.PostAsync(Rota(id), null), cliente.DeleteAsync($"/api/rh/treinamentos/{id}/modulos/{modulo}"));

        Assert.DoesNotContain(500, respostas.Select(r => (int)r.StatusCode));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS t WHERE t.ID_TREINAMENTO = {id} AND t.PUBLICADO = TRUE AND NOT EXISTS (SELECT 1 FROM TREINAMENTOS_MODULOS m WHERE m.ID_TREINAMENTO = t.ID_TREINAMENTO)"));
    }

    [Fact]
    public async Task Publicar_inexistente_e_404_sem_login_401_e_gestor_ou_funcionario_403()
    {
        var api = await ApiAsync();
        var id = await RascunhoProntoAsync(api, "ZPub10 acesso");

        var inexistente = await PublicarAsync(api, 987654);
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).PostAsync(Rota(id), null);
        var membro = await api.Api.ClienteComo(api.Elenco.MembroA).PostAsync(Rota(id), null);
        var semLogin = await api.Api.ClienteAnonimo().PostAsync(Rota(id), null);

        Assert.Equal([404, 403, 403, 401], new[] { inexistente, gestor, membro, semLogin }.Select(r => (int)r.StatusCode));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id} AND PUBLICADO = TRUE"));
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os destinatários do formulário (<c>adicionarDestinatariosFormulario</c>) e a caixa "Enviar o formulário de admissão" do cadastro, que chama o MESMO efeito por dentro: só <c>alvo = PESSOAS</c> em rascunho, agendado ou publicado; pessoa já incluída conta em <c>jaEstavam</c>; pessoa inativa
/// é 422 e NINGUÉM entra; o anônimo publicado não recebe ninguém (409); avaliação não vale; nenhum e-mail sai. O cadastro confere a caixa ANTES de gravar (400 em <c>formularioAdmissaoId</c> se não existe, não é de admissão ou ainda não está publicado; 422 se encerrado ou por setores): erro = ninguém é cadastrado.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DestinatariosDoFormularioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";
    private static int _cpf = 700_000;
    private static int _email;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(DestinatariosDoFormularioTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> IncluirAsync(ApiDeAcesso api, int formularioId, params int[] pessoas) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{formularioId}/destinatarios", new { funcionarioIds = pessoas });

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, ativo: ativa);

    private static Task<long> DestinatariosAsync(ApiDeAcesso api, int formularioId) => ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {formularioId} AND ID_FUNCIONARIO IS NOT NULL");

    // ---------------------------------------------------------------- a rota

    [Fact]
    public async Task Incluir_em_formulario_publicado_por_pessoas_devolve_adicionados_e_jaEstavam_e_nao_manda_email()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Admissão de outubro"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var rafael = await PessoaAsync(api, "Rafael");
        var tiago = await PessoaAsync(api, "Tiago");
        var aline = await PessoaAsync(api, "Aline");
        await IncluirAsync(api, id, rafael);
        var emailsAntes = await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await IncluirAsync(api, id, rafael, tiago, aline);
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("adicionarDestinatariosFormulario");
        Assert.Equal((2, 1), (corpo.GetProperty("adicionados").GetInt32(), corpo.GetProperty("jaEstavam").GetInt32()));
        Assert.Equal(3L, await DestinatariosAsync(api, id));
        Assert.Equal(emailsAntes, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS")); // nenhum e-mail, nem o de "formulário atribuído"
        var repetida = await CorpoAsync(await IncluirAsync(api, id, rafael, tiago));
        Assert.Equal((0, 2), (repetida.GetProperty("adicionados").GetInt32(), repetida.GetProperty("jaEstavam").GetInt32()));
    }

    [Fact]
    public async Task Rascunho_e_agendado_por_pessoas_tambem_aceitam_e_o_mesmo_id_repetido_no_corpo_conta_uma_vez()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("Rascunho por pessoas"), alvo: "PESSOAS");
        var agendado = await SemearAsync(api, Titulo("Agendado por pessoas"), "AGENDADO", alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana");

        var a = await CorpoAsync(await IncluirAsync(api, rascunho, ana, ana, ana));
        var b = await CorpoAsync(await IncluirAsync(api, agendado, ana));

        Assert.Equal((1, 0, 1, 0), (a.GetProperty("adicionados").GetInt32(), a.GetProperty("jaEstavam").GetInt32(), b.GetProperty("adicionados").GetInt32(), b.GetProperty("jaEstavam").GetInt32()));
    }

    [Fact]
    public async Task Por_setores_por_link_sem_alvo_e_avaliacao_sao_422_FORMULARIO_NAO_E_POR_PESSOAS()
    {
        var api = await ApiAsync();
        var porSetores = await SemearAsync(api, Titulo("Por setores"), "PUBLICADO", alvo: "SETORES");
        var porLink = await SemearAsync(api, Titulo("Por link"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('d', 43));
        var semAlvo = await SemearAsync(api, Titulo("Sem alvo"));
        var avaliacao = await SemearAsync(api, Titulo("Avaliação"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var ana = await PessoaAsync(api, "Ana");

        var respostas = new[] { await IncluirAsync(api, porSetores, ana), await IncluirAsync(api, porLink, ana), await IncluirAsync(api, semAlvo, ana), await IncluirAsync(api, avaliacao, ana) };

        Assert.All(respostas, r => Assert.Equal(422, (int)r.StatusCode));
        foreach (var r in respostas)
        {
            Assert.Equal("FORMULARIO_NAO_E_POR_PESSOAS", Codigo(await CorpoAsync(r)));
        }

        Assert.Equal("Só dá para incluir pessoas em formulário feito para pessoas escolhidas.", (await CorpoAsync(respostas[0])).GetProperty("detail").GetString());
        await respostas[0].AfirmarConformeAoContratoAsync("adicionarDestinatariosFormulario");
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO IN ({porSetores}, {semAlvo}, {avaliacao}) AND ID_FUNCIONARIO IS NOT NULL"));
    }

    [Fact]
    public async Task Encerrado_e_422_FORMULARIO_ENCERRADO_inclusive_o_prazo_vencido_com_a_tarefa_parada()
    {
        var api = await ApiAsync();
        var encerrado = await SemearAsync(api, Titulo("Encerrado"), "ENCERRADO", alvo: "PESSOAS");
        var vencido = await SemearAsync(api, Titulo("Prazo vencido"), "PUBLICADO", alvo: "PESSOAS", prazo: "2026-10-06");
        var ana = await PessoaAsync(api, "Ana");

        var a = await IncluirAsync(api, encerrado, ana);
        var b = await IncluirAsync(api, vencido, ana);

        Assert.Equal((422, "FORMULARIO_ENCERRADO", 422, "FORMULARIO_ENCERRADO"), ((int)a.StatusCode, Codigo(await CorpoAsync(a)), (int)b.StatusCode, Codigo(await CorpoAsync(b))));
    }

    [Fact]
    public async Task O_anonimo_publicado_nao_recebe_ninguem_409_o_mesmo_com_e_sem_resposta_e_o_anonimo_em_rascunho_ainda_recebe()
    {
        var api = await ApiAsync();
        var semResposta = await SemearAsync(api, Titulo("Anônimo sem resposta"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var comResposta = await PublicadoRespondidoAsync(api, Titulo("Anônimo com resposta"), anonimo: true);
        var rascunho = await SemearAsync(api, Titulo("Anônimo em rascunho"), anonimo: true, alvo: "PESSOAS");
        var agendado = await SemearAsync(api, Titulo("Anônimo agendado"), "AGENDADO", anonimo: true, alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana");

        var a = await IncluirAsync(api, semResposta, ana);
        var b = await IncluirAsync(api, comResposta, ana);
        var c = await IncluirAsync(api, rascunho, ana);
        var d = await IncluirAsync(api, agendado, ana);

        Assert.Equal((409, "ANONIMO_SEM_NOVOS_DESTINATARIOS", 409, "ANONIMO_SEM_NOVOS_DESTINATARIOS"), ((int)a.StatusCode, Codigo(await CorpoAsync(a)), (int)b.StatusCode, Codigo(await CorpoAsync(b))));
        Assert.Equal("Um formulário anônimo publicado não recebe pessoas novas.", (await CorpoAsync(a)).GetProperty("detail").GetString());
        Assert.Equal((200, 200), ((int)c.StatusCode, (int)d.StatusCode));
        Assert.Equal(0L, await DestinatariosAsync(api, semResposta));
    }

    [Fact]
    public async Task Pessoa_inativa_entre_as_escolhidas_e_422_FUNCIONARIO_INATIVO_e_nenhuma_entra()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Com inativa"), "PUBLICADO", alvo: "PESSOAS");
        var ativa1 = await PessoaAsync(api, "Ativa 1");
        var ativa2 = await PessoaAsync(api, "Ativa 2");
        var inativa = await PessoaAsync(api, "Inativa", ativa: false);

        var resposta = await IncluirAsync(api, id, ativa1, inativa, ativa2);
        var problema = await CorpoAsync(resposta);

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Uma das pessoas escolhidas está inativa e não pode receber o formulário."), ((int)resposta.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(0L, await DestinatariosAsync(api, id));
    }

    [Fact]
    public async Task A_lista_vazia_a_de_mais_de_200_o_numero_invalido_e_a_pessoa_que_nao_existe_sao_400()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Corpo inválido"), "PUBLICADO", alvo: "PESSOAS");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var vazia = await IncluirAsync(api, id);
        var demais = await IncluirAsync(api, id, [.. Enumerable.Range(1, 201)]);
        var zero = await IncluirAsync(api, id, 0);
        var inexistente = await IncluirAsync(api, id, 2147483000);
        var semCampo = await cliente.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { });

        Assert.All(new[] { vazia, demais, zero, inexistente, semCampo }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.Equal(["Escolha de 1 a 200 pessoas."], (await CorpoAsync(vazia)).GetProperty("errors").GetProperty("funcionarioIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(["Uma das pessoas escolhidas não existe."], (await CorpoAsync(inexistente)).GetProperty("errors").GetProperty("funcionarioIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(0L, await DestinatariosAsync(api, id));
        await vazia.AfirmarConformeAoContratoAsync("adicionarDestinatariosFormulario");
    }

    [Fact]
    public async Task Duas_abas_incluindo_a_mesma_pessoa_ao_mesmo_tempo_contam_uma_so_vez_e_nunca_500()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas abas"), "PUBLICADO", alvo: "PESSOAS");
        var pessoas = new[] { await PessoaAsync(api, "P1"), await PessoaAsync(api, "P2"), await PessoaAsync(api, "P3") };
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => cliente.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = pessoas })));

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode));
        var adicionados = 0;
        foreach (var r in respostas)
        {
            adicionados += (await CorpoAsync(r)).GetProperty("adicionados").GetInt32();
        }

        Assert.Equal((3, 3L), (adicionados, await DestinatariosAsync(api, id))); // cada pessoa entra uma vez, em uma só das abas
    }

    [Fact]
    public async Task Incluir_grava_o_evento_ALTEROU_so_quando_entrou_alguem_e_sem_os_nomes()
    {
        var api = await ApiAsync();
        var titulo = Titulo("Auditoria dos destinatários");
        var id = await SemearAsync(api, titulo, anonimo: true, alvo: "PESSOAS");
        var ana = await PessoaAsync(api, "Ana Auditada");

        await IncluirAsync(api, id, ana);
        await IncluirAsync(api, id, ana); // a segunda não inclui ninguém

        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Incluiu pessoas no formulário \"{titulo}\"."), (evento.Area, evento.Acao, evento.Descricao));
    }

    [Fact]
    public async Task Formulario_inexistente_e_404_e_quem_nao_e_do_RH_recebe_403()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Só o RH"), "PUBLICADO", alvo: "PESSOAS");
        var comum = await PessoaAsync(api, "Comum");
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, comum);

        Assert.Equal(404, (int)(await IncluirAsync(api, 2147483000, comum)).StatusCode);
        Assert.Equal(403, (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = new[] { comum } })).StatusCode);
    }

    // ---------------------------------------------------------------- a caixa "Enviar o formulário de admissão" do cadastro

    private static object NovoColaborador(int cargo, int setor, int? formulario) => new Dictionary<string, object?>
    {
        ["nome"] = "Rafael Torres",
        ["cpf"] = CpfsDeTeste.Valido(Interlocked.Increment(ref _cpf)),
        ["dataNascimento"] = "1995-02-09",
        ["telefone"] = "11991234567",
        ["emailCorporativo"] = $"admissao.{Interlocked.Increment(ref _email)}@admissao.com.br",
        ["dataAdmissao"] = "2026-10-05",
        ["tipoContrato"] = "CLT",
        ["cargoId"] = cargo,
        ["setorId"] = setor,
        ["perfil"] = "Funcionario",
        ["formularioAdmissaoId"] = formulario,
    };

    private static async Task<(int Cargo, int Setor)> CargoESetorAsync(ApiDeAcesso api) =>
        (await SementeDoCadastro.CargoAsync(api.Conexao, Titulo("Cargo admissão"), 44m), await SementeDoCadastro.SetorAsync(api.Conexao, Titulo("Setor admissão"), null));

    [Fact]
    public async Task Cadastrar_com_o_formulario_de_admissao_publicado_inclui_a_pessoa_nova_como_destinatario_sem_email_do_formulario()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api);
        var admissao = await SemearAsync(api, Titulo("Admissão de outubro"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO LIKE '%FORMULARIO%'");

        var resposta = await api.Api.ClienteComo(api.Elenco.Rh).PostAsJsonAsync("/api/rh/funcionarios", NovoColaborador(cargo, setor, admissao));
        var ficha = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {admissao} AND ID_FUNCIONARIO = {ficha.GetProperty("id").GetInt32()}"));
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO LIKE '%FORMULARIO%'"));
    }

    [Fact]
    public async Task A_caixa_recusa_antes_de_gravar_inexistente_nao_admissao_e_nao_publicado_400_e_encerrado_ou_por_setores_422_e_ninguem_e_cadastrado()
    {
        var api = await ApiAsync();
        var (cargo, setor) = await CargoESetorAsync(api);
        var pesquisa = await SemearAsync(api, Titulo("Pesquisa"), "PUBLICADO", tipo: "PESQUISA", alvo: "PESSOAS");
        var rascunho = await SemearAsync(api, Titulo("Admissão rascunho"), tipo: "ADMISSAO", alvo: "PESSOAS");
        var agendado = await SemearAsync(api, Titulo("Admissão agendada"), "AGENDADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var porSetores = await SemearAsync(api, Titulo("Admissão por setores"), "PUBLICADO", tipo: "ADMISSAO", alvo: "SETORES");
        var encerrada = await SemearAsync(api, Titulo("Admissão encerrada"), "ENCERRADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var anonima = await SemearAsync(api, Titulo("Admissão anônima"), "PUBLICADO", anonimo: true, tipo: "ADMISSAO", alvo: "PESSOAS");
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS");

        var respostas = new Dictionary<string, HttpResponseMessage>();
        foreach (var (nome, formulario) in new[] { ("inexistente", 2147483000), ("pesquisa", pesquisa), ("rascunho", rascunho), ("agendada", agendado), ("porSetores", porSetores), ("encerrada", encerrada), ("anonima", anonima) })
        {
            respostas[nome] = await cliente.PostAsJsonAsync("/api/rh/funcionarios", NovoColaborador(cargo, setor, formulario));
        }

        foreach (var nome in new[] { "inexistente", "pesquisa", "rascunho", "agendada", "anonima" })
        {
            Assert.Equal(400, (int)respostas[nome].StatusCode);
            Assert.True((await CorpoAsync(respostas[nome])).GetProperty("errors").TryGetProperty("formularioAdmissaoId", out _), nome);
        }

        var setores = await CorpoAsync(respostas["porSetores"]);
        var encerrado = await CorpoAsync(respostas["encerrada"]);
        Assert.Equal((422, "FORMULARIO_NAO_E_POR_PESSOAS", 422, "FORMULARIO_ENCERRADO"), ((int)respostas["porSetores"].StatusCode, Codigo(setores), (int)respostas["encerrada"].StatusCode, Codigo(encerrado)));
        Assert.Equal("O formulário de admissão escolhido já foi encerrado. Escolha outro.", encerrado.GetProperty("detail").GetString());
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS")); // nenhum cadastro saiu
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO IN ({pesquisa}, {rascunho}, {agendado}, {porSetores}, {encerrada}, {anonima}) AND ID_FUNCIONARIO IS NOT NULL"));
    }
}

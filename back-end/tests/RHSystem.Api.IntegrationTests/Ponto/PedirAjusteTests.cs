using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>POST/GET /api/eu/ponto/pedidos-de-ajuste</c>, no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). A equipe de Carlos Ribeiro (Comercial) é Diego Farias e Júlia Nunes; Bruno Lima é do Financeiro (gestora Marina Costa); Paulo Vieira é gestor sem
/// setor. Cada teste usa o dia e a pessoa dele (os pedidos pendentes são um por dia e por pessoa).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PedirAjusteTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/ponto/pedidos-de-ajuste";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PedirAjusteTests));
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Corpo(string data, string justificativa, params object[] itens) => new { data, justificativa, itens };

    private static object Entrada(string utc, int? marcacaoId = null) => new { tipo = "ENTRADA", instanteUtc = utc, marcacaoId };

    private static object Saida(string utc, int? marcacaoId = null) => new { tipo = "SAIDA", instanteUtc = utc, marcacaoId };

    private static async Task<HttpResponseMessage> PedirAsync(ApiDeAcesso api, PessoaDeTeste pessoa, object corpo) =>
        await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Rota, corpo);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static long Contar(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql).GetAwaiter().GetResult();

    private const string Justificativa = "Esqueci de registrar quando voltei do almoço. Voltei às 13:05 e saí às 17:50.";

    // ---------------------------------------------------------------- criar: as etapas do contrato

    [Fact]
    public async Task Colaboradora_de_setor_com_gestor_cria_o_pedido_na_etapa_do_gestor_com_o_periodo_do_contrato()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;

        var resposta = await PedirAsync(api, julia, Corpo("2026-10-05", Justificativa, Entrada("2026-10-05T16:05:00Z"), Saida("2026-10-05T20:50:00Z")));

        Assert.Equal(201, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("PENDENTE", "GESTOR", "2026-10-05", "2026-10-07T12:42:00Z"), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString(), pedido.GetProperty("data").GetString(), pedido.GetProperty("criadoEmUtc").GetString()));
        Assert.Equal((api.Elenco.MembroB.FuncionarioId, "Júlia Nunes"), (pedido.GetProperty("funcionario").GetProperty("id").GetInt32(), pedido.GetProperty("funcionario").GetProperty("nome").GetString()));
        Assert.Equal((api.Elenco.Gestor.FuncionarioId, "Carlos Ribeiro"), (pedido.GetProperty("gestorResponsavel").GetProperty("id").GetInt32(), pedido.GetProperty("gestorResponsavel").GetProperty("nome").GetString())); // o id é o funcionarioId do gestor
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (pedido.GetProperty("decisaoGestor").ValueKind, pedido.GetProperty("decisaoRh").ValueKind));
        Assert.Equal("Incluir o período 13:05 - 17:50", pedido.GetProperty("resumoDaCorrecao").GetString());
        Assert.Equal(["ENTRADA", "SAIDA"], pedido.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("tipo").GetString()!));
        Assert.Equal((false, "Aguardando a análise do gestor."), (pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").GetString())); // quem pede não decide
        Assert.Equal(0, pedido.GetProperty("periodosDoDia").GetArrayLength()); // nada foi aplicado às batidas
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {julia.FuncionarioId}"));
    }

    [Fact]
    public async Task Quem_e_gestor_ou_esta_sem_gestor_ativo_cria_o_pedido_direto_na_etapa_do_RH_sem_gestor_responsavel()
    {
        var api = await ApiAsync();
        await PontoDeTeste.BaterEmAsync(api.Conexao, api.Elenco.Gestor.FuncionarioId, new DateOnly(2026, 10, 1), (TipoDeMarcacao.Entrada, "2026-10-01T11:00:00Z"));

        var doGestor = await PedirAsync(api, api.Elenco.Gestor, Corpo("2026-10-01", "Esqueci de registrar a saída no fim do dia. Saí às 17:48.", Saida("2026-10-01T20:48:00Z")));
        var semSetor = await PedirAsync(api, api.Elenco.GestorSemSetor, Corpo("2026-10-01", "Esqueci de registrar o dia todo. Entrei às 08:00 e saí às 17:00.", Entrada("2026-10-01T11:00:00Z"), Saida("2026-10-01T20:00:00Z")));

        foreach (var resposta in new[] { doGestor, semSetor })
        {
            Assert.Equal(201, (int)resposta.StatusCode);
            var pedido = await CorpoAsync(resposta);
            Assert.Equal(("RH", JsonValueKind.Null, JsonValueKind.Null), (pedido.GetProperty("etapa").GetString(), pedido.GetProperty("gestorResponsavel").ValueKind, pedido.GetProperty("decisaoGestor").ValueKind));
            Assert.Equal((false, "Aguardando a análise do RH."), (pedido.GetProperty("podeDecidir").GetBoolean(), pedido.GetProperty("motivoNaoPodeDecidir").GetString()));
        }

        Assert.Equal(new[] { "Incluir a saída às 17:48" }, new[] { (await CorpoAsync(doGestor)).GetProperty("resumoDaCorrecao").GetString() });
    }

    [Fact]
    public async Task Gestor_ausente_faz_o_pedido_nascer_com_a_etapa_do_gestor_aprovada_sozinha_e_na_etapa_do_RH()
    {
        var api = await ApiAsync();
        var marina = api.Elenco.GestorDoFinanceiro;
        var bruno = api.Elenco.DeOutroSetor;
        var gestorId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {marina.FuncionarioId}");
        await PontoDeTeste.BaterEmAsync(api.Conexao, bruno.FuncionarioId, new DateOnly(2026, 10, 1), (TipoDeMarcacao.Entrada, "2026-10-01T11:00:00Z"));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE, APROVACAO_AUTOMATICA_ATE = NULL WHERE ID_GESTOR = {gestorId}");
        try
        {
            var resposta = await PedirAsync(api, bruno, Corpo("2026-10-01", "Esqueci de registrar a saída no fim do dia. Saí às 17:52.", Saida("2026-10-01T20:52:00Z")));

            Assert.Equal(201, (int)resposta.StatusCode);
            var pedido = await CorpoAsync(resposta);
            Assert.Equal(("PENDENTE", "RH"), (pedido.GetProperty("status").GetString(), pedido.GetProperty("etapa").GetString()));
            Assert.Equal((marina.FuncionarioId, "Marina Costa"), (pedido.GetProperty("gestorResponsavel").GetProperty("id").GetInt32(), pedido.GetProperty("gestorResponsavel").GetProperty("nome").GetString()));
            var decisao = pedido.GetProperty("decisaoGestor");
            Assert.Equal(("APROVADA", JsonValueKind.Null, true, "2026-10-07T12:42:00Z", JsonValueKind.Null), (decisao.GetProperty("decisao").GetString(), decisao.GetProperty("decididaPor").ValueKind, decisao.GetProperty("automatica").GetBoolean(), decisao.GetProperty("decididaEmUtc").GetString(), decisao.GetProperty("observacao").ValueKind));
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = FALSE, APROVACAO_AUTOMATICA_ATE = NULL WHERE ID_GESTOR = {gestorId}");
        }
    }

    // ---------------------------------------------------------------- criar: as recusas

    [Fact]
    public async Task Cada_recusa_422_tem_o_codigo_e_o_texto_do_contrato_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var antes = Contar(api, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES");

        var diaNaoPassou = await PedirAsync(api, pessoa, Corpo("2026-10-07", Justificativa, Saida("2026-10-07T11:00:00Z")));
        var amanha = await PedirAsync(api, pessoa, Corpo("2026-10-08", Justificativa, Saida("2026-10-08T11:00:00Z")));
        var noFuturo = await PedirAsync(api, pessoa, Corpo("2026-10-06", Justificativa, Saida("2026-10-07T12:43:00Z"))); // um minuto depois de agora
        var foraDoDia = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa, Saida("2026-10-04T03:00:00Z"))); // 00:00 do dia 04 em Brasília: dois dias depois
        var antesDoDia = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa, Entrada("2026-10-02T02:59:00Z"))); // 23:59 do dia 01
        var sequencia = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa, Entrada("2026-10-02T11:00:00Z"))); // uma entrada sem saída

        (HttpResponseMessage Resposta, string Codigo, string Detalhe)[] esperados =
        [
            (diaNaoPassou, "PONTO_PEDIDO_DIA_NAO_PASSOU", "Só dá para pedir ajuste de um dia que já passou. Para hoje, use o cartão \"Seu ponto hoje\"."),
            (amanha, "PONTO_PEDIDO_DIA_NAO_PASSOU", "Só dá para pedir ajuste de um dia que já passou. Para hoje, use o cartão \"Seu ponto hoje\"."),
            (noFuturo, "PONTO_INSTANTE_NO_FUTURO", "Uma das batidas pedidas tem horário que ainda não chegou. Confira os horários."),
            (foraDoDia, "PONTO_INSTANTE_FORA_DO_DIA", "Cada batida precisa cair no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite)."),
            (antesDoDia, "PONTO_INSTANTE_FORA_DO_DIA", "Cada batida precisa cair no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite)."),
            (sequencia, "PONTO_SEQUENCIA_INVALIDA", "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários."),
        ];
        foreach (var (resposta, codigo, detalhe) in esperados)
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal((codigo, detalhe), (Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        Assert.Equal(antes, Contar(api, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
    }

    [Fact]
    public async Task A_batida_as_00h10_do_dia_seguinte_conta_como_do_dia_do_pedido_jornada_que_atravessa_a_meia_noite()
    {
        var api = await ApiAsync();

        var resposta = await PedirAsync(api, api.Elenco.MembroB, Corpo("2026-09-30", "Trabalhei até depois da meia-noite no fechamento do mês.", Entrada("2026-09-30T22:00:00Z"), Saida("2026-10-01T03:10:00Z")));

        Assert.Equal(201, (int)resposta.StatusCode);
    }

    [Fact]
    public async Task Pessoa_inativa_da_422_FUNCIONARIO_INATIVO_com_regraDoBanco_e_antes_da_admissao_da_422_sem_codigo()
    {
        var api = await ApiAsync();

        var inativo = await PedirAsync(api, api.Elenco.Inativo, Corpo("2026-09-01", Justificativa, Entrada("2026-09-01T11:00:00Z"), Saida("2026-09-01T20:00:00Z")));
        var antesDaAdmissao = await PedirAsync(api, api.Elenco.MembroA, Corpo("2025-12-31", Justificativa, Entrada("2025-12-31T11:00:00Z"), Saida("2025-12-31T20:00:00Z"))); // admitido em 2026-01-02

        Assert.Equal(422, (int)inativo.StatusCode);
        var problema = await CorpoAsync(inativo);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Este colaborador está inativo e não pode pedir ajuste de ponto.", "Nao e possivel registrar ponto de funcionario inativo."), (Codigo(problema), problema.GetProperty("detail").GetString(), problema.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(422, (int)antesDaAdmissao.StatusCode);
        var semCodigo = await CorpoAsync(antesDaAdmissao);
        Assert.Equal(("(sem código)", "A data do ponto e anterior a admissao."), (Codigo(semCodigo), semCodigo.GetProperty("regraDoBanco").GetString()));
    }

    [Fact]
    public async Task Segundo_pedido_do_mesmo_dia_e_409_PONTO_PEDIDO_JA_EM_ANALISE_e_o_duplo_envio_em_paralelo_grava_um_so()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.DeOutroSetor;
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
        var portao = new TaskCompletionSource();

        var tentativas = Enumerable.Range(0, 3).Select(async _ =>
        {
            await portao.Task;
            return await cliente.PostAsJsonAsync(Rota, Corpo("2026-10-02", Justificativa, Entrada("2026-10-02T11:00:00Z"), Saida("2026-10-02T20:00:00Z")));
        }).ToList();
        portao.SetResult();
        var respostas = await Task.WhenAll(tentativas);
        var depois = await cliente.PostAsJsonAsync(Rota, Corpo("2026-10-02", Justificativa, Entrada("2026-10-02T11:10:00Z"), Saida("2026-10-02T20:10:00Z")));

        Assert.Equal([201, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        foreach (var recusada in respostas.Append(depois).Where(r => (int)r.StatusCode == 409))
        {
            var problema = await CorpoAsync(recusada);
            Assert.Equal(("PONTO_PEDIDO_JA_EM_ANALISE", "Este dia já tem um pedido de ajuste em análise. Espere a resposta antes de pedir outro."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM PONTOS_MANUTENCOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {pessoa.FuncionarioId} AND p.DATA = '2026-10-02'"));
    }

    // ---------------------------------------------------------------- criar: corrigir batida existente e validar o corpo

    [Fact]
    public async Task Corrigir_uma_batida_existente_do_dia_e_corrigir_o_tipo_para_o_horario_e_a_batida_de_outro_dia_e_400()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorDoFinanceiro;
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, new DateOnly(2026, 10, 1), (TipoDeMarcacao.Entrada, "2026-10-01T11:10:00Z"), (TipoDeMarcacao.Saida, "2026-10-01T20:00:00Z"));
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, new DateOnly(2026, 10, 2), (TipoDeMarcacao.Entrada, "2026-10-02T11:00:00Z"), (TipoDeMarcacao.Saida, "2026-10-02T20:00:00Z"));
        var entradaDe1 = (int)await Sql.ContarAsync(api.Conexao, $"SELECT m.ID_MARCACAO FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO WHERE p.ID_FUNCIONARIO = {pessoa.FuncionarioId} AND p.DATA = '2026-10-01' AND m.TIPO_MARCACAO = 'ENTRADA'");

        var corrigindo = await PedirAsync(api, pessoa, Corpo("2026-10-01", "A entrada saiu atrasada, cheguei às 08:00.", Entrada("2026-10-01T11:00:00Z", entradaDe1)));
        var deOutroDia = await PedirAsync(api, pessoa, Corpo("2026-10-02", "A entrada saiu atrasada, cheguei às 08:00.", Entrada("2026-10-02T11:00:00Z", entradaDe1)));

        Assert.Equal(201, (int)corrigindo.StatusCode);
        var pedido = await CorpoAsync(corrigindo);
        Assert.Equal("Corrigir a entrada para 08:00", pedido.GetProperty("resumoDaCorrecao").GetString());
        Assert.Equal(entradaDe1, pedido.GetProperty("itens")[0].GetProperty("marcacaoId").GetInt32());
        Assert.Equal(2, pedido.GetProperty("periodosDoDia")[0].EnumerateObject().Count()); // o "registro atual" do dia continua com as batidas originais
        Assert.Equal(400, (int)deOutroDia.StatusCode);
        Assert.True((await CorpoAsync(deOutroDia)).GetProperty("errors").TryGetProperty("itens", out _));
    }

    [Fact]
    public async Task Corpo_invalido_e_400_com_errors_por_campo_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.Rh;

        var justificativaCurta = await PedirAsync(api, pessoa, Corpo("2026-10-02", "Foi.", Entrada("2026-10-02T11:00:00Z"), Saida("2026-10-02T20:00:00Z")));
        var justificativaLonga = await PedirAsync(api, pessoa, Corpo("2026-10-02", new string('x', 501), Entrada("2026-10-02T11:00:00Z"), Saida("2026-10-02T20:00:00Z")));
        var semItens = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa));
        var itensDemais = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa, Enumerable.Range(0, 11).Select(i => Entrada($"2026-10-02T1{i % 10}:00:00Z")).ToArray()));
        var horarioSemFuso = await PedirAsync(api, pessoa, Corpo("2026-10-02", Justificativa, Entrada("2026-10-02T11:00:00"), Saida("2026-10-02T20:00:00Z")));
        var semData = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(Rota, new { justificativa = Justificativa, itens = new[] { Entrada("2026-10-02T11:00:00Z") } });
        var semCorpo = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsync(Rota, content: null);

        Assert.All(new[] { justificativaCurta, justificativaLonga, semItens, itensDemais, horarioSemFuso, semData, semCorpo }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.True((await CorpoAsync(justificativaCurta)).GetProperty("errors").TryGetProperty("justificativa", out _));
        Assert.True((await CorpoAsync(justificativaLonga)).GetProperty("errors").TryGetProperty("justificativa", out _));
        Assert.True((await CorpoAsync(semItens)).GetProperty("errors").TryGetProperty("itens", out _));
        Assert.True((await CorpoAsync(itensDemais)).GetProperty("errors").TryGetProperty("itens", out _));
        Assert.True((await CorpoAsync(horarioSemFuso)).GetProperty("errors").TryGetProperty("corpo", out _)); // o conversor do JSON recusa o instante sem Z nem deslocamento
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM PONTOS_MANUTENCOES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    [Fact]
    public async Task Horario_com_fuso_em_vez_de_Z_vale_pelo_instante_e_a_justificativa_e_aparada()
    {
        var api = await ApiAsync();

        var resposta = await PedirAsync(api, api.Elenco.GestorSemSetor, Corpo("2026-09-30", "   Esqueci de bater a saída no fim do dia.   ", Entrada("2026-09-30T08:00:00-03:00"), Saida("2026-09-30T17:00:00-03:00")));

        Assert.Equal(201, (int)resposta.StatusCode);
        var pedido = await CorpoAsync(resposta);
        Assert.Equal("Esqueci de bater a saída no fim do dia.", pedido.GetProperty("justificativa").GetString());
        Assert.Equal(["2026-09-30T11:00:00Z", "2026-09-30T20:00:00Z"], pedido.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("instanteUtc").GetString()!));
    }

    // ---------------------------------------------------------------- auditoria, e-mail e quem vê

    [Fact]
    public async Task Auditoria_registra_PONTO_PEDIU_AJUSTE_sem_a_justificativa_e_criar_o_pedido_nao_manda_e_mail()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB;
        var emails = Contar(api, "SELECT COUNT(*) FROM FILA_EMAILS");

        var criado = await CorpoAsync(await PedirAsync(api, pessoa, Corpo("2026-10-03", "Estava em visita externa e esqueci de bater.", Entrada("2026-10-03T11:00:00Z"), Saida("2026-10-03T20:00:00Z"))));

        var filtro = $"AREA = 'PONTO' AND ACAO = 'PEDIU_AJUSTE' AND ID_FUNCIONARIO_ATOR = {pessoa.FuncionarioId} AND ENTIDADE = 'PONTOS_MANUTENCOES' AND ID_REGISTRO = {criado.GetProperty("id").GetInt32()} AND CANAL = 'PORTAL' AND IP IS NOT NULL";
        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE {filtro}"));
        var descricao = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM AUDITORIA_EVENTOS WHERE {filtro}"))!;
        Assert.Equal("Pediu o ajuste do ponto do dia 03/10/2026.", descricao);
        Assert.Equal(emails, Contar(api, "SELECT COUNT(*) FROM FILA_EMAILS")); // criar não manda e-mail
    }

    [Fact]
    public async Task Cada_um_ve_so_os_proprios_pedidos_e_o_de_outra_pessoa_e_404()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var rh = api.Elenco.Rh;
        var criado = await CorpoAsync(await PedirAsync(api, rh, Corpo("2026-10-06", "Fiquei no atendimento da auditoria e não bati a saída.", Entrada("2026-10-06T11:00:00Z"), Saida("2026-10-06T20:00:00Z"))));
        var id = criado.GetProperty("id").GetInt32();

        var doDono = await (await api.ClienteAgoraAsync(rh.UsuarioId)).GetAsync($"{Rota}/{id}");
        var deOutro = await (await api.ClienteAgoraAsync(diego.UsuarioId)).GetAsync($"{Rota}/{id}");
        var inexistente = await (await api.ClienteAgoraAsync(diego.UsuarioId)).GetAsync($"{Rota}/999999");
        var listaDoOutro = await CorpoAsync(await (await api.ClienteAgoraAsync(diego.UsuarioId)).GetAsync(Rota));
        var anonimo = await api.Api.ClienteAnonimo().GetAsync(Rota);

        Assert.Equal((200, 404, 404, 401), ((int)doDono.StatusCode, (int)deOutro.StatusCode, (int)inexistente.StatusCode, (int)anonimo.StatusCode));
        Assert.DoesNotContain(listaDoOutro.GetProperty("itens").EnumerateArray(), p => p.GetProperty("id").GetInt32() == id);
    }

    // ---------------------------------------------------------------- listas

    [Fact]
    public async Task A_lista_vem_do_mais_recente_ao_mais_antigo_filtra_por_status_e_etapa_e_ordena_por_dia_do_ponto_decrescente_por_padrao()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, new DateOnly(2026, 9, 14), (TipoDeMarcacao.Entrada, "2026-09-14T11:00:00Z"));
        await PontoDeTeste.BaterEmAsync(api.Conexao, pessoa.FuncionarioId, new DateOnly(2026, 9, 15), (TipoDeMarcacao.Entrada, "2026-09-15T11:00:00Z"));
        var primeiro = (await CorpoAsync(await PedirAsync(api, pessoa, Corpo("2026-09-14", "Esqueci a saída do dia 14.", Saida("2026-09-14T20:00:00Z"))))).GetProperty("id").GetInt32();
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddMinutes(5));
        var segundo = (await CorpoAsync(await PedirAsync(api, pessoa, Corpo("2026-09-15", "Esqueci a saída do dia 15.", Saida("2026-09-15T20:00:00Z"))))).GetProperty("id").GetInt32();
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE PONTOS_MANUTENCOES SET STATUS_SOLICITACAO = 'REJEITADA', ID_GESTOR = (SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {api.Elenco.Gestor.FuncionarioId}), RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = NOW(), OBSERVACAO = 'Sem comprovação.' WHERE ID_MANUTENCAO_PONTO = {primeiro}");
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

        var todos = await CorpoAsync(await cliente.GetAsync(Rota));
        var pendentes = await CorpoAsync(await cliente.GetAsync($"{Rota}?status=PENDENTE"));
        var concluidos = await CorpoAsync(await cliente.GetAsync($"{Rota}?etapa=CONCLUIDA"));
        var porDia = await CorpoAsync(await cliente.GetAsync($"{Rota}?ordenarPor=DIA_DO_PONTO"));
        var porDiaAsc = await CorpoAsync(await cliente.GetAsync($"{Rota}?ordenarPor=DIA_DO_PONTO&direcao=ASC"));

        Assert.Equal([segundo, primeiro], todos.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal([segundo], pendentes.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal([primeiro], concluidos.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal([segundo, primeiro], porDia.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32())); // DESC por padrão
        Assert.Equal([primeiro, segundo], porDiaAsc.GetProperty("itens").EnumerateArray().Select(p => p.GetProperty("id").GetInt32()));
        Assert.Equal((2, 1, 1, 20), (todos.GetProperty("total").GetInt32(), todos.GetProperty("pagina").GetInt32(), todos.GetProperty("totalPaginas").GetInt32(), todos.GetProperty("tamanhoPagina").GetInt32()));
        var recusado = todos.GetProperty("itens")[1];
        Assert.Equal(("REJEITADA", "CONCLUIDA", "REJEITADA", JsonValueKind.Null, "Sem comprovação.", "Este pedido já foi decidido."),
            (recusado.GetProperty("status").GetString(), recusado.GetProperty("etapa").GetString(), recusado.GetProperty("decisaoGestor").GetProperty("decisao").GetString(), recusado.GetProperty("decisaoRh").ValueKind, recusado.GetProperty("decisaoGestor").GetProperty("observacao").GetString(), recusado.GetProperty("motivoNaoPodeDecidir").GetString()));
    }

    [Theory]
    [InlineData("?status=ENVIADO", "status")]
    [InlineData("?etapa=ZZZ", "etapa")]
    [InlineData("?ordenarPor=NOME", "ordenarPor")]
    [InlineData("?direcao=DOWN", "direcao")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    public async Task Filtro_invalido_e_400_com_o_nome_do_campo(string consulta, string campo)
    {
        var api = await ApiAsync();

        var resposta = await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync($"{Rota}{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task As_respostas_dos_pedidos_proprios_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorDoFinanceiro;
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

        var criada = await PedirAsync(api, pessoa, Corpo("2026-09-28", "Fiquei fora em reunião externa o dia todo.", Entrada("2026-09-28T11:00:00Z"), Saida("2026-09-28T20:00:00Z")));
        await criada.AfirmarConformeAoContratoAsync("criarPedidoDeAjuste");
        await (await PedirAsync(api, pessoa, Corpo("2026-09-28", "Fiquei fora em reunião externa o dia todo.", Entrada("2026-09-28T11:00:00Z"), Saida("2026-09-28T20:00:00Z")))).AfirmarConformeAoContratoAsync("criarPedidoDeAjuste"); // 409
        await (await PedirAsync(api, pessoa, Corpo("2026-10-07", Justificativa, Saida("2026-10-07T11:00:00Z")))).AfirmarConformeAoContratoAsync("criarPedidoDeAjuste"); // 422
        await (await PedirAsync(api, pessoa, Corpo("2026-09-27", "curta", Saida("2026-09-27T11:00:00Z")))).AfirmarConformeAoContratoAsync("criarPedidoDeAjuste"); // 400
        await (await cliente.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarMeusPedidosDeAjuste");
        await (await cliente.GetAsync($"{Rota}?status=XX")).AfirmarConformeAoContratoAsync("listarMeusPedidosDeAjuste"); // 400
        await (await cliente.GetAsync($"{Rota}/{(await CorpoAsync(criada)).GetProperty("id").GetInt32()}")).AfirmarConformeAoContratoAsync("obterMeuPedidoDeAjuste");
        await (await cliente.GetAsync($"{Rota}/999999")).AfirmarConformeAoContratoAsync("obterMeuPedidoDeAjuste"); // 404
    }
}

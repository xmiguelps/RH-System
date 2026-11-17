using System.Globalization;
using System.Text.Json;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de ponto do <c>V007</c> (<c>SP_REGISTRAR_MARCACAO_PONTO</c> e <c>SP_SOLICITAR_AJUSTE_PONTO</c>) contra as regras da API (<c>RegistrarMarcacaoCasoDeUso</c>, <c>CriarPedidoDeAjusteCasoDeUso</c>, <c>GestorAusente</c>).
/// As procedures leem a hora com <c>NOW()</c>; quase todos os testes fixam o relógio da SESSÃO (<c>SET timestamp</c>, que o <c>NOW()</c> obedece) para provar a meia-noite, a janela de 12 horas e o "mesmo segundo" sem depender da hora em que a suíte roda.
/// Os que provam o relógio de verdade (a hora lida depois da trava) dizem isso no nome.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PontoProceduresTests(MySqlFixture mysql)
{
    private const string Registrar = "SP_REGISTRAR_MARCACAO_PONTO";
    private const string Solicitar = "SP_SOLICITAR_AJUSTE_PONTO";
    private const string Justificativa = "Esqueci de bater o ponto.";

    private const string ColunasDoPedido =
        "ID_PONTO, ID_MARCACAO, ID_FUNCIONARIO, JUSTIFICATIVA, DATA_SOLICITACAO, STATUS_SOLICITACAO, DATA_ANALISE, ID_GESTOR, OBSERVACAO, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ETAPA_GESTOR_AUTOMATICA, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH";

    /// <summary>Quinta-feira, 10:00 de Brasília: o "agora" dos testes de pedido.</summary>
    private static readonly DateTime Agora = new(2026, 3, 12, 10, 0, 0);

    /// <summary>Terça-feira: o dia do ponto dos testes (dois dias antes de <see cref="Agora"/>).</summary>
    private static readonly DateOnly Dia = new(2026, 3, 10);

    // ================================================================ SP_REGISTRAR_MARCACAO_PONTO

    [Fact]
    public async Task A_primeira_batida_do_dia_e_a_entrada_e_cria_o_dia_com_a_carga_do_cargo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var batida = await BaterAsync(conexao, Em(10, 8, 0), pessoa, "ENTRADA");

        Assert.Equal([$"{pessoa}|2026-03-10|nulo|8.80"], await LinhasAsync(conexao, "ID_FUNCIONARIO, DATA, HORAS_TRABALHADAS, CARGA_HORARIA", "FROM PONTOS"));
        Assert.Equal(
            [$"{batida}|2026-03-10 08:00:00|ENTRADA|SISTEMA|nulo"],
            await LinhasAsync(conexao, "M.ID_MARCACAO, M.DATA_HORA_MARCACAO, M.TIPO_MARCACAO, M.ORIGEM, M.DATA_CORRECAO", $"FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO WHERE P.ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task As_batidas_do_dia_alternam_entrada_e_saida_no_mesmo_registro_do_dia()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        await BaterAsync(conexao, Em(10, 8, 0), pessoa, "ENTRADA");
        await BaterAsync(conexao, Em(10, 12, 0), pessoa, "SAIDA");
        await BaterAsync(conexao, Em(10, 13, 0), pessoa, "ENTRADA");
        await BaterAsync(conexao, Em(10, 17, 30), pessoa, "SAIDA");

        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
        Assert.Equal(
            ["2026-03-10 08:00:00|ENTRADA", "2026-03-10 12:00:00|SAIDA", "2026-03-10 13:00:00|ENTRADA", "2026-03-10 17:30:00|SAIDA"],
            await LinhasAsync(conexao, "DATA_HORA_MARCACAO, TIPO_MARCACAO", "FROM PONTOS_MARCACOES ORDER BY DATA_HORA_MARCACAO"));
        Assert.Equal("CONSISTENTE", await Sql.EscalarAsync(conexao, "SELECT FN_VERIFICAR_CONSISTENCIA_PONTO(ID_PONTO) FROM PONTOS"));
    }

    [Fact]
    public async Task A_saida_como_primeira_batida_e_recusada_dizendo_que_a_proxima_e_a_entrada_e_nada_fica_gravado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var mensagem = await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, pessoa, "SAIDA", new Saida("id"));

        Assert.Equal("A sequencia do ponto mudou: a proxima marcacao e ENTRADA.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM PONTOS) + (SELECT COUNT(*) FROM PONTOS_MARCACOES)"));
    }

    [Fact]
    public async Task A_entrada_depois_de_uma_entrada_e_recusada_dizendo_que_a_proxima_e_a_saida()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 8, 0), pessoa, "ENTRADA");

        var mensagem = await RecusaEmAsync(conexao, Em(10, 12, 0), Registrar, pessoa, "ENTRADA", new Saida("id"));

        Assert.Equal("A sequencia do ponto mudou: a proxima marcacao e SAIDA.", mensagem);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Fact]
    public async Task A_proxima_batida_segue_a_ultima_do_dia_por_horario_e_nao_a_ultima_gravada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ponto = await DiaDoPontoAsync(conexao, pessoa, Dia);
        await BatidaAsync(conexao, ponto, Em(10, 9, 0), "SAIDA"); // gravada primeiro, mas é a última do dia por horário
        await BatidaAsync(conexao, ponto, Em(10, 8, 0), "ENTRADA");

        await BaterAsync(conexao, Em(10, 13, 0), pessoa, "ENTRADA");

        Assert.Equal(3L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Theory]
    [InlineData("SAIDA")]
    [InlineData("ENTRADA")]
    public async Task Duas_batidas_no_mesmo_segundo_sao_recusadas_antes_de_olhar_a_sequencia(string segunda)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 8, 0), pessoa, "ENTRADA");

        var mensagem = await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, pessoa, segunda, new Saida("id"));

        Assert.Equal("O ponto ja foi registrado neste mesmo segundo.", mensagem);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Fact]
    public async Task A_saida_da_jornada_que_atravessou_a_meia_noite_entra_no_dia_da_entrada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 22, 0), pessoa, "ENTRADA");

        var saida = await BaterAsync(conexao, Em(11, 6, 0), pessoa, "SAIDA");

        Assert.Equal(["2026-03-10"], await LinhasAsync(conexao, "DATA", "FROM PONTOS"));
        Assert.Equal(
            ["2026-03-10|2026-03-10 22:00:00|ENTRADA", "2026-03-10|2026-03-11 06:00:00|SAIDA"],
            await LinhasAsync(conexao, "P.DATA, M.DATA_HORA_MARCACAO, M.TIPO_MARCACAO", "FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO ORDER BY M.DATA_HORA_MARCACAO"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {saida} AND TIPO_MARCACAO = 'SAIDA'"));
    }

    [Fact]
    public async Task Com_a_entrada_de_ontem_aberta_ha_menos_de_12_horas_a_entrada_de_hoje_e_recusada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 22, 0), pessoa, "ENTRADA");

        var mensagem = await RecusaEmAsync(conexao, Em(11, 6, 0), Registrar, pessoa, "ENTRADA", new Saida("id"));

        Assert.Equal("A sequencia do ponto mudou: a proxima marcacao e SAIDA.", mensagem);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
    }

    [Fact]
    public async Task A_janela_da_meia_noite_vale_ate_11_horas_59_minutos_e_59_segundos_depois_da_entrada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 22, 0), pessoa, "ENTRADA");

        await BaterAsync(conexao, new DateTime(2026, 3, 11, 9, 59, 59), pessoa, "SAIDA");

        Assert.Equal(["2026-03-10|2026-03-11 09:59:59|SAIDA"], await LinhasAsync(conexao, "P.DATA, M.DATA_HORA_MARCACAO, M.TIPO_MARCACAO", "FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO WHERE M.TIPO_MARCACAO = 'SAIDA'"));
    }

    [Fact]
    public async Task Com_12_horas_exatas_a_entrada_de_ontem_ja_nao_puxa_a_batida_e_ela_e_a_entrada_de_hoje()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 22, 0), pessoa, "ENTRADA");

        var mensagem = await RecusaEmAsync(conexao, Em(11, 10, 0), Registrar, pessoa, "SAIDA", new Saida("id"));
        await BaterAsync(conexao, Em(11, 10, 0), pessoa, "ENTRADA");

        Assert.Equal("A sequencia do ponto mudou: a proxima marcacao e ENTRADA.", mensagem);
        Assert.Equal(
            ["2026-03-10|2026-03-10 22:00:00|ENTRADA", "2026-03-11|2026-03-11 10:00:00|ENTRADA"],
            await LinhasAsync(conexao, "P.DATA, M.DATA_HORA_MARCACAO, M.TIPO_MARCACAO", "FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO ORDER BY M.DATA_HORA_MARCACAO"));
    }

    [Fact]
    public async Task Quem_ja_bateu_hoje_segue_a_sequencia_de_hoje_mesmo_com_a_entrada_de_ontem_aberta()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ontem = await DiaDoPontoAsync(conexao, pessoa, Dia);
        await BatidaAsync(conexao, ontem, Em(10, 23, 0), "ENTRADA");
        var hoje = await DiaDoPontoAsync(conexao, pessoa, Dia.AddDays(1));
        await BatidaAsync(conexao, hoje, Em(11, 5, 0), "ENTRADA");

        var saida = await BaterAsync(conexao, Em(11, 6, 0), pessoa, "SAIDA");

        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {saida} AND ID_PONTO = {hoje}"));
    }

    [Fact]
    public async Task Com_o_dia_de_ontem_fechado_a_primeira_batida_de_hoje_e_a_entrada_no_dia_de_hoje()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await BaterAsync(conexao, Em(10, 20, 0), pessoa, "ENTRADA");
        await BaterAsync(conexao, Em(10, 23, 0), pessoa, "SAIDA");

        await BaterAsync(conexao, Em(11, 6, 0), pessoa, "ENTRADA");

        Assert.Equal(["2026-03-11|2026-03-11 06:00:00|ENTRADA"], await LinhasAsync(conexao, "P.DATA, M.DATA_HORA_MARCACAO, M.TIPO_MARCACAO", "FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO WHERE P.DATA = '2026-03-11'"));
    }

    [Fact]
    public async Task Pessoa_inativa_nao_bate_ponto_nem_em_dia_que_ja_existe()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await DiaDoPontoAsync(conexao, pessoa, Dia); // com o dia já criado o trigger de PONTOS não roda: quem recusa é a procedure
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {pessoa}");

        var mensagem = await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, pessoa, "ENTRADA", new Saida("id"));

        Assert.Equal("Nao e possivel registrar ponto de funcionario inativo.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Fact]
    public async Task Pessoa_que_nao_existe_nao_bate_ponto()
    {
        var conexao = await NovoBancoAsync(mysql);

        Assert.Equal("Funcionario nao encontrado.", await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, 987654, "ENTRADA", new Saida("id")));
    }

    [Fact]
    public async Task Antes_da_admissao_a_batida_e_recusada_com_o_texto_do_trigger_e_no_dia_da_admissao_ela_entra()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao, admissao: "2026-03-10");

        var mensagem = await RecusaEmAsync(conexao, new DateTime(2026, 3, 9, 23, 59, 59), Registrar, pessoa, "ENTRADA", new Saida("id"));
        await BaterAsync(conexao, Em(10, 0, 0), pessoa, "ENTRADA");

        Assert.Equal("A data do ponto e anterior a admissao.", mensagem);
        Assert.Equal(["2026-03-10"], await LinhasAsync(conexao, "DATA", "FROM PONTOS"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ALMOCO")]
    public async Task Tipo_de_marcacao_que_nao_e_entrada_nem_saida_e_recusado(string? tipo)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        Assert.Equal("Tipo de marcacao invalido: informe ENTRADA ou SAIDA.", await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, pessoa, tipo, new Saida("id")));
    }

    [Fact]
    public async Task Sem_o_funcionario_a_batida_e_recusada()
    {
        var conexao = await NovoBancoAsync(mysql);

        Assert.Equal("Informe o funcionario.", await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, null, "ENTRADA", new Saida("id")));
    }

    [Fact]
    public async Task Se_a_batida_falha_depois_de_criar_o_dia_o_dia_tambem_e_desfeito()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, "CREATE TRIGGER TRG_TESTE_FALHA BEFORE INSERT ON PONTOS_MARCACOES FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Falha de teste.'");

        var mensagem = await RecusaEmAsync(conexao, Em(10, 8, 0), Registrar, pessoa, "ENTRADA", new Saida("id"));

        Assert.Equal("Falha de teste.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
    }

    [Fact]
    public async Task Duas_batidas_simultaneas_da_mesma_pessoa_se_enfileiram_e_so_uma_entra()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var resultados = await ComAPessoaTravadaAsync(
            conexao,
            pessoa,
            TimeSpan.FromMilliseconds(700),
            () => TentarAsync(conexao, Em(10, 8, 0), Registrar, pessoa, "ENTRADA", new Saida("id")),
            () => TentarAsync(conexao, Em(10, 8, 0), Registrar, pessoa, "ENTRADA", new Saida("id")));

        Assert.Single(resultados, r => r is null);
        Assert.Single(resultados, r => r == "45000:O ponto ja foi registrado neste mesmo segundo.");
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Fact]
    public async Task No_relogio_de_verdade_a_hora_da_batida_e_lida_depois_da_trava_e_nao_no_inicio_da_chamada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var antes = Convert.ToDateTime(await Sql.EscalarAsync(conexao, "SELECT NOW()"), CultureInfo.InvariantCulture);

        var resultados = await ComAPessoaTravadaAsync(conexao, pessoa, TimeSpan.FromMilliseconds(2600), () => TentarAsync(conexao, relogio: null, Registrar, pessoa, "ENTRADA", new Saida("id")));

        Assert.Null(Assert.Single(resultados));
        Assert.Equal(
            1L,
            await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MARCACOES M JOIN PONTOS P ON P.ID_PONTO = M.ID_PONTO WHERE TIMESTAMPDIFF(SECOND, '{Texto(antes)}', M.DATA_HORA_MARCACAO) BETWEEN 2 AND 60 AND P.DATA = DATE(M.DATA_HORA_MARCACAO)"));
    }

    // ================================================================ SP_SOLICITAR_AJUSTE_PONTO

    [Fact]
    public async Task O_pedido_de_um_dia_sem_batida_cria_o_dia_o_pedido_pendente_e_os_itens_sem_mexer_em_batida_nenhuma()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, " \t Esqueci de bater o ponto.\n ", Itens((null, "2026-03-10 08:00:00", "ENTRADA"), (null, "2026-03-10 17:00:00", "SAIDA")));

        var ponto = Inteiro(await Sql.EscalarAsync(conexao, "SELECT ID_PONTO FROM PONTOS"));
        Assert.Equal([$"{pessoa}|2026-03-10|8.80"], await LinhasAsync(conexao, "ID_FUNCIONARIO, DATA, CARGA_HORARIA", "FROM PONTOS"));
        Assert.Equal(
            [$"{ponto}|nulo|{pessoa}|Esqueci de bater o ponto.|2026-03-12 10:00:00|PENDENTE|nulo|nulo|nulo|nulo|nulo|0|nulo|nulo"],
            await LinhasAsync(conexao, ColunasDoPedido, $"FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}"));
        Assert.Equal(
            [$"{pedido}|{ponto}|nulo|2026-03-10 08:00:00|ENTRADA", $"{pedido}|{ponto}|nulo|2026-03-10 17:00:00|SAIDA"],
            await LinhasAsync(conexao, "ID_MANUTENCAO_PONTO, ID_PONTO, ID_MARCACAO, DATA_HORA_PROPOSTA, TIPO_MARCACAO", "FROM PONTOS_MANUTENCOES_ITENS ORDER BY ID_MANUTENCAO_PONTO_ITEM"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MARCACOES"));
    }

    [Fact]
    public async Task O_pedido_que_corrige_uma_batida_e_inclui_outra_guarda_a_batida_corrigida_e_deixa_o_dia_como_estava()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ponto = await DiaDoPontoAsync(conexao, pessoa, Dia);
        var entrada = await BatidaAsync(conexao, ponto, Em(10, 8, 0), "ENTRADA");
        await BatidaAsync(conexao, ponto, Em(10, 12, 0), "SAIDA");
        await BatidaAsync(conexao, ponto, Em(10, 13, 0), "ENTRADA");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, Itens((null, "2026-03-10 17:00:00.900", "SAIDA"), (entrada, "2026-03-10 07:30:00", "ENTRADA")));

        Assert.Equal(
            [$"{ponto}|nulo|2026-03-10 17:00:00|SAIDA", $"{ponto}|{entrada}|2026-03-10 07:30:00|ENTRADA"],
            await LinhasAsync(conexao, "ID_PONTO, ID_MARCACAO, DATA_HORA_PROPOSTA, TIPO_MARCACAO", $"FROM PONTOS_MANUTENCOES_ITENS WHERE ID_MANUTENCAO_PONTO = {pedido} ORDER BY ID_MANUTENCAO_PONTO_ITEM"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
        Assert.Equal(
            ["2026-03-10 08:00:00|ENTRADA|SISTEMA", "2026-03-10 12:00:00|SAIDA|SISTEMA", "2026-03-10 13:00:00|ENTRADA|SISTEMA"],
            await LinhasAsync(conexao, "DATA_HORA_MARCACAO, TIPO_MARCACAO, ORIGEM", "FROM PONTOS_MARCACOES ORDER BY DATA_HORA_MARCACAO"));
    }

    [Fact]
    public async Task O_pedido_de_quem_tem_gestor_no_setor_nasce_na_etapa_do_gestor_do_setor_e_nao_do_gestor_da_ficha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, gestorDoSetor, _) = await EquipeAsync(conexao);
        var outroGestor = await GestorAsync(conexao, await FuncionarioAsync(conexao, nivel: "Funcionario Gestor"));
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {outroGestor} WHERE ID_FUNCIONARIO = {pessoa}");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal([$"PENDENTE|{gestorDoSetor}|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Theory]
    [InlineData("NULL")]
    [InlineData("'2026-03-13'")]
    public async Task Com_o_gestor_ausente_hoje_a_etapa_dele_ja_nasce_aprovada_sozinha_e_o_pedido_segue_para_o_rh(string volta)
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, gestor, _) = await EquipeAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE, APROVACAO_AUTOMATICA_ATE = {volta} WHERE ID_GESTOR = {gestor}");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal([$"PENDENTE|{gestor}|APROVOU|2026-03-12 10:00:00|1"], await EtapaAsync(conexao, pedido));
    }

    [Theory]
    [InlineData("'2026-03-12'")] // a volta é hoje: já não está ausente
    [InlineData("'2026-03-01'")]
    public async Task Com_a_volta_do_gestor_hoje_ou_antes_o_pedido_nasce_na_etapa_dele(string volta)
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, gestor, _) = await EquipeAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE, APROVACAO_AUTOMATICA_ATE = {volta} WHERE ID_GESTOR = {gestor}");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal([$"PENDENTE|{gestor}|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Fact]
    public async Task Com_o_gestor_do_setor_inativo_o_pedido_nasce_sem_etapa_do_gestor()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, _, chefe) = await EquipeAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {chefe}");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal(["PENDENTE|nulo|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Fact]
    public async Task O_pedido_de_quem_nao_tem_setor_nasce_sem_etapa_do_gestor()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, _, _) = await EquipeAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = NULL WHERE ID_FUNCIONARIO = {pessoa}");

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal(["PENDENTE|nulo|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Fact]
    public async Task O_pedido_do_gestor_que_gerencia_um_setor_nasce_sem_etapa_do_gestor_mesmo_lotado_no_setor_de_outro()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (_, _, chefe) = await EquipeAsync(conexao);
        var (_, _, outroChefe) = await EquipeAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = (SELECT ID_SETOR FROM (SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {outroChefe}) X) WHERE ID_FUNCIONARIO = {chefe}");

        var pedido = await PedirAsync(conexao, Agora, chefe, Dia, Justificativa, DiaInteiro());

        Assert.Equal(["PENDENTE|nulo|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Fact]
    public async Task O_pedido_de_quem_e_do_rh_e_esta_em_gestores_nasce_sem_etapa_do_gestor_mesmo_sem_gerenciar_setor()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (_, _, chefe) = await EquipeAsync(conexao);
        var setor = Inteiro(await Sql.EscalarAsync(conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {chefe}"));
        var doRh = await FuncionarioAsync(conexao, nivel: "RH", setorId: setor);
        await GestorAsync(conexao, doRh);

        var pedido = await PedirAsync(conexao, Agora, doRh, Dia, Justificativa, DiaInteiro());

        Assert.Equal(["PENDENTE|nulo|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Theory]
    [InlineData("RH", false)] // do RH, mas fora de GESTORES: segue o gestor do setor
    [InlineData("Funcionario", true)] // em GESTORES, mas sem gerenciar setor e sem ser do RH (rebaixado): segue o gestor do setor
    public async Task Quem_nao_e_gestor_de_verdade_segue_o_gestor_do_setor(string nivel, bool emGestores)
    {
        var conexao = await NovoBancoAsync(mysql);
        var (_, gestor, chefe) = await EquipeAsync(conexao);
        var setor = Inteiro(await Sql.EscalarAsync(conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {chefe}"));
        var pessoa = await FuncionarioAsync(conexao, nivel: nivel, setorId: setor);
        if (emGestores)
        {
            await GestorAsync(conexao, pessoa);
        }

        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal([$"PENDENTE|{gestor}|nulo|nulo|0"], await EtapaAsync(conexao, pedido));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("abcd")]
    [InlineData("   abcd \t\n")]
    public async Task Justificativa_com_menos_de_5_caracteres_uteis_e_recusada(string? justificativa)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        Assert.Equal("A justificativa deve ter de 5 a 500 caracteres.", await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, justificativa, DiaInteiro(), new Saida("id")));
    }

    [Fact]
    public async Task Justificativa_de_5_e_de_500_caracteres_entram_e_a_de_501_e_recusada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        await PedirAsync(conexao, Agora, pessoa, Dia, "abcde", DiaInteiro());
        await PedirAsync(conexao, Agora, pessoa, Dia.AddDays(-1), new string('x', 500), DiaInteiro(Dia.AddDays(-1)));
        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia.AddDays(-2), new string('x', 501), DiaInteiro(Dia.AddDays(-2)), new Saida("id"));

        Assert.Equal("A justificativa deve ter de 5 a 500 caracteres.", mensagem);
        Assert.Equal(2L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("\"texto\"")]
    public async Task Lista_de_itens_ausente_vazia_ou_que_nao_e_lista_e_recusada(string? itens)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        Assert.Equal("Informe de 1 a 10 marcacoes em P_ITENS (lista JSON).", await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, itens, new Saida("id")));
    }

    [Fact]
    public async Task Dez_itens_entram_e_onze_sao_recusados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var onze = Enumerable.Range(0, 11).Select(i => ((int?)null, $"2026-03-10 {8 + i:00}:00:00", i % 2 == 0 ? "ENTRADA" : "SAIDA")).ToArray();

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens(onze), new Saida("id"));
        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, Itens(onze[..10]));

        Assert.Equal("Informe de 1 a 10 marcacoes em P_ITENS (lista JSON).", mensagem);
        Assert.Equal(10L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MANUTENCOES_ITENS WHERE ID_MANUTENCAO_PONTO = {pedido}"));
    }

    [Theory]
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": "ALMOCO"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": null}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"tipo": "ENTRADA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"dataHora": "2026-02-30 08:00:00", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"dataHora": "oito horas", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"idMarcacao": "abc", "dataHora": "2026-03-10 08:00:00", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""[{"idMarcacao": 99999999999, "dataHora": "2026-03-10 08:00:00", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "SAIDA"}]""")]
    [InlineData("""["ENTRADA", "SAIDA"]""")]
    public async Task Item_sem_tipo_valido_sem_data_hora_valida_ou_com_id_que_nao_e_numero_e_recusado(string itens)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        Assert.Equal("Ha item com tipo, dataHora ou idMarcacao invalido.", await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, itens, new Saida("id")));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM PONTOS) + (SELECT COUNT(*) FROM PONTOS_MANUTENCOES)"));
    }

    [Fact]
    public async Task Sem_o_funcionario_ou_sem_o_dia_o_pedido_e_recusado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        Assert.Equal("Informe o funcionario e o dia do ponto.", await RecusaEmAsync(conexao, Agora, Solicitar, null, Dia, Justificativa, DiaInteiro(), new Saida("id")));
        Assert.Equal("Informe o funcionario e o dia do ponto.", await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, null, Justificativa, DiaInteiro(), new Saida("id")));
    }

    [Fact]
    public async Task Pessoa_que_nao_existe_nao_pede_ajuste()
    {
        var conexao = await NovoBancoAsync(mysql);

        Assert.Equal("Funcionario nao encontrado.", await RecusaEmAsync(conexao, Agora, Solicitar, 987654, Dia, Justificativa, DiaInteiro(), new Saida("id")));
    }

    [Fact]
    public async Task Pessoa_inativa_nao_pede_ajuste_nem_de_dia_que_ja_existe()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await DiaDoPontoAsync(conexao, pessoa, Dia);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {pessoa}");

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, DiaInteiro(), new Saida("id"));

        Assert.Equal("Nao e possivel registrar ponto de funcionario inativo.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
    }

    [Fact]
    public async Task Dia_anterior_a_admissao_e_recusado_com_o_texto_do_trigger_e_o_dia_da_admissao_entra()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao, admissao: "2026-03-10");

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia.AddDays(-1), Justificativa, DiaInteiro(Dia.AddDays(-1)), new Saida("id"));
        await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal("A data do ponto e anterior a admissao.", mensagem);
        Assert.Equal(["2026-03-10"], await LinhasAsync(conexao, "DATA", "FROM PONTOS"));
    }

    [Theory]
    [InlineData(12)] // hoje
    [InlineData(13)] // amanhã
    public async Task So_da_para_pedir_ajuste_de_um_dia_que_ja_passou(int diaDoMes)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var dia = new DateOnly(2026, 3, diaDoMes);

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, dia, Justificativa, Itens((null, $"2026-03-{diaDoMes} 08:00:00", "ENTRADA"), (null, $"2026-03-{diaDoMes} 09:00:00", "SAIDA")), new Saida("id"));

        Assert.Equal("So e possivel pedir ajuste de um dia que ja passou.", mensagem);
    }

    [Fact]
    public async Task O_dia_de_ontem_ja_passou_e_o_horario_pode_ir_ate_agora_mas_nao_um_segundo_depois()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ontem = new DateOnly(2026, 3, 11);

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, ontem, Justificativa, Itens((null, "2026-03-11 22:00:00", "ENTRADA"), (null, "2026-03-12 10:00:01", "SAIDA")), new Saida("id"));
        await PedirAsync(conexao, Agora, pessoa, ontem, Justificativa, Itens((null, "2026-03-11 22:00:00", "ENTRADA"), (null, "2026-03-12 10:00:00", "SAIDA")));

        Assert.Equal("Ha marcacao com horario no futuro.", mensagem);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
    }

    [Theory]
    [InlineData("2026-03-08 23:59:59", "2026-03-09 08:00:00")] // antes do dia
    [InlineData("2026-03-09 08:00:00", "2026-03-11 00:00:00")] // dois dias depois
    public async Task Horario_fora_do_dia_do_pedido_e_do_dia_seguinte_e_recusado(string entrada, string saida)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, new DateOnly(2026, 3, 9), Justificativa, Itens((null, entrada, "ENTRADA"), (null, saida, "SAIDA")), new Saida("id"));

        Assert.Equal("Cada marcacao deve cair no dia do pedido ou no dia seguinte.", mensagem);
    }

    [Fact]
    public async Task O_horario_pode_ir_da_meia_noite_do_dia_ate_o_ultimo_segundo_do_dia_seguinte()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var pedido = await PedirAsync(conexao, Agora, pessoa, new DateOnly(2026, 3, 9), Justificativa, Itens((null, "2026-03-09 00:00:00", "ENTRADA"), (null, "2026-03-10 23:59:59", "SAIDA")));

        Assert.Equal(["2026-03-09 00:00:00", "2026-03-10 23:59:59"], await LinhasAsync(conexao, "DATA_HORA_PROPOSTA", $"FROM PONTOS_MANUTENCOES_ITENS WHERE ID_MANUTENCAO_PONTO = {pedido} ORDER BY DATA_HORA_PROPOSTA"));
    }

    [Fact]
    public async Task So_um_pedido_em_analise_por_dia_e_outro_dia_ou_o_mesmo_dia_depois_da_resposta_aceitam_pedido_novo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var (pessoa, gestor, _) = await EquipeAsync(conexao);
        var primeiro = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, DiaInteiro());

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, "Outro motivo.", DiaInteiro(), new Saida("id"));
        await PedirAsync(conexao, Agora, pessoa, Dia.AddDays(-1), Justificativa, DiaInteiro(Dia.AddDays(-1)));
        await Sql.ExecutarAsync(conexao, $"UPDATE PONTOS_MANUTENCOES SET STATUS_SOLICITACAO = 'REJEITADA', RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = '2026-03-12 10:05:00', ID_GESTOR = {gestor} WHERE ID_MANUTENCAO_PONTO = {primeiro}");
        var depoisDaResposta = await PedirAsync(conexao, Agora.AddMinutes(10), pessoa, Dia, Justificativa, DiaInteiro());

        Assert.Equal("Este dia ja tem um pedido de ajuste em analise.", mensagem);
        Assert.Equal(["REJEITADA", "PENDENTE", "PENDENTE"], await LinhasAsync(conexao, "STATUS_SOLICITACAO", "FROM PONTOS_MANUTENCOES ORDER BY ID_MANUTENCAO_PONTO"));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS")); // o segundo pedido do mesmo dia reusa o dia
        Assert.True(depoisDaResposta > primeiro);
    }

    [Fact]
    public async Task Batida_de_outro_dia_de_outra_pessoa_ou_que_nao_existe_nao_pode_ser_corrigida_pelo_pedido()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var deOutroDia = await BatidaAsync(conexao, await DiaDoPontoAsync(conexao, pessoa, Dia.AddDays(-1)), Em(9, 8, 0), "ENTRADA");
        var deOutraPessoa = await BatidaAsync(conexao, await DiaDoPontoAsync(conexao, await FuncionarioAsync(conexao), Dia), Em(10, 8, 0), "ENTRADA");

        foreach (var id in new[] { deOutroDia, deOutraPessoa, 987654 })
        {
            var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((id, "2026-03-10 08:00:00", "ENTRADA"), (null, "2026-03-10 17:00:00", "SAIDA")), new Saida("id"));
            Assert.Equal("A marcacao a corrigir (idMarcacao) nao e deste dia.", mensagem);
        }

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa} AND DATA = '2026-03-10'"));
    }

    [Fact]
    public async Task A_mesma_batida_corrigida_duas_vezes_e_recusada_e_entre_dois_problemas_vale_o_do_primeiro_item()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ponto = await DiaDoPontoAsync(conexao, pessoa, Dia);
        var entrada = await BatidaAsync(conexao, ponto, Em(10, 8, 0), "ENTRADA");

        var repetida = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((entrada, "2026-03-10 08:10:00", "ENTRADA"), (entrada, "2026-03-10 17:00:00", "SAIDA"), (987654, "2026-03-10 18:00:00", "ENTRADA")), new Saida("id"));
        var deOutroDia = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((987654, "2026-03-10 18:00:00", "ENTRADA"), (entrada, "2026-03-10 08:10:00", "ENTRADA"), (entrada, "2026-03-10 17:00:00", "SAIDA")), new Saida("id"));

        Assert.Equal("Cada marcacao so pode ser corrigida uma vez no mesmo pedido.", repetida);
        Assert.Equal("A marcacao a corrigir (idMarcacao) nao e deste dia.", deOutroDia);
    }

    [Theory]
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": "ENTRADA"}]""")] // entrada sem saída
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": "SAIDA"}, {"dataHora": "2026-03-10 17:00:00", "tipo": "ENTRADA"}]""")] // começa pela saída
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 09:00:00", "tipo": "ENTRADA"}]""")] // duas entradas
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 08:00:00", "tipo": "SAIDA"}]""")] // mesmo segundo
    [InlineData("""[{"dataHora": "2026-03-10 08:00:00.200", "tipo": "ENTRADA"}, {"dataHora": "2026-03-10 08:00:00.700", "tipo": "SAIDA"}]""")] // mesmo segundo depois de cortar a fração
    public async Task O_pedido_que_nao_fecha_o_dia_em_pares_de_entrada_e_saida_e_recusado(string itens)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, itens, new Saida("id"));

        Assert.Equal("Com estas marcacoes o dia nao fecha em pares de ENTRADA e SAIDA. Confira os horarios.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM PONTOS) + (SELECT COUNT(*) FROM PONTOS_MANUTENCOES)"));
    }

    [Fact]
    public async Task A_sequencia_e_conferida_com_as_batidas_que_o_dia_ja_tem()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ponto = await DiaDoPontoAsync(conexao, pessoa, Dia);
        var entrada = await BatidaAsync(conexao, ponto, Em(10, 8, 0), "ENTRADA");
        const string NaoFecha = "Com estas marcacoes o dia nao fecha em pares de ENTRADA e SAIDA. Confira os horarios.";

        // O par que sozinho fecharia, somado à entrada aberta do dia, deixa três batidas.
        Assert.Equal(NaoFecha, await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((null, "2026-03-10 13:00:00", "ENTRADA"), (null, "2026-03-10 17:00:00", "SAIDA")), new Saida("id")));

        // A saída no mesmo segundo da entrada que já existe.
        Assert.Equal(NaoFecha, await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((null, "2026-03-10 08:00:00", "SAIDA")), new Saida("id")));

        // A saída antes da entrada que já existe.
        Assert.Equal(NaoFecha, await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((null, "2026-03-10 07:00:00", "SAIDA")), new Saida("id")));

        // Corrigir a entrada para depois da saída pedida.
        Assert.Equal(NaoFecha, await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, Itens((entrada, "2026-03-10 18:00:00", "ENTRADA"), (null, "2026-03-10 17:00:00", "SAIDA")), new Saida("id")));

        // Só a saída que faltava: fecha. E a batida corrigida sai do horário antigo (08:00 fica livre para a saída de outro par).
        var pedido = await PedirAsync(conexao, Agora, pessoa, Dia, Justificativa, Itens((entrada, "2026-03-10 07:00:00", "ENTRADA"), (null, "2026-03-10 08:00:00", "SAIDA")));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MANUTENCOES_ITENS WHERE ID_MANUTENCAO_PONTO = {pedido}"));
    }

    [Fact]
    public async Task Se_a_gravacao_dos_itens_falha_o_dia_e_o_pedido_tambem_sao_desfeitos()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, "CREATE TRIGGER TRG_TESTE_FALHA BEFORE INSERT ON PONTOS_MANUTENCOES_ITENS FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Falha de teste.'");

        var mensagem = await RecusaEmAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, DiaInteiro(), new Saida("id"));

        Assert.Equal("Falha de teste.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM PONTOS) + (SELECT COUNT(*) FROM PONTOS_MANUTENCOES)"));
    }

    [Fact]
    public async Task Dois_pedidos_simultaneos_do_mesmo_dia_se_enfileiram_e_so_um_entra()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var resultados = await ComAPessoaTravadaAsync(
            conexao,
            pessoa,
            TimeSpan.FromMilliseconds(700),
            () => TentarAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, DiaInteiro(), new Saida("id")),
            () => TentarAsync(conexao, Agora, Solicitar, pessoa, Dia, Justificativa, DiaInteiro(), new Saida("id")));

        Assert.Single(resultados, r => r is null);
        Assert.Single(resultados, r => r == "45000:Este dia ja tem um pedido de ajuste em analise.");
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES_ITENS"));
    }

    [Fact]
    public async Task O_lancamento_manual_de_banco_de_horas_nao_existe_mais()
    {
        var conexao = await NovoBancoAsync(mysql);

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_NAME = 'SP_LANCAR_BANCO_HORAS'"));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_NAME IN ('{Registrar}', '{Solicitar}')"));
    }

    // ================================================================ apoio

    /// <summary>Um instante de março de 2026, no relógio de Brasília (como o banco guarda).</summary>
    private static DateTime Em(int dia, int hora, int minuto) => new(2026, 3, dia, hora, minuto, 0);

    private static string Texto(DateTime instante) => instante.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    /// <summary>A lista JSON de <c>P_ITENS</c>, no formato que a procedure documenta.</summary>
    private static string Itens(params (int? Id, string DataHora, string Tipo)[] itens) =>
        JsonSerializer.Serialize(itens.Select(i => new Dictionary<string, object?> { ["idMarcacao"] = i.Id, ["dataHora"] = i.DataHora, ["tipo"] = i.Tipo }));

    /// <summary>Uma entrada às 08:00 e uma saída às 17:00 do dia: o pedido mais simples que fecha um dia sem batida.</summary>
    private static string DiaInteiro(DateOnly? dia = null)
    {
        var data = (dia ?? Dia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return Itens((null, $"{data} 08:00:00", "ENTRADA"), (null, $"{data} 17:00:00", "SAIDA"));
    }

    /// <summary>Uma sessão com o relógio parado em <paramref name="relogio"/> (o <c>NOW()</c> das procedures obedece); sem ele, o relógio de verdade.</summary>
    private static async Task<MySqlConnection> SessaoAsync(string conexao, DateTime? relogio)
    {
        var sessao = new MySqlConnection(conexao);
        await sessao.OpenAsync();
        if (relogio is { } instante)
        {
            await using var comando = new MySqlCommand($"SET timestamp = UNIX_TIMESTAMP('{Texto(instante)}')", sessao);
            await comando.ExecuteNonQueryAsync();
        }

        return sessao;
    }

    private static async Task<int> BaterAsync(string conexao, DateTime relogio, int pessoa, string tipo)
    {
        await using var sessao = await SessaoAsync(conexao, relogio);
        return Inteiro((await ChamarNaConexaoAsync(sessao, Registrar, pessoa, tipo, new Saida("id")))["id"]);
    }

    private static async Task<int> PedirAsync(string conexao, DateTime relogio, int pessoa, DateOnly dia, string justificativa, string itens)
    {
        await using var sessao = await SessaoAsync(conexao, relogio);
        return Inteiro((await ChamarNaConexaoAsync(sessao, Solicitar, pessoa, dia, justificativa, itens, new Saida("id")))["id"]);
    }

    /// <summary>Chama a procedure com o relógio parado e exige a recusa de negócio (SQLSTATE 45000); devolve a mensagem.</summary>
    private static async Task<string> RecusaEmAsync(string conexao, DateTime relogio, string procedure, params object?[] argumentos)
    {
        await using var sessao = await SessaoAsync(conexao, relogio);
        var erro = await Assert.ThrowsAsync<MySqlException>(() => ChamarNaConexaoAsync(sessao, procedure, argumentos));
        Assert.Equal("45000", erro.SqlState);
        return erro.Message;
    }

    /// <summary>Chama a procedure e devolve <c>null</c> se ela gravou, ou <c>SQLSTATE:mensagem</c> se o banco recusou (as chamadas simultâneas: uma grava, a outra é recusada).</summary>
    private static async Task<string?> TentarAsync(string conexao, DateTime? relogio, string procedure, params object?[] argumentos)
    {
        await using var sessao = await SessaoAsync(conexao, relogio);
        try
        {
            await ChamarNaConexaoAsync(sessao, procedure, argumentos);
            return null;
        }
        catch (MySqlException erro)
        {
            return $"{erro.SqlState}:{erro.Message}";
        }
    }

    /// <summary>
    /// Trava a linha da pessoa (como a API e as procedures fazem), dispara as chamadas, que ficam esperando a trava, e a solta depois de <paramref name="espera"/>: assim as chamadas disputam a trava de verdade, e não uma depois da outra por acaso.
    /// </summary>
    private static async Task<string?[]> ComAPessoaTravadaAsync(string conexao, int pessoa, TimeSpan espera, params Func<Task<string?>>[] chamadas)
    {
        await using var trava = new MySqlConnection(conexao);
        await trava.OpenAsync();
        await using var transacao = await trava.BeginTransactionAsync();
        await using (var travar = new MySqlCommand($"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} FOR UPDATE", trava, transacao))
        {
            await travar.ExecuteScalarAsync();
        }

        var emAndamento = chamadas.Select(chamada => Task.Run(chamada)).ToArray();
        await Task.Delay(espera);
        Assert.All(emAndamento, tarefa => Assert.False(tarefa.IsCompleted, "A chamada não esperou a trava da pessoa."));
        await transacao.CommitAsync();
        return await Task.WhenAll(emAndamento);
    }

    /// <summary>O registro do dia (<c>PONTOS</c>) criado direto no banco; devolve o <c>ID_PONTO</c>.</summary>
    private static async Task<int> DiaDoPontoAsync(string conexao, int pessoa, DateOnly dia)
    {
        var data = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO PONTOS (ID_FUNCIONARIO, DATA) VALUES ({pessoa}, '{data}')");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_PONTO FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa} AND DATA = '{data}'");
    }

    /// <summary>Uma batida gravada direto no banco; devolve o <c>ID_MARCACAO</c>.</summary>
    private static async Task<int> BatidaAsync(string conexao, int ponto, DateTime instante, string tipo)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO PONTOS_MARCACOES (ID_PONTO, DATA_HORA_MARCACAO, TIPO_MARCACAO) VALUES ({ponto}, '{Texto(instante)}', '{tipo}')");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_MARCACAO FROM PONTOS_MARCACOES WHERE ID_PONTO = {ponto} AND DATA_HORA_MARCACAO = '{Texto(instante)}'");
    }

    /// <summary>Um setor com gestor (o chefe, de outro setor) e uma pessoa lotada nele. Devolve a pessoa, o <c>ID_GESTOR</c> do setor e o funcionário que é o chefe.</summary>
    private static async Task<(int Pessoa, int Gestor, int Chefe)> EquipeAsync(string conexao)
    {
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor");
        var gestor = await GestorAsync(conexao, chefe);
        var setor = await SetorAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {setor}");
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor} WHERE ID_FUNCIONARIO = {chefe}");
        var pessoa = await FuncionarioAsync(conexao, setorId: setor);
        return (pessoa, gestor, chefe);
    }

    private static Task<List<string>> EtapaAsync(string conexao, int pedido) =>
        LinhasAsync(conexao, "STATUS_SOLICITACAO, ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ETAPA_GESTOR_AUTOMATICA", $"FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido}");

    /// <summary>As linhas da consulta, cada uma com as <paramref name="colunas"/> (separadas por vírgula) juntas por <c>|</c>; nulo do banco vira <c>nulo</c>.</summary>
    private static Task<List<string>> LinhasAsync(string conexao, string colunas, string resto) =>
        Sql.ColunaAsync(conexao, $"SELECT CONCAT_WS('|', {string.Join(", ", colunas.Split(',').Select(c => $"IFNULL({c.Trim()}, 'nulo')"))}) {resto}");
}

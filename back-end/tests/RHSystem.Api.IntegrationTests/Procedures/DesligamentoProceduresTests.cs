using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ApoioDaParteD;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de desligamento do <c>V007</c> (parte D): <c>SP_AGENDAR_DESLIGAMENTO</c>, <c>SP_CANCELAR_DESLIGAMENTO</c> e <c>SP_EFETIVAR_DESLIGAMENTO</c>, num MySQL 8.4 de verdade. "Hoje" é o <c>CURDATE()</c> do banco; a consolidação do banco de horas é a tarefa <c>CONSOLIDAR_BANCO_HORAS</c> concluída em
/// <c>TAREFAS_AGENDADAS</c>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DesligamentoProceduresTests(MySqlFixture mysql)
{
    private const string Agendar = "SP_AGENDAR_DESLIGAMENTO";
    private const string Cancelar = "SP_CANCELAR_DESLIGAMENTO";
    private const string Efetivar = "SP_EFETIVAR_DESLIGAMENTO";
    private const string JaTem = "Este funcionario ja tem um desligamento registrado.";
    private const string SoAgendadoCancela = "Nao ha desligamento agendado para cancelar: so o desligamento AGENDADO pode ser cancelado.";
    private const string NaoHaOQueEfetivar = "Desligamento cancelado ou revertido: nao ha o que efetivar.";
    private const string SoNoDiaSeguinte = "O desligamento so pode ser efetivado a partir do dia seguinte ao ultimo dia de trabalho.";
    private const string SemConsolidacao = "O banco de horas do ultimo dia de trabalho ainda nao foi consolidado: aguarde a consolidacao.";

    private static async Task<int> AgendarAsync(string conexao, int pessoa, DateOnly ultimoDia) =>
        Inteiro((await ChamarAsync(conexao, Agendar, pessoa, ultimoDia, new Saida("id")))["id"]);

    /// <summary>O desligamento: situação, último dia e quais instantes estão preenchidos.</summary>
    private static Task<string?> DesligamentoAsync(string conexao, int id) =>
        TextoAsync(conexao, $"SELECT CONCAT_WS('|', SITUACAO, DATE_FORMAT(ULTIMO_DIA_TRABALHO, '%Y-%m-%d'), IF(EFETIVADO_EM IS NULL, 'sem efetivacao', 'efetivado'), IF(CANCELADO_EM IS NULL, 'sem cancelamento', 'cancelado'), IF(REVERTIDO_EM IS NULL, 'sem reversao', 'revertido')) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id}");

    /// <summary>A pessoa: ativa ou não, e a data de desligamento.</summary>
    private static Task<string?> PessoaAsync(string conexao, int pessoa) =>
        TextoAsync(conexao, $"SELECT CONCAT_WS('|', IF(ATIVO, 'ativa', 'inativa'), COALESCE(DATE_FORMAT(DATA_DESLIGAMENTO, '%Y-%m-%d'), 'sem desligamento')) FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}");

    private static Task<long> DesligamentosAsync(string conexao, int pessoa) =>
        Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {pessoa}");

    private static async Task<int> BeneficioAsync(string conexao)
    {
        var nome = $"Beneficio {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS (NOME_BENEFICIO) VALUES ('{nome}')");
        return await IdAsync(conexao, $"SELECT ID_BENEFICIO FROM BENEFICIOS WHERE NOME_BENEFICIO = '{nome}'");
    }

    /// <summary>A adesão da pessoa a um benefício novo: aprovada (e ativa), ou ainda pendente no RH.</summary>
    private static async Task<int> AdesaoAsync(string conexao, int pessoa, int rh, bool aprovada)
    {
        var beneficio = await BeneficioAsync(conexao);
        await Sql.ExecutarAsync(conexao, aprovada
            ? $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA) VALUES ({pessoa}, {beneficio}, TRUE, 'APROVADO', NOW(), {rh}, NOW())"
            : $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO) VALUES ({pessoa}, {beneficio}, FALSE, 'PENDENTE', NOW())");
        return await IdAsync(conexao, $"SELECT ID_BENEFICIO_FUNCIONARIOS FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ID_BENEFICIO = {beneficio}");
    }

    // ---------------------------------------------------------------- SP_AGENDAR_DESLIGAMENTO

    [Fact]
    public async Task Agendar_para_um_dia_futuro_guarda_o_ultimo_dia_e_a_pessoa_continua_ativa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var ultimoDia = await DiaAsync(conexao, 12);

        var id = await AgendarAsync(conexao, pessoa, ultimoDia);

        Assert.Equal($"AGENDADO|{Iso(ultimoDia)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id} AND ID_FUNCIONARIO = {pessoa} AND VIGENTE = 1 AND CRIADO_EM BETWEEN UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE AND UTC_TIMESTAMP(6)")); // esta tabela guarda UTC
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa)); // gravar a data em FUNCIONARIOS inativaria a pessoa na hora (trigger)
    }

    [Fact]
    public async Task Com_o_ultimo_dia_hoje_a_pessoa_fica_agendada_mesmo_com_o_banco_de_horas_de_hoje_ja_consolidado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await DiaAsync(conexao);
        await ConsolidarBancoDeHorasAsync(conexao, hoje);

        var id = await AgendarAsync(conexao, pessoa, hoje);

        Assert.Equal($"AGENDADO|{Iso(hoje)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id)); // no último dia a pessoa ainda trabalha
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
    }

    [Fact]
    public async Task Com_o_ultimo_dia_ja_passado_e_o_banco_de_horas_dele_consolidado_o_desligamento_e_efetivado_na_mesma_chamada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({pessoa}, '{new string('h', 60)}')");
        var ontem = await DiaAsync(conexao, -1);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        var id = await AgendarAsync(conexao, pessoa, ontem);

        Assert.Equal($"EFETIVADO|{Iso(ontem)}|efetivado|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, pessoa)); // a data é o último dia de trabalho, não hoje
        Assert.Equal(0L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ATIVO")); // TRG_FUNCIONARIOS_AFTER_UPDATE desativou o login
    }

    [Fact]
    public async Task A_efetivacao_na_mesma_chamada_do_agendamento_faz_tudo_o_que_a_efetivacao_faz_pedidos_do_gestor_ao_RH_e_pedidos_de_beneficio_cancelados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var setor = await SetorAsync(conexao);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor", setorId: setor);
        var gestor = await GestorDoSetorAsync(conexao, chefe, setor);
        var pessoa = await FuncionarioAsync(conexao, setorId: setor, gestorId: gestor);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ID_GESTOR) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 30 DAY, CURDATE() + INTERVAL 39 DAY, {gestor})");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO PONTOS (ID_FUNCIONARIO, DATA) VALUES ({pessoa}, '2026-01-05')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO PONTOS_MANUTENCOES (ID_PONTO, ID_FUNCIONARIO, JUSTIFICATIVA, ID_GESTOR) SELECT ID_PONTO, ID_FUNCIONARIO, 'parado no gestor', {gestor} FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa}");
        var aprovada = await AdesaoAsync(conexao, chefe, rh, aprovada: true);
        var pendente = await AdesaoAsync(conexao, chefe, rh, aprovada: false);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO) VALUES ({aprovada}, 'Dependente pendente', 'FILHO', '2015-03-04')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TRAJETOS_VALE_TRANSPORTE (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS) VALUES ({aprovada}, 'Casa', 'Empresa', 'Onibus 10')");
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        var id = await AgendarAsync(conexao, chefe, ontem);

        Assert.Equal($"EFETIVADO|{Iso(ontem)}|efetivado|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, chefe));
        var efeitos = string.Join(
            "; ",
            "ferias da equipe " + await TextoAsync(conexao, $"SELECT CONCAT_WS(' com ', STATUS_FERIAS, COALESCE(ID_GESTOR, 'RH')) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa}"),
            "ajuste da equipe " + await TextoAsync(conexao, $"SELECT CONCAT_WS(' com ', STATUS_SOLICITACAO, COALESCE(ID_GESTOR, 'RH')) FROM PONTOS_MANUTENCOES WHERE ID_FUNCIONARIO = {pessoa}"),
            "dependente " + await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {aprovada}"),
            "trajeto " + await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {aprovada}"),
            "adesao pendente " + await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {pendente}"),
            "adesao aprovada " + await TextoAsync(conexao, $"SELECT CONCAT_WS(' ', IF(BENEFICIO_ATIVO, 'ativa', 'desativada'), STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {aprovada}"));
        Assert.Equal("ferias da equipe SOLICITADA com RH; ajuste da equipe PENDENTE com RH; dependente CANCELADO; trajeto CANCELADO; adesao pendente CANCELADO; adesao aprovada desativada APROVADO", efeitos);
    }

    [Fact]
    public async Task Com_o_ultimo_dia_ja_passado_mas_sem_o_banco_de_horas_dele_consolidado_o_desligamento_fica_agendado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var semConsolidacao = await FuncionarioAsync(conexao);
        var ontem = await DiaAsync(conexao, -1);

        var primeiro = await AgendarAsync(conexao, semConsolidacao, ontem);
        await ConsolidarBancoDeHorasAsync(conexao, ontem.AddDays(-1)); // a consolidação parou na véspera do último dia
        var atrasada = await FuncionarioAsync(conexao);
        var segundo = await AgendarAsync(conexao, atrasada, ontem);

        Assert.Equal($"AGENDADO|{Iso(ontem)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, primeiro));
        Assert.Equal($"AGENDADO|{Iso(ontem)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, segundo));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, semConsolidacao));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, atrasada));
    }

    [Fact]
    public async Task O_ultimo_dia_vai_da_admissao_ate_10_anos_a_frente_e_as_entradas_sao_obrigatorias()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao, admissao: "2025-01-06");
        var outra = await FuncionarioAsync(conexao, admissao: "2025-01-06");
        var limite = DateOnly.ParseExact(await TextoAsync(conexao, "SELECT DATE_FORMAT(CURDATE() + INTERVAL 10 YEAR, '%Y-%m-%d')") ?? string.Empty, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

        Assert.Equal("Informe o funcionario.", await RecusaDeNegocioAsync(conexao, Agendar, null, limite, new Saida("id")));
        Assert.Equal("Ultimo dia de trabalho e obrigatorio.", await RecusaDeNegocioAsync(conexao, Agendar, pessoa, null, new Saida("id")));
        Assert.Equal("Funcionario nao encontrado.", await RecusaDeNegocioAsync(conexao, Agendar, 999_999, limite, new Saida("id")));
        Assert.Equal("O ultimo dia de trabalho nao pode ser anterior a data de admissao.", await RecusaDeNegocioAsync(conexao, Agendar, pessoa, new DateOnly(2025, 1, 5), new Saida("id")));
        Assert.Equal("O ultimo dia de trabalho nao pode passar de 10 anos a partir de hoje.", await RecusaDeNegocioAsync(conexao, Agendar, pessoa, limite.AddDays(1), new Saida("id")));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS"));

        var noLimite = await AgendarAsync(conexao, pessoa, limite);
        var naAdmissao = await AgendarAsync(conexao, outra, new DateOnly(2025, 1, 6)); // dia do passado vale (sem a consolidação daquele dia, fica agendado)

        Assert.StartsWith($"AGENDADO|{Iso(limite)}|", await DesligamentoAsync(conexao, noLimite), StringComparison.Ordinal);
        Assert.StartsWith("AGENDADO|2025-01-06|", await DesligamentoAsync(conexao, naAdmissao), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pessoa_inativa_ou_que_ja_tem_desligamento_vigente_e_recusada_e_depois_de_cancelar_pode_agendar_de_novo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var inativa = await FuncionarioAsync(conexao, ativo: false);
        var pessoa = await FuncionarioAsync(conexao);
        var efetivada = await FuncionarioAsync(conexao);
        var ontem = await DiaAsync(conexao, -1);
        var futuro = await DiaAsync(conexao, 20);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);
        var agendado = await AgendarAsync(conexao, pessoa, futuro);
        _ = await AgendarAsync(conexao, efetivada, ontem);

        Assert.Equal("Este funcionario ja esta inativo.", await RecusaDeNegocioAsync(conexao, Agendar, inativa, futuro, new Saida("id")));
        Assert.Equal("Este funcionario ja esta inativo.", await RecusaDeNegocioAsync(conexao, Agendar, efetivada, futuro, new Saida("id"))); // já efetivado: quem volta é readmitido, pela API
        Assert.Equal(JaTem, await RecusaDeNegocioAsync(conexao, Agendar, pessoa, futuro.AddDays(3), new Saida("id")));
        Assert.Equal(1L, await DesligamentosAsync(conexao, pessoa));
        Assert.Equal(0L, await DesligamentosAsync(conexao, inativa));

        await ChamarAsync(conexao, Cancelar, agendado);
        var novo = await AgendarAsync(conexao, pessoa, futuro.AddDays(3));

        Assert.Equal($"CANCELADO|{Iso(futuro)}|sem efetivacao|cancelado|sem reversao", await DesligamentoAsync(conexao, agendado));
        Assert.Equal($"AGENDADO|{Iso(futuro.AddDays(3))}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, novo));
    }

    [Fact]
    public async Task Dois_agendamentos_ao_mesmo_tempo_para_a_mesma_pessoa_um_vale_e_o_outro_e_recusado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var futuro = await DiaAsync(conexao, 20);
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var pessoa = await FuncionarioAsync(conexao);

            var desfechos = await AoMesmoTempoAsync(
                () => TentarAsync(conexao, Agendar, pessoa, futuro, new Saida("id")),
                () => TentarAsync(conexao, Agendar, pessoa, futuro.AddDays(1), new Saida("id")));

            Assert.Single(desfechos, d => d.Passou);
            var recusa = Assert.Single(desfechos, d => !d.Passou);
            Assert.Equal(JaTem, recusa.Mensagem);
            Assert.Equal("45000", recusa.SqlState); // a recusa é a da regra, não a da chave única
            Assert.Equal(1L, await DesligamentosAsync(conexao, pessoa));
        }
    }

    [Fact]
    public async Task Pessoas_diferentes_agendadas_ao_mesmo_tempo_nao_se_atrapalham()
    {
        var conexao = await NovoBancoAsync(mysql);
        var futuro = await DiaAsync(conexao, 20);
        var pessoas = new List<int>();
        for (var i = 0; i < 8; i++)
        {
            pessoas.Add(await FuncionarioAsync(conexao));
        }

        var desfechos = await AoMesmoTempoAsync([.. pessoas.Select(pessoa => (Func<Task<Desfecho>>)(() => TentarAsync(conexao, Agendar, pessoa, futuro, new Saida("id"))))]);

        Assert.All(desfechos, d => Assert.True(d.Passou, d.Mensagem)); // sem deadlock: a leitura do desligamento vigente não segura bloqueio de intervalo
        Assert.Equal(8L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE SITUACAO = 'AGENDADO'"));
    }

    // ---------------------------------------------------------------- SP_CANCELAR_DESLIGAMENTO

    [Fact]
    public async Task Cancelar_o_desligamento_agendado_grava_o_instante_e_a_pessoa_continua_ativa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var futuro = await DiaAsync(conexao, 20);
        var id = await AgendarAsync(conexao, pessoa, futuro);

        await ChamarAsync(conexao, Cancelar, id);

        Assert.Equal($"CANCELADO|{Iso(futuro)}|sem efetivacao|cancelado|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id} AND VIGENTE IS NULL AND CANCELADO_EM BETWEEN UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE AND UTC_TIMESTAMP(6)"));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
    }

    [Fact]
    public async Task So_o_desligamento_agendado_e_cancelado_o_efetivado_e_o_ja_cancelado_sao_recusados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var efetivada = await FuncionarioAsync(conexao);
        var ontem = await DiaAsync(conexao, -1);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);
        var cancelado = await AgendarAsync(conexao, pessoa, await DiaAsync(conexao, 20));
        var efetivado = await AgendarAsync(conexao, efetivada, ontem);
        await ChamarAsync(conexao, Cancelar, cancelado);
        var antes = await TextoAsync(conexao, $"SELECT CANCELADO_EM FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {cancelado}");

        Assert.Equal("Informe o desligamento.", await RecusaDeNegocioAsync(conexao, Cancelar, [null]));
        Assert.Equal("Desligamento nao encontrado.", await RecusaDeNegocioAsync(conexao, Cancelar, 999_999));
        Assert.Equal(SoAgendadoCancela, await RecusaDeNegocioAsync(conexao, Cancelar, cancelado));
        Assert.Equal(SoAgendadoCancela, await RecusaDeNegocioAsync(conexao, Cancelar, efetivado));

        Assert.Equal(antes, await TextoAsync(conexao, $"SELECT CANCELADO_EM FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {cancelado}"));
        Assert.Equal($"EFETIVADO|{Iso(ontem)}|efetivado|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, efetivado));
        Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, efetivada));
    }

    [Fact]
    public async Task Cancelar_e_efetivar_ao_mesmo_tempo_terminam_coerentes_nunca_cancelado_com_a_pessoa_inativa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var pessoa = await FuncionarioAsync(conexao);
            var id = await AgendarAsync(conexao, pessoa, ontem); // sem consolidação: fica agendado
            await Sql.ExecutarAsync(conexao, "DELETE FROM TAREFAS_AGENDADAS");
            await ConsolidarBancoDeHorasAsync(conexao, ontem); // agora a efetivação já pode acontecer

            var desfechos = await AoMesmoTempoAsync(() => TentarAsync(conexao, Cancelar, id), () => TentarAsync(conexao, Efetivar, id));

            Assert.Single(desfechos, d => d.Passou);
            if (desfechos[0].Passou)
            {
                Assert.Equal(NaoHaOQueEfetivar, desfechos[1].Mensagem);
                Assert.StartsWith("CANCELADO|", await DesligamentoAsync(conexao, id), StringComparison.Ordinal);
                Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
            }
            else
            {
                Assert.Equal(SoAgendadoCancela, desfechos[0].Mensagem);
                Assert.StartsWith("EFETIVADO|", await DesligamentoAsync(conexao, id), StringComparison.Ordinal);
                Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, pessoa));
            }

            await Sql.ExecutarAsync(conexao, "DELETE FROM TAREFAS_AGENDADAS"); // a próxima rodada agenda de novo sem efetivar na hora
        }
    }

    // ---------------------------------------------------------------- SP_EFETIVAR_DESLIGAMENTO

    [Fact]
    public async Task Efetivar_no_dia_seguinte_com_o_banco_de_horas_consolidado_inativa_a_pessoa_com_a_data_do_ultimo_dia_e_o_banco_faz_o_resto()
    {
        var conexao = await NovoBancoAsync(mysql);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var pessoa = await FuncionarioAsync(conexao);
        var anteontem = await DiaAsync(conexao, -2);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({pessoa}, '{new string('h', 60)}')");
        var adesaoAtiva = await AdesaoAsync(conexao, pessoa, rh, aprovada: true);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 30 DAY, CURDATE() + INTERVAL 39 DAY)");
        var id = await AgendarAsync(conexao, pessoa, anteontem);
        await ConsolidarBancoDeHorasAsync(conexao, anteontem.AddDays(1)); // a consolidação de um dia DEPOIS do último também libera

        await ChamarAsync(conexao, Efetivar, id);

        Assert.Equal($"EFETIVADO|{Iso(anteontem)}|efetivado|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id} AND VIGENTE = 1 AND EFETIVADO_EM BETWEEN UTC_TIMESTAMP(6) - INTERVAL 2 MINUTE AND UTC_TIMESTAMP(6)"));
        Assert.Equal($"inativa|{Iso(anteontem)}", await PessoaAsync(conexao, pessoa)); // a antiga punha a data certa, mas no próprio último dia
        Assert.Equal(0L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ATIVO"));
        Assert.Equal("0|APROVADO", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', BENEFICIO_ATIVO, STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoAtiva}"));
        Assert.Equal("CANCELADA", await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Ao_efetivar_o_desligamento_de_um_gestor_os_pedidos_parados_na_etapa_dele_passam_para_o_RH()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var setor = await SetorAsync(conexao);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor", setorId: setor);
        var gestor = await GestorDoSetorAsync(conexao, chefe, setor);
        var outroGestor = await GestorAsync(conexao, await FuncionarioAsync(conexao, nivel: "Funcionario Gestor"));
        var pessoa = await FuncionarioAsync(conexao, setorId: setor, gestorId: gestor);
        const string ferias = "INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ID_GESTOR";
        await Sql.ExecutarAsync(conexao, $"{ferias}, OBSERVACAO) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 30 DAY, CURDATE() + INTERVAL 39 DAY, {gestor}, 'parado no gestor')");
        await Sql.ExecutarAsync(conexao, $"{ferias}, OBSERVACAO, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, SUGESTAO_DATA_INICIO, SUGESTAO_DATA_FIM) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 60 DAY, CURDATE() + INTERVAL 69 DAY, {gestor}, 'espera a pessoa', 'SUGERIU_DATA', NOW(), CURDATE() + INTERVAL 90 DAY, CURDATE() + INTERVAL 99 DAY)");
        await Sql.ExecutarAsync(conexao, $"{ferias}, OBSERVACAO, RESULTADO_GESTOR, DATA_ETAPA_GESTOR) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 120 DAY, CURDATE() + INTERVAL 129 DAY, {gestor}, 'gestor ja aprovou', 'APROVOU', NOW())");
        await Sql.ExecutarAsync(conexao, $"{ferias}, OBSERVACAO) VALUES ({pessoa}, '2025-01-06', '2026-01-05', CURDATE() + INTERVAL 150 DAY, CURDATE() + INTERVAL 159 DAY, {outroGestor}, 'de outro gestor')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO PONTOS (ID_FUNCIONARIO, DATA) VALUES ({pessoa}, '2026-01-05'), ({pessoa}, '2026-01-06'), ({pessoa}, '2026-01-07')");
        const string ajuste = "INSERT INTO PONTOS_MANUTENCOES (ID_PONTO, ID_FUNCIONARIO, JUSTIFICATIVA, ID_GESTOR)";
        await Sql.ExecutarAsync(conexao, $"{ajuste} SELECT ID_PONTO, ID_FUNCIONARIO, 'parado no gestor', {gestor} FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa} AND DATA = '2026-01-05'");
        await Sql.ExecutarAsync(conexao, $"{ajuste} SELECT ID_PONTO, ID_FUNCIONARIO, 'gestor ja aprovou', {gestor} FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa} AND DATA = '2026-01-06'");
        await Sql.ExecutarAsync(conexao, "UPDATE PONTOS_MANUTENCOES SET RESULTADO_GESTOR = 'APROVOU', DATA_ETAPA_GESTOR = NOW() WHERE JUSTIFICATIVA = 'gestor ja aprovou'");
        await Sql.ExecutarAsync(conexao, $"{ajuste} SELECT ID_PONTO, ID_FUNCIONARIO, 'de outro gestor', {outroGestor} FROM PONTOS WHERE ID_FUNCIONARIO = {pessoa} AND DATA = '2026-01-07'");
        var id = await AgendarAsync(conexao, chefe, ontem);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        await ChamarAsync(conexao, Efetivar, id);

        Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, chefe));
        Assert.Equal(
            [$"de outro gestor={outroGestor}", $"espera a pessoa={gestor}", $"gestor ja aprovou={gestor}", "parado no gestor=RH"],
            await Sql.ColunaAsync(conexao, $"SELECT CONCAT(OBSERVACAO, '=', COALESCE(ID_GESTOR, 'RH')) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa} ORDER BY OBSERVACAO"));
        Assert.Equal(
            [$"de outro gestor={outroGestor}", $"gestor ja aprovou={gestor}", "parado no gestor=RH"],
            await Sql.ColunaAsync(conexao, $"SELECT CONCAT(JUSTIFICATIVA, '=', COALESCE(ID_GESTOR, 'RH')) FROM PONTOS_MANUTENCOES WHERE ID_FUNCIONARIO = {pessoa} ORDER BY JUSTIFICATIVA"));
        Assert.Equal(4L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa} AND STATUS_FERIAS = 'SOLICITADA'")); // os pedidos da equipe continuam em aberto
        Assert.Equal(3L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM PONTOS_MANUTENCOES WHERE ID_FUNCIONARIO = {pessoa} AND STATUS_SOLICITACAO = 'PENDENTE'"));
    }

    [Fact]
    public async Task Ao_efetivar_os_pedidos_de_beneficio_da_pessoa_que_esperavam_o_RH_sao_cancelados_e_os_dos_outros_ficam()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var pessoa = await FuncionarioAsync(conexao);
        var colega = await FuncionarioAsync(conexao);
        var aprovada = await AdesaoAsync(conexao, pessoa, rh, aprovada: true);
        var pendente = await AdesaoAsync(conexao, pessoa, rh, aprovada: false);
        var doColega = await AdesaoAsync(conexao, colega, rh, aprovada: false);
        var aprovadaDoColega = await AdesaoAsync(conexao, colega, rh, aprovada: true);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA) VALUES ({aprovada}, 'Dependente pendente', 'FILHO', '2015-03-04', 'PENDENTE', NULL, NULL), ({aprovada}, 'Dependente aprovado', 'FILHO', '2016-03-04', 'APROVADO', {rh}, NOW()), ({aprovadaDoColega}, 'Dependente do colega', 'FILHO', '2017-03-04', 'PENDENTE', NULL, NULL)");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TRAJETOS_VALE_TRANSPORTE (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS) VALUES ({aprovada}, 'Trajeto da pessoa', 'Empresa', 'Onibus 10'), ({aprovadaDoColega}, 'Trajeto do colega', 'Empresa', 'Onibus 20')");
        var id = await AgendarAsync(conexao, pessoa, ontem);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        await ChamarAsync(conexao, Efetivar, id);

        Assert.Equal("0|APROVADO", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', BENEFICIO_ATIVO, STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {aprovada}")); // o trigger só desativa a adesão ativa
        Assert.Equal("0|CANCELADO|sem resposta", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', BENEFICIO_ATIVO, STATUS_PEDIDO, COALESCE(DATA_RESPOSTA, 'sem resposta')) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {pendente}")); // a procedure cancela o pedido pendente, sem resposta
        Assert.Equal("0|PENDENTE", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', BENEFICIO_ATIVO, STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {doColega}"));
        Assert.Equal(
            ["Dependente aprovado=APROVADO", "Dependente do colega=PENDENTE", "Dependente pendente=CANCELADO"],
            await Sql.ColunaAsync(conexao, "SELECT CONCAT(NOME, '=', STATUS_PEDIDO) FROM DEPENDENTES ORDER BY NOME"));
        Assert.Equal(
            ["Trajeto da pessoa=CANCELADO", "Trajeto do colega=PENDENTE"],
            await Sql.ColunaAsync(conexao, "SELECT CONCAT(ORIGEM, '=', STATUS_PEDIDO) FROM TRAJETOS_VALE_TRANSPORTE ORDER BY ORIGEM"));
    }

    [Fact]
    public async Task No_proprio_ultimo_dia_ou_sem_o_banco_de_horas_dele_consolidado_a_efetivacao_e_recusada_e_a_pessoa_continua_ativa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = await DiaAsync(conexao);
        var saiHoje = await FuncionarioAsync(conexao);
        var saiuOntem = await FuncionarioAsync(conexao);
        var noUltimoDia = await AgendarAsync(conexao, saiHoje, hoje);
        var semConsolidacao = await AgendarAsync(conexao, saiuOntem, hoje.AddDays(-1));

        Assert.Equal("Informe o desligamento.", await RecusaDeNegocioAsync(conexao, Efetivar, [null]));
        Assert.Equal("Desligamento nao encontrado.", await RecusaDeNegocioAsync(conexao, Efetivar, 999_999));
        Assert.Equal(SemConsolidacao, await RecusaDeNegocioAsync(conexao, Efetivar, semConsolidacao)); // nenhuma consolidação concluída
        await ConsolidarBancoDeHorasAsync(conexao, hoje.AddDays(-2));
        Assert.Equal(SemConsolidacao, await RecusaDeNegocioAsync(conexao, Efetivar, semConsolidacao)); // a consolidação parou na véspera do último dia
        await ConsolidarBancoDeHorasAsync(conexao, hoje);
        Assert.Equal(SoNoDiaSeguinte, await RecusaDeNegocioAsync(conexao, Efetivar, noUltimoDia)); // a antiga efetivava no próprio último dia

        Assert.Equal($"AGENDADO|{Iso(hoje)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, noUltimoDia));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, saiHoje));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, saiuOntem));
    }

    [Fact]
    public async Task Uma_tarefa_de_consolidacao_que_nao_concluiu_ou_de_outro_tipo_nao_libera_a_efetivacao()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var pessoa = await FuncionarioAsync(conexao);
        var id = await AgendarAsync(conexao, pessoa, ontem);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, DIA_REFERENCIA, SITUACAO) VALUES ('CONSOLIDAR_BANCO_HORAS', 'CONSOLIDAR_BANCO_HORAS:dia={Iso(ontem)}', '{Iso(ontem)}', 'FALHOU')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, DIA_REFERENCIA, SITUACAO, CONCLUIDA_EM) VALUES ('PUBLICAR_AVISO', 'PUBLICAR_AVISO:dia={Iso(ontem)}', '{Iso(ontem)}', 'CONCLUIDA', UTC_TIMESTAMP(6))");

        Assert.Equal(SemConsolidacao, await RecusaDeNegocioAsync(conexao, Efetivar, id));

        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
    }

    [Fact]
    public async Task Efetivar_o_que_ja_esta_efetivado_termina_sem_erro_e_sem_mudar_nada_e_o_cancelado_e_recusado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var pessoa = await FuncionarioAsync(conexao);
        var desistiu = await FuncionarioAsync(conexao);
        var id = await AgendarAsync(conexao, pessoa, ontem);
        var cancelado = await AgendarAsync(conexao, desistiu, ontem);
        await ChamarAsync(conexao, Cancelar, cancelado);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);
        await ChamarAsync(conexao, Efetivar, id);
        var instante = await TextoAsync(conexao, $"SELECT EFETIVADO_EM FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id}");

        await ChamarAsync(conexao, Efetivar, id); // a tarefa da API pode rodar duas vezes: a antiga recusava

        Assert.Equal(instante, await TextoAsync(conexao, $"SELECT EFETIVADO_EM FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {id}"));
        Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, pessoa));
        Assert.Equal(NaoHaOQueEfetivar, await RecusaDeNegocioAsync(conexao, Efetivar, cancelado));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, desistiu));
    }

    [Fact]
    public async Task Duas_efetivacoes_ao_mesmo_tempo_tem_um_efeito_so_e_nenhuma_falha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var pessoas = new List<(int Pessoa, int Id)>();
        for (var i = 0; i < 6; i++)
        {
            var pessoa = await FuncionarioAsync(conexao);
            pessoas.Add((pessoa, await AgendarAsync(conexao, pessoa, ontem)));
        }

        await ConsolidarBancoDeHorasAsync(conexao, ontem);
        foreach (var (pessoa, id) in pessoas)
        {
            var desfechos = await AoMesmoTempoAsync(() => TentarAsync(conexao, Efetivar, id), () => TentarAsync(conexao, Efetivar, id));

            Assert.All(desfechos, d => Assert.True(d.Passou, d.Mensagem));
            Assert.StartsWith("EFETIVADO|", await DesligamentoAsync(conexao, id), StringComparison.Ordinal);
            Assert.Equal($"inativa|{Iso(ontem)}", await PessoaAsync(conexao, pessoa));
        }
    }

    [Fact]
    public async Task Se_a_efetivacao_falha_no_meio_nada_fica_gravado_a_pessoa_continua_ativa_e_o_desligamento_agendado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var pessoa = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({pessoa}, '{new string('h', 60)}')");
        var adesao = await AdesaoAsync(conexao, pessoa, rh, aprovada: true);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO) VALUES ({adesao}, 'Dependente pendente', 'FILHO', '2015-03-04')");
        await Sql.ExecutarAsync(conexao, "CREATE TRIGGER TRG_TESTE_DEPENDENTES_FALHA BEFORE UPDATE ON DEPENDENTES FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Falha de teste no meio da efetivacao.'"); // só deste teste: a efetivação já gravou a pessoa e o desligamento quando chega aqui
        var id = await AgendarAsync(conexao, pessoa, ontem);
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        Assert.Equal("Falha de teste no meio da efetivacao.", await RecusaAsync(conexao, Efetivar, id));

        Assert.Equal($"AGENDADO|{Iso(ontem)}|sem efetivacao|sem cancelamento|sem reversao", await DesligamentoAsync(conexao, id));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ATIVO"));
        Assert.Equal("1|APROVADO", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', BENEFICIO_ATIVO, STATUS_PEDIDO) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao}"));
    }

    [Fact]
    public async Task Se_a_efetivacao_na_hora_do_agendamento_falha_nem_o_agendamento_fica_gravado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ontem = await DiaAsync(conexao, -1);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var pessoa = await FuncionarioAsync(conexao);
        var adesao = await AdesaoAsync(conexao, pessoa, rh, aprovada: true);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO) VALUES ({adesao}, 'Dependente pendente', 'FILHO', '2015-03-04')");
        await Sql.ExecutarAsync(conexao, "CREATE TRIGGER TRG_TESTE_DEPENDENTES_FALHA BEFORE UPDATE ON DEPENDENTES FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Falha de teste no meio da efetivacao.'");
        await ConsolidarBancoDeHorasAsync(conexao, ontem);

        Assert.Equal("Falha de teste no meio da efetivacao.", await RecusaAsync(conexao, Agendar, pessoa, ontem, new Saida("id")));

        Assert.Equal(0L, await DesligamentosAsync(conexao, pessoa));
        Assert.Equal("ativa|sem desligamento", await PessoaAsync(conexao, pessoa));
    }
}

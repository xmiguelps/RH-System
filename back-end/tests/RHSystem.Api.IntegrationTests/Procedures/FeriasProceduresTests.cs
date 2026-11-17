using System.Globalization;
using System.Text;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de férias do <c>V007</c> (seção B) num MySQL 8.4 de verdade: <c>SP_CONFERIR_FERIAS</c>, <c>SP_SOLICITAR_FERIAS</c>, <c>SP_REGISTRAR_FERIAS_RH</c>, <c>SP_DECIDIR_FERIAS_GESTOR</c>, <c>SP_RESPONDER_SUGESTAO_FERIAS</c>, <c>SP_CONFIRMAR_FERIAS_RH</c> e <c>SP_RECUSAR_FERIAS_RH</c>.
/// "Hoje" é o relógio do servidor (<c>CURDATE()</c>): todas as datas dos testes saem dele. Os feriados do <c>V001</c> são apagados no cenário, para o dia em que a suíte roda não mudar o resultado; o teste dos feriados cadastra os dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FeriasProceduresTests(MySqlFixture mysql)
{
    private const string NaoEncontradoNaEquipe = "Pedido de ferias nao encontrado na equipe deste gestor.";
    private const string NaoEncontradoDaPessoa = "Pedido de ferias nao encontrado para esta pessoa.";
    private const string JaDecidido = "Este pedido de ferias ja foi decidido.";
    private const string EtapaErradaDoGestor = "Pedido na etapa errada: ele nao esta esperando a decisao do gestor.";
    private const string EtapaErradaDoRh = "Pedido na etapa errada: ele ainda nao chegou ao RH (esta com o gestor ou esperando a pessoa).";
    private const string EtapaErradaDaSugestao = "Pedido na etapa errada: ele nao esta esperando a resposta a uma sugestao de data.";
    private const string Duracao = "A parte precisa ter entre 5 e 30 dias.";
    private const string Parte14 = "Depois deste pedido ainda precisa dar para ter uma parte de 14 dias seguidos.";
    private const string TresPartes = "O periodo aquisitivo ja tem 3 partes.";
    private const string Antecedencia = "Precisa comecar pelo menos 30 dias depois de hoje.";
    private const string Descanso = "Nao pode comecar em feriado, no fim de semana nem nos 2 dias antes deles.";
    private const string Sobreposicao = "Coincide com outras ferias da pessoa.";
    private const string SemPeriodo = "A pessoa ainda nao completou um ano de casa: nao ha periodo aquisitivo fechado.";
    private const string Inativo = "Funcionario inativo nao pode solicitar ferias.";
    private const string Abono = "O abono e de 1 a 10 dias, e so com o abono pecuniario marcado.";
    private const string Motivo = "O motivo da recusa tem de 5 a 500 caracteres.";
    private const string Proprio = "Pedido sem etapa do gestor: quem decide nao pode ser a propria pessoa do pedido.";
    private const string ForaDeGestores = "Pedido sem etapa do gestor: quem decide precisa estar cadastrado tambem em GESTORES.";

    /// <summary>
    /// Um setor com gestor (<c>Chefe</c>, linha <c>Gestor</c>), uma pessoa da equipe com um período aquisitivo fechado (30 dias de saldo), alguém do RH que também está em <c>GESTORES</c> (<c>Rh</c>, linha <c>GestorDoRh</c>) e alguém do RH que não está (<c>RhComum</c>).
    /// </summary>
    private sealed record Cenario(string Conexao, DateOnly Hoje, int Setor, int Chefe, int Gestor, int Pessoa, DateOnly Admissao, int Rh, int GestorDoRh, int RhComum)
    {
        /// <summary>O período aquisitivo fechado da pessoa.</summary>
        public DateOnly PeriodoInicio => Admissao;

        public DateOnly PeriodoFim => Admissao.AddYears(1).AddDays(-1);

        /// <summary>A primeira segunda-feira com a antecedência de 30 dias: o início padrão de um pedido que passa em todas as regras.</summary>
        public DateOnly Inicio => Segunda(Hoje.AddDays(30));
    }

    private async Task<Cenario> CenarioAsync(int diasDeCasa = 400)
    {
        var conexao = await NovoBancoAsync(mysql);
        await Sql.ExecutarAsync(conexao, "DELETE FROM FERIADOS");
        var hoje = DateOnly.FromDateTime(Convert.ToDateTime(await Sql.EscalarAsync(conexao, "SELECT CURDATE()"), CultureInfo.InvariantCulture));
        var setor = await SetorAsync(conexao);
        var chefe = await FuncionarioAsync(conexao, nome: "Chefe do Setor", admissao: "2015-03-02", nivel: "Funcionario Gestor", setorId: setor);
        var gestor = await GestorAsync(conexao, chefe);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {setor}");
        var admissao = hoje.AddDays(-diasDeCasa);
        var pessoa = await FuncionarioAsync(conexao, nome: "Pessoa da Equipe", admissao: Iso(admissao), setorId: setor);
        var rh = await FuncionarioAsync(conexao, nome: "RH que tambem e gestor", admissao: "2015-03-02", nivel: "RH");
        var gestorDoRh = await GestorAsync(conexao, rh);
        var rhComum = await FuncionarioAsync(conexao, nome: "RH fora de GESTORES", admissao: "2015-03-02", nivel: "RH");
        return new Cenario(conexao, hoje, setor, chefe, gestor, pessoa, admissao, rh, gestorDoRh, rhComum);
    }

    // ---------------------------------------------------------------- SP_SOLICITAR_FERIAS

    [Fact]
    public async Task Pedir_ferias_grava_o_pedido_SOLICITADA_no_periodo_aquisitivo_da_pessoa_e_esperando_o_gestor_do_setor()
    {
        var c = await CenarioAsync();

        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 10, abono: true, diasAbono: 5, adiantar13: true, mensagem: "  Viagem em família  ");

        await ConfereAsync(c.Conexao, id, $"""
            ID_FUNCIONARIO = {c.Pessoa}
            AND DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.PeriodoInicio)}' AND DATA_FIM_PERIODO_AQUISITIVO = '{Iso(c.PeriodoFim)}'
            AND DATA_INICIO = '{Iso(c.Inicio)}' AND DATA_FIM = '{Iso(c.Inicio.AddDays(9))}'
            AND ABONO_PECUNIARIO = TRUE AND DIAS_ABONO = 5 AND ADIANTAR_13_SALARIO = TRUE
            AND STATUS_FERIAS = 'SOLICITADA' AND {Agora("DATA_SOLICITACAO")} AND OBSERVACAO = BINARY 'Viagem em família'
            AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR IS NULL AND DATA_ETAPA_GESTOR IS NULL AND ETAPA_GESTOR_AUTOMATICA = FALSE
            AND ID_FUNCIONARIO_RH IS NULL AND DATA_CONFIRMACAO_RH IS NULL AND DATA_ANALISE IS NULL AND MOTIVO_RECUSA IS NULL
            AND SUGESTAO_DATA_INICIO IS NULL AND SUGESTAO_DATA_FIM IS NULL AND RESPOSTA_SUGESTAO IS NULL AND DATA_RESPOSTA_SUGESTAO IS NULL
            """);
        Assert.Equal(1L, await Sql.ContarAsync(c.Conexao, "SELECT COUNT(*) FROM FERIAS"));
    }

    [Fact]
    public async Task Com_o_gestor_ausente_o_pedido_ja_nasce_com_a_etapa_do_gestor_aprovada_sozinha_e_a_data_de_volta_desliga_a_ausencia()
    {
        var c = await CenarioAsync();

        await Sql.ExecutarAsync(c.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE WHERE ID_GESTOR = {c.Gestor}");
        var semVolta = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA_ATE = '{Iso(c.Hoje.AddDays(1))}' WHERE ID_GESTOR = {c.Gestor}");
        var voltaAmanha = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 5);
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA_ATE = '{Iso(c.Hoje)}' WHERE ID_GESTOR = {c.Gestor}");
        var voltouHoje = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(35), 5);

        const string Automatica = "STATUS_FERIAS = 'SOLICITADA' AND RESULTADO_GESTOR = 'APROVOU' AND ETAPA_GESTOR_AUTOMATICA = TRUE AND DATA_ETAPA_GESTOR = DATA_SOLICITACAO AND ID_FUNCIONARIO_RH IS NULL";
        await ConfereAsync(c.Conexao, semVolta, $"ID_GESTOR = {c.Gestor} AND {Automatica}");
        await ConfereAsync(c.Conexao, voltaAmanha, $"ID_GESTOR = {c.Gestor} AND {Automatica}");
        await ConfereAsync(c.Conexao, voltouHoje, $"ID_GESTOR = {c.Gestor} AND STATUS_FERIAS = 'SOLICITADA' AND RESULTADO_GESTOR IS NULL AND DATA_ETAPA_GESTOR IS NULL AND ETAPA_GESTOR_AUTOMATICA = FALSE");
    }

    [Fact]
    public async Task O_pedido_nasce_sem_etapa_do_gestor_para_quem_e_gestor_e_para_quem_nao_tem_gestor_ativo_no_setor()
    {
        var c = await CenarioAsync();
        var haMuito = Iso(c.Hoje.AddDays(-400));
        var setorSemGestor = await SetorAsync(c.Conexao);
        var semGestorNoSetor = await FuncionarioAsync(c.Conexao, admissao: haMuito, setorId: setorSemGestor);
        var semSetor = await FuncionarioAsync(c.Conexao, admissao: haMuito);
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = NULL WHERE ID_FUNCIONARIO = {semSetor}");
        var setorDoInativo = await SetorAsync(c.Conexao);
        var chefeInativo = await FuncionarioAsync(c.Conexao, admissao: haMuito, nivel: "Funcionario Gestor", setorId: setorDoInativo);
        var gestorInativo = await GestorAsync(c.Conexao, chefeInativo);
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE SETORES SET ID_GESTOR = {gestorInativo} WHERE ID_SETOR = {setorDoInativo}");
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {chefeInativo}");
        var comGestorInativo = await FuncionarioAsync(c.Conexao, admissao: haMuito, setorId: setorDoInativo);
        var rhEmGestoresNoSetor = await FuncionarioAsync(c.Conexao, admissao: haMuito, nivel: "RH", setorId: c.Setor);
        await GestorAsync(c.Conexao, rhEmGestoresNoSetor);
        var rhComumNoSetor = await FuncionarioAsync(c.Conexao, admissao: haMuito, nivel: "RH", setorId: c.Setor);
        var exGestorNoSetor = await FuncionarioAsync(c.Conexao, admissao: haMuito, setorId: c.Setor);
        await GestorAsync(c.Conexao, exGestorNoSetor); // tem linha em GESTORES, mas não gerencia setor nenhum nem é do RH: segue o gestor do setor

        foreach (var semEtapa in new[] { c.Chefe, semGestorNoSetor, semSetor, comGestorInativo, rhEmGestoresNoSetor })
        {
            var id = await SolicitarAsync(c, semEtapa, c.Inicio, 10);
            await ConfereAsync(c.Conexao, id, "ID_GESTOR IS NULL AND RESULTADO_GESTOR IS NULL AND DATA_ETAPA_GESTOR IS NULL AND ETAPA_GESTOR_AUTOMATICA = FALSE AND STATUS_FERIAS = 'SOLICITADA'");
        }

        foreach (var comEtapa in new[] { rhComumNoSetor, exGestorNoSetor })
        {
            var id = await SolicitarAsync(c, comEtapa, c.Inicio, 10);
            await ConfereAsync(c.Conexao, id, $"ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR IS NULL AND STATUS_FERIAS = 'SOLICITADA'");
        }
    }

    [Fact]
    public async Task Pedir_ferias_recusa_entradas_invalidas_sem_gravar_nada_e_a_mensagem_em_branco_fica_nula()
    {
        var c = await CenarioAsync();
        var fim = c.Inicio.AddDays(9);

        Assert.Equal("Informe a data de inicio e a data de fim das ferias.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, null, fim, false, 0, false, null, new Saida("id")));
        Assert.Equal("Informe a data de inicio e a data de fim das ferias.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, null, false, 0, false, null, new Saida("id")));
        Assert.Equal("Informe datas entre 2000 e 2100.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, new DateOnly(1999, 12, 20), new DateOnly(2000, 1, 5), false, 0, false, null, new Saida("id")));
        Assert.Equal("Informe datas entre 2000 e 2100.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, new DateOnly(2100, 12, 20), new DateOnly(2101, 1, 5), false, 0, false, null, new Saida("id")));
        Assert.Equal("A data final nao pode ser antes da inicial.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, c.Inicio.AddDays(-1), false, 0, false, null, new Saida("id")));
        Assert.Equal(Abono, await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, fim, false, 3, false, null, new Saida("id")));
        Assert.Equal(Abono, await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, fim, true, 0, false, null, new Saida("id")));
        Assert.Equal(Abono, await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, fim, true, 11, false, null, new Saida("id")));
        Assert.Equal("A mensagem pode ter ate 500 caracteres.", await RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, fim, false, 0, false, new string('x', 501), new Saida("id")));

        // 500 caracteres depois de aparar cabem; nulos no abono e no 13º valem "não".
        var id = await ChamarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, fim, null, null, null, $"  {new string('x', 500)} \n", new Saida("id"));
        await ConfereAsync(c.Conexao, Inteiro(id["id"]), "CHAR_LENGTH(OBSERVACAO) = 500 AND ABONO_PECUNIARIO = FALSE AND DIAS_ABONO = 0 AND ADIANTAR_13_SALARIO = FALSE");
        var emBranco = await ChamarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio.AddDays(21), c.Inicio.AddDays(34), false, 0, false, " \t ", new Saida("id"));
        await ConfereAsync(c.Conexao, Inteiro(emBranco["id"]), "OBSERVACAO IS NULL");
    }

    [Fact]
    public async Task Pedir_ferias_recusa_pessoa_que_nao_existe_e_pessoa_inativa_antes_de_olhar_as_regras()
    {
        var c = await CenarioAsync();

        Assert.Equal("Funcionario nao encontrado.", await RecusaAoSolicitarAsync(c, 999_999, c.Inicio, 10));

        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {c.Pessoa}");
        Assert.Equal(Inativo, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 10));
        Assert.Equal(Inativo, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(3), 3)); // 3 dias numa quinta: a pessoa inativa é recusada antes das regras
    }

    [Fact]
    public async Task So_ha_ferias_para_pedir_depois_de_um_ano_de_casa_completo()
    {
        var c = await CenarioAsync(diasDeCasa: 200);
        var faltaUmDia = await FuncionarioAsync(c.Conexao, admissao: Iso(c.Hoje.AddYears(-1).AddDays(1)), setorId: c.Setor); // o período termina hoje: ainda não fechou
        var completouOntem = await FuncionarioAsync(c.Conexao, admissao: Iso(c.Hoje.AddYears(-1)), setorId: c.Setor); // o período terminou ontem

        Assert.Equal(SemPeriodo, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 10));
        Assert.Equal(SemPeriodo, await RecusaAoSolicitarAsync(c, faltaUmDia, c.Inicio, 10));
        Assert.Equal(SemPeriodo, await RecusaAoRegistrarAsync(c, faltaUmDia, c.Inicio, 10));

        var id = await SolicitarAsync(c, completouOntem, c.Inicio, 10);
        await ConfereAsync(c.Conexao, id, $"DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.Hoje.AddYears(-1))}' AND DATA_FIM_PERIODO_AQUISITIVO = '{Iso(c.Hoje.AddDays(-1))}'");
    }

    [Fact]
    public async Task A_parte_tem_de_5_a_30_dias_e_o_gozo_mais_o_abono_tem_de_caber_no_saldo_contando_o_que_ja_esta_em_pedido()
    {
        var c = await CenarioAsync();

        Assert.Equal(Duracao, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 4));
        Assert.Equal(Duracao, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 31));
        Assert.Equal("Passa do saldo disponivel: falta 1 dia.", await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 30, abono: true, diasAbono: 1));
        Assert.Equal("Passa do saldo disponivel: faltam 5 dias.", await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 25, abono: true, diasAbono: 10));

        await SolicitarAsync(c, c.Pessoa, c.Inicio, 20, abono: true, diasAbono: 10); // 30 do saldo: 20 de gozo e 10 de abono

        // O que espera decisão já está reservado: não sobra nada, nem para o registro do RH.
        Assert.Equal("Passa do saldo disponivel: faltam 5 dias.", await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 5));
        Assert.Equal("Passa do saldo disponivel: faltam 14 dias.", await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 14));
    }

    [Fact]
    public async Task Uma_das_partes_do_periodo_precisa_ter_14_dias()
    {
        var c = await CenarioAsync();

        await SolicitarAsync(c, c.Pessoa, c.Inicio, 10); // sobram 20: ainda dá para uma parte de 14

        Assert.Equal(Parte14, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 10)); // sobrariam 10 e nenhuma parte teria 14
        Assert.Equal(Parte14, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 7)); // sobrariam 13

        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 6); // sobram 14 e ainda cabe uma terceira parte
        Assert.Equal(Parte14, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(42), 13)); // a terceira e última parte teria de ser a de 14
        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(42), 14);
    }

    [Fact]
    public async Task O_periodo_aquisitivo_tem_no_maximo_3_partes()
    {
        var c = await CenarioAsync();
        await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 5);
        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(35), 5);

        Assert.Equal(TresPartes, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(49), 5)); // ainda sobram 6 dias, mas seria a quarta parte
    }

    [Fact]
    public async Task O_pedido_da_pessoa_precisa_de_30_dias_de_antecedencia()
    {
        var c = await CenarioAsync();

        Assert.Equal(Antecedencia, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Hoje.AddDays(29), 10));
        Assert.Equal(Antecedencia, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Hoje, 10));

        // Com exatamente 30 dias a antecedência está cumprida: o que ainda pode recusar é o dia da semana (a regra seguinte).
        var noLimite = await RecusaOuNadaAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, c.Hoje.AddDays(30), c.Hoje.AddDays(39), false, 0, true, null, new Saida("inicio"), new Saida("fim"));
        Assert.True(noLimite is null or Descanso, noLimite);
    }

    [Fact]
    public async Task As_ferias_nao_comecam_na_quinta_na_sexta_nem_no_fim_de_semana()
    {
        var c = await CenarioAsync();

        foreach (var dia in new[] { 3, 4, 5, 6 })
        {
            Assert.Equal(Descanso, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(dia), 10)); // quinta, sexta, sábado e domingo
        }

        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(2), 10); // quarta: o sábado está a 3 dias
    }

    [Fact]
    public async Task As_ferias_nao_comecam_em_feriado_nem_nos_2_dias_antes_e_so_contam_os_feriados_ativos_que_valem_para_a_empresa()
    {
        var c = await CenarioAsync();
        var segunda = c.Inicio;

        async Task<string?> ConferirAsync() => await RecusaOuNadaAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, segunda, segunda.AddDays(9), false, 0, true, null, new Saida("inicio"), new Saida("fim"));

        Assert.Null(await ConferirAsync());

        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO) VALUES ('Tres dias depois', '{Iso(segunda.AddDays(3))}', 'NACIONAL')");
        Assert.Null(await ConferirAsync());

        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO) VALUES ('Dois dias depois', '{Iso(segunda.AddDays(2))}', 'NACIONAL')");
        Assert.Equal(Descanso, await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, "UPDATE FERIADOS SET ATIVO = FALSE WHERE NOME_FERIADO = 'Dois dias depois'");
        Assert.Null(await ConferirAsync());

        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO) VALUES ('Da empresa, no dia', '{Iso(segunda)}', 'EMPRESA')");
        Assert.Equal(Descanso, await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, "DELETE FROM FERIADOS WHERE NOME_FERIADO = 'Da empresa, no dia'");

        // Estadual e municipal só valem na UF e no município da empresa; sem a empresa configurada, não valem.
        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF) VALUES ('Estadual do RJ', '{Iso(segunda.AddDays(1))}', 'ESTADUAL', 'RJ')");
        Assert.Null(await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, "INSERT INTO CONFIGURACOES_EMPRESA (NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, DIA_PAGAMENTO) VALUES ('Empresa', 'SP', 'São Paulo', '08:00', '17:00', 5)");
        Assert.Null(await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, "UPDATE CONFIGURACOES_EMPRESA SET UF = 'RJ', MUNICIPIO = 'Niterói'");
        Assert.Equal(Descanso, await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, "DELETE FROM FERIADOS WHERE NOME_FERIADO = 'Estadual do RJ'");

        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF, MUNICIPIO) VALUES ('Municipal do Rio', '{Iso(segunda)}', 'MUNICIPAL', 'RJ', 'Rio de Janeiro')");
        Assert.Null(await ConferirAsync());
        await Sql.ExecutarAsync(c.Conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF, MUNICIPIO) VALUES ('Municipal de Niteroi', '{Iso(segunda)}', 'MUNICIPAL', 'RJ', 'niteroi')");
        Assert.Equal(Descanso, await ConferirAsync()); // o município se compara sem distinguir maiúscula nem acento, como na API
    }

    [Fact]
    public async Task As_ferias_nao_coincidem_com_outras_da_pessoa_mas_o_pedido_recusado_libera_as_datas()
    {
        var c = await CenarioAsync();
        var primeiro = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(7), 14);

        Assert.Equal(Sobreposicao, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(14), 14)); // a segunda semana do primeiro
        Assert.Equal(Sobreposicao, await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio, 8)); // termina no primeiro dia do primeiro
        Assert.Equal(Sobreposicao, await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio.AddDays(14), 14));

        await DecidirAsync(c, primeiro, "RECUSOU", "Equipe reduzida no periodo");
        await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(14), 14);
    }

    [Fact]
    public async Task O_pedido_entra_inteiro_no_periodo_aquisitivo_mais_antigo_em_que_cabe()
    {
        var c = await CenarioAsync(diasDeCasa: 800); // dois períodos fechados
        var segundoPeriodo = c.Admissao.AddYears(1);

        var vinte = await SolicitarAsync(c, c.Pessoa, c.Inicio, 20);
        var quatorze = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 14); // no primeiro só sobram 10: vai inteiro para o segundo
        var dez = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(49), 10); // cabe no que sobrou do primeiro

        await ConfereAsync(c.Conexao, vinte, $"DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.PeriodoInicio)}' AND DATA_FIM_PERIODO_AQUISITIVO = '{Iso(c.PeriodoFim)}'");
        await ConfereAsync(c.Conexao, quatorze, $"DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(segundoPeriodo)}' AND DATA_FIM_PERIODO_AQUISITIVO = '{Iso(c.Admissao.AddYears(2).AddDays(-1))}'");
        await ConfereAsync(c.Conexao, dez, $"DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.PeriodoInicio)}'");

        // O primeiro período está todo reservado e no segundo sobram 16: 17 dias não cabem em nenhum, e os números falam do período mais antigo que ainda não foi tirado (o primeiro), como na API.
        Assert.Equal("Passa do saldo disponivel: faltam 17 dias.", await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(70), 17));
    }

    [Fact]
    public async Task Dois_lancamentos_ao_mesmo_tempo_para_a_mesma_pessoa_entram_em_fila_e_so_um_grava()
    {
        var c = await CenarioAsync();

        var erros = await AoMesmoTempoComAPessoaTravadaAsync(
            c.Conexao,
            c.Pessoa,
            () => ChamarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio, c.Inicio.AddDays(19), false, 0, false, null, new Saida("id")),
            () => ChamarAsync(c.Conexao, "SP_REGISTRAR_FERIAS_RH", c.Pessoa, c.Inicio, c.Inicio.AddDays(19), false, 0, false, null, new Saida("id")),
            () => ChamarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", c.Pessoa, c.Inicio.AddDays(28), c.Inicio.AddDays(47), false, 0, false, null, new Saida("id")));

        // Cada um caberia sozinho (20 dias); juntos estourariam o saldo de 30 ou coincidiriam. Quem perdeu a fila foi recusado pela regra.
        Assert.Equal(2, erros.Count(e => e is not null));
        Assert.All(erros.Where(e => e is not null), e => Assert.True(e!.SqlState == "45000" && (e.Message == Sobreposicao || e.Message.StartsWith("Passa do saldo disponivel", StringComparison.Ordinal)), e.Message));
        Assert.Equal(1L, await Sql.ContarAsync(c.Conexao, "SELECT COUNT(*) FROM FERIAS"));
    }

    // ---------------------------------------------------------------- SP_REGISTRAR_FERIAS_RH

    [Fact]
    public async Task O_RH_registra_ferias_que_ja_nascem_APROVADA_sem_etapas_ate_no_passado_e_sem_antecedencia()
    {
        var c = await CenarioAsync();
        var passado = Segunda(c.Hoje.AddDays(-30)); // depois do fim do período aquisitivo e já começadas

        var id = await RegistrarAsync(c, c.Pessoa, passado, 10, abono: true, diasAbono: 3, adiantar13: true, observacao: " Acordado com a pessoa ");
        var semana = await RegistrarAsync(c, c.Pessoa, Segunda(c.Hoje.AddDays(1)), 14); // sem antecedência

        await ConfereAsync(c.Conexao, id, $"""
            ID_FUNCIONARIO = {c.Pessoa} AND STATUS_FERIAS = 'APROVADA'
            AND DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.PeriodoInicio)}' AND DATA_FIM_PERIODO_AQUISITIVO = '{Iso(c.PeriodoFim)}'
            AND DATA_INICIO = '{Iso(passado)}' AND DATA_FIM = '{Iso(passado.AddDays(9))}'
            AND ABONO_PECUNIARIO = TRUE AND DIAS_ABONO = 3 AND ADIANTAR_13_SALARIO = TRUE AND OBSERVACAO = 'Acordado com a pessoa'
            AND {Agora("DATA_SOLICITACAO")} AND DATA_ANALISE IS NULL
            AND ID_GESTOR IS NULL AND RESULTADO_GESTOR IS NULL AND DATA_ETAPA_GESTOR IS NULL AND ETAPA_GESTOR_AUTOMATICA = FALSE
            AND ID_FUNCIONARIO_RH IS NULL AND DATA_CONFIRMACAO_RH IS NULL AND MOTIVO_RECUSA IS NULL AND SUGESTAO_DATA_INICIO IS NULL
            """);
        await ConfereAsync(c.Conexao, semana, "STATUS_FERIAS = 'APROVADA' AND ID_GESTOR IS NULL");

        // As férias registradas já contam como tiradas: 13 + 14 = 27, sobram 3.
        Assert.Equal("Passa do saldo disponivel: faltam 2 dias.", await RecusaAoSolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 5));
    }

    [Fact]
    public async Task O_registro_do_RH_recusa_ferias_que_comecam_antes_do_fim_do_periodo_aquisitivo_pessoa_inativa_e_entradas_invalidas()
    {
        var c = await CenarioAsync();

        Assert.Equal(
            $"As ferias precisam comecar depois do fim do periodo aquisitivo ({c.PeriodoFim.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}).",
            await RecusaAoRegistrarAsync(c, c.Pessoa, c.PeriodoFim, 10));
        Assert.Equal(Duracao, await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio, 4));
        Assert.Equal(Descanso, await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio.AddDays(4), 10));
        Assert.Equal(Abono, await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio, 10, abono: true, diasAbono: 11));
        Assert.Equal("A observacao pode ter ate 500 caracteres.", await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio, 10, observacao: new string('x', 501)));
        Assert.Equal("A data final nao pode ser antes da inicial.", await RecusaSemGravarAsync(c.Conexao, "SP_REGISTRAR_FERIAS_RH", c.Pessoa, c.Inicio, c.Inicio.AddDays(-2), false, 0, false, null, new Saida("id")));
        Assert.Equal("Informe a data de inicio e a data de fim das ferias.", await RecusaSemGravarAsync(c.Conexao, "SP_REGISTRAR_FERIAS_RH", c.Pessoa, null, null, false, 0, false, null, new Saida("id")));
        Assert.Equal("Funcionario nao encontrado.", await RecusaAoRegistrarAsync(c, 999_999, c.Inicio, 10));

        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {c.Pessoa}");
        Assert.Equal(Inativo, await RecusaAoRegistrarAsync(c, c.Pessoa, c.Inicio, 10));
    }

    // ---------------------------------------------------------------- SP_DECIDIR_FERIAS_GESTOR

    [Fact]
    public async Task O_gestor_aprova_e_o_pedido_passa_ao_RH_ainda_SOLICITADA_sem_tocar_na_mensagem_da_pessoa()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");

        await DecidirAsync(c, id, "aprovou"); // o resultado vale em qualquer caixa e é gravado como o banco espera

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'SOLICITADA' AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = BINARY 'APROVOU' AND {Agora("DATA_ETAPA_GESTOR")} AND ETAPA_GESTOR_AUTOMATICA = FALSE
            AND OBSERVACAO = 'Mensagem da pessoa' AND MOTIVO_RECUSA IS NULL AND DATA_ANALISE IS NULL AND ID_FUNCIONARIO_RH IS NULL AND DATA_CONFIRMACAO_RH IS NULL
            AND SUGESTAO_DATA_INICIO IS NULL AND DATA_INICIO = '{Iso(c.Inicio)}' AND DATA_FIM = '{Iso(c.Inicio.AddDays(13))}'
            """);
    }

    [Fact]
    public async Task O_gestor_recusa_e_o_pedido_termina_REJEITADA_com_o_motivo_sem_tocar_na_mensagem_da_pessoa()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");

        await DecidirAsync(c, id, "RECUSOU", "  Equipe reduzida no periodo \n");

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'REJEITADA' AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = 'RECUSOU' AND {Agora("DATA_ETAPA_GESTOR")} AND {Agora("DATA_ANALISE")}
            AND MOTIVO_RECUSA = 'Equipe reduzida no periodo' AND OBSERVACAO = 'Mensagem da pessoa' AND ID_FUNCIONARIO_RH IS NULL AND DATA_CONFIRMACAO_RH IS NULL
            """);
        Assert.Equal(JaDecidido, await RecusaAoDecidirAsync(c, id, "APROVOU"));
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.Rh)); // a recusa do gestor encerra: o RH não confirma
    }

    [Fact]
    public async Task O_gestor_sugere_outra_data_sem_antecedencia_e_o_pedido_fica_esperando_a_resposta_da_pessoa()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");
        var sugerida = Segunda(c.Hoje.AddDays(1)); // amanhã ou depois: a sugestão não tem a antecedência de 30 dias

        await DecidirAsync(c, id, "SUGERIU_DATA", null, sugerida, sugerida.AddDays(9));

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'SOLICITADA' AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = 'SUGERIU_DATA' AND {Agora("DATA_ETAPA_GESTOR")}
            AND SUGESTAO_DATA_INICIO = '{Iso(sugerida)}' AND SUGESTAO_DATA_FIM = '{Iso(sugerida.AddDays(9))}' AND RESPOSTA_SUGESTAO IS NULL AND DATA_RESPOSTA_SUGESTAO IS NULL
            AND DATA_INICIO = '{Iso(c.Inicio)}' AND DATA_FIM = '{Iso(c.Inicio.AddDays(13))}' AND OBSERVACAO = 'Mensagem da pessoa' AND DATA_ANALISE IS NULL AND MOTIVO_RECUSA IS NULL
            """);

        // Ninguém decide enquanto a pessoa não responde.
        Assert.Equal(EtapaErradaDoGestor, await RecusaAoDecidirAsync(c, id, "APROVOU"));
        Assert.Equal(EtapaErradaDoRh, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.Rh));
        Assert.Equal(EtapaErradaDoRh, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.Rh, "Motivo qualquer"));
    }

    [Fact]
    public async Task A_sugestao_pode_coincidir_com_as_datas_do_proprio_pedido_mas_cumpre_as_outras_regras()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        var registradas = await RegistrarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 10);
        Assert.True(registradas > 0);

        Assert.Equal("A sugestao precisa comecar depois do fim do periodo aquisitivo do pedido.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.PeriodoFim, c.PeriodoFim.AddDays(9)));
        Assert.Equal("A sugestao nao pode comecar antes de hoje.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Hoje.AddDays(-1), c.Hoje.AddDays(8)));
        Assert.Equal(Duracao, await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio, c.Inicio.AddDays(3)));
        Assert.Equal("Passa do saldo disponivel: faltam 5 dias.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio, c.Inicio.AddDays(24))); // 25 + as 10 registradas
        Assert.Equal(Descanso, await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio.AddDays(3), c.Inicio.AddDays(16)));
        Assert.Equal(Sobreposicao, await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio.AddDays(21), c.Inicio.AddDays(34))); // encosta nas registradas pelo RH

        await DecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio.AddDays(7), c.Inicio.AddDays(20)); // a segunda semana do próprio pedido
        await ConfereAsync(c.Conexao, id, $"RESULTADO_GESTOR = 'SUGERIU_DATA' AND SUGESTAO_DATA_INICIO = '{Iso(c.Inicio.AddDays(7))}'");
    }

    [Fact]
    public async Task A_decisao_do_gestor_recusa_entradas_invalidas_sem_gravar_nada()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        const string ResultadoInvalido = "Resultado do gestor invalido: use APROVOU, RECUSOU ou SUGERIU_DATA.";

        Assert.Equal(ResultadoInvalido, await RecusaAoDecidirAsync(c, id, "TALVEZ"));
        Assert.Equal(ResultadoInvalido, await RecusaAoDecidirAsync(c, id, null));
        Assert.Equal(Motivo, await RecusaAoDecidirAsync(c, id, "RECUSOU"));
        Assert.Equal(Motivo, await RecusaAoDecidirAsync(c, id, "RECUSOU", "   "));
        Assert.Equal(Motivo, await RecusaAoDecidirAsync(c, id, "RECUSOU", "  nao.  ")); // 4 caracteres depois de aparar
        Assert.Equal(Motivo, await RecusaAoDecidirAsync(c, id, "RECUSOU", new string('x', 501)));
        Assert.Equal("A observacao e o motivo da recusa: so se informa com RECUSOU.", await RecusaAoDecidirAsync(c, id, "APROVOU", "Pode ir"));
        Assert.Equal("As datas sugeridas so se informam com SUGERIU_DATA.", await RecusaAoDecidirAsync(c, id, "APROVOU", null, c.Inicio, c.Inicio.AddDays(9)));
        Assert.Equal("As datas sugeridas so se informam com SUGERIU_DATA.", await RecusaAoDecidirAsync(c, id, "RECUSOU", "Equipe reduzida", c.Inicio, null));
        Assert.Equal("Informe a data de inicio e a data de fim sugeridas.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA"));
        Assert.Equal("Informe a data de inicio e a data de fim sugeridas.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio, null));
        Assert.Equal("A data final nao pode ser antes da inicial.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio, c.Inicio.AddDays(-1)));
        Assert.Equal("Informe datas entre 2000 e 2100.", await RecusaAoDecidirAsync(c, id, "SUGERIU_DATA", null, new DateOnly(2101, 1, 3), new DateOnly(2101, 1, 12)));

        await DecidirAsync(c, id, "RECUSOU", "nao da"); // 6 caracteres
    }

    [Fact]
    public async Task O_gestor_so_decide_o_pedido_que_nasceu_com_ele_e_de_quem_esta_na_equipe_dele_hoje()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        var doChefe = await SolicitarAsync(c, c.Chefe, c.Inicio, 14); // sem etapa do gestor
        var outroSetor = await SetorAsync(c.Conexao);
        var outroChefe = await FuncionarioAsync(c.Conexao, admissao: "2015-03-02", nivel: "Funcionario Gestor", setorId: outroSetor);
        var outroGestor = await GestorAsync(c.Conexao, outroChefe);
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE SETORES SET ID_GESTOR = {outroGestor} WHERE ID_SETOR = {outroSetor}");

        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, 999_999, "APROVOU"));
        Assert.Equal("Gestor inexistente, inativo ou sem setor.", await RecusaAoDecidirAsync(c, id, "APROVOU", gestor: 999_999));
        Assert.Equal("Gestor inexistente, inativo ou sem setor.", await RecusaAoDecidirAsync(c, id, "APROVOU", gestor: c.GestorDoRh)); // em GESTORES, mas sem setor
        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, id, "APROVOU", gestor: outroGestor));
        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, doChefe, "APROVOU")); // o próprio pedido, sem etapa do gestor
        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, doChefe, "APROVOU", gestor: outroGestor));

        // A pessoa trocou de setor: o pedido deixou de ser da equipe do gestor antigo, e o novo gestor não é o gestor da etapa.
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {outroSetor} WHERE ID_FUNCIONARIO = {c.Pessoa}");
        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, id, "APROVOU"));
        Assert.Equal(NaoEncontradoNaEquipe, await RecusaAoDecidirAsync(c, id, "APROVOU", gestor: outroGestor));
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {c.Setor} WHERE ID_FUNCIONARIO = {c.Pessoa}");

        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {c.Chefe}");
        Assert.Equal("Gestor inexistente, inativo ou sem setor.", await RecusaAoDecidirAsync(c, id, "APROVOU"));
    }

    [Fact]
    public async Task O_gestor_nao_decide_duas_vezes_nem_o_que_ja_esta_com_o_RH()
    {
        var c = await CenarioAsync();
        var aprovado = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await DecidirAsync(c, aprovado, "APROVOU");
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE WHERE ID_GESTOR = {c.Gestor}");
        var automatico = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 5);

        Assert.Equal(EtapaErradaDoGestor, await RecusaAoDecidirAsync(c, aprovado, "APROVOU"));
        Assert.Equal(EtapaErradaDoGestor, await RecusaAoDecidirAsync(c, aprovado, "RECUSOU", "Mudei de ideia"));
        Assert.Equal(EtapaErradaDoGestor, await RecusaAoDecidirAsync(c, automatico, "RECUSOU", "Nao estava aqui"));

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", aprovado, c.RhComum);
        Assert.Equal(JaDecidido, await RecusaAoDecidirAsync(c, aprovado, "RECUSOU", "Mudei de ideia"));
    }

    [Fact]
    public async Task Aprovar_e_recusar_ao_mesmo_tempo_como_gestor_so_uma_decisao_vale()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);

        var erros = await AoMesmoTempoComAPessoaTravadaAsync(
            c.Conexao,
            c.Pessoa,
            () => ChamarAsync(c.Conexao, "SP_DECIDIR_FERIAS_GESTOR", id, c.Gestor, "APROVOU", null, null, null),
            () => ChamarAsync(c.Conexao, "SP_DECIDIR_FERIAS_GESTOR", id, c.Gestor, "RECUSOU", "Equipe reduzida", null, null));

        var perdeu = Assert.Single(erros, e => e is not null)!;
        Assert.Equal("45000", perdeu.SqlState);
        if (erros[0] is null)
        {
            Assert.Equal(EtapaErradaDoGestor, perdeu.Message);
            await ConfereAsync(c.Conexao, id, "STATUS_FERIAS = 'SOLICITADA' AND RESULTADO_GESTOR = 'APROVOU' AND MOTIVO_RECUSA IS NULL");
        }
        else
        {
            Assert.Equal(JaDecidido, perdeu.Message);
            await ConfereAsync(c.Conexao, id, "STATUS_FERIAS = 'REJEITADA' AND RESULTADO_GESTOR = 'RECUSOU' AND MOTIVO_RECUSA = 'Equipe reduzida'");
        }
    }

    // ---------------------------------------------------------------- SP_RESPONDER_SUGESTAO_FERIAS

    [Fact]
    public async Task A_pessoa_aceita_a_sugestao_as_datas_do_pedido_mudam_e_ele_vai_para_o_RH_que_confirma()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");
        var sugerida = c.Inicio.AddDays(7);
        await DecidirAsync(c, id, "SUGERIU_DATA", null, sugerida, sugerida.AddDays(9));

        await ChamarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "aceitou");

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'SOLICITADA' AND DATA_INICIO = '{Iso(sugerida)}' AND DATA_FIM = '{Iso(sugerida.AddDays(9))}'
            AND SUGESTAO_DATA_INICIO = '{Iso(sugerida)}' AND SUGESTAO_DATA_FIM = '{Iso(sugerida.AddDays(9))}'
            AND RESPOSTA_SUGESTAO = BINARY 'ACEITOU' AND {Agora("DATA_RESPOSTA_SUGESTAO")} AND RESULTADO_GESTOR = 'SUGERIU_DATA' AND ID_GESTOR = {c.Gestor}
            AND OBSERVACAO = 'Mensagem da pessoa' AND ID_FUNCIONARIO_RH IS NULL AND DATA_ANALISE IS NULL
            AND DATA_INICIO_PERIODO_AQUISITIVO = '{Iso(c.PeriodoInicio)}'
            """);
        Assert.Equal(EtapaErradaDaSugestao, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "RECUSOU"));
        Assert.Equal(EtapaErradaDoGestor, await RecusaAoDecidirAsync(c, id, "APROVOU"));

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.RhComum);
        await ConfereAsync(c.Conexao, id, $"STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO_RH = {c.RhComum} AND ID_GESTOR = {c.Gestor} AND DATA_INICIO = '{Iso(sugerida)}' AND RESPOSTA_SUGESTAO = 'ACEITOU'");
    }

    [Fact]
    public async Task A_pessoa_recusa_a_sugestao_e_o_pedido_e_cancelado()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await DecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio.AddDays(7), c.Inicio.AddDays(16));

        await ChamarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "RECUSOU");

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'CANCELADA' AND RESPOSTA_SUGESTAO = 'RECUSOU' AND {Agora("DATA_RESPOSTA_SUGESTAO")}
            AND DATA_INICIO = '{Iso(c.Inicio)}' AND DATA_FIM = '{Iso(c.Inicio.AddDays(13))}' AND SUGESTAO_DATA_INICIO = '{Iso(c.Inicio.AddDays(7))}'
            AND ID_FUNCIONARIO_RH IS NULL AND MOTIVO_RECUSA IS NULL
            """);
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "ACEITOU"));
    }

    [Fact]
    public async Task So_a_dona_do_pedido_responde_a_sugestao_e_so_enquanto_ele_espera_a_resposta()
    {
        var c = await CenarioAsync();
        var comOGestor = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        var comORh = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 5);
        await DecidirAsync(c, comORh, "APROVOU");
        var sugerido = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(35), 5);
        await DecidirAsync(c, sugerido, "SUGERIU_DATA", null, c.Inicio.AddDays(42), c.Inicio.AddDays(46));

        Assert.Equal("Resposta invalida: use ACEITOU ou RECUSOU.", await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", sugerido, c.Pessoa, "EXPIROU"));
        Assert.Equal("Resposta invalida: use ACEITOU ou RECUSOU.", await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", sugerido, c.Pessoa, null));
        Assert.Equal(NaoEncontradoDaPessoa, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", sugerido, c.Chefe, "ACEITOU"));
        Assert.Equal(NaoEncontradoDaPessoa, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", sugerido, 999_999, "ACEITOU"));
        Assert.Equal(NaoEncontradoDaPessoa, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", 999_999, c.Pessoa, "ACEITOU"));
        Assert.Equal(EtapaErradaDaSugestao, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", comOGestor, c.Pessoa, "ACEITOU"));
        Assert.Equal(EtapaErradaDaSugestao, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", comORh, c.Pessoa, "RECUSOU"));
    }

    [Fact]
    public async Task O_pedido_que_espera_a_resposta_nao_reserva_saldo_nem_datas_e_o_aceite_reconfere_as_regras()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await DecidirAsync(c, id, "SUGERIU_DATA", null, c.Inicio.AddDays(7), c.Inicio.AddDays(20));

        // Enquanto a pessoa não responde, o pedido não conta: o RH registra 20 dias nas mesmas datas.
        await RegistrarAsync(c, c.Pessoa, c.Inicio, 20);

        Assert.Equal("Passa do saldo disponivel: faltam 4 dias.", await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "ACEITOU"));
        await ConfereAsync(c.Conexao, id, $"STATUS_FERIAS = 'SOLICITADA' AND RESPOSTA_SUGESTAO IS NULL AND DATA_INICIO = '{Iso(c.Inicio)}'");

        // Com saldo, mas coincidindo com as férias registradas: a regra seguinte recusa.
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FERIAS SET SUGESTAO_DATA_FIM = '{Iso(c.Inicio.AddDays(16))}' WHERE ID_FERIAS = {id}");
        Assert.Equal(Sobreposicao, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "ACEITOU"));

        await ChamarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", id, c.Pessoa, "RECUSOU"); // recusar não depende das regras
        await ConfereAsync(c.Conexao, id, "STATUS_FERIAS = 'CANCELADA' AND RESPOSTA_SUGESTAO = 'RECUSOU'");
    }

    [Fact]
    public async Task A_resposta_que_chega_depois_do_prazo_expira_o_pedido_isso_fica_gravado_e_a_chamada_e_recusada()
    {
        var c = await CenarioAsync();
        const string PrazoTerminou = "O prazo para responder a sugestao terminou: o pedido de ferias foi cancelado.";
        var noPrazo = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await DecidirAsync(c, noPrazo, "SUGERIU_DATA", null, c.Inicio.AddDays(7), c.Inicio.AddDays(20));
        var vencido = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(28), 5);
        await DecidirAsync(c, vencido, "SUGERIU_DATA", null, c.Inicio.AddDays(35), c.Inicio.AddDays(39));
        var pertoDoInicio = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(49), 5);
        await DecidirAsync(c, pertoDoInicio, "SUGERIU_DATA", null, c.Inicio.AddDays(56), c.Inicio.AddDays(60));

        // O prazo é o menor entre 5 dias depois do dia da sugestão e 2 dias antes do início sugerido.
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FERIAS SET DATA_SOLICITACAO = NOW() - INTERVAL 10 DAY, DATA_ETAPA_GESTOR = NOW() - INTERVAL 5 DAY WHERE ID_FERIAS = {noPrazo}");
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FERIAS SET DATA_SOLICITACAO = NOW() - INTERVAL 10 DAY, DATA_ETAPA_GESTOR = NOW() - INTERVAL 6 DAY WHERE ID_FERIAS = {vencido}");
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FERIAS SET SUGESTAO_DATA_INICIO = '{Iso(c.Hoje.AddDays(1))}', SUGESTAO_DATA_FIM = '{Iso(c.Hoje.AddDays(5))}' WHERE ID_FERIAS = {pertoDoInicio}");

        Assert.Equal(PrazoTerminou, await RecusaDeNegocioAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", vencido, c.Pessoa, "ACEITOU"));
        Assert.Equal(PrazoTerminou, await RecusaDeNegocioAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", pertoDoInicio, c.Pessoa, "RECUSOU"));

        const string Expirado = "STATUS_FERIAS = 'CANCELADA' AND RESPOSTA_SUGESTAO = 'EXPIROU' AND DATA_RESPOSTA_SUGESTAO BETWEEN NOW() - INTERVAL 5 MINUTE AND NOW()";
        await ConfereAsync(c.Conexao, vencido, $"{Expirado} AND DATA_INICIO = '{Iso(c.Inicio.AddDays(28))}'");
        await ConfereAsync(c.Conexao, pertoDoInicio, Expirado);
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", vencido, c.Pessoa, "ACEITOU"));

        await ChamarAsync(c.Conexao, "SP_RESPONDER_SUGESTAO_FERIAS", noPrazo, c.Pessoa, "ACEITOU"); // no quinto dia ainda vale
        await ConfereAsync(c.Conexao, noPrazo, "STATUS_FERIAS = 'SOLICITADA' AND RESPOSTA_SUGESTAO = 'ACEITOU'");
    }

    // ---------------------------------------------------------------- SP_CONFIRMAR_FERIAS_RH e SP_RECUSAR_FERIAS_RH

    [Fact]
    public async Task O_RH_confirma_o_que_o_gestor_aprovou_e_as_ferias_ficam_APROVADA_com_o_gestor_da_etapa_preservado()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");
        await DecidirAsync(c, id, "APROVOU");

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.RhComum); // com etapa do gestor, quem decide não precisa estar em GESTORES

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO_RH = {c.RhComum} AND {Agora("DATA_CONFIRMACAO_RH")} AND {Agora("DATA_ANALISE")}
            AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = 'APROVOU' AND ETAPA_GESTOR_AUTOMATICA = FALSE
            AND OBSERVACAO = 'Mensagem da pessoa' AND MOTIVO_RECUSA IS NULL AND DATA_INICIO = '{Iso(c.Inicio)}'
            """);
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.RhComum));
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, "Mudei de ideia"));
    }

    [Fact]
    public async Task O_RH_confirma_o_pedido_que_o_gestor_ausente_aprovou_sozinho()
    {
        var c = await CenarioAsync();
        await Sql.ExecutarAsync(c.Conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA = TRUE WHERE ID_GESTOR = {c.Gestor}");
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.RhComum);

        await ConfereAsync(c.Conexao, id, $"STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO_RH = {c.RhComum} AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = 'APROVOU' AND ETAPA_GESTOR_AUTOMATICA = TRUE AND {Agora("DATA_ANALISE")}");
    }

    [Fact]
    public async Task No_pedido_sem_etapa_do_gestor_o_RH_que_decide_fica_gravado_como_gestor_e_precisa_estar_em_GESTORES_e_nao_ser_a_dona()
    {
        var c = await CenarioAsync();
        var doChefe = await SolicitarAsync(c, c.Chefe, c.Inicio, 14);
        var doRh = await SolicitarAsync(c, c.Rh, c.Inicio, 14); // quem é do RH e está em GESTORES também não tem etapa do gestor
        var setorSemGestor = await SetorAsync(c.Conexao);
        var semGestor = await FuncionarioAsync(c.Conexao, admissao: Iso(c.Hoje.AddDays(-400)), setorId: setorSemGestor);
        var doSemGestor = await SolicitarAsync(c, semGestor, c.Inicio, 14);

        Assert.Equal(ForaDeGestores, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", doChefe, c.RhComum));
        Assert.Equal(ForaDeGestores, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", doChefe, c.RhComum, "Motivo qualquer"));
        Assert.Equal(Proprio, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", doRh, c.Rh));
        Assert.Equal(Proprio, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", doRh, c.Rh, "Motivo qualquer"));
        Assert.Equal(Proprio, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", doSemGestor, semGestor)); // a dona vence: ela nem está em GESTORES
        Assert.Equal(Proprio, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", doSemGestor, semGestor, "Motivo qualquer"));

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", doChefe, c.Rh);
        await ChamarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", doSemGestor, c.Rh, "Sem cobertura no setor");

        await ConfereAsync(c.Conexao, doChefe, $"STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO_RH = {c.Rh} AND ID_GESTOR = {c.GestorDoRh} AND RESULTADO_GESTOR IS NULL AND DATA_ETAPA_GESTOR IS NULL AND {Agora("DATA_CONFIRMACAO_RH")} AND {Agora("DATA_ANALISE")}");
        await ConfereAsync(c.Conexao, doSemGestor, $"STATUS_FERIAS = 'REJEITADA' AND ID_FUNCIONARIO_RH = {c.Rh} AND ID_GESTOR = {c.GestorDoRh} AND RESULTADO_GESTOR IS NULL AND MOTIVO_RECUSA = 'Sem cobertura no setor' AND {Agora("DATA_ANALISE")}");
    }

    [Fact]
    public async Task Quem_e_do_RH_confirma_o_proprio_pedido_quando_a_etapa_do_gestor_foi_de_outra_pessoa()
    {
        var c = await CenarioAsync();
        var rhDoSetor = await FuncionarioAsync(c.Conexao, admissao: Iso(c.Hoje.AddDays(-400)), nivel: "RH", setorId: c.Setor); // fora de GESTORES: segue o gestor do setor
        var id = await SolicitarAsync(c, rhDoSetor, c.Inicio, 14);
        await DecidirAsync(c, id, "APROVOU");

        await ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, rhDoSetor);

        await ConfereAsync(c.Conexao, id, $"STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO = {rhDoSetor} AND ID_FUNCIONARIO_RH = {rhDoSetor} AND ID_GESTOR = {c.Gestor}");
    }

    [Fact]
    public async Task O_RH_so_decide_o_pedido_que_ja_chegou_a_ele_e_quem_decide_precisa_existir_e_estar_ativo()
    {
        var c = await CenarioAsync();
        var comOGestor = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        var aprovado = await SolicitarAsync(c, c.Pessoa, c.Inicio.AddDays(21), 5);
        await DecidirAsync(c, aprovado, "APROVOU");

        Assert.Equal("Pedido de ferias nao encontrado.", await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", 999_999, c.Rh));
        Assert.Equal("Pedido de ferias nao encontrado.", await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", 999_999, c.Rh, "Motivo qualquer"));
        Assert.Equal(EtapaErradaDoRh, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", comOGestor, c.Rh));
        Assert.Equal(EtapaErradaDoRh, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", comOGestor, c.Rh, "Motivo qualquer"));
        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", aprovado, 999_999));
        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", aprovado, 999_999, "Motivo qualquer"));

        await Sql.ExecutarAsync(c.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {c.RhComum}");
        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", aprovado, c.RhComum));
        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", aprovado, c.RhComum, "Motivo qualquer"));
    }

    [Fact]
    public async Task O_RH_recusa_ate_o_que_o_gestor_aprovou_com_o_motivo_e_sem_tocar_na_mensagem_da_pessoa()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14, mensagem: "Mensagem da pessoa");
        await DecidirAsync(c, id, "APROVOU");

        Assert.Equal(Motivo, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, null));
        Assert.Equal(Motivo, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, " nao. "));
        Assert.Equal(Motivo, await RecusaSemGravarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, new string('x', 501)));

        await ChamarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, "  Fechamento do ano fiscal  ");

        await ConfereAsync(c.Conexao, id, $"""
            STATUS_FERIAS = 'REJEITADA' AND MOTIVO_RECUSA = 'Fechamento do ano fiscal' AND ID_FUNCIONARIO_RH = {c.RhComum}
            AND {Agora("DATA_CONFIRMACAO_RH")} AND {Agora("DATA_ANALISE")} AND ID_GESTOR = {c.Gestor} AND RESULTADO_GESTOR = 'APROVOU' AND OBSERVACAO = 'Mensagem da pessoa'
            """);
        Assert.Equal(JaDecidido, await RecusaSemGravarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.RhComum));

        // O pedido recusado devolve o saldo e as datas.
        await SolicitarAsync(c, c.Pessoa, c.Inicio, 30);
    }

    [Fact]
    public async Task Confirmar_e_recusar_ao_mesmo_tempo_como_RH_so_uma_decisao_vale()
    {
        var c = await CenarioAsync();
        var id = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        await DecidirAsync(c, id, "APROVOU");

        var erros = await AoMesmoTempoComAPessoaTravadaAsync(
            c.Conexao,
            c.Pessoa,
            () => ChamarAsync(c.Conexao, "SP_CONFIRMAR_FERIAS_RH", id, c.Rh),
            () => ChamarAsync(c.Conexao, "SP_RECUSAR_FERIAS_RH", id, c.RhComum, "Fechamento do ano fiscal"));

        var perdeu = Assert.Single(erros, e => e is not null)!;
        Assert.Equal("45000", perdeu.SqlState);
        Assert.Equal(JaDecidido, perdeu.Message);
        await ConfereAsync(
            c.Conexao,
            id,
            erros[0] is null
                ? $"STATUS_FERIAS = 'APROVADA' AND ID_FUNCIONARIO_RH = {c.Rh} AND MOTIVO_RECUSA IS NULL"
                : $"STATUS_FERIAS = 'REJEITADA' AND ID_FUNCIONARIO_RH = {c.RhComum} AND MOTIVO_RECUSA = 'Fechamento do ano fiscal'");
    }

    // ---------------------------------------------------------------- SP_CONFERIR_FERIAS

    [Fact]
    public async Task Conferir_devolve_o_periodo_aquisitivo_em_que_as_ferias_entram_e_nao_grava_nada()
    {
        var c = await CenarioAsync();
        var existente = await SolicitarAsync(c, c.Pessoa, c.Inicio, 14);
        var antes = await FotoAsync(c.Conexao);

        var saidas = await ChamarAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, c.Inicio.AddDays(21), c.Inicio.AddDays(30), true, 6, true, null, new Saida("inicio"), new Saida("fim"));
        var doPedido = await ChamarAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, c.Inicio.AddDays(7), c.Inicio.AddDays(36), false, 0, false, existente, new Saida("inicio"), new Saida("fim")); // 30 dias em cima dele mesmo

        Assert.Equal(c.PeriodoInicio, Dia(saidas["inicio"]));
        Assert.Equal(c.PeriodoFim, Dia(saidas["fim"]));
        Assert.Equal(c.PeriodoInicio, Dia(doPedido["inicio"]));
        Assert.Equal(antes, await FotoAsync(c.Conexao));

        Assert.Equal("Passa do saldo disponivel: falta 1 dia.", await RecusaSemGravarAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, c.Inicio.AddDays(21), c.Inicio.AddDays(30), true, 7, true, null, new Saida("inicio"), new Saida("fim")));
        Assert.Equal(Abono, await RecusaSemGravarAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Pessoa, c.Inicio.AddDays(21), c.Inicio.AddDays(30), false, 7, true, null, new Saida("inicio"), new Saida("fim")));
        Assert.Equal("Funcionario nao encontrado.", await RecusaSemGravarAsync(c.Conexao, "SP_CONFERIR_FERIAS", 999_999, c.Inicio, c.Inicio.AddDays(9), false, 0, true, null, new Saida("inicio"), new Saida("fim")));
        Assert.Equal(NaoEncontradoDaPessoa, await RecusaSemGravarAsync(c.Conexao, "SP_CONFERIR_FERIAS", c.Chefe, c.Inicio, c.Inicio.AddDays(9), false, 0, false, existente, new Saida("inicio"), new Saida("fim")));
    }

    // ---------------------------------------------------------------- apoio

    private static string Iso(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>A primeira segunda-feira de <paramref name="aPartirDe"/> em diante.</summary>
    private static DateOnly Segunda(DateOnly aPartirDe) => aPartirDe.AddDays((8 - (int)aPartirDe.DayOfWeek) % 7);

    private static DateOnly Dia(object? valor) => DateOnly.FromDateTime(Convert.ToDateTime(valor, CultureInfo.InvariantCulture));

    /// <summary>A coluna foi gravada agora, pelo relógio do banco.</summary>
    private static string Agora(string coluna) => $"{coluna} BETWEEN NOW() - INTERVAL 5 MINUTE AND NOW()";

    private static object?[] Lancamento(int pessoa, DateOnly inicio, int dias, bool abono, int diasAbono, bool adiantar13, string? texto) =>
        [pessoa, inicio, inicio.AddDays(dias - 1), abono, diasAbono, adiantar13, texto, new Saida("id")];

    private static async Task<int> SolicitarAsync(Cenario c, int pessoa, DateOnly inicio, int dias, bool abono = false, int diasAbono = 0, bool adiantar13 = false, string? mensagem = null) =>
        Inteiro((await ChamarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", Lancamento(pessoa, inicio, dias, abono, diasAbono, adiantar13, mensagem)))["id"]);

    private static Task<string> RecusaAoSolicitarAsync(Cenario c, int pessoa, DateOnly inicio, int dias, bool abono = false, int diasAbono = 0) =>
        RecusaSemGravarAsync(c.Conexao, "SP_SOLICITAR_FERIAS", Lancamento(pessoa, inicio, dias, abono, diasAbono, false, null));

    private static async Task<int> RegistrarAsync(Cenario c, int pessoa, DateOnly inicio, int dias, bool abono = false, int diasAbono = 0, bool adiantar13 = false, string? observacao = null) =>
        Inteiro((await ChamarAsync(c.Conexao, "SP_REGISTRAR_FERIAS_RH", Lancamento(pessoa, inicio, dias, abono, diasAbono, adiantar13, observacao)))["id"]);

    private static Task<string> RecusaAoRegistrarAsync(Cenario c, int pessoa, DateOnly inicio, int dias, bool abono = false, int diasAbono = 0, string? observacao = null) =>
        RecusaSemGravarAsync(c.Conexao, "SP_REGISTRAR_FERIAS_RH", Lancamento(pessoa, inicio, dias, abono, diasAbono, false, observacao));

    private static Task DecidirAsync(Cenario c, int ferias, string resultado, string? motivo = null, DateOnly? sugestaoInicio = null, DateOnly? sugestaoFim = null) =>
        ChamarAsync(c.Conexao, "SP_DECIDIR_FERIAS_GESTOR", ferias, c.Gestor, resultado, motivo, sugestaoInicio, sugestaoFim);

    private static Task<string> RecusaAoDecidirAsync(Cenario c, int ferias, string? resultado, string? motivo = null, DateOnly? sugestaoInicio = null, DateOnly? sugestaoFim = null, int? gestor = null) =>
        RecusaSemGravarAsync(c.Conexao, "SP_DECIDIR_FERIAS_GESTOR", ferias, gestor ?? c.Gestor, resultado, motivo, sugestaoInicio, sugestaoFim);

    /// <summary>A recusa de negócio (SQLSTATE 45000) e a prova de que ela não deixou nada gravado: a tabela <c>FERIAS</c> inteira fica como estava.</summary>
    private static async Task<string> RecusaSemGravarAsync(string conexao, string procedure, params object?[] argumentos)
    {
        var antes = await FotoAsync(conexao);
        var mensagem = await RecusaDeNegocioAsync(conexao, procedure, argumentos);
        Assert.Equal(antes, await FotoAsync(conexao));
        return mensagem;
    }

    /// <summary>A mensagem da recusa de negócio, ou <c>null</c> se a procedure aceitou.</summary>
    private static async Task<string?> RecusaOuNadaAsync(string conexao, string procedure, params object?[] argumentos)
    {
        try
        {
            await ChamarAsync(conexao, procedure, argumentos);
            return null;
        }
        catch (MySqlException erro) when (erro.SqlState == "45000")
        {
            return erro.Message;
        }
    }

    /// <summary>Todas as colunas de todas as linhas de <c>FERIAS</c>, com os nulos marcados.</summary>
    private static async Task<string> FotoAsync(string conexao, string where = "1 = 1")
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand($"SELECT * FROM FERIAS WHERE {where} ORDER BY ID_FERIAS", c);
        await using var leitor = await comando.ExecuteReaderAsync();
        var texto = new StringBuilder();
        while (await leitor.ReadAsync())
        {
            for (var i = 0; i < leitor.FieldCount; i++)
            {
                texto.Append(leitor.GetName(i)).Append('=').Append(await leitor.IsDBNullAsync(i) ? "NULL" : Convert.ToString(leitor.GetValue(i), CultureInfo.InvariantCulture)).Append("; ");
            }

            texto.AppendLine();
        }

        return texto.ToString();
    }

    /// <summary>A linha de <c>FERIAS</c> cumpre a condição (SQL); se não cumpre, o erro mostra a linha como está.</summary>
    private static async Task ConfereAsync(string conexao, int ferias, string condicao)
    {
        var bate = await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FERIAS WHERE ID_FERIAS = {ferias} AND ({condicao})");
        if (bate != 1)
        {
            Assert.Fail($"A linha {ferias} de FERIAS não cumpre: {condicao}{Environment.NewLine}Linha: {await FotoAsync(conexao, $"ID_FERIAS = {ferias}")}");
        }
    }

    /// <summary>
    /// Segura a trava da pessoa (<c>FOR UPDATE</c> em <c>FUNCIONARIOS</c>) numa sessão do teste, dispara as chamadas (cada uma na sua sessão), confere que TODAS ficaram esperando a trava (é a prova de que a procedure trava a pessoa antes de ler) e só então solta. Devolve, na ordem das chamadas,
    /// o erro de cada uma (<c>null</c> para a que gravou).
    /// </summary>
    private static async Task<MySqlException?[]> AoMesmoTempoComAPessoaTravadaAsync(string conexao, int pessoa, params Func<Task>[] chamadas)
    {
        await using var sessao = new MySqlConnection(conexao);
        await sessao.OpenAsync();
        await using var transacao = await sessao.BeginTransactionAsync();
        await using (var travar = new MySqlCommand($"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} FOR UPDATE", sessao, transacao))
        {
            await travar.ExecuteScalarAsync();
        }

        var tarefas = chamadas
            .Select(chamada => Task.Run(async () =>
            {
                try
                {
                    await chamada();
                    return null;
                }
                catch (MySqlException erro)
                {
                    return erro;
                }
            }))
            .ToArray();

        await Task.Delay(TimeSpan.FromSeconds(2));
        Assert.All(tarefas, tarefa => Assert.False(tarefa.IsCompleted, "A chamada terminou sem esperar a trava da pessoa."));
        await transacao.CommitAsync();
        return await Task.WhenAll(tarefas);
    }
}

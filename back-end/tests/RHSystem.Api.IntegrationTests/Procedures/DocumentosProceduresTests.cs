using System.Globalization;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de holerite, atestado e benefícios do <c>V007</c> (seção C), num MySQL 8.4 de verdade e sem a API: o caminho feliz com o que ficou gravado, cada recusa com a mensagem, a recusa sem nada gravado, a corrida de duas sessões e os efeitos dos triggers. "Hoje" é o
/// <c>CURDATE()</c> do servidor (a procedure não tem relógio injetado): as datas dos testes saem dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DocumentosProceduresTests(MySqlFixture mysql)
{
    private const string HoleriteJaExiste = "Ja existe holerite desta competencia e tipo para o funcionario. Para corrigir, use SP_SUBSTITUIR_HOLERITE.";
    private const string HoleriteDesatualizado = "Este holerite ja foi substituido por uma versao mais nova. Substitua a versao atual.";
    private const string AtestadoJaDecidido = "Este atestado ja foi decidido.";
    private const string BeneficioJaPedido = "O funcionario ja tem este beneficio ou ja pediu a adesao e o RH ainda vai responder.";
    private const string PedidoJaRespondido = "Este pedido de beneficio ja foi respondido.";
    private const string MotivoObrigatorio = "Informe o motivo da recusa, de 1 a 500 caracteres.";
    private const string RhInvalido = "Responsavel do RH inexistente ou inativo.";
    private const int PessoasEmParalelo = 12;
    private const int RodadasEmParalelo = 6;
    private const string UmDependente = """[{"nome": "  Ana Lima  ", "parentesco": " Filha ", "dataNascimento": "2015-03-09"}]""";

    // ---------------------------------------------------------------- apoio desta seção

    private static string Iso(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Logico(bool valor) => valor ? "TRUE" : "FALSE";

    private static async Task<DateOnly> HojeAsync(string conexao) =>
        DateOnly.FromDateTime(Convert.ToDateTime(await Sql.EscalarAsync(conexao, "SELECT CURDATE()"), CultureInfo.InvariantCulture));

    /// <summary>Uma linha lida como texto (o <c>sql</c> junta as colunas com <c>CONCAT_WS</c> e troca o nulo por <c>NULL</c>).</summary>
    private static async Task<string> LinhaAsync(string conexao, string sql) => Convert.ToString(await Sql.EscalarAsync(conexao, sql), CultureInfo.InvariantCulture) ?? "(sem linha)";

    private static async Task DesligarAsync(string conexao, int funcionarioId, DateOnly desligamento) =>
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET DATA_DESLIGAMENTO = '{Iso(desligamento)}' WHERE ID_FUNCIONARIO = {funcionarioId}"); // o trigger inativa a pessoa

    private static async Task<int> BeneficioAsync(string conexao, bool opcional = true, bool aceitaDependentes = false, bool aceitaTrajeto = false)
    {
        var nome = $"Beneficio {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS (NOME_BENEFICIO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO) VALUES ('{nome}', {Logico(opcional)}, {Logico(aceitaDependentes)}, {Logico(aceitaTrajeto)})");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_BENEFICIO FROM BENEFICIOS WHERE NOME_BENEFICIO = '{nome}'");
    }

    /// <summary>
    /// Duas (ou mais) chamadas ao mesmo tempo de verdade: o teste segura a trava da pessoa, solta as chamadas (que ficam esperando a trava dentro da procedure) e só então libera. Devolve as mensagens das recusas de negócio; qualquer outro erro (impasse, chave repetida) derruba o teste.
    /// </summary>
    private static async Task<List<string>> AoMesmoTempoAsync(string conexao, int funcionarioTravado, params Func<Task>[] chamadas)
    {
        var tarefas = new List<Task>();
        await using (var trava = new MySqlConnection(conexao))
        {
            await trava.OpenAsync();
            await using var transacao = await trava.BeginTransactionAsync();
            await using (var comando = new MySqlCommand($"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioTravado} FOR UPDATE", trava, transacao))
            {
                _ = await comando.ExecuteScalarAsync();
            }

            tarefas.AddRange(chamadas.Select(chamada => Task.Run(chamada)));
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            Assert.All(tarefas, t => Assert.False(t.IsCompleted, "A chamada deveria estar esperando a trava da pessoa."));
            await transacao.CommitAsync();
        }

        var recusas = new List<string>();
        foreach (var tarefa in tarefas)
        {
            try
            {
                await tarefa;
            }
            catch (MySqlException erro) when (erro.SqlState == "45000")
            {
                recusas.Add(erro.Message);
            }
        }

        return recusas;
    }

    private static Task<IReadOnlyDictionary<string, object?>> PublicarAsync(string conexao, int funcionarioId, DateOnly competencia, string tipo = "MENSAL", string caminho = "holerites/a.pdf", DateOnly? publicarEm = null) =>
        ChamarAsync(conexao, "SP_PUBLICAR_HOLERITE", funcionarioId, competencia.Year, competencia.Month, tipo, caminho, publicarEm, new Saida("id"), new Saida("versao"));

    private static Task<string> RecusaAoPublicarAsync(string conexao, int? funcionarioId, DateOnly competencia, string? tipo = "MENSAL", string? caminho = "holerites/a.pdf", DateOnly? publicarEm = null) =>
        RecusaDeNegocioAsync(conexao, "SP_PUBLICAR_HOLERITE", funcionarioId, competencia.Year, competencia.Month, tipo, caminho, publicarEm, new Saida("id"), new Saida("versao"));

    private static Task<IReadOnlyDictionary<string, object?>> SubstituirAsync(string conexao, int holeriteId, string caminho = "holerites/b.pdf", DateOnly? publicarEm = null) =>
        ChamarAsync(conexao, "SP_SUBSTITUIR_HOLERITE", holeriteId, caminho, publicarEm, new Saida("id"), new Saida("versao"));

    private static Task<string> RecusaAoSubstituirAsync(string conexao, int? holeriteId, string? caminho = "holerites/b.pdf", DateOnly? publicarEm = null) =>
        RecusaDeNegocioAsync(conexao, "SP_SUBSTITUIR_HOLERITE", holeriteId, caminho, publicarEm, new Saida("id"), new Saida("versao"));

    private static async Task<int> EnviarAtestadoAsync(string conexao, int funcionarioId, DateOnly inicio, DateOnly fim, TimeOnly? horaInicio = null, TimeOnly? horaFim = null, string caminho = "atestados/a.pdf", string? nome = "atestado.pdf") =>
        Inteiro((await ChamarAsync(conexao, "SP_ENVIAR_ATESTADO", funcionarioId, inicio, fim, horaInicio, horaFim, caminho, nome, new Saida("id")))["id"]);

    private static Task<string> RecusaAoEnviarAtestadoAsync(string conexao, int? funcionarioId, DateOnly? inicio, DateOnly? fim, object? horaInicio = null, object? horaFim = null, string? caminho = "atestados/a.pdf") =>
        RecusaDeNegocioAsync(conexao, "SP_ENVIAR_ATESTADO", funcionarioId, inicio, fim, horaInicio, horaFim, caminho, "atestado.pdf", new Saida("id"));

    private static async Task<int> SolicitarAsync(string conexao, int funcionarioId, int beneficioId, DateOnly inicio, bool autoriza = true, string? dependentes = null, string? origem = null, string? destino = null, string? linhas = null) =>
        Inteiro((await ChamarAsync(conexao, "SP_SOLICITAR_BENEFICIO", funcionarioId, beneficioId, inicio, autoriza, dependentes, origem, destino, linhas, new Saida("id")))["id"]);

    private static Task<string> RecusaAoSolicitarAsync(string conexao, int? funcionarioId, int? beneficioId, DateOnly? inicio, bool? autoriza = true, string? dependentes = null, string? origem = null, string? destino = null, string? linhas = null) =>
        RecusaDeNegocioAsync(conexao, "SP_SOLICITAR_BENEFICIO", funcionarioId, beneficioId, inicio, autoriza, dependentes, origem, destino, linhas, new Saida("id"));

    private static Task<string> AdesaoAsync(string conexao, int adesaoId) =>
        LinhaAsync(
            conexao,
            $"SELECT CONCAT_WS('|', STATUS_PEDIDO, BENEFICIO_ATIVO, IFNULL(DATA_INICIO_DESEJADA, 'NULL'), AUTORIZA_DESCONTO_FOLHA, IFNULL(ID_FUNCIONARIO_RH, 'NULL'), IF(DATA_RESPOSTA IS NULL, 'NULL', 'respondido'), IFNULL(MOTIVO_RECUSA, 'NULL')) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");

    private static Task<List<string>> DependentesAsync(string conexao, int adesaoId) =>
        Sql.ColunaAsync(
            conexao,
            $"SELECT CONCAT_WS('|', NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, IFNULL(ID_FUNCIONARIO_RH, 'NULL'), IF(DATA_RESPOSTA IS NULL, 'NULL', 'respondido'), IFNULL(MOTIVO_RECUSA, 'NULL')) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId} ORDER BY ID_DEPENDENTE");

    private static Task<List<string>> TrajetosAsync(string conexao, int adesaoId) =>
        Sql.ColunaAsync(
            conexao,
            $"SELECT CONCAT_WS('|', ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, IFNULL(ID_FUNCIONARIO_RH, 'NULL'), IF(DATA_RESPOSTA IS NULL, 'NULL', 'respondido'), IFNULL(MOTIVO_RECUSA, 'NULL')) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId} ORDER BY ID_TRAJETO");

    private static DateOnly PrimeiroDia(DateOnly dia) => new(dia.Year, dia.Month, 1);

    // ================================================================ SP_PUBLICAR_HOLERITE

    [Fact]
    public async Task Publicar_holerite_sem_data_grava_a_versao_1_visivel_agora()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var competencia = (await HojeAsync(conexao)).AddMonths(-1);

        var saidas = await PublicarAsync(conexao, pessoa, competencia, caminho: "  holerites/2026/a.pdf  ");

        var id = Inteiro(saidas["id"]);
        Assert.Equal(1, Inteiro(saidas["versao"]));
        Assert.Equal($"{pessoa}|{competencia.Year}|{competencia.Month}|MENSAL|1|holerites/2026/a.pdf", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, CAMINHO_ARQUIVO) FROM HOLERITES WHERE ID_HOLERITE = {id}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_HOLERITE = {id} AND DATA_PUBLICACAO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS")); // a tarefa do e-mail é da API
    }

    [Fact]
    public async Task Publicar_holerite_com_data_futura_agenda_para_a_meia_noite_do_dia_e_com_a_data_de_hoje_publica_agora()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var competencia = hoje.AddMonths(-1);

        var agendado = Inteiro((await PublicarAsync(conexao, pessoa, competencia, publicarEm: hoje.AddDays(5)))["id"]);
        var deHoje = Inteiro((await PublicarAsync(conexao, pessoa, competencia, tipo: "DECIMO_TERCEIRO", publicarEm: hoje))["id"]);

        Assert.Equal($"{Iso(hoje.AddDays(5))} 00:00:00", await LinhaAsync(conexao, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM HOLERITES WHERE ID_HOLERITE = {agendado}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_HOLERITE = {deHoje} AND DATA_PUBLICACAO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
    }

    [Fact]
    public async Task Publicar_holerite_recusa_cada_entrada_invalida_e_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var competencia = hoje.AddMonths(-1);
        const string Tipo = "Informe o tipo do holerite: MENSAL, DECIMO_TERCEIRO ou FERIAS.";
        const string Caminho = "Informe o caminho do arquivo do holerite.";

        Assert.Equal("Informe o funcionario do holerite.", await RecusaAoPublicarAsync(conexao, null, competencia));
        foreach (var mes in new int?[] { 0, 13, null })
        {
            Assert.Equal("Informe o mes do holerite, de 1 a 12.", await RecusaDeNegocioAsync(conexao, "SP_PUBLICAR_HOLERITE", pessoa, 2026, mes, "MENSAL", "a.pdf", null, new Saida("id"), new Saida("versao")));
        }

        foreach (var ano in new int?[] { 1999, 2101, null })
        {
            Assert.Equal("Informe o ano do holerite, de 2000 a 2100.", await RecusaDeNegocioAsync(conexao, "SP_PUBLICAR_HOLERITE", pessoa, ano, 1, "MENSAL", "a.pdf", null, new Saida("id"), new Saida("versao")));
        }

        Assert.Equal(Tipo, await RecusaAoPublicarAsync(conexao, pessoa, competencia, tipo: "ADIANTAMENTO"));
        Assert.Equal(Tipo, await RecusaAoPublicarAsync(conexao, pessoa, competencia, tipo: "mensal")); // a API só aceita o nome do contrato, em maiúsculas
        Assert.Equal(Tipo, await RecusaAoPublicarAsync(conexao, pessoa, competencia, tipo: null));
        Assert.Equal(Caminho, await RecusaAoPublicarAsync(conexao, pessoa, competencia, caminho: null));
        Assert.Equal(Caminho, await RecusaAoPublicarAsync(conexao, pessoa, competencia, caminho: "   "));
        Assert.Equal("A data de publicacao do holerite nao pode ser anterior a hoje.", await RecusaAoPublicarAsync(conexao, pessoa, competencia, publicarEm: hoje.AddDays(-1)));
        Assert.Equal("Funcionario nao encontrado.", await RecusaAoPublicarAsync(conexao, pessoa + 1000, competencia));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));
    }

    [Fact]
    public async Task Publicar_holerite_de_competencia_futura_so_vale_para_ferias()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var proximoMes = hoje.AddMonths(1);

        Assert.Equal("Nao e possivel cadastrar holerite de competencia futura.", await RecusaAoPublicarAsync(conexao, pessoa, proximoMes));
        Assert.Equal("Nao e possivel cadastrar holerite de competencia futura.", await RecusaAoPublicarAsync(conexao, pessoa, proximoMes, tipo: "DECIMO_TERCEIRO"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));

        _ = await PublicarAsync(conexao, pessoa, proximoMes, tipo: "FERIAS");
        _ = await PublicarAsync(conexao, pessoa, hoje); // o mês corrente não é futuro

        Assert.Equal(["FERIAS", "MENSAL"], await Sql.ColunaAsync(conexao, "SELECT TIPO_HOLERITE FROM HOLERITES ORDER BY TIPO_HOLERITE"));
    }

    [Fact]
    public async Task Publicar_holerite_recusa_a_competencia_anterior_a_admissao_e_a_posterior_ao_desligamento()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = await HojeAsync(conexao);
        var admissao = PrimeiroDia(hoje.AddMonths(-6)).AddDays(14); // dia 15 de seis meses atrás
        var pessoa = await FuncionarioAsync(conexao, admissao: Iso(admissao));
        var desligamento = PrimeiroDia(hoje.AddMonths(-2)).AddDays(9); // dia 10 de dois meses atrás
        await DesligarAsync(conexao, pessoa, desligamento);

        Assert.Equal("A competencia do holerite e anterior a admissao.", await RecusaAoPublicarAsync(conexao, pessoa, admissao.AddMonths(-1)));
        Assert.Equal("A competencia do holerite e posterior ao desligamento.", await RecusaAoPublicarAsync(conexao, pessoa, desligamento.AddMonths(1)));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));

        // O mês da admissão e o do desligamento valem, e a pessoa inativa recebe holerite (a rescisão).
        _ = await PublicarAsync(conexao, pessoa, admissao);
        _ = await PublicarAsync(conexao, pessoa, desligamento);

        Assert.Equal(2L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Publicar_holerite_que_ja_existe_e_recusado_em_vez_de_criar_versao_nova()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var outra = await FuncionarioAsync(conexao);
        var competencia = (await HojeAsync(conexao)).AddMonths(-1);
        _ = await PublicarAsync(conexao, pessoa, competencia);

        Assert.Equal(HoleriteJaExiste, await RecusaAoPublicarAsync(conexao, pessoa, competencia, caminho: "holerites/outro.pdf"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));

        // Outro tipo, outra competência e outra pessoa não chocam.
        _ = await PublicarAsync(conexao, pessoa, competencia, tipo: "DECIMO_TERCEIRO");
        _ = await PublicarAsync(conexao, pessoa, competencia.AddMonths(-1));
        _ = await PublicarAsync(conexao, outra, competencia);

        Assert.Equal(4L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES WHERE VERSAO = 1"));
    }

    [Fact]
    public async Task Publicar_holerite_confere_a_competencia_antes_de_dizer_que_ja_existe_como_a_API()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var competencia = hoje.AddMonths(-1);
        _ = await PublicarAsync(conexao, pessoa, competencia);
        await DesligarAsync(conexao, pessoa, PrimeiroDia(competencia).AddDays(-1)); // a competência que já tem holerite passa a ser posterior ao desligamento

        Assert.Equal("A competencia do holerite e posterior ao desligamento.", await RecusaAoPublicarAsync(conexao, pessoa, competencia));
    }

    [Fact]
    public async Task Dois_envios_iguais_de_holerite_ao_mesmo_tempo_viram_um_holerite_e_uma_recusa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var competencia = (await HojeAsync(conexao)).AddMonths(-1);

        var recusas = await AoMesmoTempoAsync(conexao, pessoa, () => PublicarAsync(conexao, pessoa, competencia), () => PublicarAsync(conexao, pessoa, competencia));

        Assert.Equal([HoleriteJaExiste], recusas);
        Assert.Equal(["1"], await Sql.ColunaAsync(conexao, "SELECT VERSAO FROM HOLERITES"));
    }

    [Fact]
    public async Task Pessoas_diferentes_publicando_holerite_ao_mesmo_tempo_gravam_todas_sem_impasse()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = await HojeAsync(conexao);
        var pessoas = new List<int>();
        for (var i = 0; i < PessoasEmParalelo; i++)
        {
            pessoas.Add(await FuncionarioAsync(conexao));
        }

        // Ninguém tem holerite da competência: a leitura da existência não pode travar o intervalo do índice (duas sessões segurariam o mesmo intervalo e os INSERT esperariam um pelo outro, erro 1213).
        for (var rodada = 1; rodada <= RodadasEmParalelo; rodada++)
        {
            var competencia = hoje.AddMonths(-rodada);
            await Task.WhenAll(pessoas.Select(pessoa => Task.Run(() => PublicarAsync(conexao, pessoa, competencia))));
        }

        Assert.Equal((long)PessoasEmParalelo * RodadasEmParalelo, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES WHERE VERSAO = 1"));
    }

    // ================================================================ SP_SUBSTITUIR_HOLERITE

    [Fact]
    public async Task Substituir_holerite_guarda_a_versao_seguinte_e_deixa_a_antiga_como_estava()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var competencia = (await HojeAsync(conexao)).AddMonths(-1);
        var primeiro = Inteiro((await PublicarAsync(conexao, pessoa, competencia, tipo: "DECIMO_TERCEIRO"))["id"]);
        var antes = await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, CAMINHO_ARQUIVO, DATA_PUBLICACAO) FROM HOLERITES WHERE ID_HOLERITE = {primeiro}");

        var saidas = await SubstituirAsync(conexao, primeiro, caminho: " holerites/corrigido.pdf ");

        var novo = Inteiro(saidas["id"]);
        Assert.NotEqual(primeiro, novo);
        Assert.Equal(2, Inteiro(saidas["versao"]));
        Assert.Equal($"{pessoa}|{competencia.Year}|{competencia.Month}|DECIMO_TERCEIRO|2|holerites/corrigido.pdf", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, CAMINHO_ARQUIVO) FROM HOLERITES WHERE ID_HOLERITE = {novo}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_HOLERITE = {novo} AND DATA_PUBLICACAO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Equal(antes, await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, CAMINHO_ARQUIVO, DATA_PUBLICACAO) FROM HOLERITES WHERE ID_HOLERITE = {primeiro}"));

        // A versão 2 agora é a atual: a terceira nasce dela, agendada.
        var hoje = await HojeAsync(conexao);
        var terceira = await SubstituirAsync(conexao, novo, publicarEm: hoje.AddDays(3));
        Assert.Equal(3, Inteiro(terceira["versao"]));
        Assert.Equal($"{Iso(hoje.AddDays(3))} 00:00:00", await LinhaAsync(conexao, $"SELECT DATE_FORMAT(DATA_PUBLICACAO, '%Y-%m-%d %H:%i:%s') FROM HOLERITES WHERE ID_HOLERITE = {Inteiro(terceira["id"])}"));
    }

    [Fact]
    public async Task Substituir_holerite_recusa_as_entradas_invalidas_o_inexistente_e_a_versao_que_ja_foi_substituida()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var primeiro = Inteiro((await PublicarAsync(conexao, pessoa, hoje.AddMonths(-1)))["id"]);

        Assert.Equal("Informe o holerite a substituir.", await RecusaAoSubstituirAsync(conexao, null));
        Assert.Equal("Informe o caminho do arquivo do holerite.", await RecusaAoSubstituirAsync(conexao, primeiro, caminho: " "));
        Assert.Equal("Informe o caminho do arquivo do holerite.", await RecusaAoSubstituirAsync(conexao, primeiro, caminho: null));
        Assert.Equal("A data de publicacao do holerite nao pode ser anterior a hoje.", await RecusaAoSubstituirAsync(conexao, primeiro, publicarEm: hoje.AddDays(-1)));
        Assert.Equal("Holerite nao encontrado.", await RecusaAoSubstituirAsync(conexao, primeiro + 1000));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));

        _ = await SubstituirAsync(conexao, primeiro);

        Assert.Equal(HoleriteDesatualizado, await RecusaAoSubstituirAsync(conexao, primeiro));
        Assert.Equal(["1", "2"], await Sql.ColunaAsync(conexao, "SELECT VERSAO FROM HOLERITES ORDER BY VERSAO"));
    }

    [Fact]
    public async Task Substituir_holerite_de_competencia_que_ficou_posterior_ao_desligamento_e_recusado_pelo_trigger()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var competencia = (await HojeAsync(conexao)).AddMonths(-1);
        var primeiro = Inteiro((await PublicarAsync(conexao, pessoa, competencia))["id"]);
        await DesligarAsync(conexao, pessoa, PrimeiroDia(competencia).AddDays(-1));

        Assert.Equal("A competencia do holerite e posterior ao desligamento.", await RecusaAoSubstituirAsync(conexao, primeiro));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM HOLERITES"));
    }

    [Fact]
    public async Task Duas_substituicoes_do_mesmo_holerite_ao_mesmo_tempo_viram_uma_versao_nova_e_uma_recusa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var primeiro = Inteiro((await PublicarAsync(conexao, pessoa, (await HojeAsync(conexao)).AddMonths(-1)))["id"]);

        var recusas = await AoMesmoTempoAsync(conexao, pessoa, () => SubstituirAsync(conexao, primeiro), () => SubstituirAsync(conexao, primeiro));

        Assert.Equal([HoleriteDesatualizado], recusas);
        Assert.Equal(["1", "2"], await Sql.ColunaAsync(conexao, "SELECT VERSAO FROM HOLERITES ORDER BY VERSAO"));
    }

    [Fact]
    public async Task Holerite_publicado_pela_procedure_nao_pode_ser_apagado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var id = Inteiro((await PublicarAsync(conexao, pessoa, (await HojeAsync(conexao)).AddMonths(-1)))["id"]);

        var erro = await Assert.ThrowsAsync<MySqlException>(() => Sql.ExecutarAsync(conexao, $"DELETE FROM HOLERITES WHERE ID_HOLERITE = {id}"));

        Assert.Equal("Exclusao fisica de holerite nao permitida.", erro.Message);
    }

    // ================================================================ SP_ENVIAR_ATESTADO

    [Fact]
    public async Task Enviar_atestado_de_dias_inteiros_nasce_ENVIADO_sem_horas_e_sem_decisao()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);

        var id = await EnviarAtestadoAsync(conexao, pessoa, hoje.AddDays(-3), hoje.AddDays(-1), caminho: " atestados/x.pdf ", nome: " meu atestado.pdf ");

        Assert.Equal(
            $"{pessoa}|{Iso(hoje.AddDays(-3))}|{Iso(hoje.AddDays(-1))}|NULL|NULL|atestados/x.pdf|meu atestado.pdf|ENVIADO|NULL|NULL|NULL",
            await LinhaAsync(
                conexao,
                $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, DATA_INICIO, DATA_FIM, IFNULL(HORA_INICIO, 'NULL'), IFNULL(HORA_FIM, 'NULL'), CAMINHO_ARQUIVO, IFNULL(NOME_ARQUIVO_ORIGINAL, 'NULL'), STATUS_ATESTADO, IFNULL(ID_FUNCIONARIO_RH, 'NULL'), IFNULL(DATA_DECISAO, 'NULL'), IFNULL(MOTIVO_RECUSA, 'NULL')) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM ATESTADOS WHERE ID_ATESTADO = {id} AND DATA_ENVIO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
    }

    [Fact]
    public async Task Enviar_atestado_de_algumas_horas_guarda_as_horas_e_o_nome_vazio_vira_nulo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var dia = (await HojeAsync(conexao)).AddDays(2); // a API não limita o período: dia futuro vale

        var id = await EnviarAtestadoAsync(conexao, pessoa, dia, dia, new TimeOnly(13, 0), new TimeOnly(17, 48), nome: "  ");

        Assert.Equal(
            $"{Iso(dia)}|{Iso(dia)}|13:00:00|17:48:00|NULL|ENVIADO",
            await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', DATA_INICIO, DATA_FIM, HORA_INICIO, HORA_FIM, IFNULL(NOME_ARQUIVO_ORIGINAL, 'NULL'), STATUS_ATESTADO) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
    }

    [Fact]
    public async Task Enviar_atestado_recusa_cada_entrada_invalida_com_a_mensagem_da_regra_e_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var inativa = await FuncionarioAsync(conexao, ativo: false);
        var dia = (await HojeAsync(conexao)).AddDays(-2);
        const string Datas = "Informe a data de inicio e a data de fim do atestado.";
        const string Juntas = "Informe a hora de inicio e a hora de fim juntas.";
        const string Minutos = "Informe as horas do atestado em horas e minutos, de 00:00 a 23:59.";

        Assert.Equal("Informe o funcionario do atestado.", await RecusaAoEnviarAtestadoAsync(conexao, null, dia, dia));
        Assert.Equal(Datas, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, null, dia));
        Assert.Equal(Datas, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, null));
        Assert.Equal("A data de fim do atestado nao pode ser antes da data de inicio.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia.AddDays(-1)));
        Assert.Equal(Juntas, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, horaInicio: new TimeOnly(8, 0)));
        Assert.Equal(Juntas, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, horaFim: new TimeOnly(12, 0)));
        Assert.Equal(Minutos, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, "08:00:30", "12:00:00"));
        Assert.Equal(Minutos, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, "08:00:00", "24:00:00"));
        Assert.Equal(Minutos, await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, "-01:00:00", "12:00:00"));
        Assert.Equal("O atestado de algumas horas vale para um dia so: use a mesma data de inicio e de fim.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia.AddDays(1), new TimeOnly(8, 0), new TimeOnly(12, 0)));
        Assert.Equal("A hora de fim do atestado precisa ser depois da hora de inicio.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, new TimeOnly(12, 0), new TimeOnly(12, 0)));
        Assert.Equal("A hora de fim do atestado precisa ser depois da hora de inicio.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, new TimeOnly(12, 0), new TimeOnly(8, 0)));
        Assert.Equal("Informe o caminho do arquivo do atestado.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, caminho: " "));
        Assert.Equal("Informe o caminho do arquivo do atestado.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa, dia, dia, caminho: null));
        Assert.Equal("Funcionario nao encontrado.", await RecusaAoEnviarAtestadoAsync(conexao, pessoa + 1000, dia, dia));
        Assert.Equal("Funcionario inativo nao pode enviar atestado.", await RecusaAoEnviarAtestadoAsync(conexao, inativa, dia, dia));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM ATESTADOS"));
    }

    // ================================================================ SP_DECIDIR_ATESTADO

    [Fact]
    public async Task Validar_atestado_grava_quem_decidiu_e_quando_sem_motivo_e_nao_mexe_no_banco_de_horas()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var hoje = await HojeAsync(conexao);
        var id = await EnviarAtestadoAsync(conexao, pessoa, hoje.AddDays(-3), hoje.AddDays(-1));

        _ = await ChamarAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "VALIDADO", "um motivo que nao vale na validacao");

        Assert.Equal($"VALIDADO|{rh}|NULL", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ATESTADO, ID_FUNCIONARIO_RH, IFNULL(MOTIVO_RECUSA, 'NULL')) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM ATESTADOS WHERE ID_ATESTADO = {id} AND DATA_DECISAO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM BANCOS_HORAS")); // o recálculo dos dias é da API
    }

    [Fact]
    public async Task Recusar_atestado_grava_o_motivo_aparado_e_o_RH_pode_decidir_o_proprio_atestado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var dia = (await HojeAsync(conexao)).AddDays(-1);
        var id = await EnviarAtestadoAsync(conexao, rh, dia, dia);
        var motivoNoLimite = new string('m', 500);

        _ = await ChamarAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "RECUSADO", $"  {motivoNoLimite}  ");

        Assert.Equal($"RECUSADO|{rh}|{motivoNoLimite}", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ATESTADO, ID_FUNCIONARIO_RH, MOTIVO_RECUSA) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM ATESTADOS WHERE ID_ATESTADO = {id} AND DATA_DECISAO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
    }

    [Fact]
    public async Task Decidir_atestado_recusa_as_entradas_invalidas_o_inexistente_e_o_RH_invalido_sem_mudar_o_atestado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var rhInativo = await FuncionarioAsync(conexao, nivel: "RH", ativo: false);
        var dia = (await HojeAsync(conexao)).AddDays(-1);
        var id = await EnviarAtestadoAsync(conexao, pessoa, dia, dia);
        const string Decisao = "Decisao de atestado invalida: informe VALIDADO ou RECUSADO.";

        Assert.Equal("Informe o atestado.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", null, rh, "VALIDADO", null));
        Assert.Equal("Informe o responsavel do RH pela decisao.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, null, "VALIDADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "APROVADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "validado", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "ENVIADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, null, null));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "RECUSADO", null));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "RECUSADO", "   "));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "RECUSADO", new string('m', 501)));
        Assert.Equal("Atestado nao encontrado.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id + 1000, rh, "VALIDADO", null));
        Assert.Equal(RhInvalido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh + 1000, "VALIDADO", null));
        Assert.Equal(RhInvalido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, rhInativo, "RECUSADO", "Ilegivel."));

        Assert.Equal("ENVIADO|NULL|NULL|NULL", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ATESTADO, IFNULL(ID_FUNCIONARIO_RH, 'NULL'), IFNULL(DATA_DECISAO, 'NULL'), IFNULL(MOTIVO_RECUSA, 'NULL')) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
    }

    [Fact]
    public async Task A_decisao_do_atestado_e_final_a_segunda_e_recusada_e_a_primeira_fica()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var outroRh = await FuncionarioAsync(conexao, nivel: "RH");
        var dia = (await HojeAsync(conexao)).AddDays(-1);
        var id = await EnviarAtestadoAsync(conexao, pessoa, dia, dia);
        _ = await ChamarAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "RECUSADO", "Ilegivel.");

        Assert.Equal(AtestadoJaDecidido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, outroRh, "VALIDADO", null));
        Assert.Equal(AtestadoJaDecidido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_ATESTADO", id, outroRh, "RECUSADO", "Outro motivo."));

        Assert.Equal($"RECUSADO|{rh}|Ilegivel.", await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ATESTADO, ID_FUNCIONARIO_RH, MOTIVO_RECUSA) FROM ATESTADOS WHERE ID_ATESTADO = {id}"));
    }

    [Fact]
    public async Task Duas_decisoes_do_mesmo_atestado_ao_mesmo_tempo_uma_vale_e_a_outra_e_recusada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var outroRh = await FuncionarioAsync(conexao, nivel: "RH");
        var dia = (await HojeAsync(conexao)).AddDays(-1);
        var id = await EnviarAtestadoAsync(conexao, pessoa, dia, dia);

        var recusas = await AoMesmoTempoAsync(
            conexao,
            pessoa,
            () => ChamarAsync(conexao, "SP_DECIDIR_ATESTADO", id, rh, "VALIDADO", null),
            () => ChamarAsync(conexao, "SP_DECIDIR_ATESTADO", id, outroRh, "RECUSADO", "Ilegivel."));

        Assert.Equal([AtestadoJaDecidido], recusas);
        var linha = await LinhaAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ATESTADO, ID_FUNCIONARIO_RH, IFNULL(MOTIVO_RECUSA, 'NULL')) FROM ATESTADOS WHERE ID_ATESTADO = {id}");
        Assert.Contains(linha, new[] { $"VALIDADO|{rh}|NULL", $"RECUSADO|{outroRh}|Ilegivel." });
    }

    // ================================================================ SP_SOLICITAR_BENEFICIO

    [Fact]
    public async Task Solicitar_beneficio_cria_o_pedido_PENDENTE_e_inativo_sem_resposta()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var beneficio = await BeneficioAsync(conexao);
        var hoje = await HojeAsync(conexao);

        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje, autoriza: false); // hoje vale como início

        Assert.Equal($"PENDENTE|0|{Iso(hoje)}|0|NULL|NULL|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id} AND ID_FUNCIONARIO = {pessoa} AND ID_BENEFICIO = {beneficio} AND DATA_PEDIDO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Empty(await DependentesAsync(conexao, id));
        Assert.Empty(await TrajetosAsync(conexao, id));
    }

    [Fact]
    public async Task Solicitar_beneficio_com_dependentes_e_trajeto_grava_os_filhos_PENDENTE_aparados_e_com_a_mesma_data()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var dependentes = $$"""[{"nome": "  Ana Lima  ", "parentesco": " Filha ", "dataNascimento": "2015-03-09"}, {"nome": "João Lima", "parentesco": "Cônjuge", "dataNascimento": "{{Iso(hoje)}}"}]""";

        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(10), dependentes: dependentes, origem: " Rua A, 10 ", destino: " Av. B, 200 ", linhas: " 175T e metro ");

        Assert.Equal($"PENDENTE|0|{Iso(hoje.AddDays(10))}|1|NULL|NULL|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(["Ana Lima|Filha|2015-03-09|PENDENTE|NULL|NULL|NULL", $"João Lima|Cônjuge|{Iso(hoje)}|PENDENTE|NULL|NULL|NULL"], await DependentesAsync(conexao, id));
        Assert.Equal(["Rua A, 10|Av. B, 200|175T e metro|PENDENTE|NULL|NULL|NULL"], await TrajetosAsync(conexao, id));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DEPENDENTES D JOIN BENEFICIOS_FUNCIONARIOS A ON A.ID_BENEFICIO_FUNCIONARIOS = D.ID_BENEFICIO_FUNCIONARIOS WHERE A.ID_BENEFICIO_FUNCIONARIOS = {id} AND D.DATA_PEDIDO = A.DATA_PEDIDO"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE T JOIN BENEFICIOS_FUNCIONARIOS A ON A.ID_BENEFICIO_FUNCIONARIOS = T.ID_BENEFICIO_FUNCIONARIOS WHERE A.ID_BENEFICIO_FUNCIONARIOS = {id} AND T.DATA_PEDIDO = A.DATA_PEDIDO"));
    }

    [Fact]
    public async Task Solicitar_beneficio_recusa_cada_entrada_invalida_e_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var amanha = Iso(hoje.AddDays(1));
        const string Lista = "Os dependentes precisam vir como uma lista JSON.";
        const string Nascimento = "Informe a data de nascimento de cada dependente no formato AAAA-MM-DD.";
        var onze = "[" + string.Join(",", Enumerable.Repeat("""{"nome": "A", "parentesco": "Filho", "dataNascimento": "2010-01-01"}""", 11)) + "]";

        Assert.Equal("Informe o funcionario que pede o beneficio.", await RecusaAoSolicitarAsync(conexao, null, beneficio, hoje));
        Assert.Equal("Informe o beneficio.", await RecusaAoSolicitarAsync(conexao, pessoa, null, hoje));
        Assert.Equal("Informe a data de inicio desejada.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, null));
        Assert.Equal("A data de inicio nao pode ser anterior a hoje.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(-1)));
        Assert.Equal("Informe uma data de inicio ate 2100.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, new DateOnly(2101, 1, 1)));
        Assert.Equal("Informe se autoriza o desconto em folha: TRUE ou FALSE.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, autoriza: null));

        var dependentesInvalidos = new (string Json, string Mensagem)[]
        {
            ("isto nao e json", Lista),
            ("""{"nome": "Ana"}""", Lista),
            (onze, "Informe no maximo 10 dependentes."),
            ("""[{"parentesco": "Filha", "dataNascimento": "2015-03-09"}]""", "Informe o nome de cada dependente."),
            ("""[{"nome": "   ", "parentesco": "Filha", "dataNascimento": "2015-03-09"}]""", "Informe o nome de cada dependente."),
            ("""["Ana"]""", "Informe o nome de cada dependente."),
            ($$"""[{"nome": "{{new string('n', 151)}}", "parentesco": "Filha", "dataNascimento": "2015-03-09"}]""", "O nome do dependente pode ter ate 150 caracteres."),
            ("""[{"nome": "Ana", "parentesco": "", "dataNascimento": "2015-03-09"}]""", "Informe o parentesco de cada dependente."),
            ($$"""[{"nome": "Ana", "parentesco": "{{new string('p', 31)}}", "dataNascimento": "2015-03-09"}]""", "O parentesco do dependente pode ter ate 30 caracteres."),
            ("""[{"nome": "Ana", "parentesco": "Filha"}]""", Nascimento),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "09/03/2015"}]""", Nascimento),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "2015-02-30"}]""", Nascimento),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "2015-13-01"}]""", Nascimento),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "2015-03-00"}]""", Nascimento),
            ($$"""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "{{amanha}}"}]""", "A data de nascimento do dependente nao pode ser futura."),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "1899-12-31"}]""", "Informe uma data de nascimento do dependente a partir de 1900."),
            ("""[{"nome": "Ana", "parentesco": "Filha", "dataNascimento": "2015-03-09"}, {"nome": "", "parentesco": "Filho", "dataNascimento": "2016-01-01"}]""", "Informe o nome de cada dependente."),
        };
        foreach (var (json, mensagem) in dependentesInvalidos)
        {
            Assert.Equal(mensagem, await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, dependentes: json));
        }

        Assert.Equal("Informe a origem do trajeto.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, destino: "B", linhas: "175T"));
        Assert.Equal("A origem do trajeto pode ter ate 200 caracteres.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, origem: new string('o', 201), destino: "B", linhas: "175T"));
        Assert.Equal("Informe o destino do trajeto.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, origem: "A", destino: "  ", linhas: "175T"));
        Assert.Equal("O destino do trajeto pode ter ate 200 caracteres.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, origem: "A", destino: new string('d', 201), linhas: "175T"));
        Assert.Equal("Informe as linhas ou os meios de transporte do trajeto.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, origem: "A", destino: "B"));
        Assert.Equal("As linhas ou os meios do trajeto podem ter ate 255 caracteres.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje, origem: "A", destino: "B", linhas: new string('l', 256)));

        Assert.Equal("Funcionario nao encontrado.", await RecusaAoSolicitarAsync(conexao, pessoa + 1000, beneficio, hoje));
        Assert.Equal("Beneficio nao encontrado.", await RecusaAoSolicitarAsync(conexao, pessoa, beneficio + 1000, hoje));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS) + (SELECT COUNT(*) FROM DEPENDENTES) + (SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE)"));
    }

    [Fact]
    public async Task Solicitar_beneficio_aceita_os_limites_dos_textos_e_a_lista_vazia_ou_nula_de_dependentes()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var noLimite = $$"""[{"nome": "{{new string('n', 150)}}", "parentesco": "{{new string('p', 30)}}", "dataNascimento": "1900-01-01"}]""";

        var cheio = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true), new DateOnly(2100, 12, 31), dependentes: noLimite, origem: new string('o', 200), destino: new string('d', 200), linhas: new string('l', 255));
        var listaVazia = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao), hoje, dependentes: "[]"); // o benefício nem aceita dependentes: lista vazia não é dependente
        var nulo = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao), hoje, dependentes: "null", origem: "  ", destino: string.Empty);

        Assert.Single(await DependentesAsync(conexao, cheio));
        Assert.Single(await TrajetosAsync(conexao, cheio));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, $"SELECT (SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS IN ({listaVazia}, {nulo})) + (SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS IN ({listaVazia}, {nulo}))"));
    }

    [Fact]
    public async Task Solicitar_beneficio_que_a_pessoa_ja_pediu_ou_ja_tem_ativo_e_recusado_sem_mudar_a_linha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var beneficio = await BeneficioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(5));
        var pendente = await AdesaoAsync(conexao, id);

        Assert.Equal(BeneficioJaPedido, await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(9), autoriza: false));
        Assert.Equal(pendente, await AdesaoAsync(conexao, id));

        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "APROVADO", null);
        var ativo = await AdesaoAsync(conexao, id);

        Assert.Equal(BeneficioJaPedido, await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(9), autoriza: false));
        Assert.Equal(ativo, await AdesaoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS"));
    }

    [Fact]
    public async Task Solicitar_beneficio_recusa_a_pessoa_inativa_o_beneficio_nao_opcional_e_os_filhos_que_o_beneficio_nao_aceita()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var inativa = await FuncionarioAsync(conexao, ativo: false);
        var hoje = await HojeAsync(conexao);
        var simples = await BeneficioAsync(conexao);
        var obrigatorio = await BeneficioAsync(conexao, opcional: false, aceitaDependentes: true, aceitaTrajeto: true);
        var soDependentes = await BeneficioAsync(conexao, aceitaDependentes: true);
        var soTrajeto = await BeneficioAsync(conexao, aceitaTrajeto: true);

        Assert.Equal("Nao e possivel pedir beneficio para funcionario inativo.", await RecusaAoSolicitarAsync(conexao, inativa, simples, hoje));
        Assert.Equal("Este beneficio nao e opcional: o RH o concede direto na ficha.", await RecusaAoSolicitarAsync(conexao, pessoa, obrigatorio, hoje));
        Assert.Equal("Este beneficio nao permite incluir dependentes.", await RecusaAoSolicitarAsync(conexao, pessoa, soTrajeto, hoje, dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T"));
        Assert.Equal("Este beneficio nao tem trajeto: so o vale-transporte tem.", await RecusaAoSolicitarAsync(conexao, pessoa, soDependentes, hoje, dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T"));

        // Ordem da API: inativo vem antes de não opcional, e não opcional antes dos filhos.
        Assert.Equal("Nao e possivel pedir beneficio para funcionario inativo.", await RecusaAoSolicitarAsync(conexao, inativa, obrigatorio, hoje));
        Assert.Equal("Este beneficio nao e opcional: o RH o concede direto na ficha.", await RecusaAoSolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao, opcional: false), hoje, dependentes: UmDependente));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS) + (SELECT COUNT(*) FROM DEPENDENTES) + (SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE)"));
    }

    [Fact]
    public async Task Solicitar_beneficio_de_quem_ja_tem_pedido_pendente_e_ficou_inativo_responde_que_ja_pediu_como_a_API()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var beneficio = await BeneficioAsync(conexao);
        var hoje = await HojeAsync(conexao);
        _ = await SolicitarAsync(conexao, pessoa, beneficio, hoje);
        await DesligarAsync(conexao, pessoa, hoje);

        Assert.Equal(BeneficioJaPedido, await RecusaAoSolicitarAsync(conexao, pessoa, beneficio, hoje));
    }

    [Fact]
    public async Task Quem_teve_o_pedido_recusado_pede_de_novo_e_a_mesma_linha_volta_a_PENDENTE_sem_a_resposta_antiga()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(5), autoriza: true, dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T");
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", "Fora do prazo.");
        await Sql.ExecutarAsync(conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET DATA_PEDIDO = DATA_PEDIDO - INTERVAL 3 DAY WHERE ID_BENEFICIO_FUNCIONARIOS = {id}");

        var reaberto = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(20), autoriza: false, dependentes: """[{"nome": "Bia Lima", "parentesco": "Filha", "dataNascimento": "2018-07-01"}]""");

        Assert.Equal(id, reaberto);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS"));
        Assert.Equal($"PENDENTE|0|{Iso(hoje.AddDays(20))}|0|NULL|NULL|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id} AND DATA_PEDIDO BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Equal(["Ana Lima|Filha|2015-03-09|CANCELADO|NULL|NULL|NULL", "Bia Lima|Filha|2018-07-01|PENDENTE|NULL|NULL|NULL"], await DependentesAsync(conexao, id));
        Assert.Equal(["A|B|175T|CANCELADO|NULL|NULL|NULL"], await TrajetosAsync(conexao, id)); // o pedido novo veio sem trajeto
    }

    [Fact]
    public async Task Quem_teve_o_beneficio_desativado_pede_de_novo_e_os_filhos_aprovados_ou_pendentes_da_adesao_antiga_sao_cancelados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje, dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T");
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "APROVADO", null);

        // Depois da aprovação: um dependente pedido e ainda pendente, um recusado (histórico), e o RH desativa o benefício na ficha.
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO) VALUES ({id}, 'Caio Lima', 'Filho', '2020-01-01', 'PENDENTE')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA, MOTIVO_RECUSA) VALUES ({id}, 'Davi Lima', 'Sobrinho', '2019-01-01', 'RECUSADO', {rh}, NOW(), 'Parentesco nao coberto.')");
        await Sql.ExecutarAsync(conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET BENEFICIO_ATIVO = FALSE WHERE ID_BENEFICIO_FUNCIONARIOS = {id}");

        var reaberto = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(1), origem: "C", destino: "D", linhas: "metro");

        Assert.Equal(id, reaberto);
        Assert.Equal($"PENDENTE|0|{Iso(hoje.AddDays(1))}|1|NULL|NULL|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(
            [$"Ana Lima|Filha|2015-03-09|CANCELADO|{rh}|respondido|NULL", "Caio Lima|Filho|2020-01-01|CANCELADO|NULL|NULL|NULL", $"Davi Lima|Sobrinho|2019-01-01|RECUSADO|{rh}|respondido|Parentesco nao coberto."],
            await DependentesAsync(conexao, id));
        Assert.Equal([$"A|B|175T|CANCELADO|{rh}|respondido|NULL", "C|D|metro|PENDENTE|NULL|NULL|NULL"], await TrajetosAsync(conexao, id));
    }

    [Fact]
    public async Task Dois_pedidos_iguais_de_beneficio_ao_mesmo_tempo_viram_um_pedido_e_uma_recusa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true);
        var hoje = await HojeAsync(conexao);

        var recusas = await AoMesmoTempoAsync(
            conexao,
            pessoa,
            () => SolicitarAsync(conexao, pessoa, beneficio, hoje, dependentes: UmDependente),
            () => SolicitarAsync(conexao, pessoa, beneficio, hoje, dependentes: UmDependente));

        Assert.Equal([BeneficioJaPedido], recusas);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM DEPENDENTES"));
    }

    [Fact]
    public async Task Pessoas_diferentes_pedindo_beneficio_ao_mesmo_tempo_gravam_todas_sem_impasse()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = await HojeAsync(conexao);
        var pessoas = new List<int>();
        for (var i = 0; i < PessoasEmParalelo; i++)
        {
            pessoas.Add(await FuncionarioAsync(conexao));
        }

        for (var rodada = 1; rodada <= RodadasEmParalelo; rodada++)
        {
            var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true);
            await Task.WhenAll(pessoas.Select(pessoa => Task.Run(() => SolicitarAsync(conexao, pessoa, beneficio, hoje, dependentes: UmDependente))));
        }

        Assert.Equal((long)PessoasEmParalelo * RodadasEmParalelo, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE STATUS_PEDIDO = 'PENDENTE'"));
        Assert.Equal((long)PessoasEmParalelo * RodadasEmParalelo, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM DEPENDENTES"));
    }

    // ================================================================ SP_DECIDIR_BENEFICIO

    [Fact]
    public async Task Aprovar_beneficio_ativa_a_adesao_e_aprova_junto_os_dependentes_e_o_trajeto_pendentes()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(5), dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T");
        var outroPedido = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao, aceitaDependentes: true), hoje, dependentes: UmDependente);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO) VALUES ({id}, 'Eva Lima', 'Filha', '2021-01-01', 'CANCELADO')");

        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "APROVADO", "um motivo que nao vale na aprovacao");

        Assert.Equal($"APROVADO|1|{Iso(hoje.AddDays(5))}|1|{rh}|respondido|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id} AND DATA_RESPOSTA BETWEEN NOW() - INTERVAL 60 SECOND AND NOW()"));
        Assert.Equal([$"Ana Lima|Filha|2015-03-09|APROVADO|{rh}|respondido|NULL", "Eva Lima|Filha|2021-01-01|CANCELADO|NULL|NULL|NULL"], await DependentesAsync(conexao, id));
        Assert.Equal([$"A|B|175T|APROVADO|{rh}|respondido|NULL"], await TrajetosAsync(conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM DEPENDENTES D JOIN BENEFICIOS_FUNCIONARIOS A ON A.ID_BENEFICIO_FUNCIONARIOS = D.ID_BENEFICIO_FUNCIONARIOS WHERE A.ID_BENEFICIO_FUNCIONARIOS = {id} AND D.DATA_RESPOSTA = A.DATA_RESPOSTA"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE T JOIN BENEFICIOS_FUNCIONARIOS A ON A.ID_BENEFICIO_FUNCIONARIOS = T.ID_BENEFICIO_FUNCIONARIOS WHERE A.ID_BENEFICIO_FUNCIONARIOS = {id} AND T.DATA_RESPOSTA = A.DATA_RESPOSTA"));

        // O outro pedido da mesma pessoa não foi tocado.
        Assert.Equal($"PENDENTE|0|{Iso(hoje)}|1|NULL|NULL|NULL", await AdesaoAsync(conexao, outroPedido));
        Assert.Equal(["Ana Lima|Filha|2015-03-09|PENDENTE|NULL|NULL|NULL"], await DependentesAsync(conexao, outroPedido));
    }

    [Fact]
    public async Task Recusar_beneficio_grava_o_motivo_aparado_e_cancela_sem_resposta_os_dependentes_e_o_trajeto_pendentes()
    {
        var conexao = await NovoBancoAsync(mysql);
        var rh = await FuncionarioAsync(conexao, nivel: "RH"); // o RH decide até o próprio pedido
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, rh, beneficio, hoje, dependentes: UmDependente, origem: "A", destino: "B", linhas: "175T");
        var motivoNoLimite = new string('m', 500);

        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", $"  {motivoNoLimite} ");

        Assert.Equal($"RECUSADO|0|{Iso(hoje)}|1|{rh}|respondido|{motivoNoLimite}", await AdesaoAsync(conexao, id));
        Assert.Equal(["Ana Lima|Filha|2015-03-09|CANCELADO|NULL|NULL|NULL"], await DependentesAsync(conexao, id));
        Assert.Equal(["A|B|175T|CANCELADO|NULL|NULL|NULL"], await TrajetosAsync(conexao, id));
    }

    [Fact]
    public async Task Decidir_beneficio_recusa_as_entradas_invalidas_o_inexistente_e_o_RH_invalido_sem_mudar_o_pedido()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var rhInativo = await FuncionarioAsync(conexao, nivel: "RH", ativo: false);
        var beneficio = await BeneficioAsync(conexao, aceitaDependentes: true);
        var id = await SolicitarAsync(conexao, pessoa, beneficio, await HojeAsync(conexao), dependentes: UmDependente);
        var antes = await AdesaoAsync(conexao, id);
        const string Decisao = "Decisao de beneficio invalida: informe APROVADO ou RECUSADO.";

        Assert.Equal("Informe o pedido de beneficio.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", null, rh, "APROVADO", null));
        Assert.Equal("Informe o responsavel do RH pela decisao.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, null, "APROVADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "VALIDADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "aprovado", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "CANCELADO", null));
        Assert.Equal(Decisao, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, null, null));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", null));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", "  "));
        Assert.Equal(MotivoObrigatorio, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", new string('m', 501)));
        Assert.Equal("Pedido de beneficio nao encontrado.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id + 1000, rh, "APROVADO", null));
        Assert.Equal(RhInvalido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh + 1000, "APROVADO", null));
        Assert.Equal(RhInvalido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rhInativo, "RECUSADO", "Fora do prazo."));

        Assert.Equal(antes, await AdesaoAsync(conexao, id));
        Assert.Equal(["Ana Lima|Filha|2015-03-09|PENDENTE|NULL|NULL|NULL"], await DependentesAsync(conexao, id));
    }

    [Fact]
    public async Task So_o_pedido_PENDENTE_se_decide_o_ja_respondido_e_o_concedido_direto_sao_recusados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var hoje = await HojeAsync(conexao);
        var aprovado = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao), hoje);
        var recusado = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao), hoje);
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", aprovado, rh, "APROVADO", null);
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", recusado, rh, "RECUSADO", "Fora do prazo.");
        var concedido = await BeneficioAsync(conexao, opcional: false);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO) VALUES ({pessoa}, {concedido}, TRUE, 'APROVADO')");
        var direto = (int)await Sql.ContarAsync(conexao, $"SELECT ID_BENEFICIO_FUNCIONARIOS FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO = {concedido}");
        var antesAprovado = await AdesaoAsync(conexao, aprovado);
        var antesRecusado = await AdesaoAsync(conexao, recusado);

        Assert.Equal(PedidoJaRespondido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", aprovado, rh, "RECUSADO", "Mudei de ideia."));
        Assert.Equal(PedidoJaRespondido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", recusado, rh, "APROVADO", null));
        Assert.Equal(PedidoJaRespondido, await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", direto, rh, "RECUSADO", "Nao e pedido."));

        Assert.Equal(antesAprovado, await AdesaoAsync(conexao, aprovado));
        Assert.Equal(antesRecusado, await AdesaoAsync(conexao, recusado));
    }

    [Fact]
    public async Task Aprovar_beneficio_de_pessoa_inativa_e_recusado_mas_recusar_o_pedido_dela_vale()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao), hoje);
        await DesligarAsync(conexao, pessoa, hoje);

        Assert.Equal("Nao e possivel ativar beneficio para funcionario inativo.", await RecusaDeNegocioAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "APROVADO", null));
        Assert.Equal($"PENDENTE|0|{Iso(hoje)}|1|NULL|NULL|NULL", await AdesaoAsync(conexao, id));

        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", "Pessoa desligada.");

        Assert.Equal($"RECUSADO|0|{Iso(hoje)}|1|{rh}|respondido|Pessoa desligada.", await AdesaoAsync(conexao, id));
    }

    [Fact]
    public async Task Duas_decisoes_do_mesmo_pedido_de_beneficio_ao_mesmo_tempo_uma_vale_e_a_outra_e_recusada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var outroRh = await FuncionarioAsync(conexao, nivel: "RH");
        var hoje = await HojeAsync(conexao);
        var id = await SolicitarAsync(conexao, pessoa, await BeneficioAsync(conexao, aceitaDependentes: true), hoje, dependentes: UmDependente);

        var recusas = await AoMesmoTempoAsync(
            conexao,
            pessoa,
            () => ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "APROVADO", null),
            () => ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, outroRh, "RECUSADO", "Fora do prazo."));

        Assert.Equal([PedidoJaRespondido], recusas);
        var estado = $"{await AdesaoAsync(conexao, id)} / {string.Join(";", await DependentesAsync(conexao, id))}";
        Assert.Contains(
            estado,
            new[]
            {
                $"APROVADO|1|{Iso(hoje)}|1|{rh}|respondido|NULL / Ana Lima|Filha|2015-03-09|APROVADO|{rh}|respondido|NULL",
                $"RECUSADO|0|{Iso(hoje)}|1|{outroRh}|respondido|Fora do prazo. / Ana Lima|Filha|2015-03-09|CANCELADO|NULL|NULL|NULL",
            });
    }

    [Fact]
    public async Task O_fluxo_inteiro_do_beneficio_pedido_recusa_novo_pedido_e_aprovacao_na_mesma_linha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var beneficio = await BeneficioAsync(conexao, aceitaTrajeto: true);
        var hoje = await HojeAsync(conexao);

        var id = await SolicitarAsync(conexao, pessoa, beneficio, hoje, origem: "A", destino: "B", linhas: "175T");
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", id, rh, "RECUSADO", "Endereco incompleto.");
        var denovo = await SolicitarAsync(conexao, pessoa, beneficio, hoje.AddDays(2), origem: "Rua A, 10", destino: "B", linhas: "175T");
        _ = await ChamarAsync(conexao, "SP_DECIDIR_BENEFICIO", denovo, rh, "APROVADO", null);

        Assert.Equal(id, denovo);
        Assert.Equal($"APROVADO|1|{Iso(hoje.AddDays(2))}|1|{rh}|respondido|NULL", await AdesaoAsync(conexao, id));
        Assert.Equal(["A|B|175T|CANCELADO|NULL|NULL|NULL", $"Rua A, 10|B|175T|APROVADO|{rh}|respondido|NULL"], await TrajetosAsync(conexao, id));
    }
}

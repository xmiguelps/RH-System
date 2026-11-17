using System.Globalization;
using System.Text.RegularExpressions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ApoioDaParteD;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary><c>SP_CONCLUIR_MODULO_TREINAMENTO</c> do <c>V007</c> (parte D), num MySQL 8.4 de verdade: o progresso, o início implícito, a conclusão no último módulo, o certificado (linha criada pelo trigger, código trocado por UUID versão 4) e as recusas.</summary>
[Collection(MySqlCollection.Nome)]
public partial class TreinamentoProceduresTests(MySqlFixture mysql)
{
    private const string Concluir = "SP_CONCLUIR_MODULO_TREINAMENTO";
    private const string NaoParticipa = "O funcionario nao participa deste treinamento.";
    private const string Encerrado = "Treinamento encerrado pelo RH: quem nao tinha comecado nao conclui mais modulos.";
    private const string InscricaoPendente = "Inscricao pendente: inscreva o funcionario no treinamento antes de concluir modulos.";
    private const string NaoLiberado = "Treinamento ainda nao liberado: precisa estar publicado e com a data de inicio ja chegada.";

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    private static partial Regex UuidVersao4();

    private static async Task<int> TreinamentoAsync(string conexao, bool publicado = true, bool obrigatorio = true, string? publico = "TODOS", int? inicioEmDias = 0, bool encerrado = false, bool emiteCertificado = true)
    {
        var titulo = $"Treinamento {Guid.NewGuid():N}";
        var inicio = inicioEmDias is { } dias ? $"CURDATE() + INTERVAL {dias.ToString(CultureInfo.InvariantCulture)} DAY" : "NULL";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS (TITULO, OBRIGATORIO, PUBLICADO, PUBLICO, DATA_INICIO, ENCERRADO_EM, EMITE_CERTIFICADO) VALUES ('{titulo}', {obrigatorio}, {publicado}, {(publico is null ? "NULL" : $"'{publico}'")}, {inicio}, {(encerrado ? "NOW()" : "NULL")}, {emiteCertificado})");
        return await IdAsync(conexao, $"SELECT ID_TREINAMENTO FROM TREINAMENTOS WHERE TITULO = '{titulo}'");
    }

    private static async Task<int> ModuloAsync(string conexao, int treinamento, int ordem)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, CONTEUDO_TEXTO) VALUES ({treinamento}, {ordem}, 'Modulo {ordem}', 'TEXTO', 'Leia com atencao.')");
        return await IdAsync(conexao, $"SELECT ID_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {treinamento} AND ORDEM = {ordem}");
    }

    private static Task LinhaAsync(string conexao, int treinamento, int pessoa, string? situacao = "INSCRITO") =>
        Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, '{situacao}')");

    /// <summary>A linha da pessoa no treinamento: situação, início, conclusão e nota.</summary>
    private static Task<string?> SituacaoAsync(string conexao, int treinamento, int pessoa) =>
        TextoAsync(conexao, $"SELECT CONCAT_WS('|', COALESCE(STATUS_TREINAMENTO, 'nula'), COALESCE(DATE_FORMAT(DATA_INICIO, '%Y-%m-%d'), 'sem inicio'), COALESCE(DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d'), 'sem conclusao'), COALESCE(NOTA, 'sem nota')) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static Task<long> ProgressoAsync(string conexao, int treinamento, int pessoa) =>
        Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO P JOIN TREINAMENTOS_MODULOS M ON M.ID_MODULO = P.ID_MODULO WHERE M.ID_TREINAMENTO = {treinamento} AND P.ID_FUNCIONARIO = {pessoa}");

    private static Task<long> CertificadosAsync(string conexao, int treinamento, int pessoa) =>
        Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static async Task<bool> ConcluirAsync(string conexao, int modulo, int pessoa) =>
        Inteiro((await ChamarAsync(conexao, Concluir, modulo, pessoa, new Saida("concluiu")))["concluiu"]) == 1;

    // ---------------------------------------------------------------- o caminho feliz

    [Fact]
    public async Task O_primeiro_modulo_grava_o_progresso_e_inicia_o_treinamento_de_quem_estava_so_inscrito()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = Iso(await DiaAsync(conexao));
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        _ = await ModuloAsync(conexao, treinamento, 2);
        await LinhaAsync(conexao, treinamento, pessoa);

        var concluiu = await ConcluirAsync(conexao, primeiro, pessoa);

        Assert.False(concluiu);
        Assert.Equal($"EM_ANDAMENTO|{hoje}|sem conclusao|sem nota", await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {primeiro} AND ID_FUNCIONARIO = {pessoa} AND DATA_CONCLUSAO BETWEEN NOW() - INTERVAL 2 MINUTE AND NOW()"));
        Assert.Equal(0L, await CertificadosAsync(conexao, treinamento, pessoa));
    }

    [Fact]
    public async Task O_ultimo_modulo_conclui_o_treinamento_e_o_certificado_nasce_com_codigo_UUID_versao_4_e_sem_arquivo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = Iso(await DiaAsync(conexao));
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        var segundo = await ModuloAsync(conexao, treinamento, 2);
        await LinhaAsync(conexao, treinamento, pessoa);
        await Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_FUNCIONARIOS SET STATUS_TREINAMENTO = 'EM_ANDAMENTO', DATA_INICIO = '2026-01-05' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

        Assert.False(await ConcluirAsync(conexao, primeiro, pessoa));
        var concluiu = await ConcluirAsync(conexao, segundo, pessoa);

        Assert.True(concluiu);
        Assert.Equal($"CONCLUIDO|2026-01-05|{hoje}|sem nota", await SituacaoAsync(conexao, treinamento, pessoa)); // quem já tinha começado guarda o próprio início
        Assert.Equal(2L, await ProgressoAsync(conexao, treinamento, pessoa));
        Assert.Equal(1L, await CertificadosAsync(conexao, treinamento, pessoa));
        Assert.Equal($"{hoje}|sem arquivo", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', DATE_FORMAT(DATA_EMISSAO, '%Y-%m-%d'), COALESCE(CAMINHO_ARQUIVO, 'sem arquivo')) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}"));
        var codigo = await TextoAsync(conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");
        Assert.Matches(UuidVersao4(), codigo!); // o UUID() do trigger é versão 1: a conferência pública da API não o aceitaria
    }

    [Fact]
    public async Task No_treinamento_de_um_modulo_so_quem_estava_inscrito_inicia_e_conclui_na_mesma_chamada_e_cada_pessoa_tem_o_proprio_codigo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = Iso(await DiaAsync(conexao));
        var uma = await FuncionarioAsync(conexao);
        var outra = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao, emiteCertificado: false);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, uma);
        await LinhaAsync(conexao, treinamento, outra);

        Assert.True(await ConcluirAsync(conexao, modulo, uma));
        Assert.True(await ConcluirAsync(conexao, modulo, outra));

        Assert.Equal($"CONCLUIDO|{hoje}|{hoje}|sem nota", await SituacaoAsync(conexao, treinamento, uma));
        Assert.Equal($"CONCLUIDO|{hoje}|{hoje}|sem nota", await SituacaoAsync(conexao, treinamento, outra));
        var codigos = await Sql.ColunaAsync(conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento}"); // a linha nasce do trigger até em treinamento que não emite certificado (a API a ignora)
        Assert.Equal(2, codigos.Distinct().Count());
        Assert.All(codigos, codigo => Assert.Matches(UuidVersao4(), codigo));
    }

    [Fact]
    public async Task Quem_estava_em_andamento_conclui_normalmente_mesmo_depois_de_o_RH_encerrar_o_treinamento()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao, encerrado: true);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa, "EM_ANDAMENTO"); // a antiga recusava qualquer conclusão em treinamento encerrado

        Assert.True(await ConcluirAsync(conexao, modulo, pessoa));

        Assert.StartsWith("CONCLUIDO|", await SituacaoAsync(conexao, treinamento, pessoa), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- repetir

    [Fact]
    public async Task Repetir_um_modulo_ja_concluido_enquanto_faltam_outros_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        _ = await ModuloAsync(conexao, treinamento, 2);
        await LinhaAsync(conexao, treinamento, pessoa);
        Assert.False(await ConcluirAsync(conexao, primeiro, pessoa));
        await Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_MODULOS_PROGRESSO SET DATA_CONCLUSAO = '2026-01-02 03:04:05' WHERE ID_MODULO = {primeiro} AND ID_FUNCIONARIO = {pessoa}");
        await Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_FUNCIONARIOS SET DATA_INICIO = '2026-01-02' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

        Assert.False(await ConcluirAsync(conexao, primeiro, pessoa));

        Assert.Equal("2026-01-02 03:04:05", await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_CONCLUSAO, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {primeiro} AND ID_FUNCIONARIO = {pessoa}")); // a antiga reescrevia a data
        Assert.Equal("EM_ANDAMENTO|2026-01-02|sem conclusao|sem nota", await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(1L, await ProgressoAsync(conexao, treinamento, pessoa));
    }

    [Fact]
    public async Task Repetir_depois_de_concluido_nao_grava_nada_e_o_codigo_do_certificado_fica_o_mesmo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa);
        Assert.True(await ConcluirAsync(conexao, modulo, pessoa));
        var codigo = await TextoAsync(conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");
        var linha = await SituacaoAsync(conexao, treinamento, pessoa);

        var deNovo = await ConcluirAsync(conexao, modulo, pessoa);

        Assert.False(deNovo); // só a chamada que concluiu diz que concluiu
        Assert.Equal(linha, await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(codigo, await TextoAsync(conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}"));
        Assert.Equal(1L, await ProgressoAsync(conexao, treinamento, pessoa));
    }

    [Fact]
    public async Task Com_todos_os_modulos_ja_concluidos_e_a_linha_ainda_em_andamento_repetir_um_modulo_conclui_o_treinamento()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        var segundo = await ModuloAsync(conexao, treinamento, 2);
        await LinhaAsync(conexao, treinamento, pessoa, "EM_ANDAMENTO");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO) VALUES ({primeiro}, {pessoa}), ({segundo}, {pessoa})"); // o que a procedure antiga deixava: progresso completo, linha parada

        var concluiu = await ConcluirAsync(conexao, segundo, pessoa);

        Assert.True(concluiu);
        Assert.StartsWith("CONCLUIDO|", await SituacaoAsync(conexao, treinamento, pessoa), StringComparison.Ordinal);
        Assert.Equal(2L, await ProgressoAsync(conexao, treinamento, pessoa));
        Assert.Equal(1L, await CertificadosAsync(conexao, treinamento, pessoa));
    }

    // ---------------------------------------------------------------- recusas

    [Fact]
    public async Task Modulo_ou_funcionario_que_nao_existe_treinamento_nao_liberado_e_pessoa_inativa_sao_recusados()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var inativa = await FuncionarioAsync(conexao, ativo: false);
        var liberado = await ModuloAsync(conexao, await TreinamentoAsync(conexao), 1);
        var rascunho = await ModuloAsync(conexao, await TreinamentoAsync(conexao, publicado: false), 1);
        var agendado = await ModuloAsync(conexao, await TreinamentoAsync(conexao, inicioEmDias: 1), 1);

        Assert.Equal("Informe o modulo e o funcionario.", await RecusaDeNegocioAsync(conexao, Concluir, null, pessoa, new Saida("concluiu")));
        Assert.Equal("Informe o modulo e o funcionario.", await RecusaDeNegocioAsync(conexao, Concluir, liberado, null, new Saida("concluiu")));
        Assert.Equal("Modulo de treinamento nao encontrado.", await RecusaDeNegocioAsync(conexao, Concluir, 999_999, pessoa, new Saida("concluiu")));
        Assert.Equal(NaoLiberado, await RecusaDeNegocioAsync(conexao, Concluir, rascunho, pessoa, new Saida("concluiu")));
        Assert.Equal(NaoLiberado, await RecusaDeNegocioAsync(conexao, Concluir, agendado, pessoa, new Saida("concluiu"))); // começa amanhã
        Assert.Equal("Funcionario inexistente ou inativo.", await RecusaDeNegocioAsync(conexao, Concluir, liberado, 999_999, new Saida("concluiu")));
        Assert.Equal("Funcionario inexistente ou inativo.", await RecusaDeNegocioAsync(conexao, Concluir, liberado, inativa, new Saida("concluiu")));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO"));
    }

    [Fact]
    public async Task Treinamento_publicado_sem_data_de_inicio_ja_esta_liberado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao, inicioEmDias: null);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa);

        Assert.True(await ConcluirAsync(conexao, modulo, pessoa));
    }

    [Fact]
    public async Task Sem_linha_da_pessoa_o_obrigatorio_e_o_de_fora_do_publico_dela_recusam_como_quem_nao_participa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var setor = await SetorAsync(conexao);
        var outroSetor = await SetorAsync(conexao);
        var pessoa = await FuncionarioAsync(conexao, setorId: setor);
        var outra = await FuncionarioAsync(conexao);
        var obrigatorio = await ModuloAsync(conexao, await TreinamentoAsync(conexao, obrigatorio: true), 1);
        var semPublico = await ModuloAsync(conexao, await TreinamentoAsync(conexao, obrigatorio: false, publico: null), 1);
        var deOutroSetor = await TreinamentoAsync(conexao, obrigatorio: false, publico: "SETORES");
        var deOutraPessoa = await TreinamentoAsync(conexao, obrigatorio: false, publico: "PESSOAS");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({deOutroSetor}, {outroSetor})");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({deOutraPessoa}, {outra})");

        Assert.Equal(NaoParticipa, await RecusaDeNegocioAsync(conexao, Concluir, obrigatorio, pessoa, new Saida("concluiu")));
        Assert.Equal(NaoParticipa, await RecusaDeNegocioAsync(conexao, Concluir, semPublico, pessoa, new Saida("concluiu")));
        Assert.Equal(NaoParticipa, await RecusaDeNegocioAsync(conexao, Concluir, await ModuloAsync(conexao, deOutroSetor, 1), pessoa, new Saida("concluiu")));
        Assert.Equal(NaoParticipa, await RecusaDeNegocioAsync(conexao, Concluir, await ModuloAsync(conexao, deOutraPessoa, 1), pessoa, new Saida("concluiu")));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS"));
    }

    [Fact]
    public async Task Sem_linha_no_opcional_do_publico_da_pessoa_falta_a_inscricao_e_se_o_RH_ja_encerrou_o_treinamento_esta_encerrado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var setor = await SetorAsync(conexao);
        var pessoa = await FuncionarioAsync(conexao, setorId: setor);
        var deTodos = await ModuloAsync(conexao, await TreinamentoAsync(conexao, obrigatorio: false), 1);
        var doSetor = await TreinamentoAsync(conexao, obrigatorio: false, publico: "SETORES");
        var daPessoa = await TreinamentoAsync(conexao, obrigatorio: false, publico: "PESSOAS");
        var encerrado = await ModuloAsync(conexao, await TreinamentoAsync(conexao, obrigatorio: false, encerrado: true), 1);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({doSetor}, {setor})");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({daPessoa}, {pessoa})");

        Assert.Equal(InscricaoPendente, await RecusaDeNegocioAsync(conexao, Concluir, deTodos, pessoa, new Saida("concluiu")));
        Assert.Equal(InscricaoPendente, await RecusaDeNegocioAsync(conexao, Concluir, await ModuloAsync(conexao, doSetor, 1), pessoa, new Saida("concluiu")));
        Assert.Equal(InscricaoPendente, await RecusaDeNegocioAsync(conexao, Concluir, await ModuloAsync(conexao, daPessoa, 1), pessoa, new Saida("concluiu")));
        Assert.Equal(Encerrado, await RecusaDeNegocioAsync(conexao, Concluir, encerrado, pessoa, new Saida("concluiu")));

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO"));
    }

    [Theory]
    [InlineData("REPROVADO")]
    [InlineData("CANCELADO")]
    [InlineData(null)]
    public async Task Linha_reprovada_cancelada_ou_sem_situacao_nao_participa(string? situacao)
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa);
        await Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_FUNCIONARIOS SET STATUS_TREINAMENTO = {(situacao is null ? "NULL" : $"'{situacao}'")} WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

        Assert.Equal(NaoParticipa, await RecusaDeNegocioAsync(conexao, Concluir, modulo, pessoa, new Saida("concluiu")));

        Assert.Equal($"{situacao ?? "nula"}|sem inicio|sem conclusao|sem nota", await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(0L, await ProgressoAsync(conexao, treinamento, pessoa));
    }

    [Fact]
    public async Task Treinamento_encerrado_fecha_a_porta_de_quem_estava_so_inscrito()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao, encerrado: true);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa);

        Assert.Equal(Encerrado, await RecusaDeNegocioAsync(conexao, Concluir, modulo, pessoa, new Saida("concluiu")));

        Assert.Equal("INSCRITO|sem inicio|sem conclusao|sem nota", await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(0L, await ProgressoAsync(conexao, treinamento, pessoa));
    }

    // ---------------------------------------------------------------- transação e corrida

    [Fact]
    public async Task Se_a_conclusao_falha_no_meio_nada_fica_gravado_nem_o_progresso_do_modulo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var treinamento = await TreinamentoAsync(conexao);
        var modulo = await ModuloAsync(conexao, treinamento, 1);
        await LinhaAsync(conexao, treinamento, pessoa);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_CERTIFICADOS (ID_TREINAMENTO, ID_FUNCIONARIO, CODIGO_VALIDACAO) VALUES ({treinamento}, {pessoa}, 'certificado-que-ja-existia')"); // o trigger vai bater na chave única ao criar o certificado

        var mensagem = await RecusaAsync(conexao, Concluir, modulo, pessoa, new Saida("concluiu"));

        Assert.Contains("UQ_TREINAMENTOS_CERTIFICADOS_TREINAMENTO_FUNCIONARIO", mensagem, StringComparison.Ordinal);
        Assert.Equal("INSCRITO|sem inicio|sem conclusao|sem nota", await SituacaoAsync(conexao, treinamento, pessoa));
        Assert.Equal(0L, await ProgressoAsync(conexao, treinamento, pessoa));
        Assert.Equal("certificado-que-ja-existia", await TextoAsync(conexao, $"SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento}"));
    }

    [Fact]
    public async Task Os_dois_ultimos_modulos_concluidos_ao_mesmo_tempo_dao_um_so_concluido_e_um_so_certificado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        var segundo = await ModuloAsync(conexao, treinamento, 2);
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var pessoa = await FuncionarioAsync(conexao);
            await LinhaAsync(conexao, treinamento, pessoa);

            var desfechos = await AoMesmoTempoAsync(
                () => TentarAsync(conexao, Concluir, primeiro, pessoa, new Saida("concluiu")),
                () => TentarAsync(conexao, Concluir, segundo, pessoa, new Saida("concluiu")));

            Assert.All(desfechos, d => Assert.True(d.Passou, d.Mensagem));
            Assert.Single(desfechos, d => Inteiro(d.Saidas!["concluiu"]) == 1); // uma das duas fechou o treinamento; sem a trava, cada uma veria só o próprio módulo e nenhuma fecharia
            Assert.StartsWith("CONCLUIDO|", await SituacaoAsync(conexao, treinamento, pessoa), StringComparison.Ordinal);
            Assert.Equal(2L, await ProgressoAsync(conexao, treinamento, pessoa));
            Assert.Equal(1L, await CertificadosAsync(conexao, treinamento, pessoa));
        }
    }

    [Fact]
    public async Task O_mesmo_modulo_concluido_duas_vezes_ao_mesmo_tempo_grava_um_progresso_so_e_nenhuma_chamada_falha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var treinamento = await TreinamentoAsync(conexao);
        var primeiro = await ModuloAsync(conexao, treinamento, 1);
        _ = await ModuloAsync(conexao, treinamento, 2);
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var pessoa = await FuncionarioAsync(conexao);
            await LinhaAsync(conexao, treinamento, pessoa);

            var desfechos = await AoMesmoTempoAsync(
                () => TentarAsync(conexao, Concluir, primeiro, pessoa, new Saida("concluiu")),
                () => TentarAsync(conexao, Concluir, primeiro, pessoa, new Saida("concluiu")));

            Assert.All(desfechos, d => Assert.True(d.Passou, d.Mensagem));
            Assert.Equal(1L, await ProgressoAsync(conexao, treinamento, pessoa));
            Assert.StartsWith("EM_ANDAMENTO|", await SituacaoAsync(conexao, treinamento, pessoa), StringComparison.Ordinal);
        }
    }
}

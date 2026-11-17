using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>O apoio dos testes de procedure: se ele mente, todos os testes das procedures mentem junto. Usa procedures criadas aqui mesmo, só para este teste.</summary>
[Collection(MySqlCollection.Nome)]
public class ProceduresDeTesteTests(MySqlFixture mysql)
{
    private async Task<string> BancoComProceduresDeApoioAsync()
    {
        var conexao = await NovoBancoAsync(mysql);
        await Sql.ExecutarAsync(conexao, "CREATE PROCEDURE SP_APOIO_SOMA(IN P_A INT, IN P_B INT, OUT P_SOMA INT, OUT P_NULO INT) BEGIN SET P_SOMA = P_A + P_B; SET P_NULO = NULL; END");
        await Sql.ExecutarAsync(conexao, "CREATE PROCEDURE SP_APOIO_RECUSA(IN P_X INT) BEGIN IF P_X < 0 THEN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Valor negativo.'; END IF; END");
        await Sql.ExecutarAsync(conexao, "CREATE PROCEDURE SP_APOIO_DATAS(IN P_DIA DATE, IN P_HORA TIME, IN P_TEXTO VARCHAR(20), IN P_OPCIONAL INT, OUT P_ECO VARCHAR(80)) BEGIN SET P_ECO = CONCAT(P_DIA, '|', P_HORA, '|', P_TEXTO, '|', COALESCE(P_OPCIONAL, 'nulo')); END");
        return conexao;
    }

    [Fact]
    public async Task Os_argumentos_entram_na_ordem_e_os_OUT_voltam_pelo_nome_inclusive_nulo()
    {
        var conexao = await BancoComProceduresDeApoioAsync();

        var saidas = await ChamarAsync(conexao, "SP_APOIO_SOMA", 2, 3, new Saida("soma"), new Saida("nulo"));

        Assert.Equal(5, Inteiro(saidas["soma"]));
        Assert.Null(saidas["nulo"]);
    }

    [Fact]
    public async Task Data_hora_texto_e_nulo_chegam_a_procedure_como_o_banco_espera()
    {
        var conexao = await BancoComProceduresDeApoioAsync();

        var saidas = await ChamarAsync(conexao, "SP_APOIO_DATAS", new DateOnly(2026, 10, 7), new TimeOnly(8, 30), "acentuação ç", null, new Saida("eco"));

        Assert.Equal("2026-10-07|08:30:00|acentuação ç|nulo", saidas["eco"]);
    }

    [Fact]
    public async Task A_recusa_de_negocio_devolve_a_mensagem_e_o_SQLSTATE_45000()
    {
        var conexao = await BancoComProceduresDeApoioAsync();

        var mensagem = await RecusaDeNegocioAsync(conexao, "SP_APOIO_RECUSA", -1);

        Assert.Equal("Valor negativo.", mensagem);
    }

    [Fact]
    public async Task Uma_chamada_que_nao_recusa_derruba_o_teste_de_recusa()
    {
        var conexao = await BancoComProceduresDeApoioAsync();

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => RecusaAsync(conexao, "SP_APOIO_RECUSA", 1));
    }

    [Fact]
    public async Task Uma_falha_que_nao_e_de_negocio_nao_passa_por_recusa_de_negocio()
    {
        var conexao = await BancoComProceduresDeApoioAsync();

        await Assert.ThrowsAnyAsync<Xunit.Sdk.XunitException>(() => RecusaDeNegocioAsync(conexao, "SP_QUE_NAO_EXISTE", 1)); // o erro é de procedure inexistente (SQLSTATE 42000), não 45000
    }

    [Fact]
    public async Task As_pessoas_de_apoio_existem_com_cargo_setor_nivel_e_gestor()
    {
        var conexao = await NovoBancoAsync(mysql);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor");
        var gestor = await GestorAsync(conexao, chefe);

        var pessoa = await FuncionarioAsync(conexao, gestorId: gestor);

        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa} AND ATIVO AND ID_GESTOR = {gestor} AND ID_CARGO IS NOT NULL AND ID_SETOR IS NOT NULL AND ID_NIVEL_ACESSO IS NOT NULL"));
        Assert.True(gestor > 0);
    }
}

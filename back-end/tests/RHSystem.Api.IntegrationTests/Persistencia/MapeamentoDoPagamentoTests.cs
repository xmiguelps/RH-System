using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Pagamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta de <c>PAGAMENTOS_EXCECOES</c> no banco REAL, relendo com OUTRO contexto: o <c>ANO</c> é do tipo <c>YEAR</c> (a sonda do Pomelo: 2020 e 2100 voltam iguais, escritos pela entidade e lidos por SQL), <c>MES</c> é <c>TINYINT</c>, a chave única <c>(ANO, MES)</c> dá 1062 e os <c>CHECK</c> recusam mês fora de 1 a
/// 12 e data fora do mês. A entidade recusa antes o que o banco recusaria.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDoPagamentoTests(MySqlFixture mysql)
{
    private static async Task<string?> TextoAsync(string conexao, string sql) => (await Sql.EscalarAsync(conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    private static async Task<MySqlException> RecusaDoSqlAsync(string conexao, string sql) => await Assert.ThrowsAsync<MySqlException>(() => Sql.ExecutarAsync(conexao, sql));

    [Theory]
    [InlineData(2020, 1, 31)]
    [InlineData(2026, 11, 6)]
    [InlineData(2100, 12, 1)]
    public async Task O_ano_do_tipo_YEAR_e_o_mes_TINYINT_vao_e_voltam_iguais(int ano, int mes, int dia)
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        int id;
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var excecao = ExcecaoDePagamento.Criar(ano, mes, new DateOnly(ano, mes, dia));
            db.ExcecoesDePagamento.Add(excecao);
            await db.SaveChangesAsync();
            id = excecao.Id;
        }

        Assert.Equal(($"{ano}", $"{mes}", $"{ano:0000}-{mes:00}-{dia:00}"), (
            await TextoAsync(conexao, $"SELECT CAST(ANO AS SIGNED) FROM PAGAMENTOS_EXCECOES WHERE ID_PAGAMENTO_EXCECAO = {id}"),
            await TextoAsync(conexao, $"SELECT CAST(MES AS SIGNED) FROM PAGAMENTOS_EXCECOES WHERE ID_PAGAMENTO_EXCECAO = {id}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PAGAMENTO, '%Y-%m-%d') FROM PAGAMENTOS_EXCECOES WHERE ID_PAGAMENTO_EXCECAO = {id}")));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.ExcecoesDePagamento.AsNoTracking().SingleAsync(e => e.Id == id);
        Assert.Equal((ano, mes, new DateOnly(ano, mes, dia)), (lida.Ano, lida.Mes, lida.DataDoPagamento));
    }

    [Fact]
    public async Task Filtrar_por_ano_e_mes_na_consulta_acha_a_linha_gravada_por_SQL()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await Sql.ExecutarAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2026, 12, '2026-12-18')");

        await using var db = ContextoDeTeste.Novo(conexao);
        var achada = await db.ExcecoesDePagamento.AsNoTracking().Where(e => e.Ano == 2026 && e.Mes == 12).SingleAsync();

        Assert.Equal(new DateOnly(2026, 12, 18), achada.DataDoPagamento);
    }

    [Fact]
    public async Task A_chave_unica_de_ano_e_mes_da_1062_na_gravacao_pela_entidade()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var primeiro = ContextoDeTeste.Novo(conexao))
        {
            primeiro.ExcecoesDePagamento.Add(ExcecaoDePagamento.Criar(2026, 11, new DateOnly(2026, 11, 6)));
            await primeiro.SaveChangesAsync();
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.ExcecoesDePagamento.Add(ExcecaoDePagamento.Criar(2026, 11, new DateOnly(2026, 11, 9)));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());

        Assert.Equal(1062, Assert.IsType<MySqlException>(erro.InnerException).Number);
        Assert.Contains("UQ_PAGAMENTOS_EXCECOES_ANO_MES", erro.InnerException.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Os_CHECK_do_banco_recusam_mes_fora_de_1_a_12_e_data_fora_do_mes()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        var mes13 = await RecusaDoSqlAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2026, 13, '2026-12-31')");
        var dataEmOutroMes = await RecusaDoSqlAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2026, 10, '2026-11-05')");
        var dataEmOutroAno = await RecusaDoSqlAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2026, 10, '2027-10-05')");

        Assert.Contains("CHK_PAGAMENTOS_EXCECOES", mes13.Message, StringComparison.Ordinal); // o mês 13 fere os dois CHECK (nenhuma data cai no mês 13); o banco cita um deles
        Assert.Contains("CHK_PAGAMENTOS_EXCECOES_DATA_NO_MES", dataEmOutroMes.Message, StringComparison.Ordinal);
        Assert.Contains("CHK_PAGAMENTOS_EXCECOES_DATA_NO_MES", dataEmOutroAno.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(2019, 10, 2019, 10, 5)]
    [InlineData(2101, 10, 2101, 10, 5)]
    [InlineData(2026, 0, 2026, 1, 5)]
    [InlineData(2026, 13, 2026, 12, 5)]
    [InlineData(2026, 10, 2026, 11, 5)]
    [InlineData(2026, 10, 2027, 10, 5)]
    public void A_entidade_recusa_antes_o_que_o_banco_recusaria(int ano, int mes, int anoDaData, int mesDaData, int dia)
    {
        Assert.ThrowsAny<ArgumentOutOfRangeException>(() => ExcecaoDePagamento.Criar(ano, mes, new DateOnly(anoDaData, mesDaData, dia)));
    }

    [Fact]
    public void Substituir_troca_a_data_dentro_do_mes_e_recusa_a_de_outro_mes()
    {
        var excecao = ExcecaoDePagamento.Criar(2026, 11, new DateOnly(2026, 11, 6));

        excecao.Substituir(new DateOnly(2026, 11, 30));

        Assert.Equal(new DateOnly(2026, 11, 30), excecao.DataDoPagamento);
        Assert.Throws<ArgumentOutOfRangeException>(() => excecao.Substituir(new DateOnly(2026, 12, 1)));
        Assert.Equal(("11/2026", "Exceção de pagamento de 11/2026"), (excecao.NomeParaAuditoria, excecao.RegistroAfetado));
    }
}

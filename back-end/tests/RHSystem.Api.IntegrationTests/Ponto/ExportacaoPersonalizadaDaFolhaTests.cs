using System.Text;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A exportação do ponto para a folha PERSONALIZADA (decisão do dono, 2026-11-17): <c>colunas</c> (as que saem e a ordem), <c>formatoHoras</c> (<c>MINUTOS</c>, <c>HORAS_DECIMAIS</c> ou <c>HH_MM</c>) e <c>separador</c> (<c>PONTO_E_VIRGULA</c> ou <c>VIRGULA</c>), todos opcionais: sem eles o arquivo é o de sempre
/// (<see cref="ExportacaoDaFolhaTests"/>). Classe à parte porque cada teste semeia o setembro do Diego e os da classe antiga contam exportações e números exatos no mesmo banco.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ExportacaoPersonalizadaDaFolhaTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/ponto/folha/exportacao";

    private const string Cabecalho = "matricula;nome;setor;cargo;dias_trabalhados;horas_trabalhadas_min;horas_previstas_min;atrasos;faltas;faltas_compensadas;banco_credito_do_mes_min;banco_debito_do_mes_min";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ExportacaoPersonalizadaDaFolhaTests));
        await ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo da folha {Guid.NewGuid():N}", 44m);
        var e = api.Elenco;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({e.MembroA.FuncionarioId}, {e.MembroB.FuncionarioId}, {e.DeOutroSetor.FuncionarioId}, {e.Gestor.FuncionarioId}, {e.Inativo.FuncionarioId})");
        return api;
    }

    private static async Task<HttpResponseMessage> ExportarAsync(ApiDeAcesso api, string consulta) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?{consulta}");

    private static async Task<(byte[] Bytes, List<string[]> Linhas)> LerAsync(HttpResponseMessage resposta)
    {
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3); // sem o BOM
        return (bytes, [.. texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Select(l => l.Split(';'))]);
    }

    private static string[] LinhaDe(List<string[]> linhas, string nome) => Assert.Single(linhas, l => l[1] == nome);

    private static string Utc(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    private static Task LinhaDoBancoAsync(ApiDeAcesso api, int funcionarioId, string dia, string tipo, string horas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, OBSERVACAO, DIA_REFERENCIA) VALUES ({funcionarioId}, '{dia}', '{tipo}', {horas}, 'teste', '{dia}')");

    // ---------------------------------------------------------------- a exportação personalizada (decisão do dono, 2026-11-17)

    /// <summary>O setembro do Diego do teste dos números: 2 dias, 1041 min trabalhados, 10560 previstos, 1 atraso, 17 faltas, 1 compensada, 154 min de crédito e 30 de débito.</summary>
    private static async Task<(string Nome, int Id)> SetembroDoDiegoAsync(ApiDeAcesso api)
    {
        var diego = api.Elenco.MembroA;
        if (await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {diego.FuncionarioId}") > 0)
        {
            return (diego.Nome, diego.FuncionarioId); // os testes da classe dividem o banco: o setembro dele é semeado uma vez só
        }

        var dia1 = new DateOnly(2026, 9, 1);
        var dia2 = new DateOnly(2026, 9, 2);
        await BaterEmAsync(api.Conexao, diego.FuncionarioId, dia1, (TipoDeMarcacao.Entrada, Utc(dia1, 11)), (TipoDeMarcacao.Saida, Utc(dia1, 15)), (TipoDeMarcacao.Entrada, Utc(dia1, 16)), (TipoDeMarcacao.Saida, Utc(dia1, 20, 48)));
        await BaterEmAsync(api.Conexao, diego.FuncionarioId, dia2, (TipoDeMarcacao.Entrada, Utc(dia2, 11, 15)), (TipoDeMarcacao.Saida, Utc(dia2, 15)), (TipoDeMarcacao.Entrada, Utc(dia2, 16)), (TipoDeMarcacao.Saida, Utc(dia2, 20, 48)));
        await NovoAtestadoAsync(api.Conexao, diego.FuncionarioId, "2026-09-03", "2026-09-03", "VALIDADO", api.Elenco.Rh.FuncionarioId);
        var incompleto = new DateOnly(2026, 9, 4);
        await BaterEmAsync(api.Conexao, diego.FuncionarioId, incompleto, (TipoDeMarcacao.Entrada, Utc(incompleto, 11)));
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-01", "CREDITO", "0.50");
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-10", "CREDITO", "2.00");
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-11", "DEBITO", "0.50");
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-30", "CREDITO", "0.07");
        return (diego.Nome, diego.FuncionarioId);
    }

    [Fact]
    public async Task As_colunas_escolhidas_saem_so_elas_na_ordem_pedida_com_espacos_e_caixa_toleradas()
    {
        var api = await ApiAsync();
        var (nome, _) = await SetembroDoDiegoAsync(api);

        var resposta = await ExportarAsync(api, "ano=2026&mes=9&colunas=horas_previstas,%20NOME,atrasos");
        var (_, linhas) = await LerAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("horas_previstas_min;nome;atrasos", string.Join(';', linhas[0]));
        Assert.Equal(["10560", nome, "1"], Assert.Single(linhas, l => l[1] == nome));
    }

    [Fact]
    public async Task Colunas_vazio_vale_como_sem_colunas_e_sai_o_arquivo_de_sempre()
    {
        var api = await ApiAsync();

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9&colunas=&formatoHoras=&separador="));

        Assert.Equal(Cabecalho, string.Join(';', linhas[0]));
    }

    [Fact]
    public async Task Horas_decimais_saem_com_duas_casas_arredondadas_para_cima_virgula_decimal_e_o_cabecalho_termina_em_h()
    {
        var api = await ApiAsync();
        var (nome, _) = await SetembroDoDiegoAsync(api);

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9&formatoHoras=HORAS_DECIMAIS"));

        Assert.Equal("matricula;nome;setor;cargo;dias_trabalhados;horas_trabalhadas_h;horas_previstas_h;atrasos;faltas;faltas_compensadas;banco_credito_do_mes_h;banco_debito_do_mes_h", string.Join(';', linhas[0]));
        Assert.Equal(["2", "17,35", "176,00", "1", "17", "1", "2,57", "0,50"], LinhaDe(linhas, nome)[4..]); // 1041/60 = 17,35; 154/60 = 2,5666 sobe para 2,57
    }

    [Fact]
    public async Task Horas_em_HH_MM_saem_com_dois_digitos_e_o_total_do_mes_pode_passar_de_cem_horas()
    {
        var api = await ApiAsync();
        var (nome, _) = await SetembroDoDiegoAsync(api);

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9&formatoHoras=HH_MM"));

        Assert.Equal("matricula;nome;setor;cargo;dias_trabalhados;horas_trabalhadas_hhmm;horas_previstas_hhmm;atrasos;faltas;faltas_compensadas;banco_credito_do_mes_hhmm;banco_debito_do_mes_hhmm", string.Join(';', linhas[0]));
        Assert.Equal(["2", "17:21", "176:00", "1", "17", "1", "02:34", "00:30"], LinhaDe(linhas, nome)[4..]);
    }

    [Fact]
    public async Task Com_o_separador_virgula_o_decimal_vira_ponto_e_a_celula_com_virgula_no_texto_leva_aspas()
    {
        var api = await ApiAsync();
        var (nome, id) = await SetembroDoDiegoAsync(api);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET NOME = 'Silva, Diego' WHERE ID_FUNCIONARIO = {id}");
        try
        {
            var resposta = await ExportarAsync(api, "ano=2026&mes=9&colunas=nome,horas_trabalhadas,banco_credito_do_mes&formatoHoras=HORAS_DECIMAIS&separador=VIRGULA");
            var bytes = await resposta.Content.ReadAsByteArrayAsync();
            var texto = new UTF8Encoding(false).GetString(bytes, 3, bytes.Length - 3);

            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.StartsWith("nome,horas_trabalhadas_h,banco_credito_do_mes_h\r\n", texto, StringComparison.Ordinal);
            Assert.Contains("\"Silva, Diego\",17.35,2.57\r\n", texto, StringComparison.Ordinal);
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET NOME = '{nome}' WHERE ID_FUNCIONARIO = {id}"); // os outros testes da classe procuram o nome de sempre
        }
    }

    [Fact]
    public async Task O_separador_ponto_e_virgula_pedido_de_forma_explicita_e_o_mesmo_do_padrao()
    {
        var api = await ApiAsync();

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9&separador=PONTO_E_VIRGULA&formatoHoras=MINUTOS"));

        Assert.Equal(Cabecalho, string.Join(';', linhas[0]));
    }

    [Theory]
    [InlineData("colunas=nome,salario", "colunas")]
    [InlineData("colunas=nome,nome", "colunas")]
    [InlineData("colunas=nome,,setor", "colunas")]
    [InlineData("formatoHoras=SEGUNDOS", "formatoHoras")]
    [InlineData("formatoHoras=horas_decimais", "formatoHoras")]
    [InlineData("separador=TAB", "separador")]
    public async Task Coluna_desconhecida_ou_repetida_formato_ou_separador_invalido_da_400_com_o_erro_no_campo_e_nao_exporta_nada(string extra, string campo)
    {
        var api = await ApiAsync();
        var antes = (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId)).Count;

        var resposta = await ExportarAsync(api, $"ano=2026&mes=9&{extra}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True(System.Text.Json.JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal(antes, (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId)).Count);
    }

    [Fact]
    public async Task Todos_os_erros_juntos_o_ano_a_coluna_e_o_separador()
    {
        var api = await ApiAsync();

        var resposta = await ExportarAsync(api, "ano=1999&mes=9&colunas=salario&separador=TAB");
        var erros = System.Text.Json.JsonDocument.Parse(await resposta.Content.ReadAsStringAsync()).RootElement.GetProperty("errors");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True(erros.TryGetProperty("ano", out _) && erros.TryGetProperty("colunas", out _) && erros.TryGetProperty("separador", out _));
    }

    [Fact]
    public async Task A_resposta_com_os_parametros_novos_segue_o_contrato()
    {
        var api = await ApiAsync();

        await (await ExportarAsync(api, "ano=2026&mes=9&colunas=nome,horas_trabalhadas&formatoHoras=HH_MM&separador=VIRGULA")).AfirmarConformeAoContratoAsync("exportarPontoParaFolha");
        await (await ExportarAsync(api, "ano=2026&mes=9&colunas=salario")).AfirmarConformeAoContratoAsync("exportarPontoParaFolha"); // 400
    }
}

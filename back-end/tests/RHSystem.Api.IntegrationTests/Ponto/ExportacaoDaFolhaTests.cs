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
/// A exportação do ponto do mês para a folha (<c>GET /api/rh/ponto/folha/exportacao?ano=&amp;mes=</c>, só o RH): um CSV UTF-8 com BOM, separador <c>;</c>, fim de linha CRLF, uma linha por pessoa que esteve ativa em algum dia do mês (admitida até o fim do mês e não desligada
/// antes do começo dele), com as colunas do contrato. Nenhum número é negativo (crédito e débito do banco vão em colunas separadas) e o texto que começa com <c>=</c>, <c>+</c>, <c>-</c> ou <c>@</c> ganha um apóstrofo. O relógio está em 2026-10-07T12:42Z e a carga do dia é de 8h48.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ExportacaoDaFolhaTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/ponto/folha/exportacao";

    private const string Cabecalho = "matricula;nome;setor;cargo;dias_trabalhados;horas_trabalhadas_min;horas_previstas_min;atrasos;faltas;faltas_compensadas;banco_credito_do_mes_min;banco_debito_do_mes_min";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ExportacaoDaFolhaTests));
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

    // ---------------------------------------------------------------- o arquivo

    [Fact]
    public async Task O_arquivo_e_um_csv_UTF8_com_BOM_cabecalho_do_contrato_ponto_e_virgula_e_CRLF_e_sai_como_anexo()
    {
        var api = await ApiAsync();

        var resposta = await ExportarAsync(api, "ano=2026&mes=8");
        var (bytes, linhas) = await LerAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Equal("text/csv; charset=utf-8", resposta.Content.Headers.ContentType!.ToString());
        Assert.Equal("attachment; filename=\"ponto-2026-08.csv\"", resposta.Content.Headers.ContentDisposition!.ToString());
        Assert.Equal(Cabecalho, string.Join(';', linhas[0]));
        Assert.All(linhas, l => Assert.Equal(12, l.Length));
        Assert.Contains("\r\n", Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain("\n", Encoding.UTF8.GetString(bytes).Replace("\r\n", string.Empty, StringComparison.Ordinal)); // só CRLF
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var nomes = linhas.Skip(1).Select(l => l[1]).ToList();
        var ordemDoBanco = (await db.Funcionarios.AsNoTracking().OrderBy(f => f.Nome).ThenBy(f => f.Id).Select(f => f.Nome).ToListAsync()).Where(nomes.Contains).ToList();
        Assert.Equal(ordemDoBanco, nomes); // por nome, como o banco ordena (sem diferenciar caixa nem acento)
    }

    [Fact]
    public async Task Os_numeros_do_mes_de_uma_pessoa_dias_horas_previstas_atrasos_faltas_compensadas_e_banco_do_mes()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var dia1 = new DateOnly(2026, 9, 1);
        var dia2 = new DateOnly(2026, 9, 2);
        await BaterEmAsync(api.Conexao, diego.FuncionarioId, dia1, (TipoDeMarcacao.Entrada, Utc(dia1, 11)), (TipoDeMarcacao.Saida, Utc(dia1, 15)), (TipoDeMarcacao.Entrada, Utc(dia1, 16)), (TipoDeMarcacao.Saida, Utc(dia1, 20, 48))); // 08:00 a 12:00 e 13:00 a 17:48: 528
        await BaterEmAsync(api.Conexao, diego.FuncionarioId, dia2, (TipoDeMarcacao.Entrada, Utc(dia2, 11, 15)), (TipoDeMarcacao.Saida, Utc(dia2, 15)), (TipoDeMarcacao.Entrada, Utc(dia2, 16)), (TipoDeMarcacao.Saida, Utc(dia2, 20, 48))); // entrou 08:15: atraso; 225 + 288 = 513
        await NovoAtestadoAsync(api.Conexao, diego.FuncionarioId, "2026-09-03", "2026-09-03", "VALIDADO", api.Elenco.Rh.FuncionarioId); // dia inteiro: falta compensada
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-08-31", "DEBITO", "9.00"); // fora do mês
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-10", "CREDITO", "2.00"); // 120
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-11", "DEBITO", "0.50"); // 30
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-09-30", "CREDITO", "0.07"); // 4 (4,2 minutos)
        await LinhaDoBancoAsync(api, diego.FuncionarioId, "2026-10-01", "CREDITO", "5.00"); // fora do mês

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9"));
        var linha = LinhaDe(linhas, diego.Nome);

        // 22 dias úteis em setembro, 7/9 é feriado: 21 com expediente, 1 deles com atestado (sem carga): 20 x 528 = 10560 previstos; 2 trabalhados, 1 compensado: 18 faltas.
        Assert.Equal(["2", "1041", "10560", "1", "18", "1", "124", "30"], linha[4..]);
    }

    [Fact]
    public async Task Quem_saiu_no_meio_do_mes_entra_e_quem_saiu_antes_do_mes_ou_so_entra_depois_nao()
    {
        var api = await ApiAsync();
        var noMeio = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Saiu No Meio", $"{Guid.NewGuid():N}@folha.com.br", new DateOnly(2026, 1, 5), true, "Funcionario");
        var antes = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Saiu Antes", $"{Guid.NewGuid():N}@folha.com.br", new DateOnly(2026, 1, 5), true, "Funcionario");
        var entrouNoMeio = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Entrou No Meio", $"{Guid.NewGuid():N}@folha.com.br", new DateOnly(2026, 9, 15), true, "Funcionario");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-15' WHERE ID_FUNCIONARIO = {noMeio}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-08-31' WHERE ID_FUNCIONARIO = {antes}");

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=9"));
        var nomes = linhas.Skip(1).Select(l => l[1]).ToList();

        Assert.Contains("Saiu No Meio", nomes);
        Assert.Contains("Entrou No Meio", nomes);
        Assert.DoesNotContain("Saiu Antes", nomes);
        Assert.DoesNotContain(api.Elenco.EmAdmissao.Nome, nomes); // começa só em 13/10
        Assert.Contains(api.Elenco.Inativo.Nome, nomes); // desligado em 30/09: trabalhou no mês
        Assert.DoesNotContain(api.Elenco.Inativo.Nome, (await LerAsync(await ExportarAsync(api, "ano=2026&mes=10"))).Linhas.Skip(1).Select(l => l[1])); // e em outubro já não está
        Assert.True(entrouNoMeio > 0);
    }

    [Fact]
    public async Task Nome_com_igual_e_neutralizado_matricula_nula_vai_vazia_e_nenhum_numero_e_negativo()
    {
        var api = await ApiAsync();
        var perigoso = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "=HYPERLINK(\"x\")", $"{Guid.NewGuid():N}@folha.com.br", new DateOnly(2026, 1, 5), true, "Funcionario");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET MATRICULA = NULL WHERE ID_FUNCIONARIO = {perigoso}");
        await LinhaDoBancoAsync(api, api.Elenco.MembroB.FuncionarioId, "2026-07-10", "DEBITO", "3.00");

        var (_, linhas) = await LerAsync(await ExportarAsync(api, "ano=2026&mes=7"));

        var linha = Assert.Single(linhas, l => l[1].Contains("HYPERLINK", StringComparison.Ordinal));
        Assert.Equal(("", "\"'=HYPERLINK(\"\"x\"\")\""), (linha[0], linha[1])); // matrícula vazia; o nome ganha o apóstrofo e, por ter aspas, vai entre aspas com elas dobradas
        Assert.All(linhas.Skip(1), l => Assert.All(l[4..], numero => Assert.DoesNotContain("-", numero, StringComparison.Ordinal)));
        Assert.Equal("180", LinhaDe(linhas, api.Elenco.MembroB.Nome)[11]); // o débito vai positivo, na coluna dele
    }

    [Fact]
    public async Task Mes_futuro_sai_com_as_linhas_zeradas_sem_erro()
    {
        var api = await ApiAsync();

        var resposta = await ExportarAsync(api, "ano=2026&mes=12");
        var (_, linhas) = await LerAsync(resposta);
        var linha = LinhaDe(linhas, api.Elenco.MembroA.Nome);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(["0", "0", "0", "0", "0", "0", "0", "0"], linha[4..]);
    }

    // ---------------------------------------------------------------- erros, perfis, auditoria e contrato

    [Theory]
    [InlineData("")]
    [InlineData("ano=2026")]
    [InlineData("mes=9")]
    [InlineData("ano=1999&mes=9")]
    [InlineData("ano=2101&mes=9")]
    [InlineData("ano=2026&mes=0")]
    [InlineData("ano=2026&mes=13")]
    [InlineData("ano=abc&mes=9")]
    public async Task Ano_ou_mes_ausente_ou_fora_da_faixa_da_400(string consulta)
    {
        var api = await ApiAsync();

        Assert.Equal(400, (int)(await ExportarAsync(api, consulta)).StatusCode);
    }

    [Fact]
    public async Task So_o_RH_exporta_e_sem_login_e_401()
    {
        var api = await ApiAsync();

        foreach (var intruso in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(intruso.UsuarioId)).GetAsync($"{Rota}?ano=2026&mes=9")).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync($"{Rota}?ano=2026&mes=9")).StatusCode);
    }

    [Fact]
    public async Task A_exportacao_deixa_um_evento_sem_nome_nem_hora_de_ninguem()
    {
        var api = await ApiAsync();

        await ExportarAsync(api, "ano=2026&mes=6");

        var eventos = await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId);
        var evento = Assert.Single(eventos, e => e.Descricao.Contains("folha", StringComparison.Ordinal));
        Assert.Equal((AuditoriaArea.Ponto, AuditoriaAcao.Baixou, "Exportou o ponto de 06/2026 para a folha (CSV)."), (evento.Area, evento.Acao, evento.Descricao));
    }

    [Fact]
    public async Task A_resposta_segue_o_contrato()
    {
        var api = await ApiAsync();

        await (await ExportarAsync(api, "ano=2026&mes=9")).AfirmarConformeAoContratoAsync("exportarPontoParaFolha");
        await (await ExportarAsync(api, "ano=2026&mes=13")).AfirmarConformeAoContratoAsync("exportarPontoParaFolha"); // 400
    }
}

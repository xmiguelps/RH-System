using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Spreadsheet;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Infrastructure.Importacoes;
using static RHSystem.Api.IntegrationTests.Importacoes.PlanilhasDeTeste;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>
/// <c>GET /api/rh/importacoes/modelo</c> (<c>baixarModeloDeImportacao</c>): só o RH; devolve o <c>.xlsx</c> como anexo, gerado na hora com os cargos, setores e benefícios de hoje. No banco REAL com o relógio fixo em
/// 2026-10-07T12:42Z.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ModeloDeImportacaoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/importacoes/modelo";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(ModeloDeImportacaoTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static async Task<byte[]> BaixarAsync(ApiDeAcesso api)
    {
        var resposta = await ComoRh(api).GetAsync(Rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await resposta.Content.ReadAsByteArrayAsync();
    }

    [Fact]
    public async Task RH_baixa_o_modelo_como_anexo_com_o_nome_e_o_tipo_do_contrato()
    {
        var api = await ApiAsync();

        var resposta = await ComoRh(api).GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", resposta.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", resposta.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Equal("modelo-importacao-colaboradores.xlsx", resposta.Content.Headers.ContentDisposition?.FileName?.Trim('"'));
        Assert.True((await resposta.Content.ReadAsByteArrayAsync()).Length > 0);
    }

    [Fact]
    public async Task Funcionario_e_Gestor_recebem_403_e_sem_token_401()
    {
        var api = await ApiAsync();

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).GetAsync(Rota)).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode);
    }

    [Fact]
    public async Task Aba_Colaboradores_tem_os_11_titulos_do_contrato_na_ordem_e_nenhuma_linha_de_dados()
    {
        var api = await ApiAsync();

        var modelo = await BaixarAsync(api);

        Assert.Equal<IEnumerable<string>>(["Colaboradores", "Valores válidos"], NomesDasAbas(modelo)); // a que se preenche vem primeiro
        var linhas = LerAba(modelo, "Colaboradores");
        var cabecalho = Assert.Single(linhas);
        Assert.Equal<IEnumerable<string?>>(
            ["Nome completo", "CPF", "Data de nascimento", "Telefone", "E-mail corporativo", "Data de admissão", "Tipo de contrato", "Cargo", "Setor", "Nível de acesso", "Benefícios"],
            cabecalho);
    }

    [Fact]
    public async Task Aba_Valores_validos_traz_os_cargos_setores_tipos_niveis_e_beneficios_de_hoje_em_ordem_de_nome()
    {
        var api = await ApiAsync();
        var sufixo = Guid.NewGuid().ToString("N")[..8];
        await SementeDoCadastro.CargoAsync(api.Conexao, $"Zeta cargo {sufixo}", 40);
        await SementeDoCadastro.CargoAsync(api.Conexao, $"Alfa cargo {sufixo}", 40);
        await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor do modelo {sufixo}");
        await SementeDoCadastro.BeneficioAsync(api.Conexao, $"Beneficio do modelo {sufixo}");

        var linhas = LerAba(await BaixarAsync(api), "Valores válidos");

        Assert.Equal<IEnumerable<string?>>(["Cargos", "Setores", "Tipos de contrato", "Níveis de acesso", "Benefícios"], linhas[0]);
        List<string> Coluna(int indice) => linhas.Skip(1).Select(l => l.Length > indice ? l[indice] : null).OfType<string>().ToList();
        Assert.Equal(await Sql.ColunaAsync(api.Conexao, "SELECT NOME_CARGO FROM CARGOS ORDER BY NOME_CARGO"), Coluna(0));
        Assert.Equal(await Sql.ColunaAsync(api.Conexao, "SELECT NOME_SETOR FROM SETORES ORDER BY NOME_SETOR"), Coluna(1));
        Assert.Equal(await Sql.ColunaAsync(api.Conexao, "SELECT NOME_BENEFICIO FROM BENEFICIOS ORDER BY NOME_BENEFICIO"), Coluna(4));
        Assert.Equal(["CLT", "Estágio"], Coluna(2));
        Assert.Equal(["Colaborador", "Gestor", "RH"], Coluna(3));
        Assert.Contains($"Alfa cargo {sufixo}", Coluna(0)); // o que acabou de ser criado já está (o arquivo é gerado na hora)
        Assert.True(Coluna(0).IndexOf($"Alfa cargo {sufixo}") < Coluna(0).IndexOf($"Zeta cargo {sufixo}"));
    }

    [Fact]
    public async Task As_tres_listas_pedem_ORDER_BY_ao_banco_e_nao_contam_com_a_ordem_do_indice()
    {
        // O MySQL devolve CARGOS, SETORES e BENEFICIOS em ordem de nome mesmo sem ORDER BY (lê o índice único do nome, que cobre a consulta): o teste de dados acima não
        // enxerga a diferença. A garantia é do ORDER BY, que só aparece no SQL.
        var api = await ApiAsync();
        var logs = new ColetorDeLogs();
        // O texto dos comandos do EF só sai de Information para baixo, e o appsettings o deixa em Warning (o SQL leva CPF e e-mail inline); aqui, só neste teste, ele volta a sair.
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l =>
        {
            l.AddProvider(logs);
            l.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Information);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = ComoRh(api).DefaultRequestHeaders.Authorization;

        Assert.Equal(200, (int)(await cliente.GetAsync(Rota)).StatusCode);

        string[] comandos;
        lock (logs.Mensagens)
        {
            comandos = logs.Mensagens.Where(m => m.Contains("Executed DbCommand")).ToArray();
        }

        foreach (var tabela in new[] { "CARGOS", "SETORES", "BENEFICIOS" })
        {
            var consulta = Assert.Single(comandos, m => m.Contains($"FROM `{tabela}`"));
            Assert.Contains("ORDER BY", consulta);
        }
    }

    [Fact]
    public async Task O_documento_da_API_descreve_o_200_como_arquivo_binario_do_Excel_com_o_Content_Disposition()
    {
        var api = await ApiAsync();

        var documento = await CarregadorDeContrato.GeradoAsync(api.Api.CreateClient());

        var resposta = documento["paths"]!["/api/rh/importacoes/modelo"]!["get"]!["responses"]!["200"]!;
        var esquema = resposta["content"]!["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"]!["schema"]!;
        Assert.Equal("string", (string?)esquema["type"]);
        Assert.Equal("binary", (string?)esquema["format"]);
        Assert.NotNull(resposta["headers"]!["Content-Disposition"]);
    }

    [Fact]
    public async Task O_modelo_baixado_e_aceito_pelo_leitor_e_da_PLANILHA_VAZIA()
    {
        var api = await ApiAsync();
        var modelo = await BaixarAsync(api);

        var erro = Assert.Throws<ProblemaDeApiException>(() => new LeitorDePlanilhaOpenXml().Ler(new MemoryStream(modelo)));

        Assert.Equal("PLANILHA_VAZIA", erro.Codigo); // o cabeçalho confere; só não há linha de dados
    }

    [Fact]
    public async Task Colunas_de_CPF_e_telefone_sao_texto_e_as_de_data_tem_formato_de_data()
    {
        var api = await ApiAsync();
        var modelo = await BaixarAsync(api);

        using var documento = SpreadsheetDocument.Open(new MemoryStream(modelo), false);
        var pasta = documento.WorkbookPart!;
        var aba = pasta.Workbook!.Descendants<Sheet>().Single(s => s.Name?.Value == "Colaboradores");
        var parte = (WorksheetPart)pasta.GetPartById(aba.Id!.Value!);
        var estilos = pasta.WorkbookStylesPart!.Stylesheet!;
        var formatos = estilos.CellFormats!.Elements<CellFormat>().ToList();
        var codigos = estilos.NumberingFormats?.Elements<NumberingFormat>().ToDictionary(n => n.NumberFormatId!.Value, n => n.FormatCode!.Value!) ?? [];

        string FormatoDaColuna(int coluna)
        {
            var estilo = parte.Worksheet!.Elements<Columns>().Single().Elements<Column>().Single(c => c.Min!.Value == coluna).Style!.Value;
            var id = formatos[(int)estilo].NumberFormatId!.Value;
            return codigos.TryGetValue(id, out var codigo) ? codigo : id.ToString();
        }

        var titulo = parte.Worksheet!.Descendants<Cell>().First();
        var fonteDoTitulo = estilos.Fonts!.Elements<Font>().ElementAt((int)formatos[(int)titulo.StyleIndex!.Value].FontId!.Value);
        Assert.NotNull(fonteDoTitulo.Bold); // o cabeçalho sai em negrito

        Assert.Equal("49", FormatoDaColuna(2)); // CPF: texto (@), para o Excel não tirar o zero à esquerda
        Assert.Equal("49", FormatoDaColuna(4)); // Telefone
        Assert.Equal("dd/mm/yyyy", FormatoDaColuna(3)); // Data de nascimento
        Assert.Equal("dd/mm/yyyy", FormatoDaColuna(6)); // Data de admissão
    }
}

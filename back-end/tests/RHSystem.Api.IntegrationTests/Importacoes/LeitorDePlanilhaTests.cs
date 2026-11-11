using System.Diagnostics;
using System.Text;
using RHSystem.Application.Comum;
using RHSystem.Application.Importacoes;
using RHSystem.Infrastructure.Importacoes;
using static RHSystem.Api.IntegrationTests.Importacoes.PlanilhasDeTeste;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>
/// O leitor da aba "Colaboradores" (<see cref="LeitorDePlanilhaOpenXml"/>), sem banco: o que o Excel do mundo real produz (número no lugar de texto, datas de dois jeitos, texto em várias partes, fórmulas) e o que um arquivo hostil
/// tenta (zip bomb, cabeçalho de zip que mente, DTD com entidade externa). Todo problema do ARQUIVO é 422 com <c>codigo</c>; nunca 500, e a mensagem nunca traz o conteúdo das células.
/// </summary>
public class LeitorDePlanilhaTests
{
    private static PlanilhaLida Ler(byte[] xlsx) => new LeitorDePlanilhaOpenXml().Ler(new MemoryStream(xlsx));

    private static ProblemaDeApiException Recusa(byte[] xlsx)
    {
        var erro = Assert.Throws<ProblemaDeApiException>(() => Ler(xlsx));
        Assert.Equal(422, erro.Status);
        Assert.Equal("Arquivo inválido.", erro.Titulo);
        return erro;
    }

    private static string? Celula(LinhaLida linha, ColunaDaImportacao coluna) => linha.Celulas[(int)coluna];

    // ---------------------------------------------------------------- o que o Excel faz

    [Fact]
    public void Le_as_linhas_com_o_numero_da_linha_do_Excel_e_ignora_linhas_em_branco()
    {
        var planilha = Ler(Xlsx([Linha(nome: "A"), Linha(nome: "B"), null, Linha(nome: "C")]));

        Assert.Equal([2, 3, 5], planilha.Linhas.Select(l => l.NumeroLinha));
        Assert.Equal(["A", "B", "C"], planilha.Linhas.Select(l => Celula(l, ColunaDaImportacao.Nome)));
        Assert.All(planilha.Linhas, l => Assert.Equal(ColunasDaImportacao.Todas.Count, l.Celulas.Count));
    }

    [Fact]
    public void Linha_so_com_celulas_vazias_ou_espacos_conta_como_em_branco()
    {
        var planilha = Ler(Xlsx([Linha(), ["   ", null, "\t"], Linha(nome: "Depois")]));

        Assert.Equal([2, 4], planilha.Linhas.Select(l => l.NumeroLinha));
    }

    [Fact]
    public void Texto_e_aparado_e_celula_vazia_vira_nulo()
    {
        var planilha = Ler(Xlsx([Linha(nome: "  Ana Souza  ", beneficios: "   ", setor: null)]));

        var linha = Assert.Single(planilha.Linhas);
        Assert.Equal("Ana Souza", Celula(linha, ColunaDaImportacao.Nome));
        Assert.Null(Celula(linha, ColunaDaImportacao.Beneficios));
        Assert.Null(Celula(linha, ColunaDaImportacao.Setor));
    }

    [Fact]
    public void Linha_mais_curta_que_o_modelo_completa_com_celulas_vazias()
    {
        var planilha = Ler(Xlsx([["Só o nome"]]));

        var linha = Assert.Single(planilha.Linhas);
        Assert.Equal(11, linha.Celulas.Count);
        Assert.Equal("Só o nome", linha.Celulas[0]);
        Assert.All(linha.Celulas.Skip(1), c => Assert.Null(c));
    }

    [Fact]
    public void Data_de_celula_de_data_vira_dd_mm_aaaa_e_texto_de_data_fica_como_digitado()
    {
        var planilha = Ler(Xlsx([
            [null, null, new DateOnly(1989, 3, 14), null, null, new DataComFormatoEmbutido(new DateOnly(2026, 10, 19))],
            [null, null, "14/3/1989", null, null, "19/10/2026"],
        ]));

        Assert.Equal("14/03/1989", Celula(planilha.Linhas[0], ColunaDaImportacao.DataNascimento));
        Assert.Equal("19/10/2026", Celula(planilha.Linhas[0], ColunaDaImportacao.DataAdmissao));
        Assert.Equal("14/3/1989", Celula(planilha.Linhas[1], ColunaDaImportacao.DataNascimento));
        Assert.Equal("19/10/2026", Celula(planilha.Linhas[1], ColunaDaImportacao.DataAdmissao));
    }

    [Fact]
    public void Planilha_no_sistema_de_datas_1904_da_a_data_certa()
    {
        var planilha = Ler(Xlsx([[null, null, new DateOnly(1989, 3, 14), null, null, new DateOnly(2026, 10, 19)]], date1904: true));

        Assert.Equal("14/03/1989", Celula(planilha.Linhas[0], ColunaDaImportacao.DataNascimento));
        Assert.Equal("19/10/2026", Celula(planilha.Linhas[0], ColunaDaImportacao.DataAdmissao));
    }

    [Fact]
    public void Numero_nas_colunas_de_CPF_e_telefone_vira_so_os_digitos_e_o_CPF_recupera_o_zero_a_esquerda()
    {
        var planilha = Ler(Xlsx([
            [null, 1234567890L, null, 11977663344L, null, null, null, 5, null, null, null],
            [3.5, 52998224725L],
        ]));

        Assert.Equal("01234567890", Celula(planilha.Linhas[0], ColunaDaImportacao.Cpf)); // o Excel tirou o zero à esquerda
        Assert.Equal("11977663344", Celula(planilha.Linhas[0], ColunaDaImportacao.Telefone));
        Assert.Equal("5", Celula(planilha.Linhas[0], ColunaDaImportacao.Cargo)); // número fora do CPF: o texto do número, sem preenchimento
        Assert.Equal("3.5", Celula(planilha.Linhas[1], ColunaDaImportacao.Nome)); // ponto decimal da cultura invariante, não a do servidor
        Assert.Equal("52998224725", Celula(planilha.Linhas[1], ColunaDaImportacao.Cpf));
    }

    [Fact]
    public void Texto_formatado_em_varias_partes_vira_um_texto_so()
    {
        var planilha = Ler(Xlsx([[new TextoRico("Ana ", "Souza", " Lima")]]));

        Assert.Equal("Ana Souza Lima", Celula(Assert.Single(planilha.Linhas), ColunaDaImportacao.Nome));
    }

    [Fact]
    public void Formula_vale_o_valor_guardado()
    {
        var planilha = Ler(Xlsx([[new FormulaGuardada("CONCATENAR(\"Ana\";\" Souza\")", "Ana Souza")]]));

        Assert.Equal("Ana Souza", Celula(Assert.Single(planilha.Linhas), ColunaDaImportacao.Nome));
    }

    [Fact]
    public void Celulas_de_outras_ferramentas_texto_em_linha_formula_de_texto_booleano_e_erro()
    {
        var abaXml = $"<?xml version=\"1.0\" encoding=\"UTF-8\"?><worksheet xmlns=\"{EspacoDeNomes}\"><sheetData>"
            + "<row r=\"1\">" + string.Concat(TitulosDoModelo.Select((t, i) => $"<c r=\"{(char)('A' + i)}1\" t=\"inlineStr\"><is><t>{t}</t></is></c>")) + "</row>"
            + "<row r=\"2\"><c r=\"A2\" t=\"inlineStr\"><is><t>em linha</t></is></c><c r=\"B2\" t=\"str\"><f>1+1</f><v>resultado</v></c><c r=\"C2\" t=\"b\"><v>1</v></c><c r=\"D2\" t=\"b\"><v>0</v></c><c r=\"E2\" t=\"e\"><v>#N/A</v></c></row>"
            + "</sheetData></worksheet>";

        var linha = Assert.Single(Ler(XlsxManual(abaXml)).Linhas);

        Assert.Equal("em linha", Celula(linha, ColunaDaImportacao.Nome));
        Assert.Equal("resultado", Celula(linha, ColunaDaImportacao.Cpf));
        Assert.Equal("VERDADEIRO", Celula(linha, ColunaDaImportacao.DataNascimento));
        Assert.Equal("FALSO", Celula(linha, ColunaDaImportacao.Telefone));
        Assert.Equal("#N/A", Celula(linha, ColunaDaImportacao.EmailCorporativo));
    }

    // ---------------------------------------------------------------- o modelo (cabeçalho, aba, linhas)

    public static TheoryData<string, string?[]> CabecalhosDiferentes()
    {
        var certos = TitulosDoModelo.Cast<string?>().ToArray();
        return new TheoryData<string, string?[]>
        {
            { "ordem trocada", [certos[1], certos[0], .. certos.Skip(2)] },
            { "faltando a última coluna", certos[..^1] },
            { "título trocado", [.. certos[..7], "Função", .. certos[8..]] },
            { "coluna a mais com título", [.. certos, "Observações"] },
            { "linha 1 vazia", [] },
        };
    }

    [Theory]
    [MemberData(nameof(CabecalhosDiferentes))]
    public void Cabecalho_diferente_do_modelo_e_MODELO_ALTERADO(string motivo, string?[] titulos)
    {
        _ = motivo; // só rotula a linha da theory
        var erro = Recusa(Xlsx([Linha()], titulos));

        Assert.Equal("MODELO_ALTERADO", erro.Codigo);
        Assert.Equal("As colunas da planilha não são as do modelo. Baixe o modelo de novo e copie os dados para ele.", erro.Detalhe);
    }

    [Fact]
    public void Titulos_em_maiusculas_sem_acento_e_com_espacos_nas_pontas_sao_aceitos()
    {
        var titulos = TitulosDoModelo.Select(t => $"  {t.ToUpperInvariant().Replace("Ã", "A").Replace("Í", "I")} ").Cast<string?>().ToArray();

        Assert.Single(Ler(Xlsx([Linha()], titulos)).Linhas);
    }

    [Fact]
    public void Aba_Colaboradores_ausente_e_MODELO_ALTERADO_e_o_nome_da_aba_nao_diferencia_maiusculas()
    {
        Assert.Equal("MODELO_ALTERADO", Recusa(Xlsx([Linha()], nomeDaAba: "Planilha1")).Codigo);

        Assert.Single(Ler(Xlsx([Linha()], nomeDaAba: "COLABORADORES")).Linhas);
        Assert.Single(Ler(Xlsx([Linha()], nomeDaAba: " Colaboradores ")).Linhas);
    }

    [Fact]
    public void Numero_com_formato_de_numero_ou_com_letras_entre_aspas_nao_vira_data_e_data_com_localidade_vira()
    {
        var serial = (new DateTime(2026, 10, 19) - new DateTime(1899, 12, 30)).TotalDays;

        // colunas A, C, D e E (a B é a do CPF, que completa com zeros): nome, nascimento, telefone, e-mail
        var planilha = Ler(Xlsx([[new NumeroComEstilo(45000, 3), null, new NumeroComEstilo(45000, 4), new NumeroComEstilo(serial, 5), new NumeroComEstilo(serial, 0)]]));

        var linha = Assert.Single(planilha.Linhas);
        Assert.Equal("45000", linha.Celulas[0]); // "[Red]#,##0.00": o d de Red, entre colchetes, não é dia
        Assert.Equal("45000", linha.Celulas[2]); // 0.00" dias": o d entre aspas é texto, não dia
        Assert.Equal("19/10/2026", linha.Celulas[3]); // [$-416]dd/mm/yyyy: o colchete é localidade, o resto é data
        Assert.Equal(((long)serial).ToString(), linha.Celulas[4]); // estilo geral: continua número
    }

    [Fact]
    public void Leitura_fonetica_nao_entra_no_texto_nem_nos_textos_compartilhados_nem_nos_em_linha()
    {
        var linhaDeDados = "<row r=\"2\"><c r=\"A2\" t=\"s\"><v>0</v></c><c r=\"B2\" t=\"inlineStr\"><is><r><t>12</t></r><rPh sb=\"0\" eb=\"1\"><t>XX</t></rPh><r><t>3</t></r></is></c></row>";
        var aba = XmlDaAbaDoModelo().Replace("</sheetData>", linhaDeDados + "</sheetData>");
        var textos = $"<?xml version=\"1.0\"?><sst xmlns=\"{EspacoDeNomes}\"><si><r><t>Ana</t></r><rPh sb=\"0\" eb=\"1\"><t>アナ</t></rPh><r><t xml:space=\"preserve\"> Souza</t></r></si><si/></sst>";

        var linha = Assert.Single(Ler(XlsxManual(aba, textos)).Linhas);

        Assert.Equal("Ana Souza", Celula(linha, ColunaDaImportacao.Nome));
        Assert.Equal("123", Celula(linha, ColunaDaImportacao.Cpf));
    }

    [Fact]
    public void Zip_sem_o_tipo_de_conteudo_ou_sem_a_pasta_de_trabalho_e_ARQUIVO_INVALIDO()
    {
        var valido = Xlsx([Linha()]);

        Assert.Equal("ARQUIVO_INVALIDO", Recusa(SemEntrada(valido, "[Content_Types].xml")).Codigo);
        Assert.Equal("ARQUIVO_INVALIDO", Recusa(SemEntrada(valido, "xl/workbook.xml")).Codigo);
    }

    // ---------------------------------------------------------------- os limites, no valor exato

    [Fact]
    public void Arquivo_com_exatamente_5_MiB_ainda_nao_e_grande_demais_e_com_1_byte_a_mais_e()
    {
        // os limites são escritos por extenso (o do contrato: 5 MB = 5 MiB): usar a constante do código aqui faria o teste acompanhar qualquer mudança nela
        Assert.Equal("ARQUIVO_INVALIDO", Recusa(new byte[5 * 1024 * 1024]).Codigo); // lido até o fim; só não é um .xlsx
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(new byte[(5 * 1024 * 1024) + 1]).Codigo);
    }

    [Fact]
    public void Cem_entradas_no_zip_passam_e_101_nao()
    {
        var valido = Xlsx([Linha()]);
        var entradas = QuantasEntradas(valido);

        Assert.Single(Ler(ComEntradasExtras(valido, 100 - entradas)).Linhas);
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(ComEntradasExtras(valido, 101 - entradas)).Codigo);
    }

    [Fact]
    public void Descompactado_exatamente_no_limite_passa_e_1_byte_a_mais_nao()
    {
        var valido = Xlsx([Linha()]);
        var folga = (50L * 1024 * 1024) - SomaDosTamanhosDeclarados(valido);

        Assert.Single(Ler(ComEntradaDeZeros(valido, "xl/lixo.bin", folga)).Linhas);
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(ComEntradaDeZeros(valido, "xl/lixo.bin", folga + 1)).Codigo);
    }

    [Fact]
    public void Exatamente_200000_celulas_passam_pelo_limite_de_celulas_e_200001_nao()
    {
        // o cabeçalho tem 11 células; 18 180 linhas cheias dão 199 980 e a última linha completa o resto. Passar pelo limite de células leva ao de linhas (18 181 > 500).
        static IEnumerable<object?[]?> Linhas(int celulasNaUltima) =>
            Enumerable.Range(0, 18_180).Select(i => (object?[]?)[i, i, i, i, i, i, i, i, i, i, i]).Append(Enumerable.Repeat((object?)1, celulasNaUltima).ToArray());

        Assert.Equal("LIMITE_DE_LINHAS_EXCEDIDO", Recusa(Xlsx(Linhas(9))).Codigo); // 11 + 199 980 + 9 = 200 000
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(Xlsx(Linhas(10))).Codigo); // 200 001
    }

    [Fact]
    public void So_cabecalho_ou_so_linhas_em_branco_e_PLANILHA_VAZIA()
    {
        Assert.Equal("PLANILHA_VAZIA", Recusa(Xlsx([])).Codigo);
        Assert.Equal("PLANILHA_VAZIA", Recusa(Xlsx([null, null, ["  "]])).Codigo);
        Assert.Equal("A planilha não tem nenhuma linha preenchida.", Recusa(Xlsx([])).Detalhe);
    }

    [Fact]
    public void Quinhentas_linhas_passam_e_501_e_LIMITE_DE_LINHAS_EXCEDIDO_com_o_total_real()
    {
        Assert.Equal(500, Ler(Xlsx(Enumerable.Range(0, 500).Select(i => Linha(nome: $"P{i}")))).Linhas.Count);

        var erro501 = Recusa(Xlsx(Enumerable.Range(0, 501).Select(i => Linha(nome: $"P{i}"))));
        Assert.Equal("LIMITE_DE_LINHAS_EXCEDIDO", erro501.Codigo);
        Assert.Equal("A planilha tem 501 linhas. O limite é de 500 por importação.", erro501.Detalhe);

        var erro1000 = Recusa(Xlsx(Enumerable.Range(0, 1000).Select(i => Linha(nome: $"P{i}"))));
        Assert.Contains("1000 linhas", erro1000.Detalhe);
    }

    [Fact]
    public void Linhas_em_branco_nao_contam_para_o_limite()
    {
        var linhas = Enumerable.Range(0, 500).Select(i => (object?[]?)Linha(nome: $"P{i}")).Concat(Enumerable.Repeat<object?[]?>(null, 300));

        Assert.Equal(500, Ler(Xlsx(linhas)).Linhas.Count);
    }

    // ---------------------------------------------------------------- o que não é um .xlsx

    public static TheoryData<string, byte[]> ArquivosQueNaoSaoXlsx() => new()
    {
        { "texto (CSV)", Encoding.UTF8.GetBytes("Nome completo;CPF\nAna;52998224725\n") },
        { "Excel antigo (.xls, OLE2)", [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1, .. new byte[512]] },
        { "PDF", Encoding.ASCII.GetBytes("%PDF-1.7\n1 0 obj\n<<>>\nendobj\n") },
        { "zip sem as partes do Excel", Zip(("a.txt", "x"u8.ToArray())) },
        { "xlsx cortado no meio", Xlsx([Linha()]).AsSpan(0, Xlsx([Linha()]).Length / 2).ToArray() },
        { "arquivo vazio", [] },
        { "um byte", [0x50] },
    };

    [Theory]
    [MemberData(nameof(ArquivosQueNaoSaoXlsx))]
    public void Arquivo_que_nao_e_xlsx_e_ARQUIVO_INVALIDO(string descricao, byte[] conteudo)
    {
        _ = descricao;
        var erro = Recusa(conteudo);

        Assert.Equal("ARQUIVO_INVALIDO", erro.Codigo);
        Assert.Equal("O arquivo não é uma planilha do Excel (.xlsx). Baixe o modelo e preencha nele.", erro.Detalhe);
    }

    // ---------------------------------------------------------------- arquivo hostil

    [Fact]
    public void Zip_que_descompacta_alem_do_limite_e_ARQUIVO_MUITO_GRANDE_e_termina_logo()
    {
        var bomba = ComEntradaDeZeros(Xlsx([Linha()]), "xl/lixo.bin", LimitesDeImportacao.DescompactadoMaximoBytes + 10L * 1024 * 1024);
        Assert.True(bomba.Length < 1024 * 1024, "a bomba de teste tem de ser pequena (comprime quase a nada)");

        var relogio = Stopwatch.StartNew();
        var erro = Recusa(bomba);

        Assert.Equal("ARQUIVO_MUITO_GRANDE", erro.Codigo);
        Assert.Equal("O arquivo é grande demais. O limite é de 5 MB.", erro.Detalhe);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(30), $"a recusa demorou {relogio.Elapsed}");
    }

    [Fact]
    public void Zip_com_cabecalho_que_mente_o_tamanho_nao_passa_pelo_limite()
    {
        // a aba é uma parte que o leitor LÊ: o enchimento de espaços depois do XML descompacta além do limite, e o cabeçalho do zip diz que ela tem 1 KB
        var bomba = XlsxManual(XmlDaAbaDoModelo(), espacosNoFinalDaAba: LimitesDeImportacao.DescompactadoMaximoBytes + 10L * 1024 * 1024);
        Assert.True(bomba.Length < 1024 * 1024, "a bomba de teste tem de ser pequena (comprime quase a nada)");
        var mentirosa = ComTamanhoDeclaradoMentindo(bomba, "xl/worksheets/sheet1.xml", 1024);

        var relogio = Stopwatch.StartNew();
        var erro = Recusa(mentirosa);

        // Medido: o .NET entrega no máximo o tamanho DECLARADO no cabeçalho (o enchimento nem é lido), então o leitor vê só o XML do início e a planilha é "vazia". O que importa é: nunca 500, nunca descompactar os 60 MiB.
        Assert.True(erro.Codigo is "PLANILHA_VAZIA" or "ARQUIVO_MUITO_GRANDE" or "ARQUIVO_INVALIDO", $"código inesperado: {erro.Codigo}");
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(30), $"a recusa demorou {relogio.Elapsed}");
    }

    [Fact]
    public void Mais_de_100_entradas_no_zip_e_ARQUIVO_MUITO_GRANDE()
    {
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(ComEntradasExtras(Xlsx([Linha()]), 100)).Codigo);
    }

    [Fact]
    public void Mais_de_10_planilhas_e_ARQUIVO_MUITO_GRANDE_e_10_passam()
    {
        Assert.Single(Ler(Xlsx([Linha()], abasExtras: 9)).Linhas); // a aba "Colaboradores" mais 9 = 10
        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(Xlsx([Linha()], abasExtras: 10)).Codigo); // 11
    }

    [Fact]
    public void Mais_de_200000_celulas_e_ARQUIVO_MUITO_GRANDE_antes_de_contar_as_linhas()
    {
        var linhas = Enumerable.Range(0, 18_200).Select(i => (object?[]?)[i, i, i, i, i, i, i, i, i, i, i]);

        Assert.Equal("ARQUIVO_MUITO_GRANDE", Recusa(Xlsx(linhas)).Codigo);
    }

    [Fact]
    public void Xml_com_entidade_externa_nao_le_arquivo_local_e_e_ARQUIVO_INVALIDO()
    {
        var arquivoLocal = Path.Combine(Path.GetTempPath(), $"segredo-{Guid.NewGuid():N}.txt");
        File.WriteAllText(arquivoLocal, "CONTEUDO-SECRETO-DO-SERVIDOR");
        try
        {
            var aba = $"<?xml version=\"1.0\"?><!DOCTYPE x [<!ENTITY lido SYSTEM \"file:///{arquivoLocal.Replace('\\', '/')}\">]><worksheet xmlns=\"{EspacoDeNomes}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&lido;</t></is></c></row></sheetData></worksheet>";

            var erro = Recusa(XlsxManual(aba));

            Assert.Equal("ARQUIVO_INVALIDO", erro.Codigo);
            Assert.DoesNotContain("CONTEUDO-SECRETO", erro.Detalhe);
            Assert.DoesNotContain("CONTEUDO-SECRETO", erro.ToString());
        }
        finally
        {
            File.Delete(arquivoLocal);
        }
    }

    [Fact]
    public void Xml_com_entidades_aninhadas_nao_expande_e_termina_logo()
    {
        var entidades = new StringBuilder("<!ENTITY l0 \"lol\">");
        for (var nivel = 1; nivel <= 9; nivel++)
        {
            entidades.Append($"<!ENTITY l{nivel} \"{string.Concat(Enumerable.Repeat($"&l{nivel - 1};", 10))}\">");
        }

        var aba = $"<?xml version=\"1.0\"?><!DOCTYPE x [{entidades}]><worksheet xmlns=\"{EspacoDeNomes}\"><sheetData><row r=\"1\"><c r=\"A1\" t=\"inlineStr\"><is><t>&l9;</t></is></c></row></sheetData></worksheet>";

        var relogio = Stopwatch.StartNew();
        var erro = Recusa(XlsxManual(aba));

        Assert.Equal("ARQUIVO_INVALIDO", erro.Codigo);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(10), $"a recusa demorou {relogio.Elapsed}");
    }

    [Fact]
    public void Doctype_nos_textos_compartilhados_tambem_e_recusado()
    {
        var textos = "<?xml version=\"1.0\"?><!DOCTYPE sst [<!ENTITY a \"x\">]><sst xmlns=\"" + EspacoDeNomes + "\"><si><t>&a;</t></si></sst>";

        Assert.Equal("ARQUIVO_INVALIDO", Recusa(XlsxManual(XmlDaAbaDoModelo(), textos)).Codigo);
    }

    [Fact]
    public void A_excecao_nao_traz_o_conteudo_das_celulas()
    {
        var titulos = TitulosDoModelo.Cast<string?>().ToArray();
        titulos[1] = "SEGREDO-123";

        var erro = Recusa(Xlsx([Linha(nome: "NOME-SECRETO", cpf: "CPF-SECRETO")], titulos));

        foreach (var texto in new[] { erro.Message, erro.Detalhe, erro.Titulo, erro.Codigo ?? string.Empty, erro.ToString() })
        {
            Assert.DoesNotContain("SEGREDO-123", texto);
            Assert.DoesNotContain("NOME-SECRETO", texto);
            Assert.DoesNotContain("CPF-SECRETO", texto);
        }
    }
}

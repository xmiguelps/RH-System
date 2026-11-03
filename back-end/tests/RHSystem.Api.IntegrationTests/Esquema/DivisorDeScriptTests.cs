using RHSystem.Infrastructure.Esquema;

namespace RHSystem.Api.IntegrationTests.Esquema;

public class DivisorDeScriptTests
{
    [Fact]
    public void Divide_por_ponto_e_virgula_e_ignora_comentarios_e_vazios()
    {
        const string conteudo = "-- cabecalho\nCREATE TABLE A (ID INT);\n\n-- outro comentario\nINSERT INTO A VALUES (1);\n;\n;\n";

        var comandos = DivisorDeScript.Dividir(conteudo);

        Assert.Equal(
            ["-- cabecalho\nCREATE TABLE A (ID INT)", "-- outro comentario\nINSERT INTO A VALUES (1)"],
            comandos);
    }

    [Fact]
    public void Respeita_DELIMITER_e_nao_emite_a_linha_DELIMITER()
    {
        const string conteudo = """
            DELIMITER $$
            CREATE FUNCTION F() RETURNS INT
            BEGIN
              DECLARE X INT;
              RETURN 1;
            END$$
            DELIMITER ;
            SELECT 1;
            """;

        var comandos = DivisorDeScript.Dividir(conteudo);

        Assert.Equal(
            ["CREATE FUNCTION F() RETURNS INT\nBEGIN\n  DECLARE X INT;\n  RETURN 1;\nEND", "SELECT 1"],
            comandos);
        Assert.DoesNotContain(comandos, c => c.Contains("DELIMITER", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Trigger_com_ponto_e_virgula_no_corpo_vira_um_unico_comando()
    {
        const string conteudo = """
            DROP TRIGGER IF EXISTS TRG_X;

            DELIMITER $$
            CREATE TRIGGER TRG_X BEFORE INSERT ON T FOR EACH ROW
            BEGIN
              SET NEW.A = 1;
              SET NEW.B = 2;
            END$$
            DELIMITER ;
            """;

        var comandos = DivisorDeScript.Dividir(conteudo);

        Assert.Equal(2, comandos.Count);
        Assert.Equal("DROP TRIGGER IF EXISTS TRG_X", comandos[0]);
        Assert.StartsWith("CREATE TRIGGER TRG_X", comandos[1]);
        Assert.Contains("SET NEW.A = 1;", comandos[1]);
        Assert.EndsWith("END", comandos[1]);
    }

    [Fact]
    public void Normaliza_CRLF_para_LF()
    {
        var comandos = DivisorDeScript.Dividir("SELECT 1;\r\nSELECT\r\n2;\r\n");

        Assert.Equal(["SELECT 1", "SELECT\n2"], comandos);
    }

    [Fact]
    public void Texto_so_com_comentarios_nao_gera_comando()
    {
        const string conteudo = "-- so comentario; com ponto e virgula\n# outro\n/* bloco; */\n\n";

        Assert.Empty(DivisorDeScript.Dividir(conteudo));
        Assert.Empty(DivisorDeScript.Dividir(string.Empty));
        Assert.Empty(DivisorDeScript.Dividir("  \n\t\n"));
    }

    [Fact]
    public void Ponto_e_virgula_dentro_de_comentario_no_modo_ponto_e_virgula_nao_divide()
    {
        // Os arquivos do grupo têm 12 casos assim no modo ';' (4 nas tabelas, 8 nas funções).
        const string conteudo = "SELECT 1; -- depois; daqui\nSELECT 2; # e; aqui\nSELECT /* a; b */ 3;\n-- SELECT x;\n";

        var comandos = DivisorDeScript.Dividir(conteudo);

        Assert.Equal(3, comandos.Count);
        Assert.Equal("SELECT 1", comandos[0]);
        Assert.Equal("-- depois; daqui\nSELECT 2", comandos[1]);
        Assert.Equal("# e; aqui\nSELECT /* a; b */ 3", comandos[2]);
    }

    [Fact]
    public void Ponto_e_virgula_dentro_de_literal_entre_aspas_nao_divide()
    {
        const string simples = "INSERT INTO T VALUES ('a;b', \"c;d\", `e;f`);";
        const string aspasDobradas = "INSERT INTO T VALUES ('it''s; ok', \"say \"\"hi;\"\"\");";
        const string comBarra = "INSERT INTO T VALUES ('barra \\'; ainda dentro');";

        Assert.Equal([simples.TrimEnd(';')], DivisorDeScript.Dividir(simples));
        Assert.Equal([aspasDobradas.TrimEnd(';')], DivisorDeScript.Dividir(aspasDobradas));
        Assert.Equal([comBarra.TrimEnd(';')], DivisorDeScript.Dividir(comBarra));
    }

    [Fact]
    public void Ultimo_comando_sem_delimitador_final_e_emitido()
    {
        Assert.Equal(["SELECT 1", "SELECT 2"], DivisorDeScript.Dividir("SELECT 1; SELECT 2"));
    }

    [Fact]
    public void DELIMITER_em_minusculas_e_reconhecido()
    {
        var comandos = DivisorDeScript.Dividir("delimiter //\nSELECT 1//\ndelimiter ;\nSELECT 2;");

        Assert.Equal(["SELECT 1", "SELECT 2"], comandos);
    }
}

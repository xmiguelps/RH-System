using System.Text;
using RHSystem.Application.Comum.Exportacao;

namespace RHSystem.Application.Tests.Comum;

public class CsvSeguroTests
{
    [Theory]
    [InlineData("=1+1", "'=1+1")]
    [InlineData("+55 11 99999-0000", "'+55 11 99999-0000")]
    [InlineData("-2+3", "'-2+3")]
    [InlineData("@SUM(A1:A9)", "'@SUM(A1:A9)")]
    [InlineData("\t=1", "'\t=1")]
    [InlineData("\r=1", "'\r=1")]
    [InlineData("José da Silva", "José da Silva")]
    [InlineData("a=1", "a=1")]
    [InlineData("", "")]
    public void Texto_que_comeca_com_igual_mais_menos_ou_arroba_ganha_aspas_simples_na_frente(string valor, string esperado)
    {
        Assert.Equal(esperado, CsvSeguro.Texto(valor));
    }

    [Fact]
    public void Texto_nulo_vira_texto_vazio()
    {
        Assert.Equal(string.Empty, CsvSeguro.Texto(null));
    }

    [Theory]
    [InlineData(-225L, "-225")]
    [InlineData(0L, "0")]
    [InlineData(1500L, "1500")]
    [InlineData(long.MinValue, "-9223372036854775808")]
    public void Numero_negativo_nao_e_neutralizado(long valor, string esperado)
    {
        // Um saldo de banco de horas negativo continua sendo número: com ' na frente o Excel o trataria como texto.
        Assert.Equal(esperado, CsvSeguro.Numero(valor));
    }

    [Fact]
    public void Escrever_usa_BOM_UTF8_ponto_e_virgula_aspas_dobradas_e_CRLF()
    {
        var bytes = CsvSeguro.Escrever(
            ["Nome", "Observação"],
            [
                ["José", "diz \"oi\"; tchau"],
                ["Ana", string.Empty],
                ["Bia", "linha1\nlinha2"],
            ]);

        var esperado = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true).GetPreamble()
            .Concat(Encoding.UTF8.GetBytes("Nome;Observação\r\nJosé;\"diz \"\"oi\"\"; tchau\"\r\nAna;\r\nBia;\"linha1\nlinha2\"\r\n"))
            .ToArray();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes.Take(3));
        Assert.Equal(esperado, bytes);
    }

    [Fact]
    public void Escrever_nao_altera_o_que_Texto_e_Numero_ja_prepararam()
    {
        var bytes = CsvSeguro.Escrever(["Campo", "Saldo"], [[CsvSeguro.Texto("=1+1"), CsvSeguro.Numero(-225)]]);

        var texto = Encoding.UTF8.GetString(bytes).TrimStart('﻿');
        Assert.Equal("Campo;Saldo\r\n'=1+1;-225\r\n", texto);
    }

    [Fact]
    public void Escrever_sem_linhas_devolve_so_o_cabecalho()
    {
        var texto = Encoding.UTF8.GetString(CsvSeguro.Escrever(["A", "B"], [])).TrimStart('﻿');

        Assert.Equal("A;B\r\n", texto);
    }
}

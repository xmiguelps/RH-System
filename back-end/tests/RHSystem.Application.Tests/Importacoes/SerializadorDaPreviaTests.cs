using System.Text.Json;
using RHSystem.Application.Importacoes;

namespace RHSystem.Application.Tests.Importacoes;

public class SerializadorDaPreviaTests
{
    private static IReadOnlyList<ImportacaoLinha> Amostra() =>
    [
        new ImportacaoLinha(
            2,
            SituacaoDaLinha.Ok,
            new ImportacaoLinhaValores("Tiago Nogueira", "402.817.593-32", "14/03/1989", "(11) 97766-3344", "tiago@empresa.com.br", "19/10/2026", "Estágio", "Estagiário", "Tecnologia", "Colaborador", "Vale-transporte"),
            []),
        new ImportacaoLinha(
            5,
            SituacaoDaLinha.ComErro,
            new ImportacaoLinhaValores("Elisa Prado", null, "02/12/1994", null, "tiago@empresa.com.br", "03/11/2026", "CLT", "Analista de compras (novo)", "Marketing", "Gestor", null),
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "Preencha esta coluna."),
                new ImportacaoLinhaErro(ColunaDaImportacao.NivelDeAcesso, "O nível de acesso \"Diretor\" não existe. Use Colaborador, Gestor ou RH."),
            ]),
    ];

    [Fact]
    public void Ida_e_volta_preserva_linhas_valores_e_erros()
    {
        var original = Amostra();

        var lidas = SerializadorDaPrevia.Ler(SerializadorDaPrevia.Serializar(original));

        Assert.Equal(original.Count, lidas.Count);
        for (var i = 0; i < original.Count; i++)
        {
            Assert.Equal(original[i].NumeroLinha, lidas[i].NumeroLinha);
            Assert.Equal(original[i].Situacao, lidas[i].Situacao);
            Assert.Equal(original[i].Valores, lidas[i].Valores);
            Assert.Equal(original[i].Erros, lidas[i].Erros);
        }
    }

    [Fact]
    public void O_texto_guardado_usa_camelCase_e_nomes_de_enum_do_contrato()
    {
        var texto = SerializadorDaPrevia.Serializar(
        [
            new ImportacaoLinha(
                5,
                SituacaoDaLinha.ComErro,
                new ImportacaoLinhaValores("Elisa", null, null, null, "e@x.com", null, null, null, null, null, null),
                [new ImportacaoLinhaErro(ColunaDaImportacao.NivelDeAcesso, "x")]),
        ]);

        using var documento = JsonDocument.Parse(texto);
        var linha = documento.RootElement[0];
        Assert.Equal(5, linha.GetProperty("numeroLinha").GetInt32());
        Assert.Equal("COM_ERRO", linha.GetProperty("situacao").GetString());
        Assert.Equal("Elisa", linha.GetProperty("valores").GetProperty("nome").GetString());
        Assert.Equal("e@x.com", linha.GetProperty("valores").GetProperty("emailCorporativo").GetString());
        Assert.Equal(JsonValueKind.Null, linha.GetProperty("valores").GetProperty("cpf").ValueKind);
        Assert.Equal("NIVEL_DE_ACESSO", linha.GetProperty("erros")[0].GetProperty("coluna").GetString());
        Assert.Equal("x", linha.GetProperty("erros")[0].GetProperty("mensagem").GetString());
    }

    [Fact]
    public void Linha_OK_sai_com_o_nome_OK()
    {
        var texto = SerializadorDaPrevia.Serializar(Amostra().Take(1).ToList());

        using var documento = JsonDocument.Parse(texto);
        Assert.Equal("OK", documento.RootElement[0].GetProperty("situacao").GetString());
        Assert.Equal(0, documento.RootElement[0].GetProperty("erros").GetArrayLength());
    }

    [Fact]
    public void O_texto_nao_e_indentado()
    {
        Assert.DoesNotContain('\n', SerializadorDaPrevia.Serializar(Amostra()));
    }

    [Fact]
    public void Texto_que_nao_e_uma_lista_de_linhas_lanca_InvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() => SerializadorDaPrevia.Ler("null"));
    }
}

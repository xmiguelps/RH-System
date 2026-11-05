using System.Text.Json.Nodes;
using Microsoft.OpenApi;
using RHSystem.Api.Configuracao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Configuracao;

public class EsquemasDoContratoTests
{
    [Fact]
    public void DateTime_vira_string_date_time_e_o_anulavel_vira_string_ou_null()
    {
        var instante = new OpenApiSchema();
        var anulavel = new OpenApiSchema();

        EsquemasDoContrato.Aplicar(instante, typeof(DateTime));
        EsquemasDoContrato.Aplicar(anulavel, typeof(DateTime?));

        Assert.Equal(JsonSchemaType.String, instante.Type);
        Assert.Equal("date-time", instante.Format);
        Assert.Equal(JsonSchemaType.String | JsonSchemaType.Null, anulavel.Type);
        Assert.Equal("date-time", anulavel.Format);
    }

    [Fact]
    public void TimeOnly_vira_string_com_o_padrao_HH_mm_do_contrato()
    {
        var horario = new OpenApiSchema();

        EsquemasDoContrato.Aplicar(horario, typeof(TimeOnly));

        Assert.Equal(JsonSchemaType.String, horario.Type);
        Assert.Equal("^([01][0-9]|2[0-3]):[0-5][0-9]$", horario.Pattern); // o mesmo de horarioEntrada no contrato
    }

    [Theory]
    [InlineData(typeof(TipoDeContrato))]
    [InlineData(typeof(TipoDeContrato?))]
    public void Enum_perde_o_null_do_componente_porque_a_anulabilidade_fica_no_uso_da_propriedade(Type tipo)
    {
        // O gerador escreve o enum anulável com null DENTRO do componente compartilhado, e aí toda propriedade que referencia o enum (inclusive as obrigatórias) passa a aceitar null no documento.
        var esquema = new OpenApiSchema { Enum = [JsonValue.Create("CLT")!, JsonValue.Create("ESTAGIO")!, null!] };

        EsquemasDoContrato.Aplicar(esquema, tipo);

        Assert.Equal(["CLT", "ESTAGIO"], esquema.Enum!.Select(valor => valor.ToString()));
    }

    [Theory]
    [InlineData(typeof(int))]
    [InlineData(typeof(string))]
    [InlineData(typeof(DateOnly))]
    public void Outros_tipos_ficam_como_o_gerador_escreveu(Type tipo)
    {
        var esquema = new OpenApiSchema { Type = JsonSchemaType.Integer };

        EsquemasDoContrato.Aplicar(esquema, tipo);

        Assert.Equal(JsonSchemaType.Integer, esquema.Type);
        Assert.Null(esquema.Format);
        Assert.Null(esquema.Pattern);
    }
}

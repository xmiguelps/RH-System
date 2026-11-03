using Microsoft.OpenApi;
using RHSystem.Api.Configuracao;

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

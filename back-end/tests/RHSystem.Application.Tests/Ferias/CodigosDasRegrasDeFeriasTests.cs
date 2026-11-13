using System.Text.Json;
using System.Text.Json.Serialization;
using RHSystem.Application.Ferias.Regras;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>O JSON escreve o enum das regras como o contrato (<c>FeriasRegraCodigo</c>), inclusive os que têm número no nome (a política do .NET escreveria "PARTE_DE14_DIAS"), e o texto usado nas listas de erro é o mesmo.</summary>
public class CodigosDasRegrasDeFeriasTests
{
    private static readonly JsonSerializerOptions Opcoes = new() { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false) } };

    private static readonly string[] DoContrato =
        ["DURACAO_DA_PARTE", "CABE_NO_SALDO", "PARTE_DE_14_DIAS", "MAXIMO_DE_3_PARTES", "ABONO_ATE_10_DIAS", "ANTECEDENCIA_DE_30_DIAS", "NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO", "SEM_SOBREPOSICAO"];

    [Fact]
    public void O_json_escreve_cada_regra_com_o_codigo_do_contrato_e_le_de_volta() =>
        Assert.All(
            Enum.GetValues<RegraDeFerias>().Zip(DoContrato),
            par =>
            {
                Assert.Equal($"\"{par.Second}\"", JsonSerializer.Serialize(par.First, Opcoes));
                Assert.Equal(par.First, JsonSerializer.Deserialize<RegraDeFerias>($"\"{par.Second}\"", Opcoes));
            });

    [Fact]
    public void O_texto_do_codigo_e_o_mesmo_que_o_json_escreve() =>
        Assert.Equal(DoContrato, Enum.GetValues<RegraDeFerias>().Select(CodigosDasRegrasDeFerias.Texto));
}

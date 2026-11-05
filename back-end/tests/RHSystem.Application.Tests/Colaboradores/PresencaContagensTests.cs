using System.Text.Json;
using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

public class PresencaContagensTests
{
    [Fact]
    public void De_conta_cada_situacao_e_a_soma_dos_seis_e_o_total()
    {
        var contagens = PresencaContagens.De(
        [
            SituacaoHoje.Trabalhando, SituacaoHoje.Trabalhando, SituacaoHoje.Trabalhando,
            SituacaoHoje.Ferias,
            SituacaoHoje.Atestado, SituacaoHoje.Atestado,
            SituacaoHoje.SemRegistro,
            SituacaoHoje.AindaNaoComecou, SituacaoHoje.AindaNaoComecou, SituacaoHoje.AindaNaoComecou, SituacaoHoje.AindaNaoComecou,
            SituacaoHoje.SemExpediente, SituacaoHoje.SemExpediente, SituacaoHoje.SemExpediente, SituacaoHoje.SemExpediente, SituacaoHoje.SemExpediente,
        ]);

        Assert.Equal((3, 1, 2, 1, 4, 5), (contagens.Trabalhando, contagens.Ferias, contagens.Atestado, contagens.SemRegistro, contagens.AindaNaoComecou, contagens.SemExpediente));
        Assert.Equal(16, contagens.Total);
    }

    [Fact]
    public void De_lista_vazia_zera_tudo()
    {
        var contagens = PresencaContagens.De([]);

        Assert.Equal(new PresencaContagens(0, 0, 0, 0, 0, 0), contagens);
        Assert.Equal(0, contagens.Total);
    }

    [Fact]
    public void Total_nao_e_propriedade_do_JSON()
    {
        var json = JsonSerializer.Serialize(PresencaContagens.De([SituacaoHoje.Trabalhando]), new JsonSerializerOptions(JsonSerializerDefaults.Web));

        Assert.DoesNotContain("total", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"trabalhando\":1", json, StringComparison.Ordinal);
    }
}

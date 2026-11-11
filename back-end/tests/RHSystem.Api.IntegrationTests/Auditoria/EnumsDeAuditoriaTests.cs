using System.Text.Json;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// Os enums de auditoria do Domain e os do contrato (<c>components.schemas.AuditoriaArea</c> e <c>AuditoriaAcao</c>) têm de ter os mesmos valores:
/// o JSON escreve o nome do enum em <c>SnakeCaseUpper</c> (<c>AcessoNegado</c> vira <c>ACESSO_NEGADO</c>), e um valor que só um dos lados conhece
/// vira filtro que não casa ou resposta que o front não entende.
/// </summary>
public class EnumsDeAuditoriaTests
{
    private static IReadOnlyList<string> DoContrato(string esquema)
    {
        var valores = CarregadorDeContrato.Contrato()["components"]!["schemas"]![esquema]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        Assert.NotEmpty(valores); // controle positivo: o leitor achou o esquema e leu os valores
        return valores;
    }

    private static IReadOnlyList<string> DoCodigo<T>()
        where T : struct, Enum =>
        [.. Enum.GetNames<T>().Select(n => JsonNamingPolicy.SnakeCaseUpper.ConvertName(n))];

    [Fact]
    public void AuditoriaArea_e_AuditoriaAcao_tem_os_mesmos_valores_dos_enums_do_contrato()
    {
        var area = (Contrato: DoContrato("AuditoriaArea"), Codigo: DoCodigo<AuditoriaArea>());
        var acao = (Contrato: DoContrato("AuditoriaAcao"), Codigo: DoCodigo<AuditoriaAcao>());

        Assert.Equal([.. area.Contrato.Order()], [.. area.Codigo.Order()]);
        Assert.Equal([.. acao.Contrato.Order()], [.. acao.Codigo.Order()]);
        Assert.Contains("ACESSO_NEGADO", acao.Contrato); // o nome composto vira SNAKE_CASE em maiúsculas dos dois lados
    }
}

using System.Text.Json.Nodes;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// O contrato (<c>contrato/openapi.yaml</c>) é lei: este teste falha se o documento OpenAPI que a API gera divergir dele nas operações que
/// a fatia já implementa. Se falhar, a divergência é lida como erro da API, não do teste; só o dono muda o contrato.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ContratoDivergenciaTests(MySqlFixture mysql)
{
    /// <summary>
    /// O escopo: uma lista de <c>operationId</c>, NUNCA prefixo de caminho (<c>/api/rh/beneficios</c> é prefixo textual de
    /// <c>/api/rh/beneficios-adesoes</c>, e <c>/api/auth/senha/*</c> é de outra fatia). Cada tarefa que entrega rotas acrescenta os
    /// <c>operationId</c> dela AQUI, no mesmo passo em que implementa as rotas: nunca antes (uma operação do contrato no escopo que a API
    /// ainda não tem é divergência, e o teste falharia no meio da fatia) e nunca depois (o teste abaixo falha).
    /// </summary>
    public static readonly HashSet<string> OperationIdsNoEscopo =
    [
        "entrar",
        "renovarSessao",
        "sair",
        "obterUsuarioLogado",
        "verificarSaude",
        "listarEventosDeAuditoria",
        "exportarAuditoria",
        "obterEventoDeAuditoria",
        "obterContadoresDoMenuDoFuncionario",
        "obterContadoresDoMenuDoGestor",
        "obterContadoresDoMenuDoRh",
        "solicitarRedefinicaoDeSenha",
        "validarLinkDeSenha",
        "definirSenhaPeloLink",
        "trocarMinhaSenha",
    ];

    private async Task<JsonNode> DocumentoGeradoAsync() =>
        await CarregadorDeContrato.GeradoAsync((await ApiCompartilhada.ObterAsync(mysql)).CreateClient());

    [Fact]
    public async Task Operacoes_da_API_no_escopo_batem_com_o_contrato()
    {
        var divergencias = ComparadorDeContrato.Comparar(CarregadorDeContrato.Contrato(), await DocumentoGeradoAsync(), OperationIdsNoEscopo);

        Assert.True(divergencias.Count == 0, "A API diverge do contrato:\n- " + string.Join("\n- ", divergencias));
    }

    [Fact]
    public async Task Toda_operacao_do_documento_gerado_esta_no_escopo()
    {
        // Nenhuma rota implementada fica fora da comparação por esquecimento. As rotas /__teste/ têm ExcludeFromDescription e não aparecem.
        var documento = await DocumentoGeradoAsync();
        var geradas = documento["paths"]!.AsObject()
            .SelectMany(caminho => caminho.Value!.AsObject().Select(metodo => metodo.Value?["operationId"]?.GetValue<string>()))
            .Where(id => id is not null)
            .Select(id => id!)
            .ToList();

        Assert.NotEmpty(geradas);
        Assert.Empty(ComparadorDeContrato.OperacoesSemOperationId(documento)); // rota sem .WithName não pode sumir da comparação
        Assert.True(
            geradas.All(OperationIdsNoEscopo.Contains),
            "Há rota implementada fora do escopo do teste de divergência: " + string.Join(", ", geradas.Where(id => !OperationIdsNoEscopo.Contains(id))));
        Assert.DoesNotContain(documento["paths"]!.AsObject(), caminho => caminho.Key.StartsWith("/__teste", StringComparison.Ordinal));
    }
}

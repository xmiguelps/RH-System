namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// As 21 operações da fatia 1c (cadastros de apoio, empresa, gestores e documentos) estão no escopo do <see cref="ContratoDivergenciaTests"/>. A lista abaixo é a tabela "Rotas e códigos" do plano da 1c; o escopo pode ter mais
/// (fatias 0, 1a e 1b), nunca menos. "Toda rota implementada está no escopo" não se repete aqui: é a prova única da 1a (<c>Toda_rota_implementada_esta_no_conjunto_do_escopo_da_divergencia</c>).
/// </summary>
public class EscopoDaDivergenciaTests
{
    /// <summary>As 3 operações da fatia 2 (importação de colaboradores por Excel): a tabela "Rotas e códigos" do plano da fatia 2.</summary>
    private static readonly string[] OperacoesDaFatia2 =
    [
        "baixarModeloDeImportacao",
        "validarPlanilhaDeImportacao",
        "confirmarImportacao",
    ];

    private static readonly string[] OperacoesDaFatia1c =
    [
        "listarCargos",
        "criarCargo",
        "editarCargo",
        "listarBeneficiosDoCatalogo",
        "criarBeneficioDoCatalogo",
        "editarBeneficioDoCatalogo",
        "excluirBeneficioDoCatalogo",
        "listarSetores",
        "criarSetor",
        "editarSetor",
        "listarGestores",
        "cadastrarGestor",
        "configurarAusenciaDoGestor",
        "obterEmpresa",
        "salvarEmpresa",
        "listarDocumentosDoFuncionario",
        "adicionarDocumentoDoFuncionario",
        "baixarDocumentoDoFuncionario",
        "listarMeusDocumentos",
        "enviarMeuDocumento",
        "baixarMeuDocumento",
    ];

    [Fact]
    public void Escopo_inclui_os_21_operationId_da_fatia_1c()
    {
        Assert.Equal(21, OperacoesDaFatia1c.Distinct().Count()); // a lista em si não repete nem perde nenhuma

        var faltando = OperacoesDaFatia1c.Where(id => !ContratoDivergenciaTests.OperationIdsNoEscopo.Contains(id)).Order().ToList();

        Assert.True(faltando.Count == 0, "Operações da 1c fora do escopo de ContratoDivergenciaTests.OperationIdsNoEscopo: " + string.Join(", ", faltando));
    }

    [Fact]
    public void Escopo_inclui_os_3_operationId_da_fatia_2()
    {
        Assert.Equal(3, OperacoesDaFatia2.Distinct().Count());

        var faltando = OperacoesDaFatia2.Where(id => !ContratoDivergenciaTests.OperationIdsNoEscopo.Contains(id)).Order().ToList();

        Assert.True(faltando.Count == 0, "Operações da fatia 2 fora do escopo de ContratoDivergenciaTests.OperationIdsNoEscopo: " + string.Join(", ", faltando));
    }
}

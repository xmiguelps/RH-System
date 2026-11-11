using RHSystem.Application.Comum;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// Os 422 do arquivo como um todo (sem prévia): título e textos exatos de <c>validarPlanilhaDeImportacao</c>. Nenhum traz o conteúdo das células: o texto é fixo, a não ser o total de linhas.
/// </summary>
public static class ErrosDePlanilha
{
    private const string Titulo = "Arquivo inválido.";

    public static ProblemaDeApiException NaoEExcel() =>
        new(422, Titulo, "O arquivo não é uma planilha do Excel (.xlsx). Baixe o modelo e preencha nele.", "ARQUIVO_INVALIDO");

    public static ProblemaDeApiException MuitoGrande() =>
        new(422, Titulo, "O arquivo é grande demais. O limite é de 5 MB.", "ARQUIVO_MUITO_GRANDE");

    public static ProblemaDeApiException ModeloAlterado() =>
        new(422, Titulo, "As colunas da planilha não são as do modelo. Baixe o modelo de novo e copie os dados para ele.", "MODELO_ALTERADO");

    public static ProblemaDeApiException Vazia() =>
        new(422, Titulo, "A planilha não tem nenhuma linha preenchida.", "PLANILHA_VAZIA");

    public static ProblemaDeApiException LimiteDeLinhas(int total) =>
        new(422, Titulo, $"A planilha tem {total} linhas. O limite é de {LimitesDeImportacao.LinhasMaximas} por importação.", "LIMITE_DE_LINHAS_EXCEDIDO");
}

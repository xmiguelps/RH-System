namespace RHSystem.Api.Configuracao;

/// <summary>
/// Os textos padrão dos erros, copiados dos <c>example</c> de <c>components.responses.Erro*</c> do contrato (<c>contrato/fontes/00-base.yaml</c>);
/// o teste <c>Titulos_e_detalhes_padrao_sao_os_dos_exemplos_do_contrato</c> os confere contra o contrato. O 400 de <c>entrar</c> tem texto próprio
/// ("Preencha o e-mail e a senha.", <c>Erro400Login</c>) e não passa por aqui.
/// </summary>
public static class TextosDeErro
{
    private const string TituloGenerico = "Requisição inválida.";
    private const string DetalheGenerico = "Confira a requisição e tente de novo.";

    public static string Titulo(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Dados inválidos.",
        StatusCodes.Status401Unauthorized => "Não autenticado.",
        StatusCodes.Status403Forbidden => "Sem permissão.",
        StatusCodes.Status404NotFound => "Não encontrado.",
        StatusCodes.Status409Conflict => "Conflito.",
        StatusCodes.Status422UnprocessableEntity => "Operação recusada.",
        StatusCodes.Status429TooManyRequests => "Muitas tentativas.",
        >= 500 => "Erro no servidor.",
        _ => TituloGenerico,
    };

    public static string DetalhePadrao(int status) => status switch
    {
        StatusCodes.Status400BadRequest => "Confira os campos indicados.",
        StatusCodes.Status401Unauthorized => "Entre novamente para continuar.",
        StatusCodes.Status403Forbidden => "Você não tem permissão para esta ação.",
        StatusCodes.Status404NotFound => "O registro não foi encontrado.",
        StatusCodes.Status409Conflict => "Já existe um registro com estes dados.",
        StatusCodes.Status422UnprocessableEntity => "A operação foi recusada por uma regra do banco de dados.",
        StatusCodes.Status429TooManyRequests => "Muitas tentativas. Aguarde um minuto e tente de novo.",
        >= 500 => "Erro no servidor. Tente novamente em instantes.",
        _ => DetalheGenerico,
    };
}

public static class ProblemasExtensions
{
    /// <summary>Declara no OpenAPI cada status como <c>application/problem+json</c> (<c>ProblemDetails</c>). Toda rota nova a usa com os códigos do contrato.</summary>
    public static RouteHandlerBuilder ProduzProblemas(this RouteHandlerBuilder construtor, params int[] status)
    {
        foreach (var codigo in status)
        {
            construtor.ProducesProblem(codigo, "application/problem+json");
        }

        return construtor;
    }
}

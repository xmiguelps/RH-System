using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Empresa;

namespace RHSystem.Api.Empresa;

/// <summary>
/// A configuração da empresa: <c>GET /api/eu/empresa</c> (qualquer perfil logado; por isso não tem 403) e <c>PUT /api/rh/empresa</c> (só o RH). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>; o 404 de "ainda não configurada" sai com o <c>codigo</c> <c>EMPRESA_NAO_CONFIGURADA</c>.
/// </summary>
public static class EmpresaEndpoints
{
    private const string Etiqueta = "Empresa";

    public static IServiceCollection AdicionarCasosDeUsoDeEmpresa(this IServiceCollection servicos)
    {
        servicos.AddScoped<EmpresaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapEmpresa(this IEndpointRouteBuilder rotas)
    {
        rotas.GrupoEu().WithTags(Etiqueta)
            .MapGet("/empresa", Obter)
            .WithName("obterEmpresa")
            .Produces<EmpresaConfiguracao>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        rotas.GrupoRh().WithTags(Etiqueta)
            .MapPut("/empresa", Salvar)
            .WithName("salvarEmpresa")
            .Produces<EmpresaConfiguracao>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<EmpresaConfiguracao> Obter(EmpresaCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ObterAsync(ct);

    private static async Task<EmpresaConfiguracao> Salvar(EmpresaConfiguracao configuracao, EmpresaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.SalvarAsync(configuracao, ct);
}

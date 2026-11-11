using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Acesso;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Api.Acesso;

/// <summary>
/// Os parâmetros de <c>GET /api/rh/usuarios</c> e SÓ eles: o contrato não declara <c>ordenarPor</c> nem <c>direcao</c> aqui (a ordem é o nome), então não se usa o <c>ParametrosDeLista</c> inteiro em
/// <c>[AsParameters]</c>. <c>Situacao</c> chega como texto: o caso de uso a lê pelo nome do contrato (<c>CONVITE_ENVIADO</c>) junto com os erros de paginação.
/// </summary>
public sealed record FiltrosDaListaDeUsuarios(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Situacao = null, bool? Ativo = null);

/// <summary>
/// "Usuários do sistema" (tela 29): lista, convite, ativar e desativar. Só o perfil RH (<c>GrupoRh</c>). Os <c>operationId</c> e os códigos de resposta são os de
/// <c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>.
/// </summary>
public static class UsuariosDoSistemaEndpoints
{
    private const string Etiqueta = "Usuários do sistema";

    public static IServiceCollection AdicionarCasosDeUsoDeUsuariosDoSistema(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarUsuariosDoSistemaCasoDeUso>();
        servicos.AddScoped<EnviarConviteDeAcessoCasoDeUso>();
        servicos.AddScoped<AtivarUsuarioDoSistemaCasoDeUso>();
        servicos.AddScoped<DesativarUsuarioDoSistemaCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapUsuariosDoSistema(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.GrupoRh().WithTags(Etiqueta);

        grupo.MapGet("/usuarios", Listar)
            .WithName("listarUsuariosDoSistema")
            .Produces<RespostaPaginada<UsuarioSistemaItem>>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/usuarios/{funcionarioId:int}/convite", EnviarConvite)
            .WithName("enviarConviteDeAcesso")
            .Produces<UsuarioSistemaItem>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/usuarios/{funcionarioId:int}/ativar", Ativar)
            .WithName("ativarUsuarioDoSistema")
            .Produces<UsuarioSistemaItem>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        grupo.MapPost("/usuarios/{funcionarioId:int}/desativar", Desativar)
            .WithName("desativarUsuarioDoSistema")
            .Produces<UsuarioSistemaItem>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<RespostaPaginada<UsuarioSistemaItem>> Listar([AsParameters] FiltrosDaListaDeUsuarios filtros, ListarUsuariosDoSistemaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(filtros.Pagina, filtros.TamanhoPagina, filtros.Busca, filtros.Situacao, filtros.Ativo, ct);

    private static async Task<UsuarioSistemaItem> EnviarConvite(int funcionarioId, EnviarConviteDeAcessoCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<UsuarioSistemaItem> Ativar(int funcionarioId, AtivarUsuarioDoSistemaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, ct);

    private static async Task<UsuarioSistemaItem> Desativar(int funcionarioId, DesativarUsuarioDoSistemaCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(funcionarioId, ct);
}

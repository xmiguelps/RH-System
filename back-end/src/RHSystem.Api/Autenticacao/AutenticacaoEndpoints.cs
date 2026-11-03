using System.Security.Claims;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.Autenticacao;

/// <summary>
/// As rotas de <c>/api/auth</c>. Os <c>operationId</c> (<c>WithName</c>) e os códigos de resposta são os de
/// <c>contrato/fontes/10-fatia-0-autenticacao.yaml</c>: a API declara todos, porque o gerador só descreve o que se declara.
/// </summary>
public static class AutenticacaoEndpoints
{
    public static IEndpointRouteBuilder MapAutenticacao(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/api/auth").WithTags("Autenticação");

        grupo.MapPost("/entrar", Entrar)
            .WithName("entrar")
            .AllowAnonymous()
            .Produces<SessaoResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status429TooManyRequests) // o limitador é da Task 9; o código já está no contrato
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapPost("/renovar", Renovar)
            .WithName("renovarSessao")
            .AllowAnonymous()
            .Produces<SessaoResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapPost("/sair", Sair)
            .WithName("sair")
            .AllowAnonymous()
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        grupo.MapGet("/eu", Eu)
            .WithName("obterUsuarioLogado")
            .RequireAuthorization()
            .Produces<UsuarioResposta>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status500InternalServerError);

        return rotas;
    }

    // O corpo é obrigatório no contrato (requestBody.required): sem parâmetro anulável, o documento gerado também o declara obrigatório.
    // Corpo ausente ou JSON inválido vira 400 pelo tratador global; campo vazio, pela validação abaixo.
    private static async Task<IResult> Entrar(EntrarComando comando, EntrarCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        var erros = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(comando.Email))
        {
            erros["email"] = ["O e-mail é obrigatório."];
        }

        if (string.IsNullOrWhiteSpace(comando.Senha))
        {
            erros["senha"] = ["A senha é obrigatória."];
        }
        else if (comando.Senha.Length > SenhaHasher.TamanhoMaximoDaSenha)
        {
            erros["senha"] = [$"A senha pode ter no máximo {SenhaHasher.TamanhoMaximoDaSenha} caracteres."];
        }

        if (erros.Count > 0)
        {
            return Problemas.Resultado(http, StatusCodes.Status400BadRequest, "Dados inválidos.", "Preencha o e-mail e a senha.", erros);
        }

        var resultado = await casoDeUso.ExecutarAsync(comando, ct);
        return resultado.Falha switch
        {
            null => Results.Ok(resultado.Sessao),
            FalhaDeEntrada.UsuarioInativo => Problemas.Resultado(http, StatusCodes.Status403Forbidden, "Acesso desativado.", "Seu acesso está desativado. Fale com o RH."),
            FalhaDeEntrada.PerfilNaoConfigurado => Problemas.Resultado(http, StatusCodes.Status403Forbidden, "Perfil não configurado.", "Seu perfil de acesso não está configurado. Fale com o RH."),
            _ => Problemas.Resultado(http, StatusCodes.Status401Unauthorized, "E-mail ou senha inválidos.", "E-mail ou senha inválidos."), // sempre os mesmos textos
        };
    }

    private static async Task<IResult> Renovar(RenovarComando comando, RenovarTokenCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        if (RefreshVazio(comando) is { } erro)
        {
            return Problemas.Resultado(http, StatusCodes.Status400BadRequest, "Dados inválidos.", "Confira os campos indicados.", erro);
        }

        var sessao = await casoDeUso.ExecutarAsync(comando, ct);
        return sessao is null ? Problemas.NaoAutenticado(http) : Results.Ok(sessao);
    }

    private static async Task<IResult> Sair(RenovarComando comando, SairCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        if (RefreshVazio(comando) is { } erro)
        {
            return Problemas.Resultado(http, StatusCodes.Status400BadRequest, "Dados inválidos.", "Confira os campos indicados.", erro);
        }

        await casoDeUso.ExecutarAsync(comando, ct);
        return Results.NoContent();
    }

    private static async Task<IResult> Eu(ClaimsPrincipal pessoa, ObterUsuarioLogadoCasoDeUso casoDeUso, HttpContext http, CancellationToken ct)
    {
        if (!int.TryParse(pessoa.FindFirstValue("sub"), out var usuarioId))
        {
            return Problemas.NaoAutenticado(http);
        }

        var usuario = await casoDeUso.ExecutarAsync(usuarioId, ct);
        return usuario is null ? Problemas.NaoAutenticado(http) : Results.Ok(usuario);
    }

    private static Dictionary<string, string[]>? RefreshVazio(RenovarComando comando) =>
        string.IsNullOrWhiteSpace(comando.RefreshToken)
            ? new Dictionary<string, string[]> { ["refreshToken"] = ["O refresh token é obrigatório."] }
            : null;
}

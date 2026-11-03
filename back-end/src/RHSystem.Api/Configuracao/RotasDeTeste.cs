using Microsoft.EntityFrameworkCore;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// Rotas que só existem para os testes provocarem erros reais (uma exceção qualquer e uma recusa de trigger do banco). Só entram quando a
/// configuração <c>Testes:RotasDeTeste</c> é verdadeira (o <c>ApiFactory</c> a liga; nenhum ambiente real a define), ficam fora do documento
/// OpenAPI e do teste de divergência e não pedem login.
/// </summary>
internal static class RotasDeTeste
{
    public static void MapRotasDeTeste(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/__teste").AllowAnonymous().ExcludeFromDescription();

        grupo.MapGet("/erro", () =>
        {
            throw new InvalidOperationException("segredo interno");
        });

        grupo.MapGet("/trigger", async (AppDbContext db, CancellationToken ct) =>
        {
            // O trigger do grupo recusa criar usuário para funcionário inativo: cria ativo, inativa e tenta criar o usuário.
            var nivel = await db.NiveisDeAcesso.FirstAsync(ct);
            var funcionario = Funcionario.Criar("Trigger de Teste", $"trigger.{Guid.NewGuid():N}@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
            db.Funcionarios.Add(funcionario);
            await db.SaveChangesAsync(ct);
            funcionario.Inativar();
            await db.SaveChangesAsync(ct);
            db.Usuarios.Add(Usuario.Criar(funcionario, new string('x', 60)));
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        });
    }
}

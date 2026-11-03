using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comum;
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

        MapErros(grupo.MapGroup("/erros"));
    }

    /// <summary>Uma rota por tipo de falha que a API sabe traduzir (ver <c>ErrosPadraoTests</c>).</summary>
    private static void MapErros(RouteGroupBuilder erros)
    {
        erros.MapGet("/nao-encontrado", () => { throw new NaoEncontradoException(); });
        erros.MapGet("/nao-encontrado-com-detalhe", () => { throw new NaoEncontradoException("A pessoa não foi encontrada neste setor."); });
        erros.MapGet("/conflito", () => { throw new ConflitoException("Esta matrícula já está em uso.", "MATRICULA_EM_USO"); });
        erros.MapGet("/conflito-sem-codigo", () => { throw new ConflitoException("O registro foi alterado por outra pessoa."); });
        erros.MapGet("/negocio", () => { throw new ErroDeNegocioException("PERIODO_FECHADO", "O período já foi fechado."); });

        erros.MapGet("/validacao", () =>
        {
            var coletor = new ColetorDeErros();
            coletor.Adicionar("nome", "O nome é obrigatório.");
            coletor.Adicionar("dataNascimento", "A data de nascimento não pode ser no futuro.");
            coletor.Adicionar("nome", "O nome é longo demais.");
            coletor.LancarSeHouver();
        });

        // Corpo JSON inválido, enum que não existe e parâmetro que não converte: tudo isso o framework recusa antes de a rota rodar.
        erros.MapPost("/corpo", (CorpoDeTeste corpo) => Results.Ok(corpo));
        erros.MapGet("/numero/{n}", (int n) => Results.Ok(n));

        // Dois funcionários com o mesmo EMAIL_CORPORATIVO no mesmo SaveChanges: a chave única do banco recusa (1062) e nada fica gravado.
        erros.MapGet("/chave-unica", async (AppDbContext db, CancellationToken ct) =>
        {
            var nivel = await db.NiveisDeAcesso.FirstAsync(ct);
            db.Funcionarios.Add(Funcionario.Criar("Duplicada Um", "duplicada@empresa.com.br", new DateOnly(2026, 1, 2), nivel));
            db.Funcionarios.Add(Funcionario.Criar("Duplicada Dois", "duplicada@empresa.com.br", new DateOnly(2026, 1, 2), nivel));
            await db.SaveChangesAsync(ct);
        });

        // DATA_DESLIGAMENTO antes de DATA_ADMISSAO: o CHECK CHK_FUNCIONARIOS_DATAS recusa (e o ATIVO = FALSE deixa o outro CHECK em paz).
        erros.MapGet("/check", async (AppDbContext db, CancellationToken ct) =>
            await db.Database.ExecuteSqlRawAsync(
                "INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, DATA_DESLIGAMENTO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Check de Teste', '2026-02-01', '2026-01-01', FALSE, 'check.teste@empresa.com.br')",
                ct));

        // Quem escreve pelo IProblemDetailsService (como o framework faz) tem de sair com os textos do contrato e o traceId.
        erros.MapGet("/framework", async (HttpContext http, IProblemDetailsService servico) =>
        {
            http.Response.StatusCode = StatusCodes.Status404NotFound;
            await servico.WriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = { Status = StatusCodes.Status404NotFound } });
        });
    }

    internal sealed record CorpoDeTeste(string? Nome, SituacaoDeTeste? Situacao);

    internal enum SituacaoDeTeste
    {
        Ativa,
        Inativa,
    }
}

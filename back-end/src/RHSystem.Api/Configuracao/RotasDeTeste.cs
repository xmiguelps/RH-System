using Microsoft.EntityFrameworkCore;
using RHSystem.Api.Autorizacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
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
        MapJson(grupo.MapGroup("/json"));
        MapAutorizacao(rotas);
    }

    /// <summary>
    /// Uma rota em cada família (<c>/__teste/ping</c>), uma que devolve o <c>IAtorAtual</c> e uma FORA de qualquer família e sem
    /// <c>AllowAnonymous</c>, para a regra de reserva decidir (ver <c>FamiliasDeRotaTests</c> e <c>AtorAtualTests</c>).
    /// </summary>
    private static void MapAutorizacao(IEndpointRouteBuilder rotas)
    {
        rotas.GrupoRh().MapGet("/__teste/ping", () => Results.Ok()).ExcludeFromDescription();
        rotas.GrupoGestor().MapGet("/__teste/ping", () => Results.Ok()).ExcludeFromDescription();
        rotas.GrupoEu().MapGet("/__teste/ping", () => Results.Ok()).ExcludeFromDescription();
        rotas.GrupoPublico().MapGet("/__teste/ping", () => Results.Ok()).ExcludeFromDescription();
        rotas.GrupoEu().MapGet("/__teste/ator", (IAtorAtual ator) => Results.Ok(Descrever(ator))).ExcludeFromDescription();
        rotas.GrupoPublico().MapGet("/__teste/ator", (IAtorAtual ator) => Results.Ok(Descrever(ator))).ExcludeFromDescription();
        rotas.MapGet("/__teste/sem-familia", () => Results.Ok()).ExcludeFromDescription();

        // O padrão de escopo que as fatias seguintes copiam: a consulta parte de DaEquipe e termina em OuNaoEncontrado, então "não existe" e
        // "é de outro setor" dão a mesma resposta. Sem equipe (gestor sem setor): lista vazia, e 404 ao pedir uma pessoa.
        rotas.GrupoGestor().MapGet(
            "/__teste/equipe",
            async (IEscopoDoUsuario escopo, AppDbContext db, CancellationToken ct) =>
            {
                var equipe = await escopo.ObterEquipeAsync(ct);
                if (equipe is null)
                {
                    return Results.Ok(Array.Empty<object>());
                }

                var pessoas = await db.Funcionarios.DaEquipe(equipe).OrderBy(f => f.Nome).Select(f => new { f.Id, f.Nome }).ToListAsync(ct);
                return Results.Ok(pessoas);
            }).ExcludeFromDescription();

        rotas.GrupoGestor().MapGet(
            "/__teste/equipe/{id:int}",
            async (int id, IEscopoDoUsuario escopo, AppDbContext db, CancellationToken ct) =>
            {
                var equipe = await escopo.ObterEquipeAsync(ct) ?? throw new NaoEncontradoException();
                var pessoa = (await db.Funcionarios.DaEquipe(equipe).Where(f => f.Id == id).Select(f => new { f.Id, f.Nome }).FirstOrDefaultAsync(ct)).OuNaoEncontrado();
                return Results.Ok(pessoa);
            }).ExcludeFromDescription();
    }

    private static object Descrever(IAtorAtual ator) => new
    {
        tipo = ator.Tipo,
        usuarioId = ator.UsuarioId,
        funcionarioId = ator.FuncionarioId,
        perfil = ator.Perfil,
        nome = ator.Nome,
        ip = ator.Ip,
        canal = ator.Canal,
    };

    /// <summary>Uma rota por tipo de valor do JSON do contrato (ver <c>JsonDoContratoTests</c>); as de POST devolvem o que leram.</summary>
    private static void MapJson(RouteGroupBuilder json)
    {
        json.MapGet("/instante", () => Results.Ok(new InstanteDeTeste(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc))));
        json.MapGet("/instante-sem-kind", () => Results.Ok(new InstanteDeTeste(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Unspecified))));
        json.MapPost("/instante", (InstanteDeTeste corpo) => Results.Ok(corpo));
        json.MapGet("/data-e-hora", () => Results.Ok(new DataEHoraDeTeste(new DateOnly(2026, 10, 7), new TimeOnly(8, 5))));
        json.MapPost("/data-e-hora", (DataEHoraDeTeste corpo) => Results.Ok(corpo));
        json.MapGet(
            "/enums",
            () => Results.Ok(new
            {
                acao = AuditoriaAcao.AcessoNegado,
                area = AuditoriaArea.Colaboradores,
                perfil = Perfil.RH,
                perfis = new[] { Perfil.Funcionario, Perfil.Gestor, Perfil.RH },
            }));
        json.MapPost("/enums", (EnumsDeTeste corpo) => Results.Ok(corpo));
    }

    internal sealed record InstanteDeTeste(DateTime Quando);

    internal sealed record DataEHoraDeTeste(DateOnly Dia, TimeOnly Horario);

    internal sealed record EnumsDeTeste(AuditoriaAcao? Acao, Perfil? Perfil);

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

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Esquema;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.Configuracao;

/// <summary>Marca a categoria dos logs de inicialização.</summary>
internal sealed class InicializacaoDoBancoLog;

public static class InicializacaoDoBanco
{
    /// <summary>
    /// Na subida: com <c>Database:AplicarScriptsAoIniciar=true</c>, aplica os scripts pendentes (se um script já aplicado foi alterado, a
    /// API NÃO sobe, com a mensagem da exceção); depois, o seed do administrador. Com o banco parado na partida e a aplicação de scripts
    /// ligada, a API falha ao iniciar (aceito e documentado no README).
    /// </summary>
    public static async Task InicializarBancoAsync(this WebApplication app, CancellationToken ct = default)
    {
        if (app.Configuration.GetValue<bool>("Database:AplicarScriptsAoIniciar"))
        {
            var executor = app.Services.GetRequiredService<ExecutorDeScripts>();
            var fonte = app.Services.GetRequiredService<IFonteDeScripts>();
            await executor.AplicarPendentesAsync(fonte.Carregar(), ct);
        }

        await SemearAdministradorAsync(app, ct);
    }

    /// <summary>
    /// Se <c>Seed:AdminEmail</c> e <c>Seed:AdminSenha</c> estão preenchidos E não existe nenhum usuário, cria o <c>Funcionario</c> (nome da
    /// configuração, e-mail normalizado, admissão = hoje no fuso -03:00, nível RH, sem cargo, sem setor e sem matrícula) e o
    /// <c>Usuario</c> com hash. A senha nunca vai para o log. O <c>SENHA_PROVISORIA</c> fica no padrão FALSE do banco: o admin tem senha de verdade.
    /// </summary>
    private static async Task SemearAdministradorAsync(WebApplication app, CancellationToken ct)
    {
        var email = app.Configuration["Seed:AdminEmail"]?.Trim();
        var senha = app.Configuration["Seed:AdminSenha"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(senha))
        {
            return;
        }

        await using var escopo = app.Services.CreateAsyncScope();
        var servicos = escopo.ServiceProvider;
        var log = servicos.GetRequiredService<ILogger<InicializacaoDoBancoLog>>();
        var db = servicos.GetRequiredService<AppDbContext>();

        if (await db.Usuarios.AnyAsync(ct))
        {
            log.LogInformation("O seed do administrador foi ignorado: já existe usuário no banco.");
            return;
        }

        var nivel = await db.NiveisDeAcesso.FirstOrDefaultAsync(n => n.Nome == "RH", ct)
            ?? throw new InvalidOperationException("O nível de acesso 'RH' não existe em NIVEIS_DE_ACESSO: o seed do administrador precisa dele (os scripts V001 a V005 foram aplicados?).");

        var nome = string.IsNullOrWhiteSpace(app.Configuration["Seed:AdminNome"]) ? "Administrador" : app.Configuration["Seed:AdminNome"]!.Trim();
        var hoje = DateOnly.FromDateTime(servicos.GetRequiredService<IRelogio>().AgoraUtc.AddHours(-3)); // fuso -03:00, sem horário de verão
        var funcionario = Funcionario.Criar(nome, email, hoje, nivel);
        db.Usuarios.Add(Usuario.Criar(funcionario, servicos.GetRequiredService<ISenhaHasher>().Gerar(senha)));
        await db.SaveChangesAsync(ct);

        log.LogInformation("Administrador inicial criado (funcionário {FuncionarioId}).", funcionario.Id);
    }
}

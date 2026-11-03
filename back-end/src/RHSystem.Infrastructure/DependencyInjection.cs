using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Escopo;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Escopo;
using RHSystem.Infrastructure.Esquema;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registra o banco (<c>AppDbContext</c>), os repositórios, o hasher de senha, o emissor de tokens, o relógio, a unidade de trabalho e o
    /// executor de scripts. Lê <c>ConnectionStrings:Padrao</c>, <c>Database:ServerVersion</c> (padrão 8.4.0) e a seção <c>Jwt</c>
    /// (<c>Chave</c> com no mínimo 32 bytes, <c>Emissor</c>, <c>Audiencia</c>, <c>AcessoMinutos</c> 15 e <c>RefreshDias</c> 7). Configuração
    /// faltando falha na inicialização, com mensagem clara, e não na primeira requisição.
    /// </summary>
    public static IServiceCollection AdicionarInfraestrutura(this IServiceCollection services, IConfiguration configuration)
    {
        var conexao = configuration.GetConnectionString("Padrao");
        if (string.IsNullOrWhiteSpace(conexao))
        {
            throw new InvalidOperationException("Falta a configuração ConnectionStrings:Padrao (a conexão com o MySQL).");
        }

        var versaoDoServidor = Version.Parse(configuration["Database:ServerVersion"] ?? "8.4.0");

        var jwt = new OpcoesJwt();
        configuration.GetSection("Jwt").Bind(jwt);
        if (Encoding.UTF8.GetByteCount(jwt.Chave) < 32)
        {
            throw new InvalidOperationException("Jwt:Chave precisa ter pelo menos 32 bytes (use user-secrets ou variável de ambiente; nunca o repositório).");
        }

        if (string.IsNullOrWhiteSpace(jwt.Emissor) || string.IsNullOrWhiteSpace(jwt.Audiencia))
        {
            throw new InvalidOperationException("Faltam Jwt:Emissor e Jwt:Audiencia na configuração.");
        }

        services.AddDbContext<AppDbContext>(opcoes => opcoes.UseMySql(conexao, new MySqlServerVersion(versaoDoServidor)));

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IRefreshTokenRepositorio, RefreshTokenRepositorio>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddScoped<IGeradorDeMatriculas, GeradorDeMatriculas>();
        services.AddScoped<IEscopoDoUsuario, EscopoDoUsuario>();

        services.AddSingleton<ISenhaHasher, SenhaHasher>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IEmissorDeTokens>(sp => new EmissorDeTokensJwt(jwt, sp.GetRequiredService<IRelogio>()));

        services.AddSingleton<IFonteDeScripts, ScriptsEmbutidos>();
        services.AddSingleton(sp => new ExecutorDeScripts(conexao, sp.GetRequiredService<ILogger<ExecutorDeScripts>>()));

        return services;
    }
}

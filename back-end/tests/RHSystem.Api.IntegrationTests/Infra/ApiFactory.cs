using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>Um relógio que anda com o tempo real e pode ser avançado pelo teste (por exemplo, para vencer um token).</summary>
public sealed class RelogioDeTeste : IRelogio
{
    private long _deslocamentoEmTicks;

    public DateTime AgoraUtc => DateTime.UtcNow.AddTicks(Interlocked.Read(ref _deslocamentoEmTicks));

    public void Avancar(TimeSpan tempo) => Interlocked.Add(ref _deslocamentoEmTicks, tempo.Ticks);
}

/// <summary>
/// A API inteira em memória (ambiente <c>Testing</c>), contra um banco novo com todos os scripts aplicados. Toda a configuração vem de
/// <c>UseSetting</c> (inclusive <c>Jwt:Chave</c>), nada do repositório. Semeia pessoas com e-mails únicos: funcionário não se apaga
/// (o trigger do grupo bloqueia o DELETE).
/// </summary>
public sealed class ApiFactory(string conexao) : WebApplicationFactory<Program>
{
    public const string SenhaPadrao = "Segredo#123";

    public const string ChaveJwt = "chave-de-teste-com-mais-de-32-bytes-0123456789";

    public const string OrigemPermitida = "https://app.exemplo.com.br";

    public const string EmailRh = "rh@empresa.com.br";
    public const string EmailComCargo = "colaborador@empresa.com.br";
    public const string EmailUsuarioInativo = "usuario.inativo@empresa.com.br";
    public const string EmailDesligado = "desligado@empresa.com.br";
    public const string EmailSemNivel = "sem.nivel@empresa.com.br";

    public RelogioDeTeste Relogio { get; } = new();

    public string Conexao { get; } = conexao;

    public static async Task<ApiFactory> CriarAsync(MySqlFixture mysql)
    {
        var fabrica = new ApiFactory(await mysql.NovoBancoComScriptsAsync());
        await fabrica.SemearAsync();
        return fabrica;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Padrao", Conexao);
        builder.UseSetting("Jwt:Chave", ChaveJwt);
        builder.UseSetting("Jwt:Emissor", "rh-system-teste");
        builder.UseSetting("Jwt:Audiencia", "rh-system-teste-web");
        builder.UseSetting("Documentacao:Habilitada", "true");
        builder.UseSetting("Testes:RotasDeTeste", "true");
        builder.UseSetting("Autenticacao:LimiteLoginPorMinuto", "1000"); // só o teste do limite usa 5
        builder.UseSetting("Database:AplicarScriptsAoIniciar", "false"); // o banco já vem com os scripts
        builder.UseSetting("Cors:OrigensPermitidas:0", OrigemPermitida);
        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(Relogio);
            servicos.AddSingleton<IStartupFilter, IpDeOrigemParaTeste>();
        });
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    /// <summary>Cria funcionário e usuário (ativos) com a senha dada. <paramref name="nivel"/> nulo deixa o funcionário sem nível de acesso.</summary>
    public async Task CriarUsuarioAsync(string email, string senha = SenhaPadrao, string? nivel = "Funcionario")
    {
        var hash = new SenhaHasher().Gerar(senha);
        await using var db = ContextoDeTeste.Novo(Conexao);

        if (nivel is null)
        {
            await ExecutarAsync(Conexao, $"INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Pessoa Sem Nivel', '2026-01-02', TRUE, '{email}')");
            await ExecutarAsync(Conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) SELECT ID_FUNCIONARIO, '{hash}' FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
            return;
        }

        var nivelDeAcesso = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nivel);
        db.Usuarios.Add(Usuario.Criar(Funcionario.Criar("Pessoa de Teste", email, new DateOnly(2026, 1, 2), nivelDeAcesso), hash));
        await db.SaveChangesAsync();
    }

    private async Task SemearAsync()
    {
        await CriarUsuarioAsync(EmailRh, nivel: "RH");
        await CriarUsuarioAsync(EmailComCargo, nivel: "Funcionario");
        await ExecutarAsync(Conexao, "INSERT INTO CARGOS (NOME_CARGO) VALUES ('Analista de RH')");
        await ExecutarAsync(Conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Recursos Humanos')");
        await ExecutarAsync(Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = (SELECT ID_CARGO FROM CARGOS LIMIT 1), ID_SETOR = (SELECT ID_SETOR FROM SETORES LIMIT 1) WHERE EMAIL_CORPORATIVO = '{EmailComCargo}'");

        await CriarUsuarioAsync(EmailUsuarioInativo);
        await ExecutarAsync(Conexao, $"UPDATE USUARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = (SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{EmailUsuarioInativo}')");

        await CriarUsuarioAsync(EmailDesligado);
        // O trigger do grupo inativa o usuário junto com o funcionário.
        await ExecutarAsync(Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = CURDATE() WHERE EMAIL_CORPORATIVO = '{EmailDesligado}'");

        await CriarUsuarioAsync(EmailSemNivel, nivel: null);
    }
}

/// <summary>O servidor de teste não tem IP de origem; aqui ele passa a vir de 127.0.0.1 (o proxy confiável por padrão), como numa conexão de verdade.</summary>
internal sealed class IpDeOrigemParaTeste : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> proximo) =>
        app =>
        {
            app.Use((contexto, seguinte) =>
            {
                contexto.Connection.RemoteIpAddress ??= IPAddress.Loopback;
                return seguinte();
            });
            proximo(app);
        };
}

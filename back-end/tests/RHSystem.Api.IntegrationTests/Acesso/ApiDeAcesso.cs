using System.Globalization;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>Uma pessoa criada para o teste: o funcionário e, se pedido, o login.</summary>
internal sealed record PessoaDoTeste(int FuncionarioId, string Nome, string? Email, int? UsuarioId);

/// <summary>
/// Uma API com o elenco semeado (o relógio fixo em 2026-10-07T12:42Z) e um segundo hospedeiro em que o <see cref="IAtorAtual"/> é a pessoa do RH: serve para
/// chamar os serviços que gravam evento de auditoria (<c>IAuditor.Registrar</c> exige ator) fora de uma requisição. Um banco por classe de teste, por <c>chave</c>.
/// </summary>
internal sealed class ApiDeAcesso
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static readonly Dictionary<string, ApiDeAcesso> Prontas = [];
    private static int _contador;

    private readonly WebApplicationFactory<Program> _comRh;

    private ApiDeAcesso(ApiFactory api, ElencoDeTeste elenco, WebApplicationFactory<Program> comRh)
    {
        Api = api;
        Elenco = elenco;
        _comRh = comRh;
    }

    public ApiFactory Api { get; }

    public ElencoDeTeste Elenco { get; }

    public string Conexao => Api.Conexao;

    public static async Task<ApiDeAcesso> ObterAsync(MySqlFixture mysql, string chave)
    {
        await Trava.WaitAsync();
        try
        {
            if (!Prontas.TryGetValue(chave, out var pronta))
            {
                var api = await ApiFactory.CriarAsync(mysql);
                var elenco = await api.SemearElencoAsync();
                var comRh = api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
                {
                    servicos.RemoveAll<IAtorAtual>();
                    servicos.AddScoped<IAtorAtual>(_ => AtorDeTeste.Pessoa(elenco.Rh));
                }));
                Prontas[chave] = pronta = new ApiDeAcesso(api, elenco, comRh);
            }

            return pronta;
        }
        finally
        {
            Trava.Release();
        }
    }

    /// <summary>
    /// Um cliente HTTP do RH do elenco com um token de acesso emitido AGORA, no instante do relógio falso. Para os testes que mudam o relógio (o token de <c>ClienteComo</c> vale 15 minutos a partir de
    /// 2026-10-07T12:42Z e a API mede a validade pelo mesmo relógio).
    /// </summary>
    public async Task<HttpClient> ClienteDoRhAgoraAsync()
    {
        await using var db = ContextoDeTeste.Novo(Conexao);
        var usuario = await db.Usuarios.Include(u => u.Funcionario).ThenInclude(f => f.NivelDeAcesso).SingleAsync(u => u.Id == Elenco.Rh.UsuarioId);
        var cliente = Api.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", Api.Services.GetRequiredService<IEmissorDeTokens>().EmitirAcesso(usuario).Token);
        return cliente;
    }

    /// <summary>Roda a ação num escopo em que o ator é o RH, dentro de uma transação (como o caso de uso que chama o serviço faria).</summary>
    public async Task<T> NaTransacaoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        using var escopo = _comRh.Services.CreateScope();
        var unidade = escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>();
        return await unidade.ExecutarEmTransacaoAsync(() => acao(escopo.ServiceProvider), CancellationToken.None);
    }

    /// <summary>Faz do colaborador (ativo) um gestor: a linha em <c>GESTORES</c>.</summary>
    public Task NovoGestorAsync(int funcionarioId) => Sql.ExecutarAsync(Conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionarioId})");

    /// <summary>
    /// Como <see cref="NaTransacaoAsync{T}"/>, num hospedeiro à parte em que <paramref name="configurar"/> troca ou acrescenta serviços (fontes falsas das fatias que ainda não existem, por exemplo). O ator
    /// continua sendo o RH.
    /// </summary>
    public async Task<T> NaTransacaoComAsync<T>(Action<IServiceCollection> configurar, Func<IServiceProvider, Task<T>> acao)
    {
        var elenco = Elenco;
        await using var fabrica = Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAtorAtual>();
            servicos.AddScoped<IAtorAtual>(_ => AtorDeTeste.Pessoa(elenco.Rh));
            configurar(servicos);
        }));
        using var escopo = fabrica.Services.CreateScope();
        var unidade = escopo.ServiceProvider.GetRequiredService<IUnidadeDeTrabalho>();
        return await unidade.ExecutarEmTransacaoAsync(() => acao(escopo.ServiceProvider), CancellationToken.None);
    }

    /// <summary>
    /// Como <see cref="NoEscopoAsync{T}"/>, num hospedeiro à parte em que <paramref name="configurar"/> troca ou acrescenta serviços (a condição e a fatia falsas do desligamento, por exemplo). O ator
    /// continua sendo o RH e o serviço abre a própria transação.
    /// </summary>
    public async Task<T> NoEscopoComAsync<T>(Action<IServiceCollection> configurar, Func<IServiceProvider, Task<T>> acao)
    {
        var elenco = Elenco;
        await using var fabrica = Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAtorAtual>();
            servicos.AddScoped<IAtorAtual>(_ => AtorDeTeste.Pessoa(elenco.Rh));
            configurar(servicos);
        }));
        using var escopo = fabrica.Services.CreateScope();
        return await acao(escopo.ServiceProvider);
    }

    /// <summary>Roda a ação num escopo em que o ator é o RH, sem abrir transação.</summary>
    public async Task<T> NoEscopoAsync<T>(Func<IServiceProvider, Task<T>> acao)
    {
        using var escopo = _comRh.Services.CreateScope();
        return await acao(escopo.ServiceProvider);
    }

    /// <summary>
    /// Cria um colaborador (e-mail e nome únicos). <paramref name="email"/> nulo cria sem e-mail corporativo. <paramref name="login"/>: sem login, com login
    /// provisório (convite não aceito) ou com senha definida (<see cref="ApiFactory.SenhaPadrao"/>). <paramref name="loginAtivo"/> falso desativa só o login.
    /// <paramref name="ativa"/> falso inativa a pessoa (o trigger do banco inativa o login junto).
    /// </summary>
    public async Task<PessoaDoTeste> NovaPessoaAsync(
        string sufixo,
        bool ativa = true,
        bool comEmail = true,
        TipoDeLogin login = TipoDeLogin.Nenhum,
        bool loginAtivo = true)
    {
        var n = Interlocked.Increment(ref _contador);
        var nome = $"Pessoa {sufixo} {n}";
        var email = $"pessoa.{sufixo}.{n}@empresa.com.br".ToLowerInvariant();
        await using var db = ContextoDeTeste.Novo(Conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "Funcionario");
        var funcionario = Funcionario.Criar(nome, email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync();

        Usuario? usuario = null;
        if (login != TipoDeLogin.Nenhum)
        {
            var hash = login == TipoDeLogin.Provisorio ? new string('x', 60) : new SenhaHasher().Gerar(ApiFactory.SenhaPadrao);
            usuario = Usuario.Criar(funcionario, hash, senhaProvisoria: login == TipoDeLogin.Provisorio);
            db.Usuarios.Add(usuario);
            await db.SaveChangesAsync();
            if (!loginAtivo)
            {
                usuario.Inativar();
                await db.SaveChangesAsync();
            }
        }

        if (!comEmail)
        {
            await Sql.ExecutarAsync(Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {funcionario.Id}");
        }

        if (!ativa)
        {
            await Sql.ExecutarAsync(Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionario.Id}");
        }

        return new PessoaDoTeste(funcionario.Id, nome, comEmail ? email : null, usuario?.Id);
    }
}

internal enum TipoDeLogin
{
    Nenhum,
    Provisorio,
    SenhaDefinida,
}

/// <summary>SQL cru para conferir o que o banco guardou (sem passar pelo EF da aplicação).</summary>
internal static class Sql
{
    public static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<long> ContarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }

    public static async Task<object?> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        var valor = await comando.ExecuteScalarAsync();
        return valor is DBNull ? null : valor;
    }

    /// <summary>O texto de todas as colunas de todas as linhas da tabela (menos as de <paramref name="excluir"/>), para varrer atrás de algo que não pode estar lá.</summary>
    public static async Task<string> TextoDaTabelaAsync(string conexao, string tabela, params string[] excluir)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand($"SELECT * FROM {tabela}", c);
        await using var leitor = await comando.ExecuteReaderAsync();
        var texto = new System.Text.StringBuilder();
        while (await leitor.ReadAsync())
        {
            for (var i = 0; i < leitor.FieldCount; i++)
            {
                if (!excluir.Contains(leitor.GetName(i), StringComparer.OrdinalIgnoreCase) && !await leitor.IsDBNullAsync(i))
                {
                    texto.Append(Convert.ToString(leitor.GetValue(i), CultureInfo.InvariantCulture)).Append('|');
                }
            }

            texto.AppendLine();
        }

        return texto.ToString();
    }
}

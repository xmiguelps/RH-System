using System.Reflection;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Falsos;

public sealed class RelogioFalso(DateTime agoraUtc) : IRelogio
{
    public DateTime AgoraUtc { get; set; } = agoraUtc;
}

public sealed class SenhaHasherFalso : ISenhaHasher
{
    public int GerarChamadas { get; private set; }

    public int VerificarChamadas { get; private set; }

    public string Gerar(string senha)
    {
        GerarChamadas++;
        return "hash:" + senha;
    }

    public bool Verificar(string hash, string senha)
    {
        VerificarChamadas++;
        return hash == "hash:" + senha;
    }
}

public sealed class EmissorDeTokensFalso(IRelogio relogio) : IEmissorDeTokens
{
    private int _sequencia;

    public TimeSpan ValidadeDoRefresh => TimeSpan.FromDays(7);

    public AcessoEmitido EmitirAcesso(Usuario usuario) =>
        new($"jwt-do-usuario-{usuario.Id}", relogio.AgoraUtc.AddMinutes(15));

    public RefreshGerado NovoRefresh()
    {
        _sequencia++;
        var token = $"refresh-{_sequencia}";
        return new RefreshGerado(token, HashDoRefresh(token));
    }

    public string HashDoRefresh(string token) => "h:" + token;
}

public sealed class UnidadeDeTrabalhoFalsa : IUnidadeDeTrabalho
{
    public int SalvarChamadas { get; private set; }

    public int Transacoes { get; private set; }

    public bool EmTransacao { get; private set; }

    public Task SalvarAsync(CancellationToken ct)
    {
        SalvarChamadas++;
        return Task.CompletedTask;
    }

    public async Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct)
    {
        Transacoes++;
        EmTransacao = true;
        try
        {
            return await acao();
        }
        finally
        {
            EmTransacao = false;
        }
    }
}

public sealed class UsuarioRepositorioEmMemoria : IUsuarioRepositorio
{
    public List<Usuario> Todos { get; } = [];

    public Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct) =>
        Task.FromResult(Todos.Find(u => u.Funcionario.EmailCorporativo == emailNormalizado));

    public Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct) =>
        Task.FromResult(Todos.Find(u => u.Id == id));
}

public sealed class RefreshTokenRepositorioEmMemoria(UnidadeDeTrabalhoFalsa unidade) : IRefreshTokenRepositorio
{
    private readonly object _trava = new();

    public List<RefreshToken> Todos { get; } = [];

    /// <summary>Se verdadeiro, <see cref="TentarRevogarAsync"/> devolve falso como se outra requisição tivesse revogado primeiro.</summary>
    public bool SimularPerdaDaCorrida { get; set; }

    public bool AdicionouDentroDeTransacao { get; private set; }

    public bool RevogouDentroDeTransacao { get; private set; }

    public Task AdicionarAsync(RefreshToken token, CancellationToken ct)
    {
        AdicionouDentroDeTransacao = unidade.EmTransacao;
        lock (_trava)
        {
            Todos.Add(token);
        }

        return Task.CompletedTask;
    }

    public Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct)
    {
        lock (_trava)
        {
            return Task.FromResult(Todos.Find(t => t.TokenHash == tokenHash));
        }
    }

    public Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct)
    {
        RevogouDentroDeTransacao = unidade.EmTransacao;
        lock (_trava)
        {
            var token = Todos.Find(t => t.TokenHash == tokenHash);
            if (SimularPerdaDaCorrida || token is null || token.RevogadoEm is not null)
            {
                return Task.FromResult(false);
            }

            token.Revogar(agoraUtc);
            return Task.FromResult(true);
        }
    }

    public Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct)
    {
        lock (_trava)
        {
            foreach (var token in Todos.Where(t => t.UsuarioId == usuarioId))
            {
                token.Revogar(agoraUtc);
            }
        }

        return Task.CompletedTask;
    }
}

public sealed class LogColetor<T> : ILogger<T>
{
    public List<(LogLevel Nivel, string Mensagem)> Registros { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
        Registros.Add((logLevel, formatter(state, exception)));
}

/// <summary>Monta funcionários e usuários do Domain, que não têm construtores públicos para Id, Cargo e Setor (o EF os preenche).</summary>
public static class Dominio
{
    public static T Novo<T>()
        where T : class => (T)Activator.CreateInstance(typeof(T), nonPublic: true)!;

    public static void Definir<T>(T objeto, string propriedade, object? valor)
        where T : class =>
        typeof(T).GetProperty(propriedade, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.SetValue(objeto, valor);

    public static Cargo NovoCargo(string nome)
    {
        var cargo = Novo<Cargo>();
        Definir(cargo, nameof(Cargo.Nome), nome);
        return cargo;
    }

    public static Setor NovoSetor(string nome)
    {
        var setor = Novo<Setor>();
        Definir(setor, nameof(Setor.Nome), nome);
        return setor;
    }
}

/// <summary>Tudo o que um teste de caso de uso precisa, com o relógio em 2026-10-07T12:42:00.700Z (fração de propósito).</summary>
public sealed class Cenario
{
    public const string SenhaCerta = "Segredo#123";

    private int _proximoId = 1;

    public Cenario()
    {
        Relogio = new RelogioFalso(new DateTime(2026, 10, 7, 12, 42, 0, 700, DateTimeKind.Utc));
        Emissor = new EmissorDeTokensFalso(Relogio);
        Tokens = new RefreshTokenRepositorioEmMemoria(Unidade);
    }

    public RelogioFalso Relogio { get; }

    public SenhaHasherFalso Hasher { get; } = new();

    public EmissorDeTokensFalso Emissor { get; }

    public UnidadeDeTrabalhoFalsa Unidade { get; } = new();

    public UsuarioRepositorioEmMemoria Usuarios { get; } = new();

    public RefreshTokenRepositorioEmMemoria Tokens { get; }

    public LogColetor<EntrarCasoDeUso> LogDaEntrada { get; } = new();

    public EntrarCasoDeUso Entrar() => new(Usuarios, Tokens, Unidade, Hasher, Emissor, Relogio, LogDaEntrada);

    public RenovarTokenCasoDeUso Renovar() => new(Usuarios, Tokens, Unidade, Emissor, Relogio);

    public SairCasoDeUso Sair() => new(Tokens, Emissor, Relogio);

    /// <summary>Cria um usuário com senha <see cref="SenhaCerta"/>. <paramref name="nivel"/> nulo deixa o funcionário sem nível de acesso.</summary>
    public Usuario NovoUsuario(
        string email = "ana@empresa.com.br",
        string? nivel = "RH",
        bool usuarioAtivo = true,
        bool funcionarioAtivo = true,
        string? cargo = null,
        string? setor = null)
    {
        var funcionario = Funcionario.Criar("Ana Souza", email, new DateOnly(2026, 1, 2), new NivelDeAcesso(nivel ?? "qualquer"));
        Dominio.Definir(funcionario, nameof(Funcionario.Id), _proximoId++);
        if (nivel is null)
        {
            Dominio.Definir(funcionario, nameof(Funcionario.NivelDeAcesso), null);
        }

        if (cargo is not null)
        {
            Dominio.Definir(funcionario, nameof(Funcionario.Cargo), Dominio.NovoCargo(cargo));
        }

        if (setor is not null)
        {
            Dominio.Definir(funcionario, nameof(Funcionario.Setor), Dominio.NovoSetor(setor));
        }

        if (!funcionarioAtivo)
        {
            funcionario.Inativar();
        }

        var usuario = Usuario.Criar(funcionario, Hasher.Gerar(SenhaCerta));
        Dominio.Definir(usuario, nameof(Usuario.Id), _proximoId++);
        if (!usuarioAtivo)
        {
            usuario.Inativar();
        }

        Usuarios.Todos.Add(usuario);
        return usuario;
    }

    /// <summary>Guarda um refresh token válido para o usuário e devolve o texto que o cliente teria.</summary>
    public string NovoRefreshDoCliente(Usuario usuario, string texto = "refresh-do-cliente")
    {
        Tokens.Todos.Add(RefreshToken.Criar(usuario.Id, Emissor.HashDoRefresh(texto), Relogio.AgoraUtc, Emissor.ValidadeDoRefresh));
        return texto;
    }
}

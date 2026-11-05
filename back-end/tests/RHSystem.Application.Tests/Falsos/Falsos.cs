using System.Reflection;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
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

    /// <summary>O hash do refresh que o chamador mandou levar na claim <c>rth</c> (nulo = token sem a claim).</summary>
    public string? UltimoHashDaSessao { get; private set; }

    public AcessoEmitido EmitirAcesso(Usuario usuario, string? hashDoRefreshDaSessao = null)
    {
        UltimoHashDaSessao = hashDoRefreshDaSessao;
        return new($"jwt-do-usuario-{usuario.Id}", relogio.AgoraUtc.AddMinutes(15));
    }

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

    public Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, System.Data.IsolationLevel nivel, CancellationToken ct) => ExecutarEmTransacaoAsync(acao, ct);
}

public sealed class UsuarioRepositorioEmMemoria : IUsuarioRepositorio
{
    public List<Usuario> Todos { get; } = [];

    public Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct) =>
        Task.FromResult(Todos.Find(u => u.Funcionario.EmailCorporativo == emailNormalizado));

    public Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct) =>
        Task.FromResult(Todos.Find(u => u.Id == id));

    public Task<Usuario?> ObterPorFuncionarioIdAsync(int funcionarioId, CancellationToken ct) =>
        Task.FromResult(Todos.Find(u => u.FuncionarioId == funcionarioId));
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

    public List<(int UsuarioId, string? HashDaSessaoAtual)> RevogacoesDeOutros { get; } = [];

    public Task<int> RevogarOutrosDoUsuarioAsync(int usuarioId, string? hashDaSessaoAtual, DateTime agoraUtc, CancellationToken ct)
    {
        RevogacoesDeOutros.Add((usuarioId, hashDaSessaoAtual));
        lock (_trava)
        {
            var alvos = Todos.Where(t => t.UsuarioId == usuarioId && t.RevogadoEm is null && (hashDaSessaoAtual is null || t.TokenHash != hashDaSessaoAtual)).ToList();
            foreach (var token in alvos)
            {
                token.Revogar(agoraUtc);
            }

            return Task.FromResult(alvos.Count);
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

/// <summary>
/// Um <see cref="IAuditor"/> que só anota o que lhe pedem: qual método, com qual ator e evento, e em que pé estava a unidade de trabalho na hora
/// (quantos <c>SalvarAsync</c> já tinham rodado e se havia transação). <see cref="Falhar"/> faz o registro lançar, depois de anotado.
/// </summary>
public sealed class AuditorFalso(UnidadeDeTrabalhoFalsa unidade) : IAuditor
{
    public sealed record Chamada(string Metodo, AtorDoEvento? Ator, EventoDeAuditoriaNovo Evento, TimeSpan? Janela, int SalvarChamadasNaHora, bool EmTransacaoNaHora);

    public List<Chamada> Chamadas { get; } = [];

    public bool Falhar { get; set; }

    public void Registrar(EventoDeAuditoriaNovo evento) => Anotar(nameof(Registrar), null, evento, null);

    public void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento) => Anotar(nameof(RegistrarComo), ator, evento, null);

    public Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct)
    {
        Anotar(nameof(RegistrarEGravarAsync), null, evento, null);
        return Task.CompletedTask;
    }

    public Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct)
    {
        Anotar(nameof(RegistrarEGravarComoAsync), ator, evento, null);
        return Task.CompletedTask;
    }

    public Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct)
    {
        Anotar(nameof(RegistrarEGravarComLimiteAsync), ator, evento, janela);
        return Task.FromResult(true);
    }

    public IDisposable SuspenderRegistroAutomatico() => throw new NotSupportedException("O login não suspende o registro automático.");

    private void Anotar(string metodo, AtorDoEvento? ator, EventoDeAuditoriaNovo evento, TimeSpan? janela)
    {
        Chamadas.Add(new Chamada(metodo, ator, evento, janela, unidade.SalvarChamadas, unidade.EmTransacao));
        if (Falhar)
        {
            throw new InvalidOperationException("auditoria fora do ar (falha de teste)");
        }
    }
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
        Auditor = new AuditorFalso(Unidade);
    }

    public RelogioFalso Relogio { get; }

    public SenhaHasherFalso Hasher { get; } = new();

    public EmissorDeTokensFalso Emissor { get; }

    public UnidadeDeTrabalhoFalsa Unidade { get; } = new();

    public UsuarioRepositorioEmMemoria Usuarios { get; } = new();

    public RefreshTokenRepositorioEmMemoria Tokens { get; }

    public AuditorFalso Auditor { get; }

    public LogColetor<EntrarCasoDeUso> LogDaEntrada { get; } = new();

    public EntrarCasoDeUso Entrar() => new(Usuarios, Tokens, Unidade, Hasher, Emissor, Relogio, Auditor, LogDaEntrada);

    public RenovarTokenCasoDeUso Renovar() => new(Usuarios, Tokens, Unidade, Emissor, Relogio);

    public SairCasoDeUso Sair() => new(Tokens, Emissor, Relogio);

    /// <summary>Cria um usuário com senha <see cref="SenhaCerta"/>. <paramref name="nivel"/> nulo deixa o funcionário sem nível de acesso.</summary>
    public Usuario NovoUsuario(
        string email = "ana@empresa.com.br",
        string? nivel = "RH",
        bool usuarioAtivo = true,
        bool funcionarioAtivo = true,
        string? cargo = null,
        string? setor = null,
        bool senhaProvisoria = false,
        string? hashProvisorio = null)
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

        var usuario = Usuario.Criar(funcionario, hashProvisorio ?? Hasher.Gerar(SenhaCerta), senhaProvisoria);
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

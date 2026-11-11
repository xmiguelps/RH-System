using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Agendamento;
using RHSystem.Api.IntegrationTests.Emails;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Emails;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Um relógio que anda com o tempo real e pode ser avançado pelo teste (por exemplo, para vencer um token) ou fixado num instante (o dia dos
/// exemplos do contrato). Depois de <see cref="Fixar"/> ele não anda sozinho; <see cref="Avancar"/> continua somando.
/// </summary>
public sealed class RelogioDeTeste : IRelogio
{
    private long _deslocamentoEmTicks;
    private long _fixoEmTicks; // 0 = anda com o tempo real

    public DateTime AgoraUtc
    {
        get
        {
            var fixo = Interlocked.Read(ref _fixoEmTicks);
            var baseUtc = fixo == 0 ? DateTime.UtcNow : new DateTime(fixo, DateTimeKind.Utc);
            return baseUtc.AddTicks(Interlocked.Read(ref _deslocamentoEmTicks));
        }
    }

    public void Avancar(TimeSpan tempo) => Interlocked.Add(ref _deslocamentoEmTicks, tempo.Ticks);

    /// <summary>Para o relógio no instante dado (Kind Utc); zera o avanço.</summary>
    public void Fixar(DateTime utc)
    {
        if (utc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante precisa ter Kind = Utc.", nameof(utc));
        }

        Interlocked.Exchange(ref _deslocamentoEmTicks, 0);
        Interlocked.Exchange(ref _fixoEmTicks, utc.Ticks);
    }
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

    private readonly object _travaDoArmazenamento = new();
    private ArmazenamentoEmMemoria? _armazenamento;

    /// <summary>O armazenamento de arquivos desta API (em memória, no lugar do Blob): guarda os bytes e conta o que os testes de arquivo precisam provar. As APIs derivadas (<c>WithWebHostBuilder</c>) usam o mesmo.</summary>
    public ArmazenamentoEmMemoria Armazenamento
    {
        get
        {
            lock (_travaDoArmazenamento)
            {
                return _armazenamento ??= new ArmazenamentoEmMemoria(Relogio);
            }
        }
    }

    /// <summary>O envio de e-mail dos testes: em memória (o serviço em segundo plano fica desligado e os testes chamam <c>ProcessarCicloAsync</c> direto).</summary>
    public EnviadorDeEmailEmMemoria Enviador { get; } = new();

    /// <summary>O que as tarefas de teste do agendador anotam (o serviço em segundo plano fica desligado: os testes chamam o ciclo direto).</summary>
    public RegistroDeTarefasDeTeste Tarefas { get; } = new();

    public string Conexao { get; } = conexao;

    private readonly SemaphoreSlim _travaDoElenco = new(1, 1);
    private ElencoDeTeste? _elenco;

    /// <summary>Semeia o elenco de <c>contrato/ELENCO.md</c> (uma vez por banco) e fixa o relógio falso em 2026-10-07T12:42:00Z.</summary>
    public async Task<ElencoDeTeste> SemearElencoAsync()
    {
        await _travaDoElenco.WaitAsync();
        try
        {
            return _elenco ??= await ElencoDeTeste.SemearAsync(this);
        }
        finally
        {
            _travaDoElenco.Release();
        }
    }

    /// <summary>Um cliente com o token que o emissor de verdade emitiu para a pessoa do elenco (sem passar pelo login).</summary>
    public HttpClient ClienteComo(PessoaDeTeste pessoa) => ClienteComo(this, pessoa);

    /// <summary>O mesmo cliente, de uma API derivada desta (como a de <see cref="ComPedidosParados"/>): a chave e o emissor do token são os mesmos, então o token do elenco vale nela.</summary>
    public HttpClient ClienteComo(WebApplicationFactory<Program> fabrica, PessoaDeTeste pessoa)
    {
        var elenco = _elenco ?? throw new InvalidOperationException("Chame SemearElencoAsync antes de ClienteComo.");
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", elenco.TokensPorUsuario[pessoa.UsuarioId]);
        return cliente;
    }

    /// <summary>
    /// Uma segunda API sobre o MESMO banco em que a porta dos pedidos parados é a <paramref name="porta"/> (o <see cref="PedidosParadosEspiao"/>), no lugar do composto padrão da 1a. Use <see cref="ClienteComo(WebApplicationFactory{Program}, PessoaDeTeste)"/>
    /// para falar com ela.
    /// </summary>
    public WebApplicationFactory<Program> ComPedidosParados(IPedidosParadosNoGestor porta) =>
        WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IPedidosParadosNoGestor>();
            servicos.AddSingleton(porta);
        }));

    /// <summary>
    /// Uma segunda API sobre o MESMO banco em que o <see cref="IGestorDoFuncionario"/> é o <see cref="GestorDoFuncionarioEspiao"/> (que delega à implementação real da 1b e anota as chamadas em
    /// <paramref name="chamadas"/>) e, se <paramref name="pedidos"/> vier, a porta dos pedidos parados também é a falsa.
    /// </summary>
    public WebApplicationFactory<Program> ComGestorDoFuncionarioEspiao(ChamadasDoGestorDoFuncionario chamadas, IPedidosParadosNoGestor? pedidos = null) =>
        WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IGestorDoFuncionario>();
            servicos.AddScoped<IGestorDoFuncionario>(sp =>
            {
                var contexto = sp.GetRequiredService<AppDbContext>();
                return new GestorDoFuncionarioEspiao(new GestorDoFuncionario(contexto), contexto, chamadas);
            });
            if (pedidos is not null)
            {
                servicos.RemoveAll<IPedidosParadosNoGestor>();
                servicos.AddSingleton(pedidos);
            }
        }));

    public HttpClient ClienteAnonimo() => CreateClient();

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
        builder.UseSetting("Autenticacao:LimiteSenhaPorMinuto", "1000"); // idem: só LimiteDeSenhaTests usa 5
        builder.UseSetting("Autenticacao:LimiteSenhaAtualPorMinuto", "1000");
        builder.UseSetting("Database:AplicarScriptsAoIniciar", "false"); // o banco já vem com os scripts
        builder.UseSetting("Agendador:Ativo", "false"); // sem o servico em segundo plano do agendador: os testes chamam o ciclo direto
        builder.UseSetting("Email:EnvioAtivo", "false"); // sem o serviço em segundo plano: os testes chamam o ciclo de envio direto
        builder.UseSetting("Cors:OrigensPermitidas:0", OrigemPermitida);
        builder.UseSetting("App:UrlDoFront", "https://app.exemplo.com.br"); // os e-mails de convite e de redefinição montam o link com ela
        builder.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IRelogio>();
            servicos.AddSingleton<IRelogio>(Relogio);
            // Arquivos: só ArmazenamentoBlobTests fala com o Azurite. Aqui o armazenamento é o em memória, e a unidade de trabalho é decorada para contar as gravações feitas com transação aberta.
            servicos.RemoveAll<IArmazenamentoDeArquivos>();
            servicos.RemoveAll<IListagemDeArquivos>();
            servicos.AddSingleton<IArmazenamentoDeArquivos>(Armazenamento);
            servicos.AddSingleton<IListagemDeArquivos>(Armazenamento);
            servicos.RemoveAll<IUnidadeDeTrabalho>();
            servicos.AddScoped<IUnidadeDeTrabalho>(sp => new UnidadeDeTrabalhoEspia(ActivatorUtilities.CreateInstance<UnidadeDeTrabalho>(sp)));
            servicos.RemoveAll<IEnviadorDeEmail>();
            servicos.AddSingleton<IEnviadorDeEmail>(Enviador);
            servicos.AddSingleton<IRenderizadorDeEmail>(new RenderizadorDeTeste("TESTE_ENVIO", "Aviso de teste enviado"));
            servicos.AddSingleton<IRenderizadorDeEmail>(new RenderizadorDeTeste("TESTE_SIGILOSO", "Lembrete anonimo de teste enviado"));
            servicos.AddSingleton(Tarefas);
            // A que lança na recuperacao vem ANTES das outras: o teste prova que as que vem depois dela ainda recuperam.
            servicos.AddScoped<ITarefaAgendada, TarefaQueNaoRecuperaDeTeste>();
            servicos.AddScoped<ITarefaAgendada, TarefaDeTeste>();
            servicos.AddScoped<ITarefaAgendada, TarefaRecuperavelDeTeste>();
            servicos.AddScoped<ITarefaAgendada, TarefaAuditadaDeTeste>();
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

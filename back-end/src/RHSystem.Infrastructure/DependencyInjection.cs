using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Acesso;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Emails;
using RHSystem.Application.Empresa;
using RHSystem.Application.Escopo;
using RHSystem.Infrastructure.Acesso;
using RHSystem.Infrastructure.Agendamento;
using RHSystem.Infrastructure.Agendamento.Tarefas;
using RHSystem.Infrastructure.Auditoria;
using RHSystem.Infrastructure.Autenticacao;
using RHSystem.Infrastructure.Cadastros;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Emails;
using RHSystem.Infrastructure.Empresa;
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

        // O interceptor da auditoria vem do escopo (ele lê o ator da requisição). O AddDbContext e o AddDbContextFactory (o contexto próprio do Auditor)
        // precisam do mesmo ServiceLifetime (Scoped), senão o DbContextOptions entra em conflito; o AddDbContext vem primeiro.
        // ATENÇÃO: o EF Core 9 ACUMULA a configuração de cada AddDbContext/AddDbContextFactory. Passar a MESMA função às duas chamadas registrava o
        // interceptor duas vezes (medido: o contexto tinha dois AuditoriaInterceptor), e o seed do administrador "salvava" sem gravar. A configuração
        // mora só no AddDbContext; a fábrica herda dela (AuditoriaNoContainerTests confere que há um interceptor só).
        services.AddScoped<ContextoDeAuditoria>();
        services.AddScoped<AuditoriaInterceptor>();
        // A mensagem da exceção de uma chave repetida (1062) traz o VALOR da chave ("Duplicate entry '<valor>'"): o código a trata como sucesso, mas o EF registraria a falha
        // de gravação em nível Error, com a exceção, por conta própria. Medido (ChaveRepetidaNoLogTests e o controle de mutação): o evento que leva o valor é o
        // SaveChangesFailed; "Failed executing DbCommand" (RelationalEventId.CommandError) NÃO o leva e continua em Error, com o SQL, para diagnosticar falha de verdade. O
        // SaveChangesFailed desce para Debug (fora do log padrão); o que falha de verdade sobe como exceção e quem a trata registra (o tratador de erros e os serviços em
        // segundo plano, que só escrevem o tipo da exceção).
        services.AddDbContext<AppDbContext>(
            (sp, opcoes) => opcoes
                .UseMySql(conexao, new MySqlServerVersion(versaoDoServidor))
                .AddInterceptors(sp.GetRequiredService<AuditoriaInterceptor>())
                .ConfigureWarnings(avisos => avisos.Log((CoreEventId.SaveChangesFailed, LogLevel.Debug))));
        services.AddDbContextFactory<AppDbContext>(_ => { }, ServiceLifetime.Scoped);
        services.AddScoped<IAuditor, Auditor>();
        services.AddScoped<IConsultaDeAuditoria, ConsultaDeAuditoria>();

        // Fila de e-mails e envio. Sem Email:EnvioAtivo (o padrao do appsettings.json) o servico em segundo plano nem comeca e a fila so acumula.
        var email = new OpcoesDeEmail();
        configuration.GetSection(OpcoesDeEmail.Secao).Bind(email);
        email.Validar();
        services.AddSingleton(email);
        var app = new OpcoesDoApp();
        configuration.GetSection(OpcoesDoApp.Secao).Bind(app);
        app.Validar(email.EnvioAtivo);
        services.AddSingleton(app);
        services.AddSingleton<IEnviadorDeEmail, SmtpEnviadorDeEmail>();
        services.AddScoped<IFilaDeEmails, FilaDeEmails>();
        services.AddScoped<IProcessadorDeEmails, ProcessadorDeEmails>();
        services.AddHostedService<EnvioDeEmailsHostedService>();

        // Agendador: o que roda depois, com recuperacao e idempotencia. Cada fatia acrescenta a sua ITarefaAgendada (AddScoped); aqui entram as duas limpezas.
        var agendador = new OpcoesDoAgendador();
        configuration.GetSection(OpcoesDoAgendador.Secao).Bind(agendador);
        agendador.Validar();
        services.AddSingleton(agendador);
        var retencao = new OpcoesDeRetencao();
        configuration.GetSection(OpcoesDeRetencao.Secao).Bind(retencao);
        retencao.Validar();
        services.AddSingleton(retencao);
        services.AddScoped<IAgendador, Agendador>();
        services.AddScoped<IExecutorDeTarefas, ExecutorDeTarefas>();
        services.AddScoped<ITarefaAgendada, LimparTarefasAntigasTarefa>();
        services.AddScoped<ITarefaAgendada, LimparEmailsAntigosTarefa>();
        services.AddScoped<ITarefaAgendada, LimparTokensDeSenhaTarefa>();
        services.AddScoped<ITarefaAgendada, EfetivarDesligamentoTarefa>();
        services.AddHostedService<AgendadorHostedService>();

        // Convite e redefinição de senha: o serviço que emite o link (o token só no e-mail; o banco guarda o hash) e os dois e-mails da lista fechada.
        var senha = new OpcoesDeSenha();
        configuration.GetSection(OpcoesDeSenha.Secao).Bind(senha);
        senha.Validar();
        services.AddSingleton(senha);
        services.AddSingleton<IGeradorDeTokenDeSenha, GeradorDeTokenDeSenha>();
        services.AddScoped<IServicoDeConvite, ServicoDeConvite>();
        services.AddScoped<ILinksDeSenha, LinksDeSenha>();
        services.AddScoped<IConsultaDeUsuariosDoSistema, ConsultaDeUsuariosDoSistema>();
        services.AddScoped<IConsultaDeFuncionarios, ConsultaDeFuncionarios>();
        services.AddScoped<IConsultaDaEquipe, ConsultaDaEquipe>();

        // Cadastros de apoio (tela 29): cargos e benefícios do catálogo (fatia 1c).
        services.AddScoped<ICargosRepositorio, CargosRepositorio>();
        services.AddScoped<IBeneficiosRepositorio, BeneficiosRepositorio>();

        // A configuração da empresa: uma classe serve ao cadastro (IEmpresaRepositorio) e às fatias 3 e 7 (IConfiguracaoDaEmpresa, sem cache).
        services.AddScoped<EmpresaRepositorio>();
        services.AddScoped<IEmpresaRepositorio>(sp => sp.GetRequiredService<EmpresaRepositorio>());
        services.AddScoped<IConfiguracaoDaEmpresa>(sp => sp.GetRequiredService<EmpresaRepositorio>());

        // As caixas "Depois de salvar" do cadastro dependem das fatias 5 e 6: as versões padrão dizem a verdade (indisponível) e ficam registradas ANTES de qualquer outra, para as fatias donas
        // registrarem as delas depois (AddScoped: o último registro vale).
        services.AddScoped<IInclusaoNoOnboarding, OnboardingIndisponivel>();
        services.AddScoped<IEnvioDoFormularioDeAdmissao, FormularioDeAdmissaoIndisponivel>();
        services.AddScoped<IGestorDoFuncionario, GestorDoFuncionario>();
        services.AddScoped<ICadastroDeFuncionarios, CadastroDeFuncionarios>();
        services.AddScoped<IEdicaoDeFuncionarios, EdicaoDeFuncionarios>();
        // Desligamento: sem nenhuma ICondicaoParaEfetivarDesligamento registrada a efetivação vale; a fatia 3 registra a dela (o banco de horas do dia fechado) com AddScoped.
        services.AddScoped<IDesligamentoDeFuncionarios, DesligamentoDeFuncionarios>();
        services.AddScoped<IReadmissaoDeFuncionarios, ReadmissaoDeFuncionarios>();
        var limiteDeSenhaAtual = configuration.GetValue<int?>("Autenticacao:LimiteSenhaAtualPorMinuto") ?? 5;
        if (limiteDeSenhaAtual < 1)
        {
            throw new InvalidOperationException("Autenticacao:LimiteSenhaAtualPorMinuto precisa ser pelo menos 1.");
        }

        services.AddSingleton<ILimiteDeSenhaAtual>(sp => new LimiteDeSenhaAtual(sp.GetRequiredService<IRelogio>(), limiteDeSenhaAtual));
        services.AddSingleton<IRenderizadorDeEmail, ConviteRenderizador>();
        services.AddSingleton<IRenderizadorDeEmail, RedefinicaoSenhaRenderizador>();

        services.AddScoped<IUsuarioRepositorio, UsuarioRepositorio>();
        services.AddScoped<IRefreshTokenRepositorio, RefreshTokenRepositorio>();
        services.AddScoped<IUnidadeDeTrabalho, UnidadeDeTrabalho>();
        services.AddScoped<IGeradorDeMatriculas, GeradorDeMatriculas>();
        services.AddScoped<IEscopoDoUsuario, EscopoDoUsuario>();

        // Pedidos parados no gestor: a porta única que a 1b e a 1c injetam. O composto soma as IPedidosParadosNoGestorDaFatia que as fatias 3 (ponto) e 7 (férias)
        // registrarem (AddScoped); sem nenhuma, não faz nada (dívida registrada em contrato/STATUS.md).
        services.AddScoped<IPedidosParadosNoGestor, PedidosParadosNoGestorComposto>();

        // Situação de hoje: a fonte padrão só conhece o fim de semana (nada de ponto, atestado, férias nem horário ainda). A fatia 3 registra a fonte dela DEPOIS desta (o último
        // registro vale) e a ficha e a lista de colaboradores passam a mostrar a situação de verdade sem mudar aqui.
        services.AddScoped<IFonteDeFatosDoDia, FonteDeFatosDoDiaSemRegistros>();
        services.AddScoped<ISituacaoDeHoje, SituacaoDeHojeServico>();

        services.AddSingleton<ISenhaHasher, SenhaHasher>();
        services.AddSingleton<IRelogio, RelogioDoSistema>();
        services.AddSingleton<IEmissorDeTokens>(sp => new EmissorDeTokensJwt(jwt, sp.GetRequiredService<IRelogio>()));

        services.AddSingleton<IFonteDeScripts, ScriptsEmbutidos>();
        services.AddSingleton(sp => new ExecutorDeScripts(conexao, sp.GetRequiredService<ILogger<ExecutorDeScripts>>()));

        return services;
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Comunicacao;
using RHSystem.Domain.Documentos;
using RHSystem.Domain.Emails;
using RHSystem.Domain.Empresa;
using RHSystem.Domain.Importacoes;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia;

/// <summary>
/// O EF Core só mapeia e consulta: as tabelas vêm dos scripts SQL versionados (sem migrations, sem EnsureCreated). O mapeamento é
/// explícito (tabela e coluna por propriedade) e o teste MapeamentoContraBancoTests o confere contra o banco criado pelos scripts.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<NivelDeAcesso> NiveisDeAcesso => Set<NivelDeAcesso>();

    public DbSet<Cargo> Cargos => Set<Cargo>();

    public DbSet<Setor> Setores => Set<Setor>();

    public DbSet<Funcionario> Funcionarios => Set<Funcionario>();

    public DbSet<Usuario> Usuarios => Set<Usuario>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<UsuarioTokenSenha> UsuariosTokensSenha => Set<UsuarioTokenSenha>();

    public DbSet<Gestor> Gestores => Set<Gestor>();

    public DbSet<DesligamentoAgendado> DesligamentosAgendados => Set<DesligamentoAgendado>();

    public DbSet<Beneficio> Beneficios => Set<Beneficio>();

    public DbSet<BeneficioDoFuncionario> BeneficiosDosFuncionarios => Set<BeneficioDoFuncionario>();

    public DbSet<FuncionarioDetalhes> DetalhesDosFuncionarios => Set<FuncionarioDetalhes>();

    public DbSet<EmailNaFila> EmailsNaFila => Set<EmailNaFila>();

    public DbSet<TarefaAgendada> TarefasAgendadas => Set<TarefaAgendada>();

    public DbSet<EventoDeAuditoria> EventosDeAuditoria => Set<EventoDeAuditoria>();

    public DbSet<MudancaDeAuditoria> MudancasDeAuditoria => Set<MudancaDeAuditoria>();

    public DbSet<Importacao> Importacoes => Set<Importacao>();

    public DbSet<ConfiguracaoDaEmpresa> ConfiguracoesDaEmpresa => Set<ConfiguracaoDaEmpresa>();

    public DbSet<DocumentoDoColaborador> Documentos => Set<DocumentoDoColaborador>();

    public DbSet<PontoDoDia> PontosDosDias => Set<PontoDoDia>();

    public DbSet<MarcacaoDePonto> Marcacoes => Set<MarcacaoDePonto>();

    public DbSet<PedidoDeAjuste> PedidosDeAjuste => Set<PedidoDeAjuste>();

    public DbSet<LancamentoDoBancoDeHoras> LancamentosDoBancoDeHoras => Set<LancamentoDoBancoDeHoras>();

    public DbSet<Feriado> Feriados => Set<Feriado>();

    public DbSet<Atestado> Atestados => Set<Atestado>();

    public DbSet<Aviso> Avisos => Set<Aviso>();

    public DbSet<LeituraDeAviso> LeiturasDeAviso => Set<LeituraDeAviso>();

    public DbSet<Holerite> Holerites => Set<Holerite>();

    public DbSet<Advertencia> Advertencias => Set<Advertencia>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
}

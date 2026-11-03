using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// SONDA: mede, com um DbContext DESCARTÁVEL só destes testes (não entra no produto), as armadilhas de mapeamento que o
/// <c>Banco de Dados/Scripts/LEIA-ME.md</c> ("Armadilhas novas") inferiu para as tabelas das fatias seguintes. Quem mapear essas
/// tabelas lê aqui o que funciona e o que o MySQL recusa. O contexto "ingênuo" é o controle: mapeia do jeito errado para provar que a
/// armadilha existe.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SondaMapeamentoDosScriptsTests(MySqlFixture mysql)
{
    private const string HashHex = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeSenha = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private static async Task<long> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        var valor = await comando.ExecuteScalarAsync();
        return valor is null or DBNull ? long.MinValue : Convert.ToInt64(valor, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<int> NovoFuncionarioAsync(string conexao, string email = "sonda@empresa.com.br")
    {
        await ExecutarAsync(conexao, $"INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Sonda', '2026-01-02', TRUE, '{email}')");
        return (int)await EscalarAsync(conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
    }

    private static async Task<int> NovoFormularioAsync(string conexao, string titulo = "Pesquisa da sonda")
    {
        await ExecutarAsync(conexao, $"INSERT INTO FORMULARIOS (TITULO) VALUES ('{titulo}')");
        return (int)await EscalarAsync(conexao, $"SELECT ID_FORMULARIO FROM FORMULARIOS WHERE TITULO = '{titulo}'");
    }

    private static async Task<int> NovoUsuarioAsync(string conexao, string email = "sonda@empresa.com.br")
    {
        var funcionario = await NovoFuncionarioAsync(conexao, email);
        await ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({funcionario}, '{HashDeSenha}')");
        return (int)await EscalarAsync(conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionario}");
    }

    [Fact]
    public async Task Formulario_com_Ativo_false_grava_false_e_nao_o_padrao_TRUE()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var db = new SondaCorreta(conexao))
        {
            db.Formularios.Add(new SondaFormulario { Titulo = "Inativo desde o início", Ativo = false });
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await EscalarAsync(conexao, "SELECT ATIVO FROM FORMULARIOS WHERE TITULO = 'Inativo desde o início'")); // o padrão do banco é TRUE
    }

    [Fact]
    public async Task Treinamento_com_EmiteCertificado_false_grava_false()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var db = new SondaCorreta(conexao))
        {
            db.Treinamentos.Add(new SondaTreinamento { Titulo = "Sem certificado", EmiteCertificado = false, IncluiAdmitidos = true });
            await db.SaveChangesAsync();
        }

        Assert.Equal(0, await EscalarAsync(conexao, "SELECT EMITE_CERTIFICADO FROM TREINAMENTOS WHERE TITULO = 'Sem certificado'")); // o padrão do banco é TRUE
        Assert.Equal(1, await EscalarAsync(conexao, "SELECT INCLUI_ADMITIDOS FROM TREINAMENTOS WHERE TITULO = 'Sem certificado'"));
    }

    [Fact]
    public async Task Participacao_com_Guid_v4_gerado_pela_aplicacao_passa_no_CHECK_e_o_valor_nao_e_regerado()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var formulario = await NovoFormularioAsync(conexao);
        var funcionario = await NovoFuncionarioAsync(conexao);
        var v4 = Guid.NewGuid();

        await using (var db = new SondaCorreta(conexao))
        {
            db.Participacoes.Add(new SondaParticipacao { Id = v4, IdFormulario = formulario, IdFuncionario = funcionario });
            await db.SaveChangesAsync();
        }

        await using var leitura = new SondaCorreta(conexao);
        var gravada = await leitura.Participacoes.SingleAsync();
        Assert.Equal(v4, gravada.Id); // o valor não foi regerado
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM FORMULARIOS_PARTICIPACOES WHERE ID_PARTICIPACAO = '{v4}'"));

        // Controle: um Guid que não é versão 4 (aqui a v7) é recusado pelo CHECK (erro 3819).
        await using var v7 = new SondaCorreta(conexao);
        v7.Participacoes.Add(new SondaParticipacao { Id = Guid.CreateVersion7(), IdFormulario = formulario, IdFuncionario = funcionario });
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => v7.SaveChangesAsync());
        Assert.Equal(3819, Assert.IsType<MySqlException>(erro.InnerException).Number);
    }

    [Fact]
    public async Task Colunas_CHAVE_DESTINO_e_CHAVE_AVALIADO_mapeadas_como_calculadas_ficam_fora_do_INSERT()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var formulario = await NovoFormularioAsync(conexao);
        var funcionario = await NovoFuncionarioAsync(conexao);
        await ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Setor da sonda')");
        var setor = (int)await EscalarAsync(conexao, "SELECT ID_SETOR FROM SETORES WHERE NOME_SETOR = 'Setor da sonda'");

        await using (var db = new SondaCorreta(conexao))
        {
            db.Destinatarios.Add(new SondaDestinatario { IdFormulario = formulario, IdSetor = setor });
            db.Participacoes.Add(new SondaParticipacao { Id = Guid.NewGuid(), IdFormulario = formulario, IdFuncionario = funcionario });
            await db.SaveChangesAsync();
        }

        Assert.NotEqual(long.MinValue, await EscalarAsync(conexao, "SELECT LENGTH(CHAVE_DESTINO) FROM FORMULARIOS_DESTINATARIOS"));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT CHAVE_AVALIADO FROM FORMULARIOS_PARTICIPACOES")); // sem avaliado: o banco calcula 0

        // Controle: mapeada como coluna comum, o EF manda o valor no INSERT e o MySQL recusa (erro 3105).
        await using var ingenuo = new SondaIngenua(conexao);
        ingenuo.Destinatarios.Add(new SondaDestinatario { IdFormulario = formulario, IdFuncionario = funcionario });
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => ingenuo.SaveChangesAsync());
        Assert.Equal(3105, Assert.IsType<MySqlException>(erro.InnerException).Number);
    }

    [Fact]
    public async Task Coluna_com_DEFAULT_CURRENT_TIMESTAMP_mapeada_como_gerada_na_inclusao_nao_grava_0001_01_01()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var formulario = await NovoFormularioAsync(conexao);

        await using (var db = new SondaCorreta(conexao))
        {
            db.Candidatos.Add(new SondaCandidato { IdFormulario = formulario, Nome = "Candidata", Email = "candidata@exemplo.com.br" });
            await db.SaveChangesAsync();
        }

        Assert.True(await EscalarAsync(conexao, "SELECT YEAR(DATA_CADASTRO) FROM FORMULARIOS_CANDIDATOS") >= 2026);
        Assert.InRange(await EscalarAsync(conexao, "SELECT ABS(TIMESTAMPDIFF(SECOND, DATA_CADASTRO, NOW())) FROM FORMULARIOS_CANDIDATOS"), 0, 5);
    }

    [Fact]
    public async Task Colunas_EM_ABERTO_VIGENTE_e_CHAVE_PENDENTE_mapeadas_como_calculadas_ficam_fora_do_INSERT_e_do_UPDATE()
    {
        // Sem isso o MySQL recusa com o erro 3105.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuario = await NovoUsuarioAsync(conexao);
        var funcionario = await NovoFuncionarioAsync(conexao, "desligamento@empresa.com.br");

        await using (var db = new SondaCorreta(conexao))
        {
            db.Tokens.Add(new SondaToken { IdUsuario = usuario, Tipo = "CONVITE", TokenHash = HashHex, ExpiraEm = DateTime.UtcNow.AddDays(7) });
            db.Desligamentos.Add(new SondaDesligamento { IdFuncionario = funcionario, UltimoDiaTrabalho = new DateOnly(2026, 12, 31) });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await EscalarAsync(conexao, "SELECT EM_ABERTO FROM USUARIOS_TOKENS_SENHA"));
        Assert.Equal(1, await EscalarAsync(conexao, "SELECT VIGENTE FROM DESLIGAMENTOS_AGENDADOS"));

        await using (var db = new SondaCorreta(conexao))
        {
            db.Tokens.Single().UsadoEm = DateTime.UtcNow;
            var desligamento = db.Desligamentos.Single();
            desligamento.Situacao = "CANCELADO";
            desligamento.CanceladoEm = DateTime.UtcNow;
            await db.SaveChangesAsync(); // o UPDATE também não manda as colunas calculadas
        }

        Assert.Equal(long.MinValue, await EscalarAsync(conexao, "SELECT EM_ABERTO FROM USUARIOS_TOKENS_SENHA")); // NULL: o link foi usado
        Assert.Equal(long.MinValue, await EscalarAsync(conexao, "SELECT VIGENTE FROM DESLIGAMENTOS_AGENDADOS")); // NULL: não está mais vigente

        // Controle: mapeadas como colunas comuns, o INSERT é recusado (erro 3105).
        var outroUsuario = await NovoUsuarioAsync(conexao, "outro@empresa.com.br");
        await using var ingenuo = new SondaIngenua(conexao);
        ingenuo.Tokens.Add(new SondaToken { IdUsuario = outroUsuario, Tipo = "CONVITE", TokenHash = HashHex.Replace('0', '1'), ExpiraEm = DateTime.UtcNow.AddDays(7) });
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => ingenuo.SaveChangesAsync());
        Assert.Equal(3105, Assert.IsType<MySqlException>(erro.InnerException).Number);
    }

    [Fact]
    public async Task Segundo_pedido_de_ajuste_pendente_do_mesmo_dia_viola_UQ_PONTOS_MANUTENCOES_UM_PENDENTE_e_depois_de_decidir_o_primeiro_o_segundo_passa()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var funcionario = await NovoFuncionarioAsync(conexao);
        await ExecutarAsync(conexao, $"INSERT INTO PONTOS (ID_FUNCIONARIO, DATA) VALUES ({funcionario}, CURDATE())");
        var ponto = (int)await EscalarAsync(conexao, $"SELECT ID_PONTO FROM PONTOS WHERE ID_FUNCIONARIO = {funcionario}");

        await using (var db = new SondaCorreta(conexao))
        {
            db.Manutencoes.Add(new SondaManutencao { IdPonto = ponto, IdFuncionario = funcionario, Justificativa = "Esqueci de bater a saída" });
            await db.SaveChangesAsync();
        }

        Assert.Equal(ponto, await EscalarAsync(conexao, "SELECT CHAVE_PENDENTE FROM PONTOS_MANUTENCOES")); // vale o ID_PONTO enquanto PENDENTE

        await using (var segundo = new SondaCorreta(conexao))
        {
            segundo.Manutencoes.Add(new SondaManutencao { IdPonto = ponto, IdFuncionario = funcionario, Justificativa = "De novo" });
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());
            var mysqlErro = Assert.IsType<MySqlException>(erro.InnerException);
            Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, mysqlErro.ErrorCode);
            Assert.Contains("UQ_PONTOS_MANUTENCOES_UM_PENDENTE", mysqlErro.Message);
        }

        // Decidido o primeiro, CHAVE_PENDENTE vira NULL e o segundo pedido passa. O trigger do grupo exige o gestor que analisou; a
        // recusa do gestor encerra o pedido (REJEITADA).
        var gestorFuncionario = await NovoFuncionarioAsync(conexao, "gestor.da.sonda@empresa.com.br");
        await ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({gestorFuncionario})");
        var gestor = (int)await EscalarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {gestorFuncionario}");
        await ExecutarAsync(conexao, $"UPDATE PONTOS_MANUTENCOES SET ID_GESTOR = {gestor}, RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = NOW(), STATUS_SOLICITACAO = 'REJEITADA' WHERE STATUS_SOLICITACAO = 'PENDENTE'");
        Assert.Equal(long.MinValue, await EscalarAsync(conexao, "SELECT CHAVE_PENDENTE FROM PONTOS_MANUTENCOES"));

        await using var terceiro = new SondaCorreta(conexao);
        terceiro.Manutencoes.Add(new SondaManutencao { IdPonto = ponto, IdFuncionario = funcionario, Justificativa = "Agora vai" });
        await terceiro.SaveChangesAsync();
        Assert.Equal(2, await EscalarAsync(conexao, "SELECT COUNT(*) FROM PONTOS_MANUTENCOES"));
    }

    [Fact]
    public async Task Coluna_com_DEFAULT_UTC_TIMESTAMP_mapeada_como_gerada_na_inclusao_grava_o_instante_UTC_do_banco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var usuario = await NovoUsuarioAsync(conexao);

        await using (var db = new SondaCorreta(conexao))
        {
            db.Tokens.Add(new SondaToken { IdUsuario = usuario, Tipo = "REDEFINICAO", TokenHash = HashHex, ExpiraEm = DateTime.UtcNow.AddHours(1) }); // CriadoEm fica de fora
            await db.SaveChangesAsync();
        }

        Assert.True(await EscalarAsync(conexao, "SELECT YEAR(CRIADO_EM) FROM USUARIOS_TOKENS_SENHA") >= 2026); // nunca 0001-01-01
        Assert.InRange(await EscalarAsync(conexao, "SELECT ABS(TIMESTAMPDIFF(SECOND, CRIADO_EM, UTC_TIMESTAMP(6))) FROM USUARIOS_TOKENS_SENHA"), 0, 5);
    }

    [Fact]
    public async Task Padroes_FALSE_de_proposito_gravam_true_e_false_como_enviados()
    {
        // FORMULARIOS.LEMBRETE_AUTOMATICO, TREINAMENTOS.AVISA_POR_EMAIL e LEMBRA_ANTES_DO_FIM (V001): o padrão do banco é FALSE de
        // propósito, NÃO é declarado no mapeamento e o EF sempre envia o valor.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var db = new SondaCorreta(conexao))
        {
            db.Formularios.Add(new SondaFormulario { Titulo = "Com lembrete", LembreteAutomatico = true });
            db.Formularios.Add(new SondaFormulario { Titulo = "Sem lembrete", LembreteAutomatico = false });
            db.Treinamentos.Add(new SondaTreinamento { Titulo = "Com aviso", AvisaPorEmail = true, LembraAntesDoFim = true });
            db.Treinamentos.Add(new SondaTreinamento { Titulo = "Sem aviso", AvisaPorEmail = false, LembraAntesDoFim = false });
            await db.SaveChangesAsync();
        }

        Assert.Equal(1, await EscalarAsync(conexao, "SELECT LEMBRETE_AUTOMATICO FROM FORMULARIOS WHERE TITULO = 'Com lembrete'"));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT LEMBRETE_AUTOMATICO FROM FORMULARIOS WHERE TITULO = 'Sem lembrete'"));
        Assert.Equal(1, await EscalarAsync(conexao, "SELECT AVISA_POR_EMAIL FROM TREINAMENTOS WHERE TITULO = 'Com aviso'"));
        Assert.Equal(1, await EscalarAsync(conexao, "SELECT LEMBRA_ANTES_DO_FIM FROM TREINAMENTOS WHERE TITULO = 'Com aviso'"));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT AVISA_POR_EMAIL FROM TREINAMENTOS WHERE TITULO = 'Sem aviso'"));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT LEMBRA_ANTES_DO_FIM FROM TREINAMENTOS WHERE TITULO = 'Sem aviso'"));
    }

    [Fact]
    public async Task Matricula_pela_SEQUENCIAS_devolve_numeros_sem_buraco_na_mesma_transacao_e_o_rollback_devolve_o_numero()
    {
        // Tabela SEQUENCIAS (V001): UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + 1) WHERE NOME = 'MATRICULA' e SELECT LAST_INSERT_ID() na
        // mesma conexão e transação. SQL cru, sem entidade; o formato é LPAD(numero, 5, '0').
        var conexao = await mysql.NovoBancoComScriptsAsync();

        async Task<long> ProximaAsync(SondaCorreta db)
        {
            await db.Database.ExecuteSqlRawAsync("UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + 1) WHERE NOME = 'MATRICULA'");
            return (await db.Database.SqlQueryRaw<long>("SELECT LAST_INSERT_ID() AS `Value`").ToListAsync()).Single();
        }

        await using (var db = new SondaCorreta(conexao))
        {
            await using var transacao = await db.Database.BeginTransactionAsync();
            Assert.Equal(1, await ProximaAsync(db));
            Assert.Equal(2, await ProximaAsync(db));
            await transacao.CommitAsync();
        }

        await using (var db = new SondaCorreta(conexao))
        {
            await using var transacao = await db.Database.BeginTransactionAsync();
            Assert.Equal(3, await ProximaAsync(db));
            await transacao.RollbackAsync(); // o cadastro falhou: o número volta
        }

        await using (var db = new SondaCorreta(conexao))
        {
            await using var transacao = await db.Database.BeginTransactionAsync();
            var numero = await ProximaAsync(db);
            await transacao.CommitAsync();
            Assert.Equal(3, numero); // sem buraco: o 3 do rollback foi devolvido
            Assert.Equal("00003", numero.ToString("D5"));
        }

        Assert.Equal(3, await EscalarAsync(conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
        Assert.Equal(1, await EscalarAsync(conexao, "SELECT LPAD(ULTIMO_VALOR, 5, '0') = '00003' FROM SEQUENCIAS WHERE NOME = 'MATRICULA'")); // formato do banco
    }
}

// ----- Entidades e contextos descartáveis da sonda (não entram no produto) -----

internal sealed class SondaFormulario
{
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public bool Ativo { get; set; } = true;

    public bool Anonimo { get; set; }

    public bool LembreteAutomatico { get; set; }
}

internal sealed class SondaTreinamento
{
    public int Id { get; set; }

    public string Titulo { get; set; } = string.Empty;

    public bool Obrigatorio { get; set; }

    public bool Publicado { get; set; }

    public bool EmiteCertificado { get; set; } = true;

    public bool IncluiAdmitidos { get; set; } = true;

    public bool AvisaPorEmail { get; set; }

    public bool LembraAntesDoFim { get; set; }
}

internal sealed class SondaDestinatario
{
    public int Id { get; set; }

    public int IdFormulario { get; set; }

    public int? IdFuncionario { get; set; }

    public int? IdSetor { get; set; }

    public string? ChaveDestino { get; set; }
}

internal sealed class SondaParticipacao
{
    public Guid Id { get; set; }

    public int IdFormulario { get; set; }

    public int IdFuncionario { get; set; }

    public int? ChaveAvaliado { get; set; }
}

internal sealed class SondaCandidato
{
    public int Id { get; set; }

    public int IdFormulario { get; set; }

    public string Nome { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public DateTime DataCadastro { get; set; }
}

internal sealed class SondaToken
{
    public int Id { get; set; }

    public int IdUsuario { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public string TokenHash { get; set; } = string.Empty;

    public DateTime CriadoEm { get; set; }

    public DateTime ExpiraEm { get; set; }

    public DateTime? UsadoEm { get; set; }

    public int? EmAberto { get; set; }
}

internal sealed class SondaDesligamento
{
    public int Id { get; set; }

    public int IdFuncionario { get; set; }

    public DateOnly UltimoDiaTrabalho { get; set; }

    public string Situacao { get; set; } = "AGENDADO";

    public DateTime? CanceladoEm { get; set; }

    public int? Vigente { get; set; }
}

internal sealed class SondaManutencao
{
    public int Id { get; set; }

    public int IdPonto { get; set; }

    public int IdFuncionario { get; set; }

    public string Justificativa { get; set; } = string.Empty;

    public string StatusSolicitacao { get; set; } = "PENDENTE";

    public int? ChavePendente { get; set; }
}

/// <summary>O mapeamento certo: colunas calculadas fora do INSERT e do UPDATE; padrões do banco sem declarar; datas geradas na inclusão.</summary>
internal sealed class SondaCorreta(string conexao) : SondaBase(conexao, calculadasDeclaradas: true);

/// <summary>O controle: as colunas calculadas viram colunas comuns, e o MySQL recusa o INSERT (erro 3105).</summary>
internal sealed class SondaIngenua(string conexao) : SondaBase(conexao, calculadasDeclaradas: false);

internal abstract class SondaBase(string conexao, bool calculadasDeclaradas) : DbContext
{
    public DbSet<SondaFormulario> Formularios => Set<SondaFormulario>();

    public DbSet<SondaTreinamento> Treinamentos => Set<SondaTreinamento>();

    public DbSet<SondaDestinatario> Destinatarios => Set<SondaDestinatario>();

    public DbSet<SondaParticipacao> Participacoes => Set<SondaParticipacao>();

    public DbSet<SondaCandidato> Candidatos => Set<SondaCandidato>();

    public DbSet<SondaToken> Tokens => Set<SondaToken>();

    public DbSet<SondaDesligamento> Desligamentos => Set<SondaDesligamento>();

    public DbSet<SondaManutencao> Manutencoes => Set<SondaManutencao>();

    protected override void OnConfiguring(DbContextOptionsBuilder opcoes) =>
        opcoes.UseMySql(conexao, new MySqlServerVersion(new Version(8, 4, 0)));

    private void Calculada<T>(Microsoft.EntityFrameworkCore.Metadata.Builders.PropertyBuilder<T> propriedade)
    {
        if (!calculadasDeclaradas)
        {
            return; // o controle deixa como coluna comum
        }

        propriedade.ValueGeneratedOnAddOrUpdate();
        propriedade.Metadata.SetBeforeSaveBehavior(PropertySaveBehavior.Ignore);
        propriedade.Metadata.SetAfterSaveBehavior(PropertySaveBehavior.Ignore);
    }

    protected override void OnModelCreating(ModelBuilder modelo)
    {
        modelo.Entity<SondaFormulario>(e =>
        {
            e.ToTable("FORMULARIOS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_FORMULARIO");
            e.Property(x => x.Titulo).HasColumnName("TITULO");
            e.Property(x => x.Ativo).HasColumnName("ATIVO"); // padrão TRUE no banco, não declarado aqui
            e.Property(x => x.Anonimo).HasColumnName("ANONIMO");
            e.Property(x => x.LembreteAutomatico).HasColumnName("LEMBRETE_AUTOMATICO");
        });

        modelo.Entity<SondaTreinamento>(e =>
        {
            e.ToTable("TREINAMENTOS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_TREINAMENTO");
            e.Property(x => x.Titulo).HasColumnName("TITULO");
            e.Property(x => x.Obrigatorio).HasColumnName("OBRIGATORIO");
            e.Property(x => x.Publicado).HasColumnName("PUBLICADO");
            e.Property(x => x.EmiteCertificado).HasColumnName("EMITE_CERTIFICADO");
            e.Property(x => x.IncluiAdmitidos).HasColumnName("INCLUI_ADMITIDOS");
            e.Property(x => x.AvisaPorEmail).HasColumnName("AVISA_POR_EMAIL");
            e.Property(x => x.LembraAntesDoFim).HasColumnName("LEMBRA_ANTES_DO_FIM");
        });

        modelo.Entity<SondaDestinatario>(e =>
        {
            e.ToTable("FORMULARIOS_DESTINATARIOS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_FORMULARIO_DESTINATARIO");
            e.Property(x => x.IdFormulario).HasColumnName("ID_FORMULARIO");
            e.Property(x => x.IdFuncionario).HasColumnName("ID_FUNCIONARIO");
            e.Property(x => x.IdSetor).HasColumnName("ID_SETOR");
            Calculada(e.Property(x => x.ChaveDestino).HasColumnName("CHAVE_DESTINO"));
        });

        modelo.Entity<SondaParticipacao>(e =>
        {
            e.ToTable("FORMULARIOS_PARTICIPACOES");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_PARTICIPACAO").ValueGeneratedNever(); // o Guid v4 vem da aplicação
            e.Property(x => x.IdFormulario).HasColumnName("ID_FORMULARIO");
            e.Property(x => x.IdFuncionario).HasColumnName("ID_FUNCIONARIO");
            Calculada(e.Property(x => x.ChaveAvaliado).HasColumnName("CHAVE_AVALIADO"));
        });

        modelo.Entity<SondaCandidato>(e =>
        {
            e.ToTable("FORMULARIOS_CANDIDATOS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_FORMULARIO_CANDIDATO");
            e.Property(x => x.IdFormulario).HasColumnName("ID_FORMULARIO");
            e.Property(x => x.Nome).HasColumnName("NOME");
            e.Property(x => x.Email).HasColumnName("EMAIL");
            e.Property(x => x.DataCadastro).HasColumnName("DATA_CADASTRO").ValueGeneratedOnAdd(); // DEFAULT CURRENT_TIMESTAMP
        });

        modelo.Entity<SondaToken>(e =>
        {
            e.ToTable("USUARIOS_TOKENS_SENHA");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_USUARIO_TOKEN_SENHA");
            e.Property(x => x.IdUsuario).HasColumnName("ID_USUARIO");
            e.Property(x => x.Tipo).HasColumnName("TIPO");
            e.Property(x => x.TokenHash).HasColumnName("TOKEN_HASH");
            e.Property(x => x.CriadoEm).HasColumnName("CRIADO_EM").ValueGeneratedOnAdd(); // DEFAULT (UTC_TIMESTAMP(6))
            e.Property(x => x.ExpiraEm).HasColumnName("EXPIRA_EM");
            e.Property(x => x.UsadoEm).HasColumnName("USADO_EM");
            Calculada(e.Property(x => x.EmAberto).HasColumnName("EM_ABERTO"));
        });

        modelo.Entity<SondaDesligamento>(e =>
        {
            e.ToTable("DESLIGAMENTOS_AGENDADOS");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_DESLIGAMENTO_AGENDADO");
            e.Property(x => x.IdFuncionario).HasColumnName("ID_FUNCIONARIO");
            e.Property(x => x.UltimoDiaTrabalho).HasColumnName("ULTIMO_DIA_TRABALHO");
            e.Property(x => x.Situacao).HasColumnName("SITUACAO");
            e.Property(x => x.CanceladoEm).HasColumnName("CANCELADO_EM");
            Calculada(e.Property(x => x.Vigente).HasColumnName("VIGENTE"));
        });

        modelo.Entity<SondaManutencao>(e =>
        {
            e.ToTable("PONTOS_MANUTENCOES");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).HasColumnName("ID_MANUTENCAO_PONTO");
            e.Property(x => x.IdPonto).HasColumnName("ID_PONTO");
            e.Property(x => x.IdFuncionario).HasColumnName("ID_FUNCIONARIO");
            e.Property(x => x.Justificativa).HasColumnName("JUSTIFICATIVA");
            e.Property(x => x.StatusSolicitacao).HasColumnName("STATUS_SOLICITACAO");
            Calculada(e.Property(x => x.ChavePendente).HasColumnName("CHAVE_PENDENTE"));
        });
    }
}

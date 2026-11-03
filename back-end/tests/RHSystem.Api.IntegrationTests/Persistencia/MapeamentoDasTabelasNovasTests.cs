using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Emails;
using RHSystem.Domain.Importacoes;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta de cada tabela nova no banco REAL (o MySQL em contêiner com os scripts de verdade), relendo com OUTRO contexto: nada vem
/// do cache do primeiro. Cada teste prova uma técnica da tabela de cuidados do plano (coluna calculada, padrão do banco, fuso, JSON).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDasTabelasNovasTests(MySqlFixture mysql)
{
    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    // Com fração de microssegundo: as colunas DATETIME(6) das tabelas novas guardam até 6 casas e a ida e volta tem de devolver todas.
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddTicks(1234560);

    private static async Task<Funcionario> NovoFuncionarioAsync(AppDbContext db, string email)
    {
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var funcionario = Funcionario.Criar($"Pessoa {email}", email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync();
        return funcionario;
    }

    private static async Task<Usuario> NovoUsuarioAsync(AppDbContext db, string email, bool senhaProvisoria = false)
    {
        var funcionario = await NovoFuncionarioAsync(db, email);
        var usuario = Usuario.Criar(funcionario, HashDeTeste, senhaProvisoria);
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync();
        return usuario;
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<object?> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return await comando.ExecuteScalarAsync();
    }

    private static MySqlException ErroDoMySql(DbUpdateException erro) => Assert.IsType<MySqlException>(erro.InnerException);

    [Fact]
    public async Task UsuarioTokenSenha_ida_e_volta_com_as_datas_do_relogio_em_UTC_e_EM_ABERTO_calculado()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        int usuarioId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            usuarioId = (await NovoUsuarioAsync(escrita, "token@empresa.com.br")).Id;
            escrita.UsuariosTokensSenha.Add(UsuarioTokenSenha.Criar(usuarioId, TipoDeTokenDeSenha.Convite, new string('a', 64), Agora, TimeSpan.FromHours(48)));
            await escrita.SaveChangesAsync();
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var token = await leitura.UsuariosTokensSenha.SingleAsync();
            Assert.Equal(Agora, token.CriadoEm);
            Assert.Equal(Agora.AddHours(48), token.ExpiraEm);
            Assert.Equal(DateTimeKind.Utc, token.CriadoEm.Kind);
            Assert.Equal(DateTimeKind.Utc, token.ExpiraEm.Kind);
            Assert.Equal(TipoDeTokenDeSenha.Convite, token.Tipo);
            Assert.Equal(usuarioId, token.UsuarioId);
            Assert.Equal((sbyte?)1, leitura.Entry(token).Property<sbyte?>("EmAberto").CurrentValue);
        }

        await ExecutarAsync(conexao, "UPDATE USUARIOS_TOKENS_SENHA SET USADO_EM = UTC_TIMESTAMP(6)");
        await using var depoisDeUsar = ContextoDeTeste.Novo(conexao);
        var usado = await depoisDeUsar.UsuariosTokensSenha.SingleAsync();
        Assert.NotNull(usado.UsadoEm);
        Assert.Null(depoisDeUsar.Entry(usado).Property<sbyte?>("EmAberto").CurrentValue);
    }

    [Fact]
    public async Task Data_em_branco_cai_no_padrao_do_banco_em_UTC_so_como_rede_de_seguranca()
    {
        // A aplicação não usa este caminho: ela sempre informa a data (IRelogio). Aqui se deixa CriadoEm em branco de propósito, e a data de
        // expiração é a hora real + 1 h (com a data do relógio falso o CHECK EXPIRA_EM > CRIADO_EM recusaria, porque CRIADO_EM seria a hora real).
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var usuario = await NovoUsuarioAsync(escrita, "padrao@empresa.com.br");
            var token = UsuarioTokenSenha.Criar(usuario.Id, TipoDeTokenDeSenha.Convite, new string('b', 64), DateTime.UtcNow, TimeSpan.FromHours(1));
            escrita.UsuariosTokensSenha.Add(token);
            escrita.Entry(token).Property(t => t.CriadoEm).CurrentValue = default;
            await escrita.SaveChangesAsync();
        }

        var horaDoBanco = (DateTime)(await EscalarAsync(conexao, "SELECT UTC_TIMESTAMP(6)"))!;
        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = await leitura.UsuariosTokensSenha.SingleAsync();

        Assert.Equal(DateTimeKind.Utc, lido.CriadoEm.Kind);
        Assert.True(Math.Abs((lido.CriadoEm - horaDoBanco).TotalSeconds) < 5, $"CRIADO_EM ({lido.CriadoEm:O}) devia estar dentro de 5 s do UTC_TIMESTAMP(6) do banco ({horaDoBanco:O}).");
    }

    [Fact]
    public async Task Segundo_token_em_aberto_do_mesmo_usuario_e_tipo_viola_a_chave_unica_e_depois_de_invalidar_o_primeiro_passa()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        int usuarioId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            usuarioId = (await NovoUsuarioAsync(escrita, "unico@empresa.com.br")).Id;
            escrita.UsuariosTokensSenha.Add(UsuarioTokenSenha.Criar(usuarioId, TipoDeTokenDeSenha.Convite, new string('a', 64), Agora, TimeSpan.FromHours(48)));
            await escrita.SaveChangesAsync();
        }

        await using (var segundo = ContextoDeTeste.Novo(conexao))
        {
            segundo.UsuariosTokensSenha.Add(UsuarioTokenSenha.Criar(usuarioId, TipoDeTokenDeSenha.Convite, new string('b', 64), Agora, TimeSpan.FromHours(48)));
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());
            Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, ErroDoMySql(erro).ErrorCode);
        }

        // Controle: outro TIPO para o mesmo usuário convive com o primeiro.
        await using (var outroTipo = ContextoDeTeste.Novo(conexao))
        {
            outroTipo.UsuariosTokensSenha.Add(UsuarioTokenSenha.Criar(usuarioId, TipoDeTokenDeSenha.Redefinicao, new string('c', 64), Agora, TimeSpan.FromHours(1)));
            await outroTipo.SaveChangesAsync();
        }

        await ExecutarAsync(conexao, $"UPDATE USUARIOS_TOKENS_SENHA SET INVALIDADO_EM = UTC_TIMESTAMP(6) WHERE TOKEN_HASH = '{new string('a', 64)}'");
        await using var depois = ContextoDeTeste.Novo(conexao);
        depois.UsuariosTokensSenha.Add(UsuarioTokenSenha.Criar(usuarioId, TipoDeTokenDeSenha.Convite, new string('b', 64), Agora, TimeSpan.FromHours(48)));
        await depois.SaveChangesAsync();
        Assert.Equal(3, await depois.UsuariosTokensSenha.CountAsync());
    }

    [Fact]
    public async Task Usuario_grava_SenhaProvisoria_true_e_false_como_enviados()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            await NovoUsuarioAsync(escrita, "provisoria@empresa.com.br", senhaProvisoria: true);
            await NovoUsuarioAsync(escrita, "definida@empresa.com.br", senhaProvisoria: false);
        }

        Assert.Equal(1L, Convert.ToInt64(await EscalarAsync(conexao, "SELECT SENHA_PROVISORIA FROM USUARIOS u JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = u.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = 'provisoria@empresa.com.br'")));
        Assert.Equal(0L, Convert.ToInt64(await EscalarAsync(conexao, "SELECT SENHA_PROVISORIA FROM USUARIOS u JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = u.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = 'definida@empresa.com.br'")));

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var provisoria = await leitura.Usuarios.SingleAsync(u => u.SenhaProvisoria);
        Assert.Equal(HashDeTeste, provisoria.SenhaHash);
        provisoria.DefinirSenha(HashDeTeste + "-nova");
        await leitura.SaveChangesAsync();
        Assert.Equal(0L, await leitura.Usuarios.LongCountAsync(u => u.SenhaProvisoria));
    }

    [Fact]
    public async Task DesligamentoAgendado_ida_e_volta_com_VIGENTE_calculado_e_segundo_vigente_viola_a_chave_unica()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        int funcionarioId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            funcionarioId = (await NovoFuncionarioAsync(escrita, "saindo@empresa.com.br")).Id;
            escrita.DesligamentosAgendados.Add(DesligamentoAgendado.Criar(funcionarioId, new DateOnly(2026, 12, 31), Agora));
            await escrita.SaveChangesAsync();
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var desligamento = await leitura.DesligamentosAgendados.SingleAsync();
            Assert.Equal(funcionarioId, desligamento.FuncionarioId);
            Assert.Equal(new DateOnly(2026, 12, 31), desligamento.UltimoDiaTrabalho);
            Assert.Equal(SituacaoDoDesligamento.Agendado, desligamento.Situacao);
            Assert.Equal(Agora, desligamento.CriadoEm);
            Assert.Equal(DateTimeKind.Utc, desligamento.CriadoEm.Kind);
            Assert.Null(desligamento.EfetivadoEm);
            Assert.Equal((sbyte?)1, leitura.Entry(desligamento).Property<sbyte?>("Vigente").CurrentValue);
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.DesligamentosAgendados.Add(DesligamentoAgendado.Criar(funcionarioId, new DateOnly(2027, 1, 31), Agora));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());
        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, ErroDoMySql(erro).ErrorCode);
    }

    [Fact]
    public async Task EmailNaFila_ida_e_volta_com_JSON_com_acentos_e_chave_ASCII()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.EmailsNaFila.Add(EmailNaFila.Criar("CONVITE", "CONVITE:func:1", "ana@empresa.com.br", Agora, referenciaId: 1, carga: """{"nome":"José Ação","link":"https://x/y"}"""));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var email = await leitura.EmailsNaFila.SingleAsync();

        Assert.True(email.Id > 0);
        Assert.Equal("CONVITE", email.Tipo);
        Assert.Equal("CONVITE:func:1", email.ChaveDeIdempotencia);
        Assert.Equal(1, email.ReferenciaId);
        Assert.Equal("ana@empresa.com.br", email.DestinatarioEmail);
        Assert.False(email.Sigiloso);
        Assert.Equal(SituacaoDoEmail.Pendente, email.Situacao);
        Assert.Equal(0, email.Tentativas);
        Assert.Equal(Agora, email.ProximaTentativaEm);
        Assert.Equal(Agora, email.CriadoEm);
        Assert.Equal(DateTimeKind.Utc, email.ProximaTentativaEm.Kind);

        // O MySQL normaliza o JSON (ordem das chaves, espaços): compara-se o JSON interpretado, não o texto.
        using var carga = JsonDocument.Parse(email.Carga!);
        Assert.Equal("José Ação", carga.RootElement.GetProperty("nome").GetString());
        Assert.Equal("https://x/y", carga.RootElement.GetProperty("link").GetString());
    }

    [Fact]
    public async Task EmailNaFila_com_a_mesma_chave_de_idempotencia_viola_a_chave_unica_com_MySqlException_1062()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var primeiro = ContextoDeTeste.Novo(conexao))
        {
            primeiro.EmailsNaFila.Add(EmailNaFila.Criar("CONVITE", "CONVITE:func:1", "ana@empresa.com.br", Agora));
            await primeiro.SaveChangesAsync();
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.EmailsNaFila.Add(EmailNaFila.Criar("CONVITE", "CONVITE:func:1", "outra@empresa.com.br", Agora));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => segundo.SaveChangesAsync());

        Assert.Equal(MySqlErrorCode.DuplicateKeyEntry, ErroDoMySql(erro).ErrorCode);
        Assert.Equal(1062, (int)ErroDoMySql(erro).ErrorCode);
    }

    [Fact]
    public async Task EmailNaFila_ENVIADO_com_CARGA_preenchida_viola_o_CHECK()
    {
        // Prova que o CHECK vale pelo caminho do EF: o texto da situação sai certo ('ENVIADO') e o banco recusa a carga que sobrou.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.EmailsNaFila.Add(EmailNaFila.Criar("CONVITE", "CONVITE:func:1", "ana@empresa.com.br", Agora, carga: """{"link":"segredo"}"""));
            await escrita.SaveChangesAsync();
        }

        await using var db = ContextoDeTeste.Novo(conexao);
        var email = await db.EmailsNaFila.SingleAsync();
        db.Entry(email).Property(e => e.Situacao).CurrentValue = SituacaoDoEmail.Enviado;
        db.Entry(email).Property(e => e.EnviadoEm).CurrentValue = Agora;

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(3819, ErroDoMySql(erro).Number); // ER_CHECK_CONSTRAINT_VIOLATED
        Assert.Contains("CHK_FILA_EMAILS_CARGA_SO_ENQUANTO_ESPERA", ErroDoMySql(erro).Message);
    }

    [Fact]
    public async Task TarefaAgendada_ida_e_volta_com_DIA_REFERENCIA_e_PARAMETROS()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.TarefasAgendadas.Add(TarefaAgendada.Criar(
                "CONSOLIDAR_BANCO_DE_HORAS",
                "CONSOLIDAR_BANCO_DE_HORAS:2026-10-07",
                Agora,
                diaReferencia: new DateOnly(2026, 10, 7),
                parametros: """{"limite":500}"""));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var tarefa = await leitura.TarefasAgendadas.SingleAsync();

        Assert.True(tarefa.Id > 0);
        Assert.Equal("CONSOLIDAR_BANCO_DE_HORAS", tarefa.Tipo);
        Assert.Equal(new DateOnly(2026, 10, 7), tarefa.DiaReferencia);
        Assert.Equal(SituacaoDaTarefa.Pendente, tarefa.Situacao);
        Assert.Equal(Agora, tarefa.ExecutarApos);
        Assert.Equal(Agora, tarefa.CriadaEm);
        Assert.Equal(DateTimeKind.Utc, tarefa.ExecutarApos.Kind);
        Assert.Equal(0, tarefa.Tentativas);
        using var parametros = JsonDocument.Parse(tarefa.Parametros!);
        Assert.Equal(500, parametros.RootElement.GetProperty("limite").GetInt32());
    }

    [Fact]
    public async Task EventoDeAuditoria_aceita_as_15_areas_e_as_14_acoes_e_devolve_QUANDO_com_Kind_Utc()
    {
        Assert.Equal(15, Enum.GetValues<AuditoriaArea>().Length);
        Assert.Equal(14, Enum.GetValues<AuditoriaAcao>().Length);

        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var pessoa = await NovoFuncionarioAsync(escrita, "auditor@empresa.com.br");
            foreach (var area in Enum.GetValues<AuditoriaArea>())
            {
                escrita.EventosDeAuditoria.Add(EventoDeAuditoria.Criar(Agora, TipoDeAtor.Pessoa, pessoa.Id, "Pessoa auditor", Perfil.RH, area, AuditoriaAcao.Criou, $"Criou em {area}", "registro", "ENTIDADE_X", 1, CanalDeOrigem.Portal, "10.0.0.1"));
            }

            foreach (var acao in Enum.GetValues<AuditoriaAcao>())
            {
                escrita.EventosDeAuditoria.Add(EventoDeAuditoria.Criar(Agora, TipoDeAtor.Pessoa, pessoa.Id, "Pessoa auditor", Perfil.Gestor, AuditoriaArea.Colaboradores, acao, $"Fez {acao}", "registro", null, null, CanalDeOrigem.Portal, null));
            }

            escrita.EventosDeAuditoria.Add(EventoDeAuditoria.Criar(Agora, TipoDeAtor.Sistema, null, "Sistema", null, AuditoriaArea.Acesso, AuditoriaAcao.Registrou, "Tarefa agendada", "-", null, null, CanalDeOrigem.Sistema, null));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var eventos = await leitura.EventosDeAuditoria.ToListAsync();

        Assert.Equal(15 + 14 + 1, eventos.Count);
        Assert.Equal(Enum.GetValues<AuditoriaArea>().Order(), eventos.Select(e => e.Area).Distinct().Order());
        Assert.Equal(Enum.GetValues<AuditoriaAcao>().Order(), eventos.Select(e => e.Acao).Distinct().Order());
        Assert.All(eventos, e =>
        {
            Assert.Equal(Agora, e.Quando);
            Assert.Equal(DateTimeKind.Utc, e.Quando.Kind);
        });
        Assert.Equal(Perfil.RH, eventos.First(e => e.AtorPerfil == Perfil.RH).AtorPerfil);
        Assert.Contains(eventos, e => e.AtorPerfil == Perfil.Gestor);

        var sistema = eventos.Single(e => e.AtorTipo == TipoDeAtor.Sistema);
        Assert.Null(sistema.FuncionarioAtorId);
        Assert.Null(sistema.AtorPerfil);
        Assert.Null(sistema.Ip);
        Assert.Equal(CanalDeOrigem.Sistema, sistema.Canal);
    }

    [Fact]
    public async Task MudancaDeAuditoria_oculta_com_antes_e_depois_viola_o_CHECK()
    {
        Assert.Throws<ArgumentException>(() => MudancaDeAuditoria.Criar(3, "Salario", "100", "200", valorOculto: true));

        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var pessoa = await NovoFuncionarioAsync(escrita, "auditor@empresa.com.br");
            var evento = EventoDeAuditoria.Criar(Agora, TipoDeAtor.Pessoa, pessoa.Id, "Pessoa auditor", Perfil.RH, AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou, "Alterou", "registro", null, null, CanalDeOrigem.Portal, null);
            evento.AdicionarMudanca(1, "Salario", "100", "200", valorOculto: false);
            evento.AdicionarMudanca(2, "Senha", null, null, valorOculto: true); // controle: oculta SEM valores grava
            escrita.EventosDeAuditoria.Add(evento);
            await escrita.SaveChangesAsync();
        }

        await using var db = ContextoDeTeste.Novo(conexao);
        var mudanca = await db.MudancasDeAuditoria.SingleAsync(m => m.Campo == "Salario");
        Assert.Equal("100", mudanca.ValorAntes);
        db.Entry(mudanca).Property(m => m.ValorOculto).CurrentValue = true; // marca oculta SEM apagar antes e depois

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.Equal(3819, ErroDoMySql(erro).Number); // ER_CHECK_CONSTRAINT_VIOLATED
    }

    [Fact]
    public async Task Importacao_ida_e_volta_com_PREVIA_json_e_smallints()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var rh = await NovoFuncionarioAsync(escrita, "rh@empresa.com.br");
            escrita.Importacoes.Add(Importacao.Criar(rh.Id, "colaboradores.xlsx", totalLinhas: 3, linhasValidas: 2, linhasComErro: 1, Agora, TimeSpan.FromHours(1), previa: """{"linhas":[{"nome":"José"}]}"""));
            escrita.Importacoes.Add(Importacao.Criar(rh.Id, "grande.xlsx", totalLinhas: 500, linhasValidas: 500, linhasComErro: 0, Agora, TimeSpan.FromHours(1), caminhoArquivo: "importacoes/grande.xlsx"));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var comErros = await leitura.Importacoes.SingleAsync(i => i.NomeArquivo == "colaboradores.xlsx");
        Assert.Equal(SituacaoDaImportacao.ComErros, comErros.Situacao);
        Assert.Equal((3, 2, 1), (comErros.TotalLinhas, comErros.LinhasValidas, comErros.LinhasComErro));
        Assert.Equal(Agora, comErros.CriadaEm);
        Assert.Equal(Agora.AddHours(1), comErros.ExpiraEm);
        Assert.Equal(DateTimeKind.Utc, comErros.ExpiraEm.Kind);
        Assert.Null(comErros.ConfirmadaEm);
        Assert.Null(comErros.TotalCriados);
        using var previa = JsonDocument.Parse(comErros.Previa!);
        Assert.Equal("José", previa.RootElement.GetProperty("linhas")[0].GetProperty("nome").GetString());

        var grande = await leitura.Importacoes.SingleAsync(i => i.NomeArquivo == "grande.xlsx");
        Assert.Equal(SituacaoDaImportacao.Pronta, grande.Situacao);
        Assert.Equal(500, grande.TotalLinhas);
        Assert.Equal("importacoes/grande.xlsx", grande.CaminhoArquivo);
        Assert.Null(grande.Previa);
    }

    [Fact]
    public async Task Beneficio_grava_Opcional_AceitaDependentes_e_AceitaTrajeto_como_enviados()
    {
        // V001: true e false, o que o EF mandar é o que fica (o padrão FALSE do banco não pode interferir).
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.Beneficios.Add(Beneficio.Criar("Vale-transporte", "Trajeto casa-trabalho", opcional: true, aceitaDependentes: false, aceitaTrajeto: true));
            escrita.Beneficios.Add(Beneficio.Criar("Plano de saúde", null, opcional: false, aceitaDependentes: true, aceitaTrajeto: false));
            await escrita.SaveChangesAsync();
        }

        Assert.Equal("1,0,1", await EscalarAsync(conexao, "SELECT CONCAT(OPCIONAL, ',', ACEITA_DEPENDENTES, ',', ACEITA_TRAJETO) FROM BENEFICIOS WHERE NOME_BENEFICIO = 'Vale-transporte'"));
        Assert.Equal("0,1,0", await EscalarAsync(conexao, "SELECT CONCAT(OPCIONAL, ',', ACEITA_DEPENDENTES, ',', ACEITA_TRAJETO) FROM BENEFICIOS WHERE NOME_BENEFICIO = 'Plano de saúde'"));

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var plano = await leitura.Beneficios.SingleAsync(b => b.Nome == "Plano de saúde");
        Assert.Null(plano.Descricao);
        Assert.True(plano.AceitaDependentes);
        Assert.False(plano.Opcional);
        Assert.False(plano.AceitaTrajeto);
    }

    [Fact]
    public async Task Funcionario_com_Matricula_Setor_e_Gestor_do_setor_ida_e_volta()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        int pessoaId;
        int chefeId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var pessoa = await NovoFuncionarioAsync(escrita, "pessoa@empresa.com.br");
            var chefe = await NovoFuncionarioAsync(escrita, "chefe@empresa.com.br");
            pessoa.AtribuirMatricula("00482");
            await escrita.SaveChangesAsync();
            pessoaId = pessoa.Id;
            chefeId = chefe.Id;
        }

        // Setor e gestor entram por SQL: quem cadastra setor e gestor é a fatia 1c.
        await ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Financeiro')");
        await ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({chefeId})");
        await ExecutarAsync(conexao, "UPDATE SETORES SET ID_GESTOR = (SELECT ID_GESTOR FROM GESTORES LIMIT 1)");
        await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = (SELECT ID_SETOR FROM SETORES LIMIT 1) WHERE ID_FUNCIONARIO = {pessoaId}");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lida = await leitura.Funcionarios.Include(f => f.Setor).SingleAsync(f => f.Id == pessoaId);
        var gestor = await leitura.Gestores.SingleAsync();

        Assert.Equal("00482", lida.Matricula);
        Assert.NotNull(lida.SetorId);
        Assert.Equal("Financeiro", lida.Setor!.Nome);
        Assert.Equal(gestor.Id, lida.Setor.GestorId);
        Assert.Equal(chefeId, gestor.FuncionarioId);
        Assert.Null(lida.CargoId);
        Assert.Null((await leitura.Funcionarios.SingleAsync(f => f.Id == chefeId)).Matricula);
    }
}

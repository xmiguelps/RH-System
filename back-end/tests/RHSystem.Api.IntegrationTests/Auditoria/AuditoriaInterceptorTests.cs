using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Infrastructure.Persistencia;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Auditoria;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// O interceptor do EF no banco REAL (um banco por classe, com tabelas de teste). Cada teste cria as linhas dele e lê só os eventos delas, por
/// tabela e id. O relógio é falso e fixo: o <c>Quando</c> do evento vem dele, nunca do <c>DEFAULT</c> do banco.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaInterceptorTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static (string Conexao, PessoaDeTeste Ator)? _pronto;

    private async Task<(string Conexao, PessoaDeTeste Ator)> BancoAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_pronto is null)
            {
                var conexao = await mysql.NovoBancoComScriptsAsync();
                _pronto = (conexao, await PrepararBancoAsync(conexao));
            }

            return _pronto.Value;
        }
        finally
        {
            Trava.Release();
        }
    }

    private async Task<(string Conexao, PessoaDeTeste Ator, ContextoAuditadoDeTeste Db)> ComoPessoaAsync(RelogioDeTeste? relogio = null, ContextoDeAuditoria? contexto = null)
    {
        var (conexao, ator) = await BancoAsync();
        return (conexao, ator, NovoContexto(conexao, PessoaComIp(ator), relogio ?? RelogioEm(Agora), contexto));
    }

    [Fact]
    public async Task Criar_entidade_de_teste_gera_um_evento_Criou_com_ator_ip_entidade_id_e_descricao_Rotulo_Nome()
    {
        var (conexao, ator, db) = await ComoPessoaAsync();
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "Caneta", Dia = new DateOnly(2026, 10, 7) };

        db.Add(item);
        await db.SaveChangesAsync();

        var evento = Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id));
        Assert.Equal(AuditoriaAcao.Criou, evento.Acao);
        Assert.Equal(AuditoriaArea.Cadastros, evento.Area);
        Assert.Equal("Item Caneta", evento.Descricao);
        Assert.Equal("Item Caneta", evento.RegistroAfetado);
        Assert.Equal((TipoDeAtor.Pessoa, ator.FuncionarioId, "Ator de Teste", Perfil.RH, CanalDeOrigem.Portal, Ip), (evento.AtorTipo, evento.FuncionarioAtorId, evento.AtorNome, evento.AtorPerfil, evento.Canal, evento.Ip));
        Assert.Equal(["Nome", "Situação", "Dia"], evento.Mudancas.Select(m => m.Campo));
        Assert.Equal([(null, "Caneta"), (null, "Sim"), (null, "07/10/2026")], evento.Mudancas.Select(m => (m.ValorAntes, m.ValorDepois)));
        Assert.Equal([1, 2, 3], evento.Mudancas.Select(m => m.Ordem));
    }

    [Fact]
    public async Task Alterar_chave_estrangeira_de_entidade_de_teste_grava_o_NOME_da_referenciada_e_nao_o_id()
    {
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var analista = new CategoriaDeTeste { Nome = "Analista" };
        var analistaPleno = new CategoriaDeTeste { Nome = "Analista pleno" };
        var item = new ItemDeTeste { Nome = "Bruno Lima" };
        db.AddRange(analista, analistaPleno);
        await db.SaveChangesAsync();
        item.CategoriaId = analista.Id;
        db.Add(item);
        await db.SaveChangesAsync();

        item.CategoriaId = analistaPleno.Id;
        await db.SaveChangesAsync();

        var eventos = await EventosAsync(conexao, "TESTE_ITENS", item.Id);
        var alterou = Assert.Single(eventos, e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal("Categoria de Bruno Lima", alterou.Descricao);
        var mudanca = Assert.Single(alterou.Mudancas);
        Assert.Equal(("Categoria", "Analista", "Analista pleno", false), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois, mudanca.ValorOculto));
    }

    [Fact]
    public async Task Alterar_varios_campos_junta_os_nomes_na_descricao_e_uma_mudanca_por_campo()
    {
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "Régua" };
        db.Add(item);
        await db.SaveChangesAsync();

        item.Nome = "Régua 30 cm";
        item.Dia = new DateOnly(2026, 11, 1);
        await db.SaveChangesAsync();

        var alterou = Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal("Nome, Dia de Régua 30 cm", alterou.Descricao);
        Assert.Equal(["Nome", "Dia"], alterou.Mudancas.Select(m => m.Campo));
        Assert.Equal([("Régua", "Régua 30 cm"), (null, "01/11/2026")], alterou.Mudancas.Select(m => (m.ValorAntes, m.ValorDepois)));
    }

    [Fact]
    public async Task Alterar_so_propriedade_nao_marcada_nao_gera_evento()
    {
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "Borracha" };
        db.Add(item);
        await db.SaveChangesAsync();

        item.NaoMarcada = "qualquer coisa";
        await db.SaveChangesAsync();

        Assert.Equal([AuditoriaAcao.Criou], (await EventosAsync(conexao, "TESTE_ITENS", item.Id)).Select(e => e.Acao));
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_ITENS WHERE ID = {item.Id} AND NAO_MARCADA = 'qualquer coisa'")); // a alteração valeu
    }

    [Fact]
    public async Task Ativo_de_true_para_false_gera_Inativou()
    {
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "Grampeador" };
        db.Add(item);
        await db.SaveChangesAsync();

        item.Ativo = false;
        await db.SaveChangesAsync();

        var inativou = Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id), e => e.Acao == AuditoriaAcao.Inativou);
        Assert.Equal("Inativação de Grampeador", inativou.Descricao);
        var mudanca = Assert.Single(inativou.Mudancas);
        Assert.Equal(("Situação", "Sim", "Não"), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois));
    }

    [Fact]
    public async Task Excluir_gera_Excluiu_com_depois_nulo()
    {
        // Setor inserido por SQL e removido pelo DbContext: nenhuma operação de domínio que só nasce depois.
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        await ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Compras da auditoria')");
        var setor = await db.Setores.SingleAsync(s => s.Nome == "Compras da auditoria");
        var id = setor.Id;

        db.Remove(setor);
        await db.SaveChangesAsync();

        var excluiu = Assert.Single(await EventosAsync(conexao, "SETORES", id));
        Assert.Equal((AuditoriaAcao.Excluiu, AuditoriaArea.Cadastros, "Setor Compras da auditoria excluído"), (excluiu.Acao, excluiu.Area, excluiu.Descricao));
        var mudanca = Assert.Single(excluiu.Mudancas);
        Assert.Equal(("Nome", "Compras da auditoria", null), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois));
    }

    [Fact]
    public async Task Campo_sensivel_grava_ValorOculto_sem_antes_nem_depois()
    {
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "Cofre", Senha = "segredo-da-auditoria-123" };
        db.Add(item);
        await db.SaveChangesAsync();
        item.Senha = "outro-segredo-da-auditoria-456";
        await db.SaveChangesAsync();

        var eventos = await EventosAsync(conexao, "TESTE_ITENS", item.Id);
        var mudancasDeSenha = eventos.SelectMany(e => e.Mudancas).Where(m => m.Campo == "Senha").ToList();

        Assert.Equal(2, mudancasDeSenha.Count); // criou e alterou
        Assert.All(mudancasDeSenha, m => Assert.Equal((true, null, null), (m.ValorOculto, m.ValorAntes, m.ValorDepois)));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT COUNT(*) FROM AUDITORIA_MUDANCAS WHERE VALOR_ANTES LIKE '%segredo-da-auditoria%' OR VALOR_DEPOIS LIKE '%segredo-da-auditoria%'"));
    }

    [Fact]
    public async Task Entidade_sem_Auditado_nao_gera_evento()
    {
        var (conexao, ator, db) = await ComoPessoaAsync();
        await using var _ = db;
        var antes = await EscalarAsync(conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS");
        var semMarca = new SemAuditoriaDeTeste { Nome = "Livre" };
        var token = RefreshToken.Criar(ator.UsuarioId, new string('a', 64), Agora, TimeSpan.FromDays(7));

        db.AddRange(semMarca, token);
        await db.SaveChangesAsync();

        Assert.Equal(antes, await EscalarAsync(conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS"));
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_SEM_AUDITORIA WHERE ID = {semMarca.Id}"));
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_desfaz_a_alteracao()
    {
        // Review Focus 2: rótulo e nome vazios deixam a descrição vazia; o evento é recusado, e a alteração NÃO pode ficar sem o evento.
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var mudo = new ItemMudoDeTeste { Valor = "não pode ficar" };

        db.Add(mudo);
        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());

        Assert.Equal(0, await EscalarAsync(conexao, "SELECT COUNT(*) FROM TESTE_ITENS_MUDOS WHERE VALOR = 'não pode ficar'"));
        Assert.Empty(await EventosAsync(conexao, "TESTE_ITENS_MUDOS"));
    }

    [Fact]
    public async Task Falha_do_proprio_salvamento_nao_deixa_transacao_aberta_e_o_salvamento_seguinte_grava()
    {
        // O interceptor abre a transação própria no SavingChanges. Se o INSERT da alteração falha (aqui: nome com 101 caracteres em VARCHAR(100)), o
        // SaveChangesFailed precisa desfazê-la e descartá-la. Se ela ficar aberta, o salvamento seguinte do MESMO contexto entra nela e, como ninguém a
        // confirma, tudo some quando o contexto morre: perda silenciosa de dados (a mesma classe do defeito do interceptor registrado duas vezes).
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var grande = new ItemDeTeste { Nome = new string('x', 101) };
        db.Add(grande);

        await Assert.ThrowsAnyAsync<Exception>(() => db.SaveChangesAsync());

        Assert.Null(db.Database.CurrentTransaction);
        db.Entry(grande).State = EntityState.Detached;
        var seguinte = new ItemDeTeste { Nome = "Depois da falha" };
        db.Add(seguinte);
        await db.SaveChangesAsync();
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_ITENS WHERE ID = {seguinte.Id}")); // visto por outra conexão, com o contexto ainda vivo
        Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", seguinte.Id));
        Assert.Equal(0, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_ITENS WHERE NOME = '{new string('x', 101)}'"));
    }

    [Fact]
    public async Task Requisicao_anonima_nao_gera_evento_automatico()
    {
        var (conexao, _) = await BancoAsync();
        await using var db = NovoContexto(conexao, AtorDeTeste.Anonimo(), RelogioEm(Agora));
        var item = new ItemDeTeste { Nome = "Anônimo" };

        db.Add(item);
        await db.SaveChangesAsync();

        Assert.Empty(await EventosAsync(conexao, "TESTE_ITENS", item.Id));
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_ITENS WHERE ID = {item.Id}"));
    }

    [Fact]
    public async Task Execucao_em_segundo_plano_grava_ator_SISTEMA_canal_SISTEMA_sem_ip()
    {
        var (conexao, _) = await BancoAsync();
        await using var db = NovoContexto(conexao, AtorDeTeste.Sistema(), RelogioEm(Agora));
        var item = new ItemDeTeste { Nome = "Da tarefa" };

        db.Add(item);
        await db.SaveChangesAsync();

        var evento = Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id));
        Assert.Equal((TipoDeAtor.Sistema, null, "Sistema", null, CanalDeOrigem.Sistema, null), (evento.AtorTipo, evento.FuncionarioAtorId, evento.AtorNome, evento.AtorPerfil, evento.Canal, evento.Ip));
    }

    [Fact]
    public async Task SuspenderRegistroAutomatico_nao_grava_ate_o_Dispose()
    {
        var contexto = new ContextoDeAuditoria();
        var (conexao, _, db) = await ComoPessoaAsync(contexto: contexto);
        await using var _ = db;
        var dentro = new ItemDeTeste { Nome = "Dentro da suspensão" };
        var fora = new ItemDeTeste { Nome = "Fora da suspensão" };

        using (contexto.Suspender())
        {
            db.Add(dentro);
            await db.SaveChangesAsync();
        }

        db.Add(fora);
        await db.SaveChangesAsync();

        Assert.Empty(await EventosAsync(conexao, "TESTE_ITENS", dentro.Id));
        Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", fora.Id));
    }

    [Fact]
    public async Task Evento_grava_QUANDO_do_relogio_falso_e_nao_o_do_banco()
    {
        var (conexao, _, db) = await ComoPessoaAsync(RelogioEm(Agora));
        await using var _ = db;
        var item = new ItemDeTeste { Nome = "No relógio falso" };

        db.Add(item);
        await db.SaveChangesAsync();

        var evento = Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id));
        Assert.Equal(Agora, evento.Quando); // 2026-10-07T12:42:00Z, e não a hora da máquina
        Assert.Equal(DateTimeKind.Utc, evento.Quando.Kind);
    }

    [Fact]
    public async Task Interceptor_registrado_duas_vezes_nas_opcoes_nao_perde_dados_nem_deixa_transacao_aberta()
    {
        // O EF chama o MESMO interceptor duas vezes por SaveChanges se ele estiver duas vezes nas opções (foi o que o contêiner fez): a segunda chamada
        // não pode apagar o estado da primeira, e a transação própria tem de ser confirmada uma vez só.
        var (conexao, _) = await BancoAsync();
        var interceptor = new AuditoriaInterceptor(AtorDeTeste.Sistema(), RelogioEm(Agora), new ContextoDeAuditoria());
        var opcoes = new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(conexao, new MySqlServerVersion(new Version(8, 4, 0)))
            .AddInterceptors(interceptor, interceptor)
            .Options;
        await using var db = new ContextoAuditadoDeTeste(opcoes);
        var item = new ItemDeTeste { Nome = "Registrado duas vezes" };

        db.Add(item);
        await db.SaveChangesAsync();

        Assert.Null(db.Database.CurrentTransaction);
        Assert.Equal(1, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TESTE_ITENS WHERE ID = {item.Id}")); // visto por outra conexão
        Assert.Single(await EventosAsync(conexao, "TESTE_ITENS", item.Id)); // e um evento só, não dois
    }

    [Fact]
    public async Task Efeito_de_trigger_nao_aparece_no_interceptor()
    {
        // Mede o buraco que justifica o evento explícito: o trigger do grupo inativa o USUÁRIO junto com o funcionário, e isso o EF não vê.
        var (conexao, _, db) = await ComoPessoaAsync();
        await using var _ = db;
        var pessoa = await NovaPessoaAsync(conexao, "Pessoa do Trigger", "pessoa.do.trigger@empresa.com.br", "Funcionario");
        var funcionario = await db.Funcionarios.SingleAsync(f => f.Id == pessoa.FuncionarioId);

        funcionario.Inativar();
        await db.SaveChangesAsync();

        var inativou = Assert.Single(await EventosAsync(conexao, "FUNCIONARIOS", pessoa.FuncionarioId), e => e.Acao == AuditoriaAcao.Inativou);
        Assert.Equal("Inativação de Pessoa do Trigger", inativou.Descricao);
        Assert.Equal(0, await EscalarAsync(conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}")); // o trigger inativou o usuário...
        Assert.Empty(await EventosAsync(conexao, "USUARIOS", pessoa.UsuarioId)); // ...sem evento do usuário
    }
}

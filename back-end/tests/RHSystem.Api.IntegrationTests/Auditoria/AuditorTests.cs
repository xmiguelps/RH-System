using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Auditoria;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// O <c>IAuditor</c> no banco REAL. A regra única: se o fato de negócio persiste, o evento sai NA MESMA conexão e transação dele (<c>Registrar</c>,
/// <c>RegistrarComo</c>); se o evento tem de sobreviver à falha, sai DEPOIS, fora de qualquer transação, em contexto próprio (<c>RegistrarEGravar*</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditorTests(MySqlFixture mysql)
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

    private static Auditor NovoAuditor(string conexao, AppDbContext db, IAtorAtual ator, RelogioDeTeste relogio) =>
        new(db, new FabricaDeContextoDeTeste(conexao), ator, relogio, new ContextoDeAuditoria());

    private static EventoDeAuditoriaNovo Evento(string descricao, string? entidade = null, long? id = null, IReadOnlyList<MudancaDeAuditoriaNova>? mudancas = null) =>
        new(AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou, descricao, "Colaborador Teste", entidade, id, mudancas);

    private static async Task<long> ContarAsync(string conexao, string descricao) =>
        await EscalarAsync(conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = '{descricao}'");

    [Fact]
    public async Task Registrar_junta_o_evento_ao_SalvarAsync_da_mesma_transacao_e_some_com_o_rollback()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, PessoaComIp(ator), RelogioEm(Agora));

        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            auditor.Registrar(Evento("Registrar com rollback", "FUNCIONARIOS", ator.FuncionarioId, [new MudancaDeAuditoriaNova("Cargo", "Analista", "Analista pleno")]));
            Assert.Equal(0, await ContarAsync(conexao, "Registrar com rollback")); // ainda só no contexto: nada gravou antes do SalvarAsync
            await db.SaveChangesAsync();
            await transacao.RollbackAsync();
        }

        Assert.Equal(0, await ContarAsync(conexao, "Registrar com rollback")); // o fato desfez, o evento junto

        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            auditor.Registrar(Evento("Registrar com commit", "FUNCIONARIOS", ator.FuncionarioId, [new MudancaDeAuditoriaNova("Cargo", "Analista", "Analista pleno")]));
            await db.SaveChangesAsync();
            await transacao.CommitAsync();
        }

        var gravados = await EventosAsync(conexao, "FUNCIONARIOS", ator.FuncionarioId);
        var evento = Assert.Single(gravados, e => e.Descricao == "Registrar com commit");
        Assert.Equal((ator.FuncionarioId, Ip, Agora), (evento.FuncionarioAtorId, evento.Ip, evento.Quando));
        var mudanca = Assert.Single(evento.Mudancas);
        Assert.Equal((1, "Cargo", "Analista", "Analista pleno"), (mudanca.Ordem, mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois));
    }

    [Fact]
    public async Task RegistrarComo_em_requisicao_anonima_junta_o_evento_ao_SalvarAsync_da_mesma_transacao_com_o_ator_informado_e_some_com_o_rollback()
    {
        // A mesma conexão do fato: nenhuma segunda conexão (que esperaria a trava do InnoDB na linha da pessoa).
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var anonimo = new AtorDeTeste { Tipo = TipoDeAtorAtual.Anonimo, Nome = "Anônimo", Ip = Ip };
        var auditor = NovoAuditor(conexao, db, anonimo, RelogioEm(Agora));
        var informado = new AtorDoEvento(ator.FuncionarioId, "Ator Informado", Perfil.Funcionario);

        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            auditor.RegistrarComo(informado, Evento("RegistrarComo com rollback"));
            await db.SaveChangesAsync();
            await transacao.RollbackAsync();
        }

        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            auditor.RegistrarComo(informado, Evento("RegistrarComo com commit"));
            await db.SaveChangesAsync();
            await transacao.CommitAsync();
        }

        Assert.Equal(0, await ContarAsync(conexao, "RegistrarComo com rollback"));
        var gravado = await EscalarAsync(conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'RegistrarComo com commit' AND ATOR_NOME = 'Ator Informado' AND ATOR_PERFIL = 'Funcionario' AND ID_FUNCIONARIO_ATOR = {ator.FuncionarioId} AND IP = '{Ip}'");
        Assert.Equal(1, gravado);
    }

    [Fact]
    public async Task RegistrarEGravar_dentro_de_transacao_aberta_lanca_InvalidOperationException_nas_tres_variantes_e_fora_dela_grava()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, PessoaComIp(ator), RelogioEm(Agora));
        var informado = new AtorDoEvento(ator.FuncionarioId, "Ator Informado", null);

        await using (var transacao = await db.Database.BeginTransactionAsync())
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.RegistrarEGravarAsync(Evento("Dentro 1"), CancellationToken.None));
            await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.RegistrarEGravarComoAsync(informado, Evento("Dentro 2"), CancellationToken.None));
            var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.RegistrarEGravarComLimiteAsync(informado, Evento("Dentro 3"), TimeSpan.FromMinutes(1), CancellationToken.None));
            Assert.Contains("Dentro de transação use Registrar ou RegistrarComo", erro.Message);
            await transacao.RollbackAsync();
        }

        Assert.Equal(0, await ContarAsync(conexao, "Dentro 1") + await ContarAsync(conexao, "Dentro 2") + await ContarAsync(conexao, "Dentro 3"));

        await auditor.RegistrarEGravarAsync(Evento("Fora 1"), CancellationToken.None);
        await auditor.RegistrarEGravarComoAsync(informado, Evento("Fora 2"), CancellationToken.None);
        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(informado, Evento("Fora 3"), TimeSpan.FromMinutes(1), CancellationToken.None));
        Assert.Equal(3, await ContarAsync(conexao, "Fora 1") + await ContarAsync(conexao, "Fora 2") + await ContarAsync(conexao, "Fora 3"));
    }

    [Fact]
    public async Task RegistrarEGravar_grava_em_contexto_proprio_sem_levar_mudancas_pendentes_do_chamador()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, PessoaComIp(ator), RelogioEm(Agora));
        var pendente = CriarSetorPendente();
        db.Add(pendente);

        await auditor.RegistrarEGravarAsync(Evento("Contexto próprio"), CancellationToken.None);

        Assert.Equal(1, await ContarAsync(conexao, "Contexto próprio"));
        Assert.Equal(0, await EscalarAsync(conexao, "SELECT COUNT(*) FROM SETORES WHERE NOME_SETOR = 'Pendente do chamador'")); // o que estava pendente no chamador não foi junto
        Assert.Equal(EntityState.Added, db.Entry(pendente).State); // e continua pendente lá
    }

    [Fact]
    public async Task RegistrarEGravarComo_grava_com_o_ator_informado_e_o_ip_da_requisicao()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var anonimo = new AtorDeTeste { Tipo = TipoDeAtorAtual.Anonimo, Nome = "Anônimo", Ip = Ip }; // o login: a requisição ainda é anônima
        var auditor = NovoAuditor(conexao, db, anonimo, RelogioEm(Agora));

        await auditor.RegistrarEGravarComoAsync(new AtorDoEvento(ator.FuncionarioId, "Ator do Login", Perfil.RH), Evento("Login com ator informado"), CancellationToken.None);

        var gravado = await EscalarAsync(conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'Login com ator informado' AND ATOR_TIPO = 'PESSOA' AND ATOR_NOME = 'Ator do Login' AND ATOR_PERFIL = 'RH' AND CANAL = 'PORTAL' AND IP = '{Ip}'");
        Assert.Equal(1, gravado);
    }

    [Fact]
    public async Task RegistrarEGravarComLimite_grava_o_primeiro_nao_grava_o_repetido_dentro_da_janela_e_grava_de_novo_depois_dela()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var relogio = RelogioEm(Agora);
        var auditor = NovoAuditor(conexao, db, AtorDeTeste.Anonimo(), relogio);
        var informado = new AtorDoEvento(ator.FuncionarioId, "Quem pede", Perfil.RH);
        var outraPessoa = await NovaPessoaAsync(conexao, "Outra pessoa do limite", "outra.do.limite@empresa.com.br", "Funcionario");
        var janela = TimeSpan.FromMinutes(5);
        var evento = Evento("Pedido com limite");

        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(informado, evento, janela, CancellationToken.None));
        relogio.Avancar(TimeSpan.FromMinutes(2));
        Assert.False(await auditor.RegistrarEGravarComLimiteAsync(informado, evento, janela, CancellationToken.None)); // repetido dentro da janela
        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(new AtorDoEvento(outraPessoa.FuncionarioId, "Outra", Perfil.Funcionario), evento, janela, CancellationToken.None)); // outro ator
        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(informado, evento with { Acao = AuditoriaAcao.Criou }, janela, CancellationToken.None)); // outra ação
        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(informado, Evento("Outra descrição com limite"), janela, CancellationToken.None)); // outra descrição
        relogio.Avancar(TimeSpan.FromMinutes(4)); // 6 minutos depois do primeiro: fora da janela
        Assert.True(await auditor.RegistrarEGravarComLimiteAsync(informado, evento, janela, CancellationToken.None));

        Assert.Equal(2, await EscalarAsync(conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'Pedido com limite' AND ACAO = 'ALTEROU' AND ID_FUNCIONARIO_ATOR = {ator.FuncionarioId}"));
    }

    [Fact]
    public async Task Registrar_com_ator_Anonimo_e_sem_ator_informado_lanca_InvalidOperationException()
    {
        var (conexao, _) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, AtorDeTeste.Anonimo(), RelogioEm(Agora));

        Assert.Throws<InvalidOperationException>(() => auditor.Registrar(Evento("Anônimo sem ator")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => auditor.RegistrarEGravarAsync(Evento("Anônimo sem ator"), CancellationToken.None));
        Assert.Equal(0, await ContarAsync(conexao, "Anônimo sem ator"));
    }

    [Fact]
    public async Task Evento_com_descricao_vazia_viola_o_CHECK_e_lanca()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, PessoaComIp(ator), RelogioEm(Agora));

        await Assert.ThrowsAnyAsync<Exception>(() => auditor.RegistrarEGravarAsync(Evento("   "), CancellationToken.None));

        Assert.Equal(0, await EscalarAsync(conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE TRIM(DESCRICAO) = ''"));
    }

    [Fact]
    public async Task Evento_do_sistema_em_segundo_plano_sai_com_ator_SISTEMA_e_sem_ip()
    {
        var (conexao, _) = await BancoAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var auditor = NovoAuditor(conexao, db, AtorDeTeste.Sistema(), RelogioEm(Agora));

        await auditor.RegistrarEGravarAsync(Evento("Tarefa do sistema"), CancellationToken.None);

        var gravado = await EscalarAsync(conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'Tarefa do sistema' AND ATOR_TIPO = 'SISTEMA' AND CANAL = 'SISTEMA' AND ID_FUNCIONARIO_ATOR IS NULL AND IP IS NULL AND ATOR_PERFIL IS NULL");
        Assert.Equal(1, gravado);
    }

    private static Setor CriarSetorPendente()
    {
        // Setor tem construtor privado: uma instância "pendente" vem do próprio EF; aqui basta qualquer entidade nova que o contexto rastreie.
        var setor = (Setor)Activator.CreateInstance(typeof(Setor), nonPublic: true)!;
        typeof(Setor).GetProperty(nameof(Setor.Nome))!.SetValue(setor, "Pendente do chamador");
        return setor;
    }
}

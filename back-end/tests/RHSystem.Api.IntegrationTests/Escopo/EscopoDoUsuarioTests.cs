using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Escopo;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Escopo.ApiComElenco;

namespace RHSystem.Api.IntegrationTests.Escopo;

/// <summary>
/// A equipe do gestor lida do banco, no banco REAL, com o elenco de <c>contrato/ELENCO.md</c> (hoje = 2026-10-07T12:42Z). Os testes que precisam
/// mexer em gente criam um gestor e um setor só deles (nome com sufixo): o elenco compartilhado nunca muda.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EscopoDoUsuarioTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static EscopoDoUsuario Escopo(AppDbContext db, IAtorAtual ator, RelogioDeTeste relogio, ILogger<EscopoDoUsuario>? log = null) =>
        new(ator, db, relogio, log ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<EscopoDoUsuario>.Instance);

    private static RelogioDeTeste RelogioEm(DateTime utc)
    {
        var relogio = new RelogioDeTeste();
        relogio.Fixar(utc);
        return relogio;
    }

    /// <summary>Um gestor (nível de gestor, com linha em GESTORES) e um setor dele, tudo novo e coerente (os membros gravam o ID_GESTOR do gestor do setor).</summary>
    private static async Task<(PessoaDeTeste Gestor, int GestorId, int SetorId)> NovoGestorComSetorAsync(ApiFactory api, string sufixo)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario Gestor", Ct);
        var funcionario = Funcionario.Criar($"Gestor {sufixo}", $"gestor.{sufixo}@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync(Ct);
        var usuario = Usuario.Criar(funcionario, new string('x', 60));
        db.Usuarios.Add(usuario);
        await db.SaveChangesAsync(Ct);

        await ExecutarAsync(api.Conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionario.Id})");
        var gestorId = await EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionario.Id}");
        var setorId = await NovoSetorAsync(api, $"Setor {sufixo}", gestorId);
        await ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorId} WHERE ID_FUNCIONARIO = {funcionario.Id}");
        return (new PessoaDeTeste(funcionario.Id, usuario.Id, funcionario.Nome, funcionario.EmailCorporativo!, Perfil.Gestor), gestorId, setorId);
    }

    private static async Task<int> NovoSetorAsync(ApiFactory api, string nome, int? gestorId)
    {
        await ExecutarAsync(api.Conexao, $"INSERT INTO SETORES (NOME_SETOR, ID_GESTOR) VALUES ('{nome}', {(gestorId is null ? "NULL" : gestorId.ToString())})");
        return await EscalarAsync(api.Conexao, $"SELECT ID_SETOR FROM SETORES WHERE NOME_SETOR = '{nome}'");
    }

    private static async Task<int> NovoMembroAsync(ApiFactory api, int setorId, int gestorId, string nome, DateOnly admissao)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario", Ct);
        var funcionario = Funcionario.Criar(nome, $"{nome.Replace(' ', '.').ToLowerInvariant()}@empresa.com.br", admissao, nivel);
        db.Funcionarios.Add(funcionario);
        await db.SaveChangesAsync(Ct);
        await ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setorId}, ID_GESTOR = {gestorId} WHERE ID_FUNCIONARIO = {funcionario.Id}");
        return funcionario.Id;
    }

    private static async Task<List<int>> IdsDaEquipeAsync(AppDbContext db, EquipeDoGestor equipe) =>
        await db.Funcionarios.DaEquipe(equipe).OrderBy(f => f.Id).Select(f => f.Id).ToListAsync(Ct);

    [Fact]
    public async Task Gestor_com_setor_tem_equipe_com_setor_gestorId_e_o_proprio_funcionarioId()
    {
        var (api, elenco) = await ObterAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var gestorId = await EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {elenco.Gestor.FuncionarioId}");

        var equipe = await Escopo(db, AtorDeTeste.Pessoa(elenco.Gestor), RelogioEm(ElencoDeTeste.Hoje)).ObterEquipeAsync(Ct);

        Assert.Equal(new EquipeDoGestor(gestorId, elenco.SetorComercialId, "Comercial", elenco.Gestor.FuncionarioId, new DateOnly(2026, 10, 7)), equipe);
    }

    [Fact]
    public async Task Gestor_sem_setor_devolve_equipe_nula_e_Funcionario_e_Rh_nao_tem_equipe()
    {
        var (api, elenco) = await ObterAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var relogio = RelogioEm(ElencoDeTeste.Hoje);

        Assert.Null(await Escopo(db, AtorDeTeste.Pessoa(elenco.GestorSemSetor), relogio).ObterEquipeAsync(Ct));
        Assert.Null(await Escopo(db, AtorDeTeste.Pessoa(elenco.MembroA), relogio).ObterEquipeAsync(Ct));
        Assert.Null(await Escopo(db, AtorDeTeste.Pessoa(elenco.Rh), relogio).ObterEquipeAsync(Ct));
    }

    [Fact]
    public async Task DaEquipe_inclui_so_ativos_ja_admitidos_do_setor_sem_o_proprio_gestor()
    {
        var (api, elenco) = await ObterAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var equipe = (await Escopo(db, AtorDeTeste.Pessoa(elenco.Gestor), RelogioEm(ElencoDeTeste.Hoje)).ObterEquipeAsync(Ct))!;

        var ids = await IdsDaEquipeAsync(db, equipe);

        // Dentro: Diego e Júlia. Fora: Rafael (admissão em 13/10), Henrique (inativo), Bruno (outro setor) e o próprio Carlos.
        Assert.Equal(new[] { elenco.MembroA.FuncionarioId, elenco.MembroB.FuncionarioId }.Order(), ids);
    }

    [Fact]
    public async Task DaEquipe_na_virada_do_dia_usa_o_dia_de_Brasilia()
    {
        // Review Focus 1. Às 02:30Z de 08/10 ainda é 07/10 em -03:00: quem entra em 07/10 já está na equipe; quem entra em 08/10, ainda não.
        var (api, _) = await ObterAsync(mysql);
        var (gestor, gestorId, setorId) = await NovoGestorComSetorAsync(api, "virada");
        var jaEntrou = await NovoMembroAsync(api, setorId, gestorId, "Membro Antigo Virada", new DateOnly(2026, 1, 2));
        var entraEm07 = await NovoMembroAsync(api, setorId, gestorId, "Membro Entra Sete", new DateOnly(2026, 10, 7));
        var entraEm08 = await NovoMembroAsync(api, setorId, gestorId, "Membro Entra Oito", new DateOnly(2026, 10, 8));
        await using var db = ContextoDeTeste.Novo(api.Conexao);

        var as0230 = (await Escopo(db, AtorDeTeste.Pessoa(gestor), RelogioEm(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc))).ObterEquipeAsync(Ct))!;
        var as0300 = (await Escopo(db, AtorDeTeste.Pessoa(gestor), RelogioEm(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc))).ObterEquipeAsync(Ct))!;

        Assert.Equal(new DateOnly(2026, 10, 7), as0230.Hoje);
        Assert.Equal(new[] { jaEntrou, entraEm07 }.Order(), await IdsDaEquipeAsync(db, as0230));
        Assert.Equal(new DateOnly(2026, 10, 8), as0300.Hoje);
        Assert.Equal(new[] { jaEntrou, entraEm07, entraEm08 }.Order(), await IdsDaEquipeAsync(db, as0300));
    }

    [Fact]
    public async Task Gestor_que_mudou_de_setor_passa_a_ver_a_equipe_nova_sem_novo_login()
    {
        var (api, _) = await ObterAsync(mysql);
        var (gestor, gestorId, setorAntigo) = await NovoGestorComSetorAsync(api, "mudou");
        var doAntigo = await NovoMembroAsync(api, setorAntigo, gestorId, "Membro Setor Antigo", new DateOnly(2026, 1, 2));
        var setorNovo = await NovoSetorAsync(api, "Setor mudou novo", null);
        var doNovo = await NovoMembroAsync(api, setorNovo, gestorId, "Membro Setor Novo", new DateOnly(2026, 1, 2));
        var ator = AtorDeTeste.Pessoa(gestor); // o mesmo "token" (as mesmas claims) o tempo todo

        await using (var antes = ContextoDeTeste.Novo(api.Conexao))
        {
            var equipeAntes = (await Escopo(antes, ator, RelogioEm(ElencoDeTeste.Hoje)).ObterEquipeAsync(Ct))!;
            Assert.Equal(setorAntigo, equipeAntes.SetorId);
            Assert.Equal([doAntigo], await IdsDaEquipeAsync(antes, equipeAntes));
        }

        await ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = NULL WHERE ID_SETOR = {setorAntigo}");
        await ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = {gestorId} WHERE ID_SETOR = {setorNovo}");

        await using var depois = ContextoDeTeste.Novo(api.Conexao);
        var equipeDepois = (await Escopo(depois, ator, RelogioEm(ElencoDeTeste.Hoje)).ObterEquipeAsync(Ct))!;
        Assert.Equal(setorNovo, equipeDepois.SetorId);
        Assert.Equal([doNovo], await IdsDaEquipeAsync(depois, equipeDepois));
    }

    [Fact]
    public async Task Gestor_com_dois_setores_usa_o_de_menor_id_e_registra_aviso_no_log()
    {
        // O banco permite (SETORES.ID_GESTOR não é único); o contrato fala em um setor (a 1c impede o cadastro duplo).
        var (api, _) = await ObterAsync(mysql);
        var (gestor, gestorId, primeiroSetor) = await NovoGestorComSetorAsync(api, "dois");
        var segundoSetor = await NovoSetorAsync(api, "Setor dois segundo", gestorId);
        Assert.True(segundoSetor > primeiroSetor);
        var logs = new ColetorDeLogs();
        using var fabrica = LoggerFactory.Create(b => b.AddProvider(logs));
        await using var db = ContextoDeTeste.Novo(api.Conexao);

        var equipe = await Escopo(db, AtorDeTeste.Pessoa(gestor), RelogioEm(ElencoDeTeste.Hoje), fabrica.CreateLogger<EscopoDoUsuario>()).ObterEquipeAsync(Ct);

        Assert.Equal(primeiroSetor, equipe!.SetorId);
        lock (logs.Mensagens)
        {
            Assert.Contains(logs.Mensagens, m => m.StartsWith("[Warning]", StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task IEscopoDoUsuario_fora_de_requisicao_autenticada_lanca_InvalidOperationException()
    {
        var (api, elenco) = await ObterAsync(mysql);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var relogio = RelogioEm(ElencoDeTeste.Hoje);

        // Fora de requisição (um serviço em segundo plano), sem login e na API de verdade: não há pessoa para ter escopo.
        foreach (var ator in new IAtorAtual[] { AtorDeTeste.Sistema(), AtorDeTeste.Anonimo() })
        {
            var escopo = Escopo(db, ator, relogio);
            Assert.Throws<InvalidOperationException>(() => escopo.UsuarioId);
            Assert.Throws<InvalidOperationException>(() => escopo.FuncionarioId);
            Assert.Throws<InvalidOperationException>(() => escopo.Perfil);
            Assert.Throws<InvalidOperationException>(() => escopo.Nome);
            await Assert.ThrowsAsync<InvalidOperationException>(() => escopo.ObterEquipeAsync(Ct));
        }

        using var escopoDaApi = api.Services.CreateScope();
        Assert.Throws<InvalidOperationException>(() => escopoDaApi.ServiceProvider.GetRequiredService<IEscopoDoUsuario>().UsuarioId);

        // Controle positivo: a pessoa logada lê os quatro.
        var logada = Escopo(db, AtorDeTeste.Pessoa(elenco.Gestor), relogio);
        Assert.Equal((elenco.Gestor.UsuarioId, elenco.Gestor.FuncionarioId, Perfil.Gestor, "Carlos Ribeiro"), (logada.UsuarioId, logada.FuncionarioId, logada.Perfil, logada.Nome));
    }

    [Fact]
    public async Task Elenco_semeado_grava_o_ID_GESTOR_do_gestor_do_setor_nos_membros_e_NULL_no_gestor_e_em_quem_nao_tem_setor()
    {
        // SELECT cru: a divergência que o validacao_consistencia.sql acusa (a ficha da 1b tira o `gestor` de FUNCIONARIOS.ID_GESTOR).
        var (api, elenco) = await ObterAsync(mysql);

        var divergentes = await EscalarAsync(
            api.Conexao,
            """
            SELECT COUNT(*) FROM FUNCIONARIOS F
            JOIN SETORES S ON S.ID_SETOR = F.ID_SETOR AND S.ID_GESTOR IS NOT NULL
            JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
            WHERE G.ID_FUNCIONARIO <> F.ID_FUNCIONARIO AND NOT (F.ID_GESTOR <=> S.ID_GESTOR)
            """);
        var gestorCarlos = await EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {elenco.Gestor.FuncionarioId}");
        var membrosComOGestorDeCarlos = await EscalarAsync(
            api.Conexao,
            $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_GESTOR = {gestorCarlos} AND ID_FUNCIONARIO IN ({elenco.MembroA.FuncionarioId}, {elenco.MembroB.FuncionarioId}, {elenco.EmAdmissao.FuncionarioId}, {elenco.Inativo.FuncionarioId})");
        var comGestorIndevido = await EscalarAsync(
            api.Conexao,
            $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_GESTOR IS NOT NULL AND ID_FUNCIONARIO IN ({elenco.Gestor.FuncionarioId}, {elenco.GestorDoFinanceiro.FuncionarioId}, {elenco.GestorSemSetor.FuncionarioId}, {elenco.Rh.FuncionarioId})");

        Assert.Equal(0, divergentes);
        Assert.Equal(4, membrosComOGestorDeCarlos); // controle positivo: a coluna não ficou toda NULL
        Assert.Equal(0, comGestorIndevido);
    }
}

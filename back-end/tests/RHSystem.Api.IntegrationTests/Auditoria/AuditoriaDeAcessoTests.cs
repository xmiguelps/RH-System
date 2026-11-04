using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Auditoria;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// Os eventos de acesso pela API de verdade (Task 8.2): login, recusa de login e 403. Uma API e um banco para a classe; cada teste cria as pessoas
/// dele (e-mails únicos), porque o limite de "um evento por pessoa por minuto" faria testes da mesma pessoa se atrapalharem.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaDeAcessoTests(MySqlFixture mysql)
{
    private const string SenhaErrada = "senha-errada-9f3kQ";
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static (ApiFactory Api, ElencoDeTeste Elenco)? _pronta;

    private async Task<(ApiFactory Api, ElencoDeTeste Elenco)> ApiAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_pronta is null)
            {
                var api = await ApiFactory.CriarAsync(mysql);
                _pronta = (api, await api.SemearElencoAsync());
            }

            return _pronta.Value;
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task<(string Email, int FuncionarioId)> PessoaQueEntraAsync(ApiFactory api, string prefixo, string? nivel = "Funcionario")
    {
        var email = $"{prefixo}.{Guid.NewGuid():N}@empresa.com.br";
        await api.CriarUsuarioAsync(email, nivel: nivel);
        return (email, (int)await EscalarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'"));
    }

    private static Task<HttpResponseMessage> EntrarAsync(ApiFactory api, string email, string senha) =>
        api.CreateClient().PostAsJsonAsync("/api/auth/entrar", new { email, senha });

    private static async Task<string> TextoAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return (string)(await comando.ExecuteScalarAsync())!;
    }

    [Fact]
    public async Task Login_bem_sucedido_grava_Acesso_Entrou_com_o_ator_e_o_ip()
    {
        var (api, _) = await ApiAsync();
        var pessoa = await PessoaQueEntraAsync(api, "entrou");

        var resposta = await EntrarAsync(api, pessoa.Email, ApiFactory.SenhaPadrao);

        resposta.EnsureSuccessStatusCode();
        var evento = Assert.Single(await EventosDoAtorAsync(api.Conexao, pessoa.FuncionarioId));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Entrou, "Entrou no portal", "Colaborador Pessoa de Teste"), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado));
        Assert.Equal((TipoDeAtor.Pessoa, "Pessoa de Teste", Perfil.Funcionario, CanalDeOrigem.Portal, "127.0.0.1"), (evento.AtorTipo, evento.AtorNome, evento.AtorPerfil, evento.Canal, evento.Ip));
        Assert.Null(evento.Entidade);
        Assert.Equal(api.Relogio.AgoraUtc, evento.Quando); // o relógio falso da API, não o do banco
    }

    [Fact]
    public async Task Senha_certa_com_acesso_desativado_ou_sem_perfil_grava_Acesso_AcessoNegado()
    {
        var (api, _) = await ApiAsync();
        var bloqueada = await PessoaQueEntraAsync(api, "bloqueada");
        await ExecutarAsync(api.Conexao, $"UPDATE USUARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {bloqueada.FuncionarioId}");
        var semPerfil = await PessoaQueEntraAsync(api, "semperfil", nivel: null);

        var r1 = await EntrarAsync(api, bloqueada.Email, ApiFactory.SenhaPadrao);
        var r2 = await EntrarAsync(api, semPerfil.Email, ApiFactory.SenhaPadrao);

        Assert.False(r1.IsSuccessStatusCode);
        Assert.False(r2.IsSuccessStatusCode);
        var e1 = Assert.Single(await EventosDoAtorAsync(api.Conexao, bloqueada.FuncionarioId));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login com usuário bloqueado", Perfil.Funcionario), (e1.Area, e1.Acao, e1.Descricao, e1.AtorPerfil));
        var e2 = Assert.Single(await EventosDoAtorAsync(api.Conexao, semPerfil.FuncionarioId));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login sem perfil de acesso configurado", (Perfil?)null), (e2.Area, e2.Acao, e2.Descricao, e2.AtorPerfil));
    }

    [Fact]
    public async Task Senha_errada_de_usuario_que_existe_grava_AcessoNegado_com_ip_sem_a_senha_e_so_um_evento_por_minuto_por_pessoa()
    {
        // Review Focus 2: três erros no mesmo minuto (relógio falso) = 1 evento; passados 61 s, o seguinte grava o segundo; outra pessoa no mesmo
        // minuto grava o dela.
        var (api, _) = await ApiAsync();
        api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var p1 = await PessoaQueEntraAsync(api, "errou.um");
        var p2 = await PessoaQueEntraAsync(api, "errou.dois");

        for (var i = 0; i < 3; i++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await EntrarAsync(api, p1.Email, SenhaErrada)).StatusCode);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await EntrarAsync(api, p2.Email, SenhaErrada)).StatusCode);

        var doP1 = await EventosDoAtorAsync(api.Conexao, p1.FuncionarioId);
        var doP2 = await EventosDoAtorAsync(api.Conexao, p2.FuncionarioId);
        var primeiro = Assert.Single(doP1);
        Assert.Single(doP2);
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login com senha incorreta", "Colaborador Pessoa de Teste"), (primeiro.Area, primeiro.Acao, primeiro.Descricao, primeiro.RegistroAfetado));
        Assert.Equal((TipoDeAtor.Pessoa, "127.0.0.1"), (primeiro.AtorTipo, primeiro.Ip));

        api.Relogio.Avancar(TimeSpan.FromSeconds(61));
        Assert.Equal(HttpStatusCode.Unauthorized, (await EntrarAsync(api, p1.Email, SenhaErrada)).StatusCode);

        Assert.Equal(2, (await EventosDoAtorAsync(api.Conexao, p1.FuncionarioId)).Count);
        Assert.Single(await EventosDoAtorAsync(api.Conexao, p2.FuncionarioId));
        Assert.DoesNotContain(SenhaErrada, (await TudoDoLogAsync(api.Conexao)).Texto);
    }

    [Fact]
    public async Task Email_inexistente_nao_grava_evento_nem_deixa_o_email_digitado_em_lugar_nenhum_do_log()
    {
        var (api, _) = await ApiAsync();
        var digitado = $"digitado.errado.{Guid.NewGuid():N}@empresa.com.br";
        // Controle positivo do próprio teste (não depende da ordem dos outros): um login de verdade deixa um evento, e a varredura o enxerga.
        var pessoa = await PessoaQueEntraAsync(api, "controle.positivo");
        (await EntrarAsync(api, pessoa.Email, ApiFactory.SenhaPadrao)).EnsureSuccessStatusCode();
        var antes = await TudoDoLogAsync(api.Conexao);
        Assert.Contains("Entrou no portal", antes.Texto);

        var resposta = await EntrarAsync(api, digitado, "qualquer-senha-123");

        Assert.Equal(HttpStatusCode.Unauthorized, resposta.StatusCode);
        var depois = await TudoDoLogAsync(api.Conexao);
        Assert.Equal(antes.Total, depois.Total);
        Assert.DoesNotContain(digitado, depois.Texto);
    }

    [Fact]
    public async Task Perfil_errado_em_rota_gera_AcessoNegado_com_metodo_e_rota_do_contrato_e_404_fora_do_escopo_nao_gera()
    {
        var (api, elenco) = await ApiAsync();
        var funcionario = api.ClienteComo(elenco.MembroA);

        var r1 = await funcionario.GetAsync("/api/rh/__teste/ping");
        var r2 = await funcionario.GetAsync("/api/gestor/__teste/equipe/987654");

        Assert.Equal(HttpStatusCode.Forbidden, r1.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, r2.StatusCode);
        var eventos = await EventosDoAtorAsync(api.Conexao, elenco.MembroA.FuncionarioId);
        Assert.Equal(
            ["Acesso negado: GET /api/rh/__teste/ping", "Acesso negado: GET /api/gestor/__teste/equipe/{id}"], // a rota com {parametro}, sem o valor nem a restrição ":int"
            eventos.Select(e => e.Descricao));
        Assert.All(eventos, e =>
        {
            Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, TipoDeAtor.Pessoa), (e.Area, e.Acao, e.AtorTipo));
            Assert.Equal((elenco.MembroA.Nome, Perfil.Funcionario), (e.AtorNome, e.AtorPerfil));
            Assert.Equal("127.0.0.1", e.Ip);
        });
        Assert.DoesNotContain("987654", (await TudoDoLogAsync(api.Conexao)).Texto);

        // 404 por estar fora do escopo não é acesso negado: a pessoa tem o perfil; o registro é que "não existe" para ela.
        var gestor = api.ClienteComo(elenco.Gestor);
        var doGestorAntes = (await EventosDoAtorAsync(api.Conexao, elenco.Gestor.FuncionarioId)).Count;
        var fora = await gestor.GetAsync($"/api/gestor/__teste/equipe/{elenco.DeOutroSetor.FuncionarioId}");
        Assert.Equal(HttpStatusCode.NotFound, fora.StatusCode);
        Assert.Equal(doGestorAntes, (await EventosDoAtorAsync(api.Conexao, elenco.Gestor.FuncionarioId)).Count);

        // E o 401 de quem nem entrou não tem ator: não grava.
        var totalAntes = (await TudoDoLogAsync(api.Conexao)).Total;
        Assert.Equal(HttpStatusCode.Unauthorized, (await api.ClienteAnonimo().GetAsync("/api/rh/__teste/ping")).StatusCode);
        Assert.Equal(totalAntes, (await TudoDoLogAsync(api.Conexao)).Total);
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_de_acesso_nao_muda_o_403_nem_o_login()
    {
        // Decisão desta tarefa: o evento de acesso é registro, não condição. Com a auditoria fora do ar, quem não tem perfil continua levando 403 (e não
        // 500) e quem acerta a senha continua entrando.
        var (api, elenco) = await ApiAsync();
        using var fabrica = api.WithWebHostBuilder(b => b.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAuditor>();
            servicos.AddScoped<IAuditor, AuditorQueFalha>();
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = new("Bearer", elenco.TokensPorUsuario[elenco.MembroB.UsuarioId]);
        var pessoa = await PessoaQueEntraAsync(api, "auditoria.fora.do.ar");

        var negado = await cliente.GetAsync("/api/rh/__teste/ping");
        var login = await fabrica.CreateClient().PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = ApiFactory.SenhaPadrao });

        Assert.Equal(HttpStatusCode.Forbidden, negado.StatusCode);
        login.EnsureSuccessStatusCode();
        Assert.Empty(await EventosDoAtorAsync(api.Conexao, pessoa.FuncionarioId)); // a auditoria de mentira não gravou nada: o login valeu sem evento
    }

    private sealed class AuditorQueFalha : IAuditor
    {
        private static InvalidOperationException Fora() => new("auditoria fora do ar (falha de teste)");

        public void Registrar(EventoDeAuditoriaNovo evento) => throw Fora();

        public void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento) => throw Fora();

        public Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct) => throw Fora();

        public Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct) => throw Fora();

        public Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct) => throw Fora();

        public IDisposable SuspenderRegistroAutomatico() => throw Fora();
    }

    [Fact]
    public async Task Evento_de_login_nunca_contem_senha_hash_nem_token()
    {
        var (api, _) = await ApiAsync();
        var pessoa = await PessoaQueEntraAsync(api, "segredos");
        var hash = await TextoAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        var senhaErrada = "senha-errada-77xqLmN";

        var ok = await EntrarAsync(api, pessoa.Email, ApiFactory.SenhaPadrao);
        ok.EnsureSuccessStatusCode();
        var corpo = await ok.Content.ReadFromJsonAsync<JsonElement>();
        var acesso = corpo.GetProperty("tokenDeAcesso").GetString()!;
        var refresh = corpo.GetProperty("refreshToken").GetString()!;
        Assert.Equal(HttpStatusCode.Unauthorized, (await EntrarAsync(api, pessoa.Email, senhaErrada)).StatusCode);

        var eventos = await EventosDoAtorAsync(api.Conexao, pessoa.FuncionarioId);
        Assert.Equal([AuditoriaAcao.Entrou, AuditoriaAcao.AcessoNegado], eventos.Select(e => e.Acao)); // a varredura não é vazia: os dois eventos existem
        var texto = (await TudoDoLogAsync(api.Conexao)).Texto;
        foreach (var proibido in new[] { ApiFactory.SenhaPadrao, senhaErrada, hash, acesso, refresh })
        {
            Assert.DoesNotContain(proibido, texto);
        }

        Assert.All(eventos, e => Assert.Empty(e.Mudancas));
    }
}

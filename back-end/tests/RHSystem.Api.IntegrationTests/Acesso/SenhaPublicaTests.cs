using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Auditoria;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// As três rotas públicas de senha (<c>esqueci</c>, <c>validar-link</c>, <c>definir</c>), no banco REAL com o relógio falso em 2026-10-07T12:42:00Z. Os links vêm do serviço de
/// convite e o token é lido da carga da fila (o único lugar em que existe em claro).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SenhaPublicaTests(MySqlFixture mysql)
{
    private const string NovaSenha = "NovaSenha#2026";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(SenhaPublicaTests));

    private static async Task<(int Status, string Title, string Detail, string Codigo)> ProblemaDeLinkAsync(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        return ((int)resposta.StatusCode, corpo.GetProperty("title").GetString()!, corpo.GetProperty("detail").GetString()!, corpo.GetProperty("codigo").GetString()!);
    }

    private static async Task<(int Status, string Title, string Detail, string Codigo)> ValidarAsync(HttpClient cliente, string token) =>
        await ProblemaDeLinkAsync(await PostAsync(cliente, "/api/auth/senha/validar-link", new { token }));

    // ---------------------------------------------------------------- esqueci

    [Fact]
    public async Task Esqueci_devolve_o_mesmo_202_vazio_para_email_existente_inexistente_inativo_e_sem_login()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();
        var existente = await api.NovaPessoaAsync("esqueci-existe", login: TipoDeLogin.SenhaDefinida);
        var inativa = await api.NovaPessoaAsync("esqueci-inativa", ativa: false, login: TipoDeLogin.SenhaDefinida);
        var semLogin = await api.NovaPessoaAsync("esqueci-sem-login");

        var respostas = new List<HttpResponseMessage>();
        foreach (var email in new[] { existente.Email!, "ninguem.existe@empresa.com.br", inativa.Email!, semLogin.Email! })
        {
            respostas.Add(await PostAsync(cliente, "/api/auth/senha/esqueci", new { email }));
        }

        Assert.All(respostas, r => Assert.Equal(202, (int)r.StatusCode));
        var corpos = new List<string>();
        foreach (var resposta in respostas)
        {
            corpos.Add(await resposta.Content.ReadAsStringAsync());
        }

        Assert.All(corpos, c => Assert.Equal(string.Empty, c)); // corpo igual: vazio
        Assert.Single(respostas.Select(r => r.Content.Headers.ContentType?.ToString()).Distinct());
        Assert.Single(respostas.Select(r => r.Content.Headers.ContentLength).Distinct());
        Assert.Single(respostas.Select(r => string.Join(",", r.Headers.Select(h => h.Key).Order())).Distinct()); // os mesmos cabeçalhos (nenhum a mais para o e-mail que existe)
    }

    [Fact]
    public async Task Esqueci_de_inexistente_nao_enfileira_nada_e_o_de_existente_enfileira_um_REDEFINICAO_SENHA()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();
        var existente = await api.NovaPessoaAsync("esqueci-fila", login: TipoDeLogin.SenhaDefinida);
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        await PostAsync(cliente, "/api/auth/senha/esqueci", new { email = "ninguem.mesmo@empresa.com.br" });
        Assert.Equal(antes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));

        await PostAsync(cliente, "/api/auth/senha/esqueci", new { email = existente.Email });
        Assert.Equal(antes + 1, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'REDEFINICAO_SENHA' AND ID_FUNCIONARIO = {existente.FuncionarioId}"));
    }

    [Fact]
    public async Task Esqueci_com_a_linha_da_pessoa_travada_pela_propria_transacao_responde_202_em_poucos_segundos_e_o_evento_sai_na_mesma_transacao()
    {
        // A transação do esqueci segura FOR UPDATE na linha da pessoa enquanto grava o evento (RegistrarComo, mesma conexão). Com o evento por segunda conexão a chamada esperaria o
        // limite do InnoDB (50 s) e viraria 500: aqui ela responde 202 em poucos segundos e o evento existe, com a pessoa como ator.
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("esqueci-trava", login: TipoDeLogin.SenhaDefinida);
        var relogio = System.Diagnostics.Stopwatch.StartNew();

        var resposta = await PostAsync(api.Api.ClienteAnonimo(), "/api/auth/senha/esqueci", new { email = pessoa.Email });

        relogio.Stop();
        Assert.Equal(202, (int)resposta.StatusCode);
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(10), $"levou {relogio.Elapsed.TotalSeconds:N1} s");
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", pessoa.UsuarioId!.Value));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Alterou, pessoa.FuncionarioId, TipoDeAtor.Pessoa), (evento.Area, evento.Acao, evento.FuncionarioAtorId, evento.AtorTipo));
    }

    [Fact]
    public async Task Esqueci_simultaneo_da_mesma_pessoa_responde_202_em_todas_as_chamadas_em_poucos_segundos_grava_um_so_token_e_nunca_500()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("esqueci-simultaneo", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var largada = new TaskCompletionSource();
        var relogio = System.Diagnostics.Stopwatch.StartNew();
        var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await largada.Task;
            return (int)(await PostAsync(cliente, "/api/auth/senha/esqueci", new { email = pessoa.Email })).StatusCode;
        })).ToList();

        largada.SetResult();
        var status = await Task.WhenAll(tarefas);

        relogio.Stop();
        Assert.All(status, s => Assert.Equal(202, s));
        Assert.True(relogio.Elapsed < TimeSpan.FromSeconds(10), $"levou {relogio.Elapsed.TotalSeconds:N1} s");
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = {pessoa.UsuarioId} AND EM_ABERTO = 1"));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = {pessoa.UsuarioId}"));
        Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", pessoa.UsuarioId!.Value));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("nao-e-um-email")]
    [InlineData("duas@@arrobas.com")]
    public async Task Esqueci_com_email_vazio_ou_sem_formato_devolve_400_errors_email(string email)
    {
        var api = await ApiAsync();

        var resposta = await PostAsync(api.Api.ClienteAnonimo(), "/api/auth/senha/esqueci", new { email });

        Assert.Equal(400, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal("Dados inválidos.", corpo.GetProperty("title").GetString());
        Assert.Equal("Confira os campos indicados.", corpo.GetProperty("detail").GetString());
        Assert.True(corpo.GetProperty("errors").TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Esqueci_sem_corpo_devolve_400()
    {
        var api = await ApiAsync();

        var resposta = await api.Api.ClienteAnonimo().PostAsync("/api/auth/senha/esqueci", new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)resposta.StatusCode);
    }

    // ---------------------------------------------------------------- validar-link

    [Fact]
    public async Task Validar_devolve_tipo_nome_email_e_expiracao_e_nao_gasta_o_link()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("validar");
        var token = await NovoConviteAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();

        for (var vez = 0; vez < 2; vez++) // chamar duas vezes: 200 e 200
        {
            var resposta = await PostAsync(cliente, "/api/auth/senha/validar-link", new { token });
            Assert.Equal(200, (int)resposta.StatusCode);
            var corpo = await CorpoAsync(resposta);
            Assert.Equal("CONVITE", corpo.GetProperty("tipo").GetString());
            Assert.Equal(pessoa.Nome, corpo.GetProperty("nome").GetString());
            Assert.Equal(pessoa.Email, corpo.GetProperty("emailCorporativo").GetString());
            Assert.Equal(ElencoDeTeste.Hoje.AddDays(7), corpo.GetProperty("expiraEmUtc").GetDateTime().ToUniversalTime());
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = (SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}) AND EM_ABERTO = 1"));
    }

    [Fact]
    public async Task Validar_de_redefinicao_devolve_tipo_REDEFINICAO()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("validar-redef", login: TipoDeLogin.SenhaDefinida);
        var token = await NovaRedefinicaoAsync(api, pessoa);

        var corpo = await CorpoAsync(await PostAsync(api.Api.ClienteAnonimo(), "/api/auth/senha/validar-link", new { token }));

        Assert.Equal("REDEFINICAO", corpo.GetProperty("tipo").GetString());
        Assert.Equal(ElencoDeTeste.Hoje.AddHours(1), corpo.GetProperty("expiraEmUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Validar_link_inexistente_usado_invalidado_vencido_e_de_login_ou_colaborador_inativo_devolvem_o_mesmo_422_com_title_detail_e_codigo_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();
        var esperado = (422, "Link inválido ou vencido.", "Este link não vale mais. Peça um novo ao RH ou use \"Esqueci minha senha\".", CodigoDeLinkInvalido);

        var inexistente = await ValidarAsync(cliente, new string('a', 64));

        var usado = await api.NovaPessoaAsync("link-usado");
        var tokenUsado = await NovoConviteAsync(api, usado);
        Assert.Equal(204, (int)(await PostAsync(cliente, "/api/auth/senha/definir", new { token = tokenUsado, novaSenha = NovaSenha })).StatusCode);

        var invalidado = await api.NovaPessoaAsync("link-invalidado");
        var tokenInvalidado = await NovoConviteAsync(api, invalidado);
        await NovoConviteAsync(api, invalidado); // o segundo convite invalida o primeiro

        var vencido = await api.NovaPessoaAsync("link-vencido");
        var tokenVencido = await NovoConviteAsync(api, vencido);

        var loginInativo = await api.NovaPessoaAsync("link-login-inativo");
        var tokenLoginInativo = await NovoConviteAsync(api, loginInativo);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE USUARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {loginInativo.FuncionarioId}");

        var colaboradorInativo = await api.NovaPessoaAsync("link-colaborador-inativo");
        var tokenColaboradorInativo = await NovoConviteAsync(api, colaboradorInativo);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {colaboradorInativo.FuncionarioId}");

        try
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(8));
            var passouDoPrazo = await ValidarAsync(cliente, tokenVencido);
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);

            Assert.Equal(esperado, inexistente);
            Assert.Equal(esperado, await ValidarAsync(cliente, tokenUsado));
            Assert.Equal(esperado, await ValidarAsync(cliente, tokenInvalidado));
            Assert.Equal(esperado, passouDoPrazo);
            Assert.Equal(esperado, await ValidarAsync(cliente, tokenLoginInativo));
            Assert.Equal(esperado, await ValidarAsync(cliente, tokenColaboradorInativo));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Link_de_convite_vence_em_7_dias_e_o_de_redefinicao_em_1_hora_e_no_instante_exato_ja_e_invalido()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();
        var comConvite = await api.NovaPessoaAsync("prazo-convite");
        var tokenConvite = await NovoConviteAsync(api, comConvite);
        var comRedefinicao = await api.NovaPessoaAsync("prazo-redef", login: TipoDeLogin.SenhaDefinida);
        var tokenRedefinicao = await NovaRedefinicaoAsync(api, comRedefinicao);

        try
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(7).AddSeconds(-1));
            Assert.Equal(200, (int)(await PostAsync(cliente, "/api/auth/senha/validar-link", new { token = tokenConvite })).StatusCode);
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(7)); // no instante exato: inválido
            Assert.Equal(422, (await ValidarAsync(cliente, tokenConvite)).Item1);

            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(1).AddSeconds(-1));
            Assert.Equal(200, (int)(await PostAsync(cliente, "/api/auth/senha/validar-link", new { token = tokenRedefinicao })).StatusCode);
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(1));
            Assert.Equal(422, (await ValidarAsync(cliente, tokenRedefinicao)).Item1);
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    // ---------------------------------------------------------------- definir

    [Fact]
    public async Task Definir_com_convite_grava_hash_de_verdade_zera_SenhaProvisoria_e_entrar_funciona_com_a_senha_nova()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-convite");
        var token = await NovoConviteAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();

        var resposta = await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = NovaSenha });

        Assert.Equal(204, (int)resposta.StatusCode); // sem login automático
        Assert.Equal(string.Empty, await resposta.Content.ReadAsStringAsync());
        var hash = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"))!.ToString()!;
        Assert.True(new SenhaHasher().Verificar(hash, NovaSenha)); // hash de verdade, não o provisório
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT SENHA_PROVISORIA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        var (acesso, refresh) = await EntrarAsync(cliente, pessoa.Email!, NovaSenha);
        Assert.False(string.IsNullOrEmpty(acesso));
        Assert.False(string.IsNullOrEmpty(refresh));
    }

    [Fact]
    public async Task Definir_usa_o_link_uma_so_vez_e_o_segundo_uso_devolve_422()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-uma-vez");
        var token = await NovoConviteAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();

        var primeira = await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = NovaSenha });
        var segunda = await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = "Outra#Senha2026" });

        Assert.Equal(204, (int)primeira.StatusCode);
        Assert.Equal(422, (int)segunda.StatusCode);
        var corpoDaSegunda = await CorpoAsync(segunda);
        Assert.Equal("Link inválido ou vencido.", corpoDaSegunda.GetProperty("title").GetString());
        Assert.Equal(CodigoDeLinkInvalido, corpoDaSegunda.GetProperty("codigo").GetString());
        await EntrarAsync(cliente, pessoa.Email!, NovaSenha); // a senha continua a primeira
    }

    [Fact]
    public async Task Dois_usos_simultaneos_do_mesmo_link_dao_um_204_e_um_422_e_nenhum_500()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-simultaneo");
        var token = await NovoConviteAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();
        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            await largada.Task;
            return (int)(await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = $"Senha#Simultanea{i:D2}" })).StatusCode;
        })).ToList();

        largada.SetResult();
        var status = await Task.WhenAll(tarefas);

        Assert.Equal(1, status.Count(s => s == 204)); // só um consome o link
        Assert.Equal(19, status.Count(s => s == 422));
        Assert.DoesNotContain(500, status);
    }

    [Fact]
    public async Task Dois_usos_simultaneos_do_mesmo_link_de_redefinicao_dao_um_204_e_os_outros_422_e_a_senha_e_a_do_vencedor()
    {
        // No link de REDEFINIÇÃO só o UPDATE atômico do link protege o uso único (o convite tem ainda a conferência de SenhaProvisoria dentro da transação).
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-redef-simultaneo", login: TipoDeLogin.SenhaDefinida);
        var token = await NovaRedefinicaoAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();
        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, 20).Select(i => Task.Run(async () =>
        {
            await largada.Task;
            return (Indice: i, Status: (int)(await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = $"Senha#Redefinida{i:D2}" })).StatusCode);
        })).ToList();

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas);

        Assert.Equal(1, resultados.Count(r => r.Status == 204));
        Assert.Equal(19, resultados.Count(r => r.Status == 422));
        var vencedor = resultados.Single(r => r.Status == 204).Indice;
        await EntrarAsync(cliente, pessoa.Email!, $"Senha#Redefinida{vencedor:D2}"); // a senha gravada é a de quem consumiu o link
    }

    [Fact]
    public async Task Definir_por_redefinicao_revoga_todos_os_refresh_tokens_da_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-redef-sessoes", login: TipoDeLogin.SenhaDefinida);
        var cliente = api.Api.ClienteAnonimo();
        var (_, primeiro) = await EntrarAsync(cliente, pessoa.Email!);
        var (_, segundo) = await EntrarAsync(cliente, pessoa.Email!);
        var token = await NovaRedefinicaoAsync(api, pessoa);

        Assert.Equal(204, (int)(await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = NovaSenha })).StatusCode);

        Assert.Equal(401, await RenovarAsync(cliente, primeiro));
        Assert.Equal(401, await RenovarAsync(cliente, segundo));
        await EntrarAsync(cliente, pessoa.Email!, NovaSenha); // e a senha nova entra
    }

    [Fact]
    public async Task Definir_invalida_os_outros_links_em_aberto_da_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-outros-links");
        var convite = await NovoConviteAsync(api, pessoa);
        // Uma redefinição em aberto ao mesmo tempo (a pessoa tem login provisório, mas o serviço aceita o pedido: o link é de outro tipo).
        var redefinicao = await NovaRedefinicaoAsync(api, pessoa);
        var cliente = api.Api.ClienteAnonimo();

        Assert.Equal(204, (int)(await PostAsync(cliente, "/api/auth/senha/definir", new { token = convite, novaSenha = NovaSenha })).StatusCode);

        Assert.Equal(422, (await ValidarAsync(cliente, redefinicao)).Item1); // o outro link deixou de valer
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = (SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}) AND EM_ABERTO = 1"));
    }

    [Fact]
    public async Task Definir_de_convite_para_quem_ja_tinha_senha_definida_e_invalido()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-ja-tem-senha");
        var token = await NovoConviteAsync(api, pessoa);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE USUARIOS SET SENHA_PROVISORIA = FALSE WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"); // a senha foi definida por outro caminho

        var cliente = api.Api.ClienteAnonimo();

        var resposta = await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = NovaSenha });

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(CodigoDeLinkInvalido, (await CorpoAsync(resposta)).GetProperty("codigo").GetString());
        // Também na conferência (validar-link): o convite de quem já tem senha não vale, e a rota que só confere diz o mesmo. (A conferência de dentro da transação do definir sozinha já recusaria o definir.)
        Assert.Equal(422, (await ValidarAsync(cliente, token)).Item1);
    }

    [Fact]
    public async Task Definir_com_campos_vazios_ou_senha_fora_de_8_a_128_devolve_400_com_errors_por_campo()
    {
        var api = await ApiAsync();
        var cliente = api.Api.ClienteAnonimo();
        var token = new string('a', 64);

        var semNada = await CorpoAsync(await PostAsync(cliente, "/api/auth/senha/definir", new { token = "", novaSenha = "" }));
        var curta = await CorpoAsync(await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = "1234567" }));
        var longa = await CorpoAsync(await PostAsync(cliente, "/api/auth/senha/definir", new { token, novaSenha = new string('x', 129) }));

        Assert.Equal("Dados inválidos.", semNada.GetProperty("title").GetString());
        Assert.True(semNada.GetProperty("errors").TryGetProperty("token", out _));
        Assert.True(semNada.GetProperty("errors").TryGetProperty("novaSenha", out _));
        Assert.True(curta.GetProperty("errors").TryGetProperty("novaSenha", out _));
        Assert.True(longa.GetProperty("errors").TryGetProperty("novaSenha", out _));
        Assert.False(curta.GetProperty("errors").TryGetProperty("token", out _));
    }

    [Fact]
    public async Task Definir_grava_Acesso_Alterou_com_a_pessoa_como_ator_e_nem_senha_nem_hash_nem_token_aparecem_no_banco_fora_de_USUARIOS_SENHA()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-auditoria");
        var token = await NovoConviteAsync(api, pessoa);
        const string senha = "SenhaSecreta#Unica-8472";

        Assert.Equal(204, (int)(await PostAsync(api.Api.ClienteAnonimo(), "/api/auth/senha/definir", new { token, novaSenha = senha })).StatusCode);

        var usuarioId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", usuarioId);
        var definiu = Assert.Single(eventos, e => e.Descricao == "Senha definida (convite)");
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Alterou, pessoa.FuncionarioId, TipoDeAtor.Pessoa), (definiu.Area, definiu.Acao, definiu.FuncionarioAtorId, definiu.AtorTipo));
        var hash = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {usuarioId}"))!.ToString()!;
        foreach (var tabela in new[] { "USUARIOS_TOKENS_SENHA", "AUDITORIA_EVENTOS", "AUDITORIA_MUDANCAS", "FUNCIONARIOS", "REFRESH_TOKENS" })
        {
            var texto = await Sql.TextoDaTabelaAsync(api.Conexao, tabela);
            Assert.DoesNotContain(senha, texto, StringComparison.Ordinal);
            Assert.DoesNotContain(hash, texto, StringComparison.Ordinal);
            Assert.DoesNotContain(token, texto, StringComparison.Ordinal); // o token só existe na carga da fila
        }

        Assert.DoesNotContain(senha, await Sql.TextoDaTabelaAsync(api.Conexao, "FILA_EMAILS"), StringComparison.Ordinal);
        Assert.Contains(token, await Sql.TextoDaTabelaAsync(api.Conexao, "FILA_EMAILS"), StringComparison.Ordinal); // controle positivo: a varredura acha o token onde ele está
    }

    [Fact]
    public async Task Se_o_evento_de_auditoria_falha_a_senha_nao_muda_e_o_link_continua_valendo()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("definir-auditor-falha");
        var token = await NovoConviteAsync(api, pessoa);
        var hashAntes = (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"))!.ToString()!;
        var comAuditorQueFalha = api.Api.WithWebHostBuilder(b => b.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAuditor>();
            servicos.AddScoped<IAuditor, AuditorQueLanca>();
        }));

        var resposta = await PostAsync(comAuditorQueFalha.CreateClient(), "/api/auth/senha/definir", new { token, novaSenha = NovaSenha });

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.Equal(hashAntes, (await Sql.EscalarAsync(api.Conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"))!.ToString()); // a senha não mudou
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT SENHA_PROVISORIA FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Equal(200, (int)(await PostAsync(api.Api.ClienteAnonimo(), "/api/auth/senha/validar-link", new { token })).StatusCode); // o link continua valendo
    }
}

/// <summary>Um auditor que lança ao registrar (a atomicidade da decisão 14: se o evento falha, o fato desfaz).</summary>
internal sealed class AuditorQueLanca : IAuditor
{
    private static InvalidOperationException Falha() => new("auditoria fora do ar (falha de teste)");

    public void Registrar(EventoDeAuditoriaNovo evento) => throw Falha();

    public void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento) => throw Falha();

    public Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct) => throw Falha();

    public Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct) => throw Falha();

    public Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct) => throw Falha();

    public IDisposable SuspenderRegistroAutomatico() => throw Falha();
}

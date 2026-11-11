using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Acesso;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// "Usuários do sistema" (tela 29): lista, convite, ativar e desativar, só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z. A situação do login é calculada, nunca gravada.
/// Cada teste cria as pessoas dele (nome com um marcador próprio, para a busca isolá-las do elenco e dos outros testes).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class UsuariosDoSistemaTests(MySqlFixture mysql)
{
    private const string Lista = "/api/rh/usuarios";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(UsuariosDoSistemaTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static Task<HttpResponseMessage> PostSemCorpoAsync(HttpClient cliente, string caminho) => cliente.PostAsync(caminho, content: null);

    private static async Task<List<JsonElement>> ItensAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(200, (int)resposta.StatusCode);
        return [.. (await CorpoAsync(resposta)).GetProperty("itens").EnumerateArray()];
    }

    private static string Situacao(JsonElement item) => item.GetProperty("situacao").GetString()!;

    private static async Task<(int Status, string Codigo, string Detalhe)> ProblemaAsync(HttpResponseMessage resposta)
    {
        var corpo = await CorpoAsync(resposta);
        return ((int)resposta.StatusCode, corpo.GetProperty("codigo").GetString()!, corpo.GetProperty("detail").GetString()!);
    }

    // ---------------------------------------------------------------- lista

    [Fact]
    public async Task Lista_so_colaboradores_ativos_e_em_admissao_com_a_situacao_e_o_total_do_menu()
    {
        var api = await ApiAsync();
        var semLogin = await api.NovaPessoaAsync("lista1-sem");
        var comConvite = await api.NovaPessoaAsync("lista1-convite");
        await NovoConviteAsync(api, comConvite);
        var comSenha = await api.NovaPessoaAsync("lista1-senha", login: TipoDeLogin.SenhaDefinida);
        await api.NovaPessoaAsync("lista1-inativa", ativa: false, login: TipoDeLogin.SenhaDefinida); // não aparece
        var cliente = ComoRh(api);

        var itens = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=lista1-&tamanhoPagina=100"));

        Assert.Equal(3, itens.Count);
        var porId = itens.ToDictionary(i => i.GetProperty("funcionarioId").GetInt32());
        Assert.Equal(("SEM_CONVITE", false), (Situacao(porId[semLogin.FuncionarioId]), porId[semLogin.FuncionarioId].GetProperty("ativo").GetBoolean()));
        Assert.Equal(("CONVITE_ENVIADO", true), (Situacao(porId[comConvite.FuncionarioId]), porId[comConvite.FuncionarioId].GetProperty("ativo").GetBoolean()));
        Assert.Equal(("SENHA_DEFINIDA", true), (Situacao(porId[comSenha.FuncionarioId]), porId[comSenha.FuncionarioId].GetProperty("ativo").GetBoolean()));
        Assert.Equal(JsonValueKind.Null, porId[semLogin.FuncionarioId].GetProperty("conviteEnviadoEmUtc").ValueKind);
        Assert.Equal(ElencoDeTeste.Hoje, porId[comConvite.FuncionarioId].GetProperty("conviteEnviadoEmUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(JsonValueKind.Null, porId[comConvite.FuncionarioId].GetProperty("ultimoLoginEmUtc").ValueKind);
        Assert.Equal("Funcionario", porId[comConvite.FuncionarioId].GetProperty("perfil").GetString());

        // O número do menu é o total: todo colaborador ativo (inclui os em admissão, como Rafael Torres, do elenco); inativos fora (Henrique Alves).
        var corpo = await CorpoAsync(await cliente.GetAsync($"{Lista}?tamanhoPagina=1"));
        Assert.Equal(await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE"), corpo.GetProperty("total").GetInt64());
        Assert.Single(await ItensAsync(await cliente.GetAsync($"{Lista}?busca=Rafael%20Torres")));
        Assert.Empty(await ItensAsync(await cliente.GetAsync($"{Lista}?busca=Henrique%20Alves")));
    }

    [Fact]
    public async Task Situacao_de_cada_login_SEM_CONVITE_CONVITE_ENVIADO_CONVITE_EXPIRADO_e_SENHA_DEFINIDA()
    {
        var api = await ApiAsync();
        var semLogin = await api.NovaPessoaAsync("situacao-sem");
        var comConvite = await api.NovaPessoaAsync("situacao-convite");
        await NovoConviteAsync(api, comConvite);
        var comSenha = await api.NovaPessoaAsync("situacao-senha", login: TipoDeLogin.SenhaDefinida);

        async Task<string> SituacaoDeAsync(PessoaDoTeste p) =>
            (await api.NoEscopoAsync(sp => sp.GetRequiredService<IConsultaDeUsuariosDoSistema>().ObterAsync(p.FuncionarioId, CancellationToken.None)))!.Situacao.ToString();

        try
        {
            Assert.Equal(("SemConvite", "ConviteEnviado", "SenhaDefinida"), (await SituacaoDeAsync(semLogin), await SituacaoDeAsync(comConvite), await SituacaoDeAsync(comSenha)));

            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(7).AddSeconds(1)); // venceu: o convite durou 7 dias
            Assert.Equal("ConviteExpirado", await SituacaoDeAsync(comConvite));

            await api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([comConvite.FuncionarioId], CancellationToken.None)); // reemitir
            Assert.Equal("ConviteEnviado", await SituacaoDeAsync(comConvite));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Filtros_situacao_e_ativo_batem_com_a_situacao_mostrada_e_busca_procura_no_nome_e_no_email()
    {
        var api = await ApiAsync();
        var semLogin = await api.NovaPessoaAsync("filtros-sem");
        var comConvite = await api.NovaPessoaAsync("filtros-convite");
        await NovoConviteAsync(api, comConvite);
        var comSenha = await api.NovaPessoaAsync("filtros-senha", login: TipoDeLogin.SenhaDefinida);
        var desativada = await api.NovaPessoaAsync("filtros-desativada", login: TipoDeLogin.SenhaDefinida, loginAtivo: false);
        var cliente = ComoRh(api);

        var todos = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=filtros-&tamanhoPagina=100"));
        Assert.Equal(4, todos.Count);

        foreach (var (situacao, esperados) in new[]
        {
            ("SEM_CONVITE", new[] { semLogin.FuncionarioId }),
            ("CONVITE_ENVIADO", [comConvite.FuncionarioId]),
            ("SENHA_DEFINIDA", [comSenha.FuncionarioId, desativada.FuncionarioId]),
            ("CONVITE_EXPIRADO", Array.Empty<int>()),
        })
        {
            var filtrados = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=filtros-&situacao={situacao}&tamanhoPagina=100"));
            Assert.Equal(esperados.Order(), filtrados.Select(i => i.GetProperty("funcionarioId").GetInt32()).Order());
            Assert.All(filtrados, i => Assert.Equal(situacao, Situacao(i)));
        }

        var liberados = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=filtros-&ativo=true&tamanhoPagina=100"));
        var bloqueados = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=filtros-&ativo=false&tamanhoPagina=100"));
        Assert.All(liberados, i => Assert.True(i.GetProperty("ativo").GetBoolean()));
        Assert.All(bloqueados, i => Assert.False(i.GetProperty("ativo").GetBoolean())); // o mesmo "ativo" que a linha mostra: inclui quem não tem login (false em SEM_CONVITE)
        Assert.Equal(todos.Count, liberados.Count + bloqueados.Count);
        Assert.Equal(new[] { semLogin.FuncionarioId, desativada.FuncionarioId }.Order(), bloqueados.Select(i => i.GetProperty("funcionarioId").GetInt32()).Order());

        // A busca procura no nome E no e-mail corporativo, ignora maiúsculas e trata % e _ como texto.
        Assert.Single(await ItensAsync(await cliente.GetAsync($"{Lista}?busca={Uri.EscapeDataString(comSenha.Email!.ToUpperInvariant())}")));
        Assert.Single(await ItensAsync(await cliente.GetAsync($"{Lista}?busca={Uri.EscapeDataString(comSenha.Nome)}")));
        Assert.Empty(await ItensAsync(await cliente.GetAsync($"{Lista}?busca=%25")));
        Assert.Empty(await ItensAsync(await cliente.GetAsync($"{Lista}?busca=_")));
    }

    [Fact]
    public async Task Lista_ordena_por_nome_com_desempate_por_id_e_paginacao_alem_do_fim_devolve_vazia_com_total_certo()
    {
        var api = await ApiAsync();
        var a = await api.NovaPessoaAsync("ordem-a");
        var b = await api.NovaPessoaAsync("ordem-b");
        var c = await api.NovaPessoaAsync("ordem-c");
        int primeiroIgual, segundoIgual;
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");
            var iguais = new[] { Funcionario.Criar("Pessoa ordem-z igual", "ordem.z1@empresa.com.br", new DateOnly(2026, 1, 2), nivel), Funcionario.Criar("Pessoa ordem-z igual", "ordem.z2@empresa.com.br", new DateOnly(2026, 1, 2), nivel) };
            db.Funcionarios.AddRange(iguais);
            await db.SaveChangesAsync();
            (primeiroIgual, segundoIgual) = (iguais[0].Id, iguais[1].Id);
        }

        var cliente = ComoRh(api);

        var tudo = await ItensAsync(await cliente.GetAsync($"{Lista}?busca=ordem-&tamanhoPagina=100"));
        Assert.Equal([a.FuncionarioId, b.FuncionarioId, c.FuncionarioId, primeiroIgual, segundoIgual], tudo.Select(i => i.GetProperty("funcionarioId").GetInt32())); // nome, e o id desempata os dois "ordem-z"

        var primeira = await CorpoAsync(await cliente.GetAsync($"{Lista}?busca=ordem-&tamanhoPagina=2&pagina=1"));
        var terceira = await CorpoAsync(await cliente.GetAsync($"{Lista}?busca=ordem-&tamanhoPagina=2&pagina=3"));
        var alemDoFim = await CorpoAsync(await cliente.GetAsync($"{Lista}?busca=ordem-&tamanhoPagina=2&pagina=99"));
        Assert.Equal([a.FuncionarioId, b.FuncionarioId], primeira.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionarioId").GetInt32()));
        Assert.Equal([segundoIgual], terceira.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionarioId").GetInt32()));
        Assert.Empty(alemDoFim.GetProperty("itens").EnumerateArray());
        Assert.Equal((5, 3, 99, 2), (alemDoFim.GetProperty("total").GetInt32(), alemDoFim.GetProperty("totalPaginas").GetInt32(), alemDoFim.GetProperty("pagina").GetInt32(), alemDoFim.GetProperty("tamanhoPagina").GetInt32()));
    }

    [Theory]
    [InlineData("situacao=XYZ")]
    [InlineData("situacao=SemConvite")] // o nome do C# não é o do contrato
    [InlineData("situacao=sem_convite")]
    public async Task Situacao_desconhecida_na_consulta_devolve_400(string consulta)
    {
        var api = await ApiAsync();

        var resposta = await ComoRh(api).GetAsync($"{Lista}?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal("Dados inválidos.", corpo.GetProperty("title").GetString());
        Assert.True(corpo.GetProperty("errors").TryGetProperty("situacao", out _));
    }

    [Fact]
    public async Task Parametros_invalidos_voltam_juntos_no_mesmo_400()
    {
        var api = await ApiAsync();

        var corpo = await CorpoAsync(await ComoRh(api).GetAsync($"{Lista}?pagina=0&tamanhoPagina=101&situacao=XYZ"));

        var erros = corpo.GetProperty("errors");
        Assert.True(erros.TryGetProperty("pagina", out _));
        Assert.True(erros.TryGetProperty("tamanhoPagina", out _));
        Assert.True(erros.TryGetProperty("situacao", out _));
    }

    // ---------------------------------------------------------------- convite

    [Fact]
    public async Task Convite_cria_login_e_devolve_o_item_CONVITE_ENVIADO_com_conviteEnviadoEmUtc_do_relogio()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("convite-ok");

        var resposta = await PostSemCorpoAsync(ComoRh(api), $"{Lista}/{pessoa.FuncionarioId}/convite");

        Assert.Equal(200, (int)resposta.StatusCode);
        var item = await CorpoAsync(resposta);
        Assert.Equal((pessoa.FuncionarioId, pessoa.Nome, pessoa.Email, "CONVITE_ENVIADO", true), (item.GetProperty("funcionarioId").GetInt32(), item.GetProperty("nome").GetString(), item.GetProperty("emailCorporativo").GetString(), Situacao(item), item.GetProperty("ativo").GetBoolean()));
        Assert.Equal(ElencoDeTeste.Hoje, item.GetProperty("conviteEnviadoEmUtc").GetDateTime().ToUniversalTime());
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE' AND ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    [Fact]
    public async Task Convite_para_inativo_sem_email_ou_com_senha_definida_devolve_os_422_e_para_id_inexistente_404()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var inativa = await api.NovaPessoaAsync("convite-inativa", ativa: false);
        var semEmail = await api.NovaPessoaAsync("convite-sem-email", comEmail: false);
        var comSenha = await api.NovaPessoaAsync("convite-com-senha", login: TipoDeLogin.SenhaDefinida);

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Não é possível enviar convite a um colaborador inativo."), await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{inativa.FuncionarioId}/convite")));
        Assert.Equal((422, "FUNCIONARIO_SEM_EMAIL_CORPORATIVO", "Esta pessoa não tem e-mail corporativo cadastrado. Edite o cadastro antes de enviar o convite."), await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{semEmail.FuncionarioId}/convite")));
        Assert.Equal((422, "SENHA_JA_DEFINIDA", "Esta pessoa já definiu a senha. Se ela esqueceu, use \"Esqueci minha senha\" na tela de entrada."), await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{comSenha.FuncionarioId}/convite")));
        Assert.Equal(404, (int)(await PostSemCorpoAsync(cliente, $"{Lista}/987654321/convite")).StatusCode);
    }

    [Fact]
    public async Task Convite_para_login_desativado_devolve_422_USUARIO_DESATIVADO_e_depois_de_Ativar_o_convite_passa()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var pessoa = await api.NovaPessoaAsync("convite-desativado", login: TipoDeLogin.Provisorio, loginAtivo: false);

        var recusado = await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/convite"));
        Assert.Equal(("USUARIO_DESATIVADO", 422), (recusado.Codigo, recusado.Status));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}")); // o convite não reativa

        Assert.Equal(200, (int)(await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/ativar")).StatusCode);
        var item = await CorpoAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/convite"));

        Assert.Equal("CONVITE_ENVIADO", Situacao(item));
        Assert.True(item.GetProperty("ativo").GetBoolean());
    }

    // ---------------------------------------------------------------- ativar e desativar

    [Fact]
    public async Task Ativar_sem_login_422_SEM_USUARIO_de_inativo_422_FUNCIONARIO_INATIVO_ja_ativo_devolve_como_esta_e_inexistente_404()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var semLogin = await api.NovaPessoaAsync("ativar-sem-login");
        var inativa = await api.NovaPessoaAsync("ativar-inativa", ativa: false, login: TipoDeLogin.SenhaDefinida);
        var jaAtiva = await api.NovaPessoaAsync("ativar-ja-ativa", login: TipoDeLogin.SenhaDefinida);
        var eventosAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS");

        Assert.Equal((422, "SEM_USUARIO", "Esta pessoa ainda não tem login. Envie o convite primeiro."), await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{semLogin.FuncionarioId}/ativar")));
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa."), await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{inativa.FuncionarioId}/ativar")));
        var comoEsta = await PostSemCorpoAsync(cliente, $"{Lista}/{jaAtiva.FuncionarioId}/ativar");
        Assert.Equal(200, (int)comoEsta.StatusCode);
        Assert.True((await CorpoAsync(comoEsta)).GetProperty("ativo").GetBoolean());
        Assert.Equal(404, (int)(await PostSemCorpoAsync(cliente, $"{Lista}/987654321/ativar")).StatusCode);
        Assert.Equal(eventosAntes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS")); // nada mudou: nenhum evento
    }

    [Fact]
    public async Task Desativar_bloqueia_o_login_revoga_os_refresh_tokens_e_o_login_com_senha_certa_vira_403_Acesso_desativado()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var pessoa = await api.NovaPessoaAsync("desativar", login: TipoDeLogin.SenhaDefinida);
        var anonimo = api.Api.ClienteAnonimo();
        var (_, refresh) = await EntrarAsync(anonimo, pessoa.Email!);

        var resposta = await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/desativar");

        Assert.Equal(200, (int)resposta.StatusCode);
        var item = await CorpoAsync(resposta);
        Assert.False(item.GetProperty("ativo").GetBoolean());
        Assert.Equal("SENHA_DEFINIDA", Situacao(item));
        // Encerradas NA HORA, no banco: o renovar também recusaria sozinho (login bloqueado), então só a conferência direta separa as duas coisas.
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM REFRESH_TOKENS WHERE ID_USUARIO = {pessoa.UsuarioId} AND REVOGADO_EM IS NULL"));
        Assert.Equal(401, await RenovarAsync(anonimo, refresh)); // as sessões abertas caíram
        var entrar = await anonimo.PostAsJsonAsync("/api/auth/entrar", new { email = pessoa.Email, senha = ApiFactory.SenhaPadrao });
        Assert.Equal(403, (int)entrar.StatusCode); // senha certa, login bloqueado
        Assert.Equal("Acesso desativado.", (await CorpoAsync(entrar)).GetProperty("title").GetString());

        // Já desativado: devolve como está (e não grava outro evento).
        var eventos = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS");
        var denovo = await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/desativar");
        Assert.Equal(200, (int)denovo.StatusCode);
        Assert.False((await CorpoAsync(denovo)).GetProperty("ativo").GetBoolean());
        Assert.Equal(eventos, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS"));

        // Bloquear e liberar de novo não ressuscita a sessão antiga: o refresh token de antes do bloqueio continua morto.
        Assert.Equal(200, (int)(await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/ativar")).StatusCode);
        Assert.Equal(401, await RenovarAsync(anonimo, refresh));
    }

    [Fact]
    public async Task Desativar_o_proprio_login_devolve_422_NAO_PODE_DESATIVAR_A_SI_MESMO_e_sem_login_422_SEM_USUARIO()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var semLogin = await api.NovaPessoaAsync("desativar-sem-login");

        var asiMesmo = await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{api.Elenco.Rh.FuncionarioId}/desativar"));
        var semUsuario = await ProblemaAsync(await PostSemCorpoAsync(cliente, $"{Lista}/{semLogin.FuncionarioId}/desativar"));

        Assert.Equal((422, "NAO_PODE_DESATIVAR_A_SI_MESMO", "Você não pode desativar o seu próprio login."), asiMesmo);
        Assert.Equal((422, "SEM_USUARIO", "Esta pessoa ainda não tem login. Envie o convite primeiro."), semUsuario);
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {api.Elenco.Rh.UsuarioId}")); // o login do RH continua ativo
        Assert.Equal(404, (int)(await PostSemCorpoAsync(cliente, $"{Lista}/987654321/desativar")).StatusCode);
    }

    [Fact]
    public async Task Ativar_e_desativar_gravam_Acesso_Alterou_com_o_ator_RH()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var pessoa = await api.NovaPessoaAsync("eventos", login: TipoDeLogin.SenhaDefinida);

        await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/desativar");
        await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/ativar");

        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", pessoa.UsuarioId!.Value);
        Assert.Equal(["Acesso de " + pessoa.Nome + " bloqueado", "Acesso de " + pessoa.Nome + " liberado"], eventos.Select(e => e.Descricao));
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Alterou, api.Elenco.Rh.FuncionarioId, TipoDeAtor.Pessoa, $"Colaborador {pessoa.Nome}"), (e.Area, e.Acao, e.FuncionarioAtorId, e.AtorTipo, e.RegistroAfetado)));
    }

    // ---------------------------------------------------------------- permissões e contrato

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_nas_quatro_rotas_e_anonimo_401()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("permissao", login: TipoDeLogin.SenhaDefinida);
        var rotas = new (HttpMethod Metodo, string Caminho)[]
        {
            (HttpMethod.Get, Lista),
            (HttpMethod.Post, $"{Lista}/{pessoa.FuncionarioId}/convite"),
            (HttpMethod.Post, $"{Lista}/{pessoa.FuncionarioId}/ativar"),
            (HttpMethod.Post, $"{Lista}/{pessoa.FuncionarioId}/desativar"),
        };

        foreach (var (metodo, caminho) in rotas)
        {
            foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
            {
                Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
            }

            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().SendAsync(new HttpRequestMessage(metodo, caminho))).StatusCode);
        }

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}")); // nenhuma das chamadas proibidas mexeu em nada
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        var pessoa = await api.NovaPessoaAsync("schema", login: TipoDeLogin.SenhaDefinida);
        var semLogin = await api.NovaPessoaAsync("schema-sem-login");

        await (await cliente.GetAsync($"{Lista}?busca=schema&situacao=SENHA_DEFINIDA")).AfirmarConformeAoContratoAsync("listarUsuariosDoSistema");
        await (await cliente.GetAsync($"{Lista}?situacao=XYZ")).AfirmarConformeAoContratoAsync("listarUsuariosDoSistema"); // o 400
        await (await PostSemCorpoAsync(cliente, $"{Lista}/{semLogin.FuncionarioId}/convite")).AfirmarConformeAoContratoAsync("enviarConviteDeAcesso");
        await (await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/convite")).AfirmarConformeAoContratoAsync("enviarConviteDeAcesso"); // o 422
        await (await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/desativar")).AfirmarConformeAoContratoAsync("desativarUsuarioDoSistema");
        await (await PostSemCorpoAsync(cliente, $"{Lista}/{pessoa.FuncionarioId}/ativar")).AfirmarConformeAoContratoAsync("ativarUsuarioDoSistema");
        await (await PostSemCorpoAsync(cliente, $"{Lista}/987654321/ativar")).AfirmarConformeAoContratoAsync("ativarUsuarioDoSistema"); // o 404
    }
}

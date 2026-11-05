using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Acesso;
using RHSystem.Application.Comum;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Acesso;

/// <summary>
/// O serviço que emite o link de convite e o de redefinição, no banco REAL com o relógio falso em 2026-10-07T12:42:00Z. O ator dos eventos é o RH do elenco. Um banco para a
/// classe; cada teste cria as pessoas dele. O token em claro só existe na carga do e-mail: o resto do banco guarda o hash.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ServicoDeConviteTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(ServicoDeConviteTests));

    private static Task<IReadOnlyList<ConviteEmitido>> EmitirAsync(ApiDeAcesso api, params int[] ids) =>
        api.NaTransacaoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync(ids, Ct));

    private static Task SolicitarRedefinicaoAsync(ApiDeAcesso api, string email) =>
        api.NoEscopoAsync(async sp =>
        {
            await sp.GetRequiredService<IServicoDeConvite>().SolicitarRedefinicaoAsync(email, Ct);
            return 0;
        });

    private static async Task<List<EmailNaFila>> EmailsDoAsync(ApiDeAcesso api, int funcionarioId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.EmailsNaFila.Where(e => e.FuncionarioId == funcionarioId).OrderBy(e => e.Id).ToListAsync(Ct);
    }

    private static async Task<List<UsuarioTokenSenha>> TokensDoAsync(ApiDeAcesso api, int usuarioId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.UsuariosTokensSenha.Where(t => t.UsuarioId == usuarioId).OrderBy(t => t.Id).ToListAsync(Ct);
    }

    private static async Task<int> UsuarioIdAsync(ApiDeAcesso api, int funcionarioId) =>
        (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionarioId}");

    private static string TokenDaCarga(EmailNaFila email) =>
        JsonDocument.Parse(email.Carga!).RootElement.GetProperty("token").GetString()!;

    private static async Task<long> EmAbertoAsync(ApiDeAcesso api, int usuarioId, string tipo) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = {usuarioId} AND TIPO = '{tipo}' AND EM_ABERTO = 1");

    // ---------------------------------------------------------------- convite

    [Fact]
    public async Task Emitir_cria_login_ativo_com_SenhaProvisoria_true_e_hash_provisorio()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("login-novo");

        var emitidos = await EmitirAsync(api, pessoa.FuncionarioId);

        var emitido = Assert.Single(emitidos);
        Assert.Equal((pessoa.FuncionarioId, true, Agora.AddDays(7)), (emitido.FuncionarioId, emitido.LoginCriado, emitido.ExpiraEmUtc));
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var usuario = await db.Usuarios.SingleAsync(u => u.FuncionarioId == pessoa.FuncionarioId, Ct);
        Assert.Equal(usuario.Id, emitido.UsuarioId);
        Assert.True(usuario.Ativo);
        Assert.True(usuario.SenhaProvisoria);
        Assert.True(usuario.SenhaHash.Length >= 40);
        Assert.Equal(0xFF, Convert.FromBase64String(usuario.SenhaHash)[0]); // o hash provisório: nunca confere com senha alguma
    }

    [Fact]
    public async Task Emitir_grava_so_o_hash_e_o_token_nao_aparece_em_nenhuma_coluna_do_banco_fora_da_CARGA_da_fila()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("sem-token-em-claro");

        await EmitirAsync(api, pessoa.FuncionarioId);

        var email = Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId));
        var token = TokenDaCarga(email);
        Assert.Matches("^[0-9a-f]{64}$", token);
        // Controle positivo: o token ESTÁ na carga; a varredura tem de achá-lo lá, senão "não achou fora" não prova nada.
        Assert.Contains(token, await Sql.TextoDaTabelaAsync(api.Conexao, "FILA_EMAILS"), StringComparison.Ordinal);
        foreach (var tabela in new[] { "USUARIOS_TOKENS_SENHA", "USUARIOS", "AUDITORIA_EVENTOS", "AUDITORIA_MUDANCAS", "FUNCIONARIOS", "TAREFAS_AGENDADAS" })
        {
            Assert.DoesNotContain(token, await Sql.TextoDaTabelaAsync(api.Conexao, tabela), StringComparison.Ordinal);
        }

        var fora = await Sql.TextoDaTabelaAsync(api.Conexao, "FILA_EMAILS", "CARGA");
        Assert.DoesNotContain(token, fora, StringComparison.Ordinal);
        Assert.Contains(await TokensDoAsync(api, await UsuarioIdAsync(api, pessoa.FuncionarioId)), t => t.TokenHash == new GeradorDeTokenDeSenhaParaTeste().Hash(token));
    }

    [Fact]
    public async Task Emitir_enfileira_CONVITE_com_chave_ASCII_carga_com_o_token_e_expira_em_7_dias()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("fila-convite");

        await EmitirAsync(api, pessoa.FuncionarioId);

        var email = Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId));
        var token = (await TokensDoAsync(api, await UsuarioIdAsync(api, pessoa.FuncionarioId))).Single();
        Assert.Equal("CONVITE", email.Tipo);
        Assert.Equal($"CONVITE:func={pessoa.FuncionarioId}:token={token.Id}", email.ChaveDeIdempotencia);
        Assert.All(email.ChaveDeIdempotencia, c => Assert.True(c < 128));
        Assert.Equal((pessoa.Email, token.Id, false, SituacaoDoEmail.Pendente), (email.DestinatarioEmail, email.ReferenciaId, email.Sigiloso, email.Situacao));
        var carga = JsonDocument.Parse(email.Carga!).RootElement;
        Assert.Equal(pessoa.Nome, carga.GetProperty("nome").GetString());
        Assert.Equal(new GeradorDeTokenDeSenhaParaTeste().Hash(carga.GetProperty("token").GetString()!), token.TokenHash);
        Assert.Equal(Agora.AddDays(7), carga.GetProperty("expiraEmUtc").GetDateTime().ToUniversalTime());
    }

    [Fact]
    public async Task Emitir_grava_CRIADO_EM_e_EXPIRA_EM_do_relogio_falso_e_nao_do_UTC_do_banco()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("datas");

        await EmitirAsync(api, pessoa.FuncionarioId);

        var token = (await TokensDoAsync(api, await UsuarioIdAsync(api, pessoa.FuncionarioId))).Single();
        Assert.Equal(Agora, token.CriadoEm); // 2026-10-07T12:42:00Z, sem violar o CHECK EXPIRA_EM > CRIADO_EM
        Assert.Equal(Agora.AddDays(7), token.ExpiraEm);
        Assert.Equal(TipoDeTokenDeSenha.Convite, token.Tipo);
    }

    [Fact]
    public async Task Reemitir_invalida_o_convite_anterior_e_deixa_um_so_em_aberto()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("reemitir");
        await EmitirAsync(api, pessoa.FuncionarioId);
        var usuarioId = await UsuarioIdAsync(api, pessoa.FuncionarioId);

        var segundo = Assert.Single(await EmitirAsync(api, pessoa.FuncionarioId));

        Assert.False(segundo.LoginCriado);
        var tokens = await TokensDoAsync(api, usuarioId);
        Assert.Equal(2, tokens.Count);
        Assert.Equal(1, await EmAbertoAsync(api, usuarioId, "CONVITE")); // EM_ABERTO = 1 em exatamente uma linha
        Assert.NotNull(tokens[0].InvalidadoEm);
        Assert.Null(tokens[1].InvalidadoEm);
        var emails = await EmailsDoAsync(api, pessoa.FuncionarioId);
        Assert.Equal(2, emails.Count);
        Assert.Equal(2, emails.Select(e => e.ChaveDeIdempotencia).Distinct().Count()); // o reenvio tem chave própria (o id do token novo)
    }

    [Fact]
    public async Task Dois_convites_simultaneos_para_a_mesma_pessoa_deixam_um_so_em_aberto_e_nenhum_lanca()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("simultaneo");
        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, 20)
            .Select(_ => Task.Run(async () =>
            {
                await largada.Task;
                return await EmitirAsync(api, pessoa.FuncionarioId);
            }))
            .ToList();

        largada.SetResult();
        var resultados = await Task.WhenAll(tarefas); // nenhuma lança

        var usuarioId = await UsuarioIdAsync(api, pessoa.FuncionarioId);
        Assert.Equal(20, resultados.Length);
        Assert.Equal(1, resultados.SelectMany(r => r).Count(r => r.LoginCriado)); // um só criou o login
        Assert.Equal(1, await EmAbertoAsync(api, usuarioId, "CONVITE"));
        Assert.Equal(20, (await TokensDoAsync(api, usuarioId)).Count);
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    [Fact]
    public async Task Emitir_para_inativo_sem_email_corporativo_e_com_senha_definida_devolve_os_tres_422_do_contrato()
    {
        var api = await ApiAsync();
        var inativa = await api.NovaPessoaAsync("inativa", ativa: false);
        var semEmail = await api.NovaPessoaAsync("sem-email", comEmail: false);
        var comSenha = await api.NovaPessoaAsync("com-senha", login: TipoDeLogin.SenhaDefinida);

        var porInativo = await Assert.ThrowsAsync<ErroDeNegocioException>(() => EmitirAsync(api, inativa.FuncionarioId));
        var porEmail = await Assert.ThrowsAsync<ErroDeNegocioException>(() => EmitirAsync(api, semEmail.FuncionarioId));
        var porSenha = await Assert.ThrowsAsync<ErroDeNegocioException>(() => EmitirAsync(api, comSenha.FuncionarioId));

        Assert.Equal(("FUNCIONARIO_INATIVO", "Não é possível enviar convite a um colaborador inativo."), (porInativo.Codigo, porInativo.Detalhe));
        Assert.Equal(("FUNCIONARIO_SEM_EMAIL_CORPORATIVO", "Esta pessoa não tem e-mail corporativo cadastrado. Edite o cadastro antes de enviar o convite."), (porEmail.Codigo, porEmail.Detalhe));
        Assert.Equal(("SENHA_JA_DEFINIDA", "Esta pessoa já definiu a senha. Se ela esqueceu, use \"Esqueci minha senha\" na tela de entrada."), (porSenha.Codigo, porSenha.Detalhe));
        // Nada ficou: nem login para quem não tinha, nem token, nem e-mail.
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO IN ({inativa.FuncionarioId}, {semEmail.FuncionarioId}, {comSenha.FuncionarioId})"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO IN ({inativa.FuncionarioId}, {semEmail.FuncionarioId})"));
    }

    [Fact]
    public async Task Emitir_para_login_desativado_devolve_422_USUARIO_DESATIVADO_nao_reativa_o_login_e_nao_grava_token_nem_email()
    {
        var api = await ApiAsync();
        var desativada = await api.NovaPessoaAsync("login-desativado", login: TipoDeLogin.Provisorio, loginAtivo: false);
        var boa = await api.NovaPessoaAsync("lote-bom");

        var erro = await Assert.ThrowsAsync<ErroDeNegocioException>(() => EmitirAsync(api, desativada.FuncionarioId));
        Assert.Equal("USUARIO_DESATIVADO", erro.Codigo);
        Assert.Equal("O login desta pessoa está desativado. Libere o acesso em Usuários do sistema (Ativar) antes de enviar o convite.", erro.Detalhe);
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {desativada.UsuarioId}")); // o convite não reativa
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE ID_USUARIO = {desativada.UsuarioId}"));
        Assert.Empty(await EmailsDoAsync(api, desativada.FuncionarioId));

        // No lote o erro de uma pessoa desfaz o lote todo.
        await Assert.ThrowsAsync<ErroDeNegocioException>(() => EmitirAsync(api, boa.FuncionarioId, desativada.FuncionarioId));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {boa.FuncionarioId}"));
        Assert.Empty(await EmailsDoAsync(api, boa.FuncionarioId));
    }

    [Fact]
    public async Task Emitir_para_id_inexistente_lanca_NaoEncontrado_e_fora_do_intervalo_ou_sem_transacao_lanca()
    {
        var api = await ApiAsync();

        await Assert.ThrowsAsync<NaoEncontradoException>(() => EmitirAsync(api, 987_654_321));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => EmitirAsync(api));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => EmitirAsync(api, [.. Enumerable.Range(1, 501)]));
        // Sem transação aberta o serviço recusa (as travas e as duas gravações só são seguras dentro dela).
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoAsync(sp => sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([1], Ct)));
    }

    [Fact]
    public async Task Emitir_500_pessoas_grava_500_logins_500_tokens_e_500_emails()
    {
        var api = await ApiAsync();
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario", Ct);
            db.Funcionarios.AddRange(Enumerable.Range(0, 500).Select(i => Funcionario.Criar($"Lote {i}", $"lote.{i}@lote500.com.br", new DateOnly(2026, 1, 2), nivel)));
            await db.SaveChangesAsync(Ct);
        }

        List<int> ids;
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            ids = await db.Funcionarios.Where(f => f.EmailCorporativo!.EndsWith("@lote500.com.br")).Select(f => f.Id).ToListAsync(Ct);
        }

        var emitidos = await EmitirAsync(api, [.. ids]);

        Assert.Equal(500, ids.Count);
        Assert.Equal(500, emitidos.Count);
        var lista = string.Join(",", ids.Select(i => i.ToString(CultureInfo.InvariantCulture)));
        Assert.Equal(500, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO IN ({lista}) AND SENHA_PROVISORIA = TRUE"));
        Assert.Equal(500, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA T JOIN USUARIOS U ON U.ID_USUARIO = T.ID_USUARIO WHERE U.ID_FUNCIONARIO IN ({lista})"));
        Assert.Equal(500, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE' AND ID_FUNCIONARIO IN ({lista})"));
    }

    [Fact]
    public async Task Emitir_dentro_de_transacao_que_desfaz_nao_deixa_login_token_nem_email()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("desfaz");

        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NaTransacaoAsync<int>(async sp =>
        {
            await sp.GetRequiredService<IServicoDeConvite>().EmitirAsync([pessoa.FuncionarioId], Ct);
            throw new InvalidOperationException("falha depois do convite");
        }));

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Empty(await EmailsDoAsync(api, pessoa.FuncionarioId));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%{pessoa.Nome}%'"));
    }

    [Fact]
    public async Task Auditoria_do_convite_e_Acesso_Criou_para_login_novo_e_Acesso_Alterou_para_reenvio_sem_token()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("auditoria-convite");

        await EmitirAsync(api, pessoa.FuncionarioId);
        await EmitirAsync(api, pessoa.FuncionarioId);

        var usuarioId = await UsuarioIdAsync(api, pessoa.FuncionarioId);
        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", usuarioId);
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], eventos.Select(e => e.Acao));
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Acesso, api.Elenco.Rh.FuncionarioId, TipoDeAtor.Pessoa), (e.Area, e.FuncionarioAtorId, e.AtorTipo)));
        Assert.All(eventos, e => Assert.Equal($"Colaborador {pessoa.Nome}", e.RegistroAfetado));
        Assert.Equal("Login criado e convite enviado", eventos[0].Descricao);
        Assert.Equal("Convite de acesso reenviado", eventos[1].Descricao);
        foreach (var email in await EmailsDoAsync(api, pessoa.FuncionarioId))
        {
            var token = TokenDaCarga(email);
            Assert.DoesNotContain(token, string.Join("|", eventos.SelectMany(e => e.Mudancas.Select(m => $"{m.ValorAntes}{m.ValorDepois}")).Append(eventos[0].Descricao + eventos[1].Descricao)), StringComparison.Ordinal);
            Assert.DoesNotContain(token, await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS"), StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------- redefinição

    [Fact]
    public async Task Redefinicao_de_ativo_com_login_cria_token_de_1_hora_e_enfileira_REDEFINICAO_SENHA()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("redef", login: TipoDeLogin.SenhaDefinida);

        await SolicitarRedefinicaoAsync(api, pessoa.Email!);

        var token = Assert.Single(await TokensDoAsync(api, pessoa.UsuarioId!.Value));
        Assert.Equal((TipoDeTokenDeSenha.Redefinicao, Agora, Agora.AddHours(1)), (token.Tipo, token.CriadoEm, token.ExpiraEm)); // ExpiraEmUtc = 2026-10-07T13:42:00Z
        var email = Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("REDEFINICAO_SENHA", email.Tipo);
        Assert.Equal($"REDEFINICAO_SENHA:func={pessoa.FuncionarioId}:token={token.Id}", email.ChaveDeIdempotencia);
        Assert.Equal((pessoa.Email, token.Id), (email.DestinatarioEmail, email.ReferenciaId));
        Assert.Equal(token.TokenHash, new GeradorDeTokenDeSenhaParaTeste().Hash(TokenDaCarga(email)));
    }

    [Fact]
    public async Task Redefinicao_de_inexistente_inativo_ou_sem_login_ou_com_login_desativado_nao_grava_nada()
    {
        var api = await ApiAsync();
        var inativa = await api.NovaPessoaAsync("redef-inativa", ativa: false, login: TipoDeLogin.SenhaDefinida);
        var semLogin = await api.NovaPessoaAsync("redef-sem-login");
        var desativada = await api.NovaPessoaAsync("redef-desativada", login: TipoDeLogin.SenhaDefinida, loginAtivo: false);
        var antesTokens = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA");
        var antesEmails = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");
        var antesEventos = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS");

        foreach (var email in new[] { "ninguem.existe@empresa.com.br", inativa.Email!, semLogin.Email!, desativada.Email!, string.Empty, "nao-e-um-email" })
        {
            await SolicitarRedefinicaoAsync(api, email); // nunca lança e nunca revela
        }

        Assert.Equal(antesTokens, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA"));
        Assert.Equal(antesEmails, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
        Assert.Equal(antesEventos, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS"));
    }

    [Fact]
    public async Task Redefinicao_repetida_em_menos_de_60_s_nao_cria_outro_token_nem_outro_email_e_depois_de_60_s_invalida_o_anterior()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("redef-repetida", login: TipoDeLogin.SenhaDefinida);
        api.Api.Relogio.Fixar(Agora);

        await SolicitarRedefinicaoAsync(api, pessoa.Email!);
        api.Api.Relogio.Avancar(TimeSpan.FromSeconds(30));
        await SolicitarRedefinicaoAsync(api, pessoa.Email!);

        Assert.Single(await TokensDoAsync(api, pessoa.UsuarioId!.Value));
        Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId));

        api.Api.Relogio.Avancar(TimeSpan.FromSeconds(31)); // 61 s depois do primeiro pedido
        await SolicitarRedefinicaoAsync(api, pessoa.Email!);

        var tokens = await TokensDoAsync(api, pessoa.UsuarioId!.Value);
        Assert.Equal(2, tokens.Count);
        Assert.NotNull(tokens[0].InvalidadoEm); // decisão 6: o link anterior deixa de valer
        Assert.Null(tokens[1].InvalidadoEm);
        Assert.Equal(1, await EmAbertoAsync(api, pessoa.UsuarioId.Value, "REDEFINICAO"));
        Assert.Equal(2, (await EmailsDoAsync(api, pessoa.FuncionarioId)).Count);
        api.Api.Relogio.Fixar(Agora);
    }

    [Fact]
    public async Task Redefinicao_normaliza_o_email_maiusculas_e_espacos()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("redef-normaliza", login: TipoDeLogin.SenhaDefinida);

        await SolicitarRedefinicaoAsync(api, $"  {pessoa.Email!.ToUpperInvariant()} ");

        Assert.Single(await TokensDoAsync(api, pessoa.UsuarioId!.Value));
    }

    [Fact]
    public async Task Redefinicao_grava_Acesso_Alterou_com_a_pessoa_como_ator_e_sem_token()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("redef-auditoria", login: TipoDeLogin.SenhaDefinida);

        await SolicitarRedefinicaoAsync(api, pessoa.Email!);

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "USUARIOS", pessoa.UsuarioId!.Value));
        Assert.Equal((AuditoriaArea.Acesso, AuditoriaAcao.Alterou, pessoa.FuncionarioId, TipoDeAtor.Pessoa), (evento.Area, evento.Acao, evento.FuncionarioAtorId, evento.AtorTipo));
        Assert.Equal("Redefinição de senha solicitada", evento.Descricao);
        Assert.Equal($"Colaborador {pessoa.Nome}", evento.RegistroAfetado);
        var token = TokenDaCarga(Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId)));
        Assert.DoesNotContain(token, await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Redefinicao_simultanea_da_mesma_pessoa_nunca_lanca_e_deixa_um_so_link_em_aberto()
    {
        var api = await ApiAsync();
        var pessoa = await api.NovaPessoaAsync("redef-simultanea", login: TipoDeLogin.SenhaDefinida);
        var largada = new TaskCompletionSource();
        var tarefas = Enumerable.Range(0, 20).Select(_ => Task.Run(async () =>
        {
            await largada.Task;
            await SolicitarRedefinicaoAsync(api, pessoa.Email!);
        })).ToList();

        largada.SetResult();
        await Task.WhenAll(tarefas);

        Assert.Equal(1, await EmAbertoAsync(api, pessoa.UsuarioId!.Value, "REDEFINICAO"));
        Assert.Single(await TokensDoAsync(api, pessoa.UsuarioId.Value)); // o intervalo de 60 s faz as outras 19 não criarem nada
        Assert.Single(await EmailsDoAsync(api, pessoa.FuncionarioId));
    }
}

/// <summary>O mesmo cálculo do gerador de produção, feito aqui de forma independente (SHA-256 do texto do token, em minúsculas).</summary>
internal sealed class GeradorDeTokenDeSenhaParaTeste
{
    public string Hash(string token) =>
        Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token))).ToLowerInvariant();
}

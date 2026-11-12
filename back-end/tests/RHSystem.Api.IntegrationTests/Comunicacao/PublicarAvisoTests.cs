using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Comunicacao;
using RHSystem.Infrastructure.Emails;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// A tarefa <c>PUBLICAR_AVISO:aviso={id}:em={instante}</c> (o e-mail do aviso e as linhas de leitura), no banco REAL com o relógio em 2026-10-07T12:42Z. A criação e a publicação do aviso NÃO mandam e-mail (nada de 300 envios dentro do pedido HTTP): só a
/// tarefa enfileira, uma mensagem por pessoa ativa, admitida e com e-mail corporativo. A tarefa relê o aviso e só manda se ele ainda está no ar com a data da chave (editado, excluído, encerrado ou reagendado nesse meio tempo não manda nada).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PublicarAvisoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/avisos";
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PublicarAvisoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    private static object Novo(string titulo, string tipo = "SIMPLES", string conteudo = "Texto do aviso.", bool publicar = true, string? publicarEmUtc = null) =>
        new { tipo, titulo, conteudo, fixado = false, publicar, publicarEmUtc, expiraEm = (string?)null };

    private static async Task<int> CriarAsync(ApiDeAcesso api, object corpo)
    {
        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync(Rota, corpo);
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static async Task<List<int>> PessoasComEmailAsync(ApiDeAcesso api, int avisoId)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.Database.SqlQuery<int>($"SELECT ID_FUNCIONARIO AS Value FROM FILA_EMAILS WHERE TIPO = 'AVISO_PUBLICADO' AND REFERENCIA_ID = {avisoId} ORDER BY ID_FUNCIONARIO").ToListAsync();
    }

    private static async Task<List<int>> DestinatariosEsperadosAsync(ApiDeAcesso api)
    {
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        return await db.Database.SqlQueryRaw<int>("SELECT ID_FUNCIONARIO AS Value FROM FUNCIONARIOS WHERE ATIVO = TRUE AND EMAIL_CORPORATIVO IS NOT NULL AND DATA_ADMISSAO <= '2026-10-07' ORDER BY ID_FUNCIONARIO").ToListAsync();
    }

    /// <summary>A tarefa pendente do aviso (a mais recente), do jeito que o executor a entrega.</summary>
    private static async Task<TarefaParaExecutar> TarefaPendenteAsync(ApiDeAcesso api, int avisoId, string situacao = "PENDENTE")
    {
        var id = await ContarAsync(api, $"SELECT MAX(ID_TAREFA) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO' AND REFERENCIA_ID = {avisoId} AND SITUACAO = '{situacao}'");
        var chave = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM TAREFAS_AGENDADAS WHERE ID_TAREFA = {id}"))!;
        return new TarefaParaExecutar(id, chave, avisoId, null, null, 0);
    }

    private static async Task ExecutarAsync(ApiDeAcesso api, TarefaParaExecutar tarefa)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoAviso.Tipo).ExecutarAsync(tarefa, Ct);
    }

    private static async Task RecuperarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoAviso.Tipo).RecuperarAsync(Ct);
    }

    // ---------------------------------------------------------------- o e-mail

    [Fact]
    public async Task Publicar_nao_manda_e_mail_no_pedido_e_a_tarefa_enfileira_um_por_pessoa_ativa_admitida_e_com_e_mail()
    {
        var api = await ApiAsync();
        var semEmail = api.Elenco.MembroB.FuncionarioId;
        var emailOriginal = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {semEmail}"))!;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {semEmail}");
        try
        {
            var id = await CriarAsync(api, Novo(Titulo("Recesso"), "DETALHADO", "## Datas\nO escritório fecha de 24 a 26/12."));
            var antes = await PessoasComEmailAsync(api, id);
            await ExecutarAsync(api, await TarefaPendenteAsync(api, id));

            var esperados = await DestinatariosEsperadosAsync(api);
            var enfileirados = await PessoasComEmailAsync(api, id);

            Assert.Empty(antes); // o pedido HTTP não enfileira nada
            Assert.Equal(esperados, enfileirados);
            Assert.DoesNotContain(semEmail, enfileirados);
            Assert.DoesNotContain(api.Elenco.Inativo.FuncionarioId, enfileirados);
            Assert.DoesNotContain(api.Elenco.EmAdmissao.FuncionarioId, enfileirados);
            Assert.Contains(api.Elenco.MembroA.FuncionarioId, enfileirados);
            var primeira = enfileirados[0];
            Assert.Equal($"AVISO_PUBLICADO:aviso={id}:func={primeira}", (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'AVISO_PUBLICADO' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {primeira}"))!);
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = '{emailOriginal}' WHERE ID_FUNCIONARIO = {semEmail} AND EMAIL_CORPORATIVO IS NULL");
        }
    }

    [Fact]
    public async Task Rodar_a_tarefa_duas_vezes_nao_duplica_nem_um_e_mail()
    {
        var api = await ApiAsync();
        var id = await CriarAsync(api, Novo(Titulo("Duas vezes")));
        var tarefa = await TarefaPendenteAsync(api, id);

        await ExecutarAsync(api, tarefa);
        var primeira = (await PessoasComEmailAsync(api, id)).Count;
        await ExecutarAsync(api, tarefa);

        Assert.True(primeira > 0);
        Assert.Equal(primeira, (await PessoasComEmailAsync(api, id)).Count);
    }

    [Fact]
    public async Task A_tarefa_completa_a_linha_de_leitura_de_quem_entrou_depois_do_aviso_criado()
    {
        var api = await ApiAsync();
        var id = await CriarAsync(api, Novo(Titulo("Quem entrou depois")));
        var email = $"entrou.depois.{Interlocked.Increment(ref _sequencia)}@empresa.com.br";
        await api.Api.CriarUsuarioAsync(email, nivel: "RH");
        var pessoa = (int)await ContarAsync(api, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{email}'");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '2026-01-02' WHERE ID_FUNCIONARIO = {pessoa}");
        var antes = await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {pessoa}");

        await ExecutarAsync(api, await TarefaPendenteAsync(api, id));

        Assert.Equal(0, antes);
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM AVISOS_VISUALIZACOES WHERE ID_AVISO = {id} AND ID_FUNCIONARIO = {pessoa} AND VISUALIZADO = FALSE AND DATA_VISUALIZACAO IS NULL"));
        Assert.Contains(pessoa, await PessoasComEmailAsync(api, id));
    }

    [Fact]
    public async Task A_tarefa_nao_manda_nada_se_o_aviso_foi_reagendado_excluido_encerrado_ou_nao_esta_publicado()
    {
        var api = await ApiAsync();
        var cliente = await api.ClienteDoRhAgoraAsync();
        var reagendado = await CriarAsync(api, Novo(Titulo("Reagendado"), publicarEmUtc: "2026-10-09T11:00:00Z"));
        var tarefaAntiga = await TarefaPendenteAsync(api, reagendado);
        await cliente.PutAsJsonAsync($"{Rota}/{reagendado}", new { titulo = "Reagendado", conteudo = "Texto.", fixado = false, publicarEmUtc = "2026-10-10T12:00:00Z", expiraEm = (string?)null });
        var excluido = await CriarAsync(api, Novo(Titulo("Excluído"), publicarEmUtc: "2026-10-09T11:00:00Z"));
        var tarefaDoExcluido = await TarefaPendenteAsync(api, excluido);
        await cliente.DeleteAsync($"{Rota}/{excluido}");
        var encerrado = await CriarAsync(api, Novo(Titulo("Encerrado")));
        var tarefaDoEncerrado = await TarefaPendenteAsync(api, encerrado);
        await cliente.PostAsJsonAsync($"{Rota}/{encerrado}/encerrar", new { });
        var rascunho = await CriarAsync(api, Novo(Titulo("Rascunho"), publicar: false));

        foreach (var (aviso, tarefa) in new[] { (reagendado, tarefaAntiga), (excluido, tarefaDoExcluido), (encerrado, tarefaDoEncerrado), (rascunho, new TarefaParaExecutar(1, $"PUBLICAR_AVISO:aviso={rascunho}:em=2026-10-07T12:42:00Z", rascunho, null, null, 0)) })
        {
            await ExecutarAsync(api, tarefa);
            Assert.Empty(await PessoasComEmailAsync(api, aviso));
        }
    }

    [Fact]
    public async Task O_aviso_agendado_so_manda_na_data_da_chave_e_um_aviso_agendado_cuja_hora_nao_chegou_nao_manda()
    {
        var api = await ApiAsync();
        var id = await CriarAsync(api, Novo(Titulo("Ainda não é a hora"), publicarEmUtc: "2026-10-09T11:00:00Z"));

        await ExecutarAsync(api, await TarefaPendenteAsync(api, id));

        Assert.Empty(await PessoasComEmailAsync(api, id)); // o relógio está em 07/10: o aviso ainda é agendado

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 9, 11, 0, 5, DateTimeKind.Utc));
        await ExecutarAsync(api, await TarefaPendenteAsync(api, id));

        Assert.NotEmpty(await PessoasComEmailAsync(api, id));
    }

    // ---------------------------------------------------------------- a recuperação

    [Fact]
    public async Task A_recuperacao_agenda_o_que_ficou_para_tras_so_o_futuro_e_o_recente_e_nao_duplica()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO'");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS_VISUALIZACOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM AVISOS"); // os avisos publicados dos outros testes da classe também entrariam na recuperação
        var futuro = await SemearAsync(api, Titulo("Futuro sem tarefa"), true, "2026-10-09 08:00:00");
        var recente = await SemearAsync(api, Titulo("Recente sem tarefa"), true, "2026-10-05 08:00:00");
        var antigo = await SemearAsync(api, Titulo("Antigo sem tarefa"), true, "2026-09-01 08:00:00");
        var rascunho = await SemearAsync(api, Titulo("Rascunho sem tarefa"), false, null);

        await RecuperarAsync(api);
        var depoisDaPrimeira = await ContarAsync(api, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO'");
        await RecuperarAsync(api);

        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO' AND CHAVE_IDEMPOTENCIA = 'PUBLICAR_AVISO:aviso={futuro}:em=2026-10-09T11:00:00Z' AND SITUACAO = 'PENDENTE'"));
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO' AND CHAVE_IDEMPOTENCIA = 'PUBLICAR_AVISO:aviso={recente}:em=2026-10-05T11:00:00Z' AND SITUACAO = 'PENDENTE'"));
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO' AND REFERENCIA_ID IN ({antigo}, {rascunho})"));
        Assert.Equal((2L, 2L), (depoisDaPrimeira, await ContarAsync(api, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_AVISO'")));
    }

    private static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, bool enviado, string? publicacaoLocal)
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AVISOS (ID_FUNCIONARIO, TITULO, CONTEUDO, DATA_PUBLICACAO, TIPO_AVISO, FIXADO, ENVIADO) VALUES ({api.Elenco.Rh.FuncionarioId}, '{titulo}', 'Conteúdo.', '{publicacaoLocal ?? "2000-01-01 00:00:00"}', 'SIMPLES', 0, {(enviado ? 1 : 0)})");
        var id = (int)await ContarAsync(api, $"SELECT ID_AVISO FROM AVISOS WHERE TITULO = '{titulo}'");
        if (publicacaoLocal is null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE AVISOS SET DATA_PUBLICACAO = NULL WHERE ID_AVISO = {id}");
        }

        return id;
    }

    // ---------------------------------------------------------------- o texto

    [Fact]
    public void O_renderizador_escapa_o_html_do_titulo_e_da_previa_e_nao_leva_o_destinatario_na_auditoria()
    {
        var carga = JsonSerializer.SerializeToElement(new { nome = "Júlia <b>Nunes</b>", titulo = "Recesso <script>alert(1)</script>", previa = "Fechado de 24 a 26/12 & feriado." });

        var modelo = new AvisoPublicadoRenderizador().Renderizar(carga, "julia@empresa.com.br");

        Assert.Equal("AVISO_PUBLICADO", new AvisoPublicadoRenderizador().Tipo);
        Assert.Equal("Novo aviso: Recesso <script>alert(1)</script>", modelo.Assunto); // o assunto é texto puro
        Assert.Contains("Júlia <b>Nunes</b>", modelo.Texto, StringComparison.Ordinal);
        Assert.Contains("Recesso <script>alert(1)</script>", modelo.Texto, StringComparison.Ordinal);
        Assert.DoesNotContain("<script>", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("Recesso &lt;script&gt;alert(1)&lt;/script&gt;", modelo.Html, StringComparison.Ordinal);
        Assert.Contains("Fechado de 24 a 26/12 &amp; feriado.", modelo.Html, StringComparison.Ordinal);
        Assert.Equal(AuditoriaArea.Avisos, modelo.AreaDeAuditoria);
        Assert.DoesNotContain("julia@empresa.com.br", modelo.DescricaoDeAuditoria + modelo.RegistroAfetado, StringComparison.Ordinal);
    }

    [Fact]
    public void O_renderizador_sem_nome_nem_previa_ainda_monta_a_mensagem()
    {
        var modelo = new AvisoPublicadoRenderizador().Renderizar(JsonSerializer.SerializeToElement(new { titulo = "Aviso" }), "x@empresa.com.br");

        Assert.StartsWith("Olá, colaborador!", modelo.Texto, StringComparison.Ordinal);
        Assert.Equal("Novo aviso: Aviso", modelo.Assunto);
    }
}

using System.Text.Json;
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
/// A tarefa <c>PUBLICAR_HOLERITE:holerite={id}</c> (o e-mail do holerite) e o texto do e-mail, no banco REAL com o relógio em 2026-10-07T12:42Z. A tarefa relê o holerite e só manda se ele já está visível, é a maior versão já publicada da competência e a pessoa é
/// ativa e tem e-mail corporativo; roda duas vezes sem mandar duas. A mensagem diz que o holerite está disponível e nunca leva valor nenhum dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PublicarHoleriteTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PublicarHoleriteTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        api.Api.Armazenamento.Limpar();
        return api;
    }

    private static string Mes(int n) => (n % 12 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static string Ano(int n) => (2060 + n / 12).ToString(System.Globalization.CultureInfo.InvariantCulture);

    private static MultipartFormDataContent Formulario(IEnumerable<(string Campo, string Valor)> campos)
    {
        var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
        {
            formulario.Add(new StringContent(valor), campo);
        }

        var parte = new ByteArrayContent(ArquivosDeTeste.Pdf(2000));
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        formulario.Add(parte, "arquivo", "holerite.pdf");
        return formulario;
    }

    /// <summary>Envia pela API um holerite de FERIAS de uma competência própria deste teste e devolve o id.</summary>
    private static async Task<int> EnviarAsync(ApiDeAcesso api, int funcionarioId, string mes, string ano, string? publicarEm = null)
    {
        var campos = new List<(string, string)> { ("mes", mes), ("ano", ano), ("tipo", "FERIAS") };
        if (publicarEm is not null)
        {
            campos.Add(("publicarEm", publicarEm));
        }

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/funcionarios/{funcionarioId}/holerites", Formulario(campos));
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static async Task<int> SubstituirAsync(ApiDeAcesso api, int holeriteId, string? publicarEm = null)
    {
        var campos = publicarEm is null ? [] : new List<(string, string)> { ("publicarEm", publicarEm) };
        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/holerites/{holeriteId}/versoes", Formulario(campos));
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static TarefaParaExecutar Tarefa(int holeriteId) => new(holeriteId, AgendamentoDoHolerite.Chave(holeriteId), holeriteId, null, null, 0);

    private static async Task ExecutarAsync(ApiDeAcesso api, int holeriteId)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoHolerite.Tipo).ExecutarAsync(Tarefa(holeriteId), Ct);
    }

    private static async Task RecuperarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoHolerite.Tipo).RecuperarAsync(Ct);
    }

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static Task<long> EmailsAsync(ApiDeAcesso api, int holeriteId) => ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {holeriteId}");

    // ---------------------------------------------------------------- a tarefa

    [Fact]
    public async Task A_tarefa_manda_um_e_mail_para_a_pessoa_e_rodar_duas_vezes_nao_duplica()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var n = Interlocked.Increment(ref _sequencia);
        var id = await EnviarAsync(api, pessoa.FuncionarioId, Mes(n), Ano(n));
        var antes = await EmailsAsync(api, id);

        await ExecutarAsync(api, id);
        await ExecutarAsync(api, id);

        Assert.Equal(0, antes); // o pedido HTTP não enfileira nada
        Assert.Equal(1, await EmailsAsync(api, id));
        Assert.Equal(pessoa.FuncionarioId, await ContarAsync(api, $"SELECT ID_FUNCIONARIO FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {id}"));
        Assert.Equal($"HOLERITE_PUBLICADO:holerite={id}", (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CHAVE_IDEMPOTENCIA FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {id}"))!);
        var carga = JsonSerializer.Deserialize<JsonElement>((string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {id}"))!);
        Assert.Equal(1, carga.GetProperty("versao").GetInt32());
        Assert.Equal($"de férias de {new System.Globalization.CultureInfo("pt-BR").DateTimeFormat.GetMonthName(int.Parse(Mes(n)))} de {Ano(n)}", carga.GetProperty("competencia").GetString());
    }

    [Fact]
    public async Task O_holerite_agendado_so_manda_a_partir_do_dia()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _sequencia);
        var id = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Mes(n), Ano(n), "2026-11-06");

        await ExecutarAsync(api, id);
        var antes = await EmailsAsync(api, id);
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc));
        await ExecutarAsync(api, id);

        Assert.Equal((0, 1), (antes, await EmailsAsync(api, id)));
    }

    [Fact]
    public async Task Com_uma_versao_mais_nova_ja_publicada_a_antiga_nao_manda_e_a_nova_manda_como_corrigida()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _sequencia);
        var v1 = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Mes(n), Ano(n));
        var v2 = await SubstituirAsync(api, v1);

        await ExecutarAsync(api, v1);
        await ExecutarAsync(api, v2);

        Assert.Equal((0, 1), (await EmailsAsync(api, v1), await EmailsAsync(api, v2)));
        var carga = JsonSerializer.Deserialize<JsonElement>((string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CARGA FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {v2}"))!);
        Assert.Equal(2, carga.GetProperty("versao").GetInt32());
    }

    [Fact]
    public async Task Com_a_nova_versao_ainda_agendada_a_antiga_continua_valendo_e_manda()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _sequencia);
        var v1 = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Mes(n), Ano(n));
        var v2 = await SubstituirAsync(api, v1, "2026-11-06");

        await ExecutarAsync(api, v1);
        await ExecutarAsync(api, v2); // o relógio ainda está em 07/10: a v2 não está visível

        Assert.Equal((1, 0), (await EmailsAsync(api, v1), await EmailsAsync(api, v2)));
    }

    [Fact]
    public async Task Pessoa_inativa_ou_sem_e_mail_corporativo_nao_recebe_e_o_holerite_que_nao_existe_e_ignorado()
    {
        var api = await ApiAsync();
        var inativaDepois = api.Elenco.DeOutroSetor.FuncionarioId;
        var semEmail = api.Elenco.MembroB.FuncionarioId;
        var n = Interlocked.Increment(ref _sequencia);
        var daInativa = await EnviarAsync(api, inativaDepois, Mes(n), Ano(n));
        var m = Interlocked.Increment(ref _sequencia);
        var semEmailId = await EnviarAsync(api, semEmail, Mes(m), Ano(m));
        var emailOriginal = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {semEmail}"))!;
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {inativaDepois}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {semEmail}");
        try
        {
            await ExecutarAsync(api, daInativa);
            await ExecutarAsync(api, semEmailId);
            await ExecutarAsync(api, int.MaxValue);

            Assert.Equal((0, 0), (await EmailsAsync(api, daInativa), await EmailsAsync(api, semEmailId)));
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE WHERE ID_FUNCIONARIO = {inativaDepois}");
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = '{emailOriginal}' WHERE ID_FUNCIONARIO = {semEmail} AND EMAIL_CORPORATIVO IS NULL");
        }
    }

    // ---------------------------------------------------------------- a recuperação

    [Fact]
    public async Task A_recuperacao_agenda_os_agendados_e_os_publicados_nos_ultimos_7_dias_sem_tarefa_e_nao_duplica()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var n = Interlocked.Increment(ref _sequencia);
        var agendado = await EnviarAsync(api, pessoa, Mes(n), Ano(n), "2026-11-06");
        var m = Interlocked.Increment(ref _sequencia);
        var recente = await EnviarAsync(api, pessoa, Mes(m), Ano(m));
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO HOLERITES (ID_FUNCIONARIO, MES, ANO, DATA_PUBLICACAO, CAMINHO_ARQUIVO, TIPO_HOLERITE, VERSAO) VALUES ({pessoa}, 3, 2026, '2026-09-01 08:00:00', 'holerites/antigo.pdf', 'MENSAL', 1)");
        var antigo = (int)await ContarAsync(api, $"SELECT ID_HOLERITE FROM HOLERITES WHERE ID_FUNCIONARIO = {pessoa} AND ANO = 2026 AND MES = 3 AND TIPO_HOLERITE = 'MENSAL'");
        await Sql.ExecutarAsync(api.Conexao, $"DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND REFERENCIA_ID IN ({agendado}, {recente}, {antigo})");

        await RecuperarAsync(api);
        var depoisDaPrimeira = await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND REFERENCIA_ID IN ({agendado}, {recente}, {antigo})");
        await RecuperarAsync(api);

        Assert.Equal(2, depoisDaPrimeira);
        Assert.Equal(2, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND REFERENCIA_ID IN ({agendado}, {recente}, {antigo})"));
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND REFERENCIA_ID = {antigo}"));
        Assert.Equal("2026-11-06 03:00:00", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT EXECUTAR_APOS FROM TAREFAS_AGENDADAS WHERE CHAVE_IDEMPOTENCIA = 'PUBLICAR_HOLERITE:holerite={agendado}'")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
    }

    // ---------------------------------------------------------------- o texto

    [Fact]
    public void O_e_mail_diz_que_o_holerite_esta_disponivel_e_a_versao_corrigida_diz_corrigido_sem_valor_nenhum()
    {
        var primeira = new HoleritePublicadoRenderizador().Renderizar(JsonSerializer.SerializeToElement(new { nome = "Júlia <b>Nunes</b>", competencia = "mensal de outubro de 2026", versao = 1 }), "julia@empresa.com.br");
        var corrigida = new HoleritePublicadoRenderizador().Renderizar(JsonSerializer.SerializeToElement(new { nome = "Júlia", competencia = "mensal de outubro de 2026", versao = 2 }), "julia@empresa.com.br");

        Assert.Equal("HOLERITE_PUBLICADO", new HoleritePublicadoRenderizador().Tipo);
        Assert.Equal("Seu holerite mensal de outubro de 2026 está disponível", primeira.Assunto);
        Assert.Equal("Olá, Júlia <b>Nunes</b>!\n\nO seu holerite mensal de outubro de 2026 já está disponível no RH-System.\n\nAbra o RH-System, em Holerites, para consultar e baixar o PDF.", primeira.Texto);
        Assert.Contains("&lt;b&gt;Nunes&lt;/b&gt;", primeira.Html, StringComparison.Ordinal);
        Assert.DoesNotContain("<b>", primeira.Html, StringComparison.Ordinal);
        Assert.Equal("Holerite corrigido: mensal de outubro de 2026", corrigida.Assunto);
        Assert.Contains("foi corrigido e a nova versão já está disponível", corrigida.Texto, StringComparison.Ordinal);
        Assert.Equal(AuditoriaArea.Holerites, primeira.AreaDeAuditoria);
        Assert.DoesNotContain("julia@empresa.com.br", primeira.DescricaoDeAuditoria + primeira.RegistroAfetado, StringComparison.Ordinal);
    }

    [Fact]
    public void Sem_nome_nem_competencia_ainda_monta_a_mensagem()
    {
        var modelo = new HoleritePublicadoRenderizador().Renderizar(JsonSerializer.SerializeToElement(new { }), "x@empresa.com.br");

        Assert.StartsWith("Olá, colaborador!", modelo.Texto, StringComparison.Ordinal);
        Assert.Equal("Seu holerite do mês está disponível", modelo.Assunto);
    }
}

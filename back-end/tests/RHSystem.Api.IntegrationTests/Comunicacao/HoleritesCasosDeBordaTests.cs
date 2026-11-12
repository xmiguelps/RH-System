using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Comunicacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// Os casos de borda do envio, da nova versão, do lote e da tarefa do e-mail de holerite que os testes principais não pegavam (achados pela mutação do incremento 4b): a corrida entre dois cliques (a leitura que já ficou velha quando a chave única do banco recusa), o dia de hoje no
/// horário do Brasil, o evento de resumo do lote, os itens do lote malformados, a janela de recuperação da tarefa e a versão atual de cada TIPO e de cada PESSOA. Cada teste usa a sua pessoa (admitida em 2026-01-02) e uma competência de FERIAS só dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class HoleritesCasosDeBordaTests(MySqlFixture mysql)
{
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(HoleritesCasosDeBordaTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        api.Api.Armazenamento.Limpar();
        return api;
    }

    private static Task<PessoaDoTeste> NovaAsync(ApiDeAcesso api) => api.NovaPessoaAsync("borda");

    /// <summary>Uma competência de FERIAS no futuro, só deste teste (o ano 2090 em diante, um mês por chamada).</summary>
    private static (int Mes, int Ano) Competencia()
    {
        var n = Interlocked.Increment(ref _sequencia);
        return (n % 12 + 1, 2090 + n / 12);
    }

    private static async Task<int> SemearAsync(ApiDeAcesso api, int funcionarioId, int ano, int mes, string tipo, int versao, string publicacaoLocal)
    {
        var caminho = CaminhosDeArquivo.Novo("holerites", ".pdf");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO HOLERITES (ID_FUNCIONARIO, MES, ANO, DATA_PUBLICACAO, CAMINHO_ARQUIVO, TIPO_HOLERITE, VERSAO) VALUES ({funcionarioId}, {mes}, {ano}, '{publicacaoLocal}', '{caminho}', '{tipo}', {versao})");
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_HOLERITE FROM HOLERITES WHERE CAMINHO_ARQUIVO = '{caminho}'");
    }

    private static MultipartFormDataContent Formulario(IEnumerable<(string Campo, string Valor)> campos, int tamanho = 1500)
    {
        var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
        {
            formulario.Add(new StringContent(valor), campo);
        }

        var parte = new ByteArrayContent(ArquivosDeTeste.Pdf(tamanho));
        parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        formulario.Add(parte, "arquivo", "holerite.pdf");
        return formulario;
    }

    private static List<(string, string)> Envio(int mes, int ano, string? publicarEm = null)
    {
        var campos = new List<(string, string)> { ("mes", mes.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("ano", ano.ToString(System.Globalization.CultureInfo.InvariantCulture)), ("tipo", "FERIAS") };
        if (publicarEm is not null)
        {
            campos.Add(("publicarEm", publicarEm));
        }

        return campos;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, int pessoa, IEnumerable<(string, string)> campos) => await cliente.PostAsync($"/api/rh/funcionarios/{pessoa}/holerites", Formulario(campos));

    private static async Task<HttpResponseMessage> SubstituirAsync(HttpClient cliente, int holerite, IEnumerable<(string, string)>? campos = null) => await cliente.PostAsync($"/api/rh/holerites/{holerite}/versoes", Formulario(campos ?? []));

    // ---------------------------------------------------------------- a competência contra a pessoa, na nova versão

    [Fact]
    public async Task A_nova_versao_de_um_holerite_de_pessoa_desligada_antes_da_competencia_e_422_com_a_regra_do_banco()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var id = await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-02-15' WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");

        var resposta = await SubstituirAsync(await api.ClienteDoRhAgoraAsync(), id);

        Assert.Equal(422, (int)resposta.StatusCode);
        var erro = await CorpoAsync(resposta);
        Assert.Equal(("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", RegrasDoBancoDaComunicacao.HoleritePosteriorAoDesligamento), (erro.GetProperty("codigo").GetString(), erro.GetProperty("regraDoBanco").GetString()));
    }

    // ---------------------------------------------------------------- a corrida: a leitura que ficou velha quando a chave única recusa

    /// <summary>A consulta que ainda não viu o holerite que outra aba acabou de gravar: nenhuma versão no envio e "esta é a versão atual" na nova versão.</summary>
    private sealed class ConsultaComLeituraVelha(IConsultaDeHolerites real) : IConsultaDeHolerites
    {
        public Task<PessoaDoHolerite?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) => real.ObterPessoaAsync(funcionarioId, ct);

        public Task<int?> MaiorVersaoAsync(int funcionarioId, int ano, int mes, TipoDeHolerite tipo, CancellationToken ct) => Task.FromResult<int?>(null);

        public async Task<HoleriteLido?> ObterAsync(int holeriteId, CancellationToken ct) => await real.ObterAsync(holeriteId, ct) is { } lido ? lido with { VersaoAtual = true } : null;

        public Task<(IReadOnlyList<HoleriteDaListaDoRh> Itens, int Total, HoleriteDaListaDoRh? UltimoPublicado)> ListarDaPessoaAsync(int funcionarioId, FiltroDeHoleritesRh filtro, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct) =>
            real.ListarDaPessoaAsync(funcionarioId, filtro, parametros, agoraUtc, ct);

        public Task<HoleriteParaDownload?> ObterParaDownloadDoRhAsync(int holeriteId, CancellationToken ct) => real.ObterParaDownloadDoRhAsync(holeriteId, ct);
    }

    private static async Task<HttpClient> ComLeituraVelhaAsync(ApiDeAcesso api)
    {
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IConsultaDeHolerites>();
            servicos.AddScoped<IConsultaDeHolerites>(sp => new ConsultaComLeituraVelha(ActivatorUtilities.CreateInstance<ConsultaDeHolerites>(sp)));
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        return cliente;
    }

    [Fact]
    public async Task Na_corrida_do_envio_a_chave_unica_do_banco_da_o_409_HOLERITE_JA_EXISTE_com_a_sugestao_de_nova_versao()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var (mes, ano) = Competencia();
        await SemearAsync(api, pessoa.FuncionarioId, ano, mes, "FERIAS", 1, "2026-10-01 10:00:00");

        var resposta = await EnviarAsync(await ComLeituraVelhaAsync(api), pessoa.FuncionarioId, Envio(mes, ano));

        Assert.Equal(409, (int)resposta.StatusCode);
        var erro = await CorpoAsync(resposta);
        Assert.Equal(("HOLERITE_JA_EXISTE", MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Ferias, mes, ano).Detalhe), (erro.GetProperty("codigo").GetString(), erro.GetProperty("detail").GetString()));
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Equal(0, api.Api.Armazenamento.Quantidade); // o arquivo da tentativa foi apagado
    }

    [Fact]
    public async Task Na_corrida_da_nova_versao_a_chave_unica_do_banco_da_o_409_HOLERITE_VERSAO_DESATUALIZADA()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var (mes, ano) = Competencia();
        var v1 = await SemearAsync(api, pessoa.FuncionarioId, ano, mes, "FERIAS", 1, "2026-10-01 10:00:00");
        await SemearAsync(api, pessoa.FuncionarioId, ano, mes, "FERIAS", 2, "2026-10-02 10:00:00");

        var resposta = await SubstituirAsync(await ComLeituraVelhaAsync(api), v1);

        Assert.Equal(409, (int)resposta.StatusCode);
        Assert.Equal("HOLERITE_VERSAO_DESATUALIZADA", (await CorpoAsync(resposta)).GetProperty("codigo").GetString());
        Assert.Equal(2, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    private sealed record Item(int FuncionarioId, string NomeDoArquivo);

    private static MultipartFormDataContent Lote(IEnumerable<(string Campo, string Valor)> campos, string? itens, IEnumerable<(string Nome, byte[] Bytes)> arquivos)
    {
        var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
        {
            formulario.Add(new StringContent(valor), campo);
        }

        if (itens is not null)
        {
            formulario.Add(new StringContent(itens, Encoding.UTF8, "application/json"), "itens");
        }

        foreach (var (nome, bytes) in arquivos)
        {
            var parte = new ByteArrayContent(bytes);
            parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            formulario.Add(parte, "arquivos", nome);
        }

        return formulario;
    }

    private static string Itens(params (object Funcionario, string Nome)[] itens) => JsonSerializer.Serialize(itens.Select(i => new { funcionarioId = i.Funcionario, nomeDoArquivo = i.Nome }));

    private static async Task<HttpResponseMessage> LoteAsync(HttpClient cliente, int mes, int ano, string? itens, params (string Nome, byte[] Bytes)[] arquivos) =>
        await cliente.PostAsync("/api/rh/holerites/lote", Lote(Envio(mes, ano), itens, arquivos));

    [Fact]
    public async Task Na_corrida_do_lote_o_item_que_colide_com_a_chave_unica_falha_sozinho_sem_a_sugestao_de_versao_e_o_proximo_item_grava()
    {
        var api = await ApiAsync();
        var a = await NovaAsync(api);
        var b = await NovaAsync(api);
        var (mes, ano) = Competencia();
        await SemearAsync(api, a.FuncionarioId, ano, mes, "FERIAS", 1, "2026-10-01 10:00:00");

        var resposta = await LoteAsync(await ComLeituraVelhaAsync(api), mes, ano, Itens((a.FuncionarioId, "a.pdf"), (b.FuncionarioId, "b.pdf")), ("a.pdf", ArquivosDeTeste.Pdf(1200)), ("b.pdf", ArquivosDeTeste.Pdf(1300)));

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal((2, 1, 1), (corpo.GetProperty("total").GetInt32(), corpo.GetProperty("enviados").GetInt32(), corpo.GetProperty("comErro").GetInt32()));
        Assert.Equal((false, "HOLERITE_JA_EXISTE", MensagensDaComunicacao.HoleriteJaExisteNoLote(TipoDeHolerite.Ferias, mes, ano)), (itens[0].GetProperty("sucesso").GetBoolean(), itens[0].GetProperty("codigoDoErro").GetString(), itens[0].GetProperty("mensagemDoErro").GetString()));
        Assert.True(itens[1].GetProperty("sucesso").GetBoolean());
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_FUNCIONARIO = {b.FuncionarioId}"));
    }

    // ---------------------------------------------------------------- o dia de hoje é o do Brasil

    [Fact]
    public async Task Perto_da_meia_noite_UTC_o_dia_de_hoje_e_o_do_Brasil_no_envio_e_na_nova_versao_e_o_evento_diz_o_nome_da_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var (mes, ano) = Competencia();
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 2, 0, 0, DateTimeKind.Utc)); // ainda é 23:00 de 07/10 no Brasil
        var rh = await api.ClienteDoRhAgoraAsync();

        var envio = await EnviarAsync(rh, pessoa.FuncionarioId, Envio(mes, ano, publicarEm: "2026-10-07"));
        var id = (await CorpoAsync(envio)).GetProperty("id").GetInt32();
        var nova = await SubstituirAsync(rh, id, [("publicarEm", "2026-10-07")]);

        Assert.Equal((201, 201), ((int)envio.StatusCode, (int)nova.StatusCode));
        Assert.Equal("PUBLICADO", (await CorpoAsync(nova)).GetProperty("situacao").GetString());
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "HOLERITES", id));
        Assert.Equal($"Holerite de {pessoa.Nome}", evento.RegistroAfetado);
    }

    // ---------------------------------------------------------------- o resumo do lote

    [Fact]
    public async Task O_evento_de_resumo_do_lote_conta_os_enviados_e_os_com_erro_e_e_uma_criacao_sem_registro_de_uma_linha()
    {
        var api = await ApiAsync();
        var a = await NovaAsync(api);
        var b = await NovaAsync(api);
        var (mes, ano) = Competencia();

        var resposta = await LoteAsync(await api.ClienteDoRhAgoraAsync(), mes, ano, Itens((a.FuncionarioId, "a.pdf"), (b.FuncionarioId, "b.pdf")), ("a.pdf", ArquivosDeTeste.Pdf(1200)), ("b.pdf", Encoding.UTF8.GetBytes("isto não é um pdf, mas tem mais de oito bytes")));

        Assert.Equal(200, (int)resposta.StatusCode);
        var esperado = $"Enviou um lote de 2 holerites {MensagensDaComunicacao.TextoDaCompetencia(TipoDeHolerite.Ferias, mes, ano)}: 1 enviados e 1 com erro.";
        var evento = (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId)).Single(e => e.Entidade is null && e.Descricao == esperado);
        Assert.Equal((AuditoriaArea.Holerites, AuditoriaAcao.Criou, "Lote de holerites"), (evento.Area, evento.Acao, evento.RegistroAfetado));
    }

    // ---------------------------------------------------------------- os itens do lote

    private async Task<JsonElement> ErrosDoLoteAsync(ApiDeAcesso api, string? itens, params (string Nome, byte[] Bytes)[] arquivos)
    {
        var (mes, ano) = Competencia();
        var resposta = await LoteAsync(await api.ClienteDoRhAgoraAsync(), mes, ano, itens, arquivos);
        Assert.Equal(400, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("errors");
    }

    [Fact]
    public async Task Itens_sem_nenhum_arquivo_e_erro_do_campo_arquivos_e_nao_so_de_itens_sem_arquivo()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        var erros = await ErrosDoLoteAsync(api, Itens((pessoa.FuncionarioId, "a.pdf")));

        Assert.True(erros.TryGetProperty("arquivos", out var arquivos));
        Assert.Equal("Envie pelo menos um PDF no campo arquivos.", arquivos[0].GetString());
    }

    [Fact]
    public async Task A_lista_de_itens_vazia_e_erro_do_campo_itens()
    {
        var api = await ApiAsync();

        var erros = await ErrosDoLoteAsync(api, "[]", ("a.pdf", ArquivosDeTeste.Pdf(1200)));

        Assert.Equal("Envie de 1 a 200 itens.", erros.GetProperty("itens")[0].GetString());
    }

    [Fact]
    public async Task A_mensagem_dos_itens_sem_arquivo_lista_so_os_cinco_primeiros_nomes()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);

        var erros = await ErrosDoLoteAsync(api, Itens((pessoa.FuncionarioId, "f1.pdf"), (pessoa.FuncionarioId, "f2.pdf"), (pessoa.FuncionarioId, "f3.pdf"), (pessoa.FuncionarioId, "f4.pdf"), (pessoa.FuncionarioId, "f5.pdf"), (pessoa.FuncionarioId, "f6.pdf")), ("outro.pdf", ArquivosDeTeste.Pdf(1200)));

        Assert.Equal("Há itens sem arquivo: f1.pdf, f2.pdf, f3.pdf, f4.pdf, f5.pdf.", erros.GetProperty("itens")[0].GetString());
        Assert.Equal("Há arquivos sem item: outro.pdf.", erros.GetProperty("arquivos")[0].GetString());
    }

    [Theory]
    [InlineData("""[{"funcionarioId":"5","nomeDoArquivo":"a.pdf"}]""")]
    [InlineData("""[{"funcionarioId":5,"nomeDoArquivo":" "}]""")]
    [InlineData("""[{"funcionarioId":5,"nomeDoArquivo":5}]""")]
    [InlineData("""[{"funcionarioId":5}]""")]
    [InlineData("""["a.pdf"]""")]
    public async Task Item_malformado_e_400_no_campo_itens_sem_estourar(string itens)
    {
        var api = await ApiAsync();

        var erros = await ErrosDoLoteAsync(api, itens, ("a.pdf", ArquivosDeTeste.Pdf(1200)));

        Assert.Equal("Cada item precisa de funcionarioId (inteiro) e nomeDoArquivo (texto).", erros.GetProperty("itens")[0].GetString());
    }

    [Fact]
    public async Task Nomes_de_arquivo_que_so_diferem_na_caixa_sao_dois_arquivos_e_os_dois_itens_gravam()
    {
        var api = await ApiAsync();
        var a = await NovaAsync(api);
        var b = await NovaAsync(api);
        var (mes, ano) = Competencia();

        var resposta = await LoteAsync(await api.ClienteDoRhAgoraAsync(), mes, ano, Itens((a.FuncionarioId, "holerite.pdf"), (b.FuncionarioId, "HOLERITE.pdf")), ("holerite.pdf", ArquivosDeTeste.Pdf(1200)), ("HOLERITE.pdf", ArquivosDeTeste.Pdf(1300)));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(2, (await CorpoAsync(resposta)).GetProperty("enviados").GetInt32());
    }

    // ---------------------------------------------------------------- a versão atual de cada TIPO e de cada PESSOA, e o id que não existe

    [Fact]
    public async Task A_nova_versao_do_mensal_vale_mesmo_com_uma_versao_2_de_FERIAS_na_mesma_competencia_e_com_a_versao_2_de_outra_pessoa()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var outra = await NovaAsync(api);
        var mensal = await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "FERIAS", 2, "2026-04-06 10:00:00");
        await SemearAsync(api, outra.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");
        await SemearAsync(api, outra.FuncionarioId, 2026, 3, "MENSAL", 2, "2026-04-06 10:00:00");
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await SubstituirAsync(rh, mensal);

        Assert.Equal(201, (int)resposta.StatusCode); // a versão 1 do mensal é a atual do mensal DELA: o FERIAS e a outra pessoa não contam
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((2, "MENSAL"), (corpo.GetProperty("versao").GetInt32(), corpo.GetProperty("tipo").GetString()));
    }

    [Fact]
    public async Task Holerite_de_id_zero_nao_existe_e_a_nova_versao_dele_e_404()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");

        var resposta = await SubstituirAsync(await api.ClienteDoRhAgoraAsync(), 0);

        Assert.Equal(404, (int)resposta.StatusCode);
    }

    // ---------------------------------------------------------------- a tarefa do e-mail

    private static TarefaParaExecutar Tarefa(int holeriteId) => new(holeriteId, AgendamentoDoHolerite.Chave(holeriteId), holeriteId, null, null, 0);

    private static async Task ExecutarAsync(ApiDeAcesso api, int holeriteId)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoHolerite.Tipo).ExecutarAsync(Tarefa(holeriteId), CancellationToken.None);
    }

    private static async Task RecuperarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == AgendamentoDoHolerite.Tipo).RecuperarAsync(CancellationToken.None);
    }

    private static Task<long> TarefasAsync(ApiDeAcesso api, int holeriteId) =>
        Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND CHAVE_IDEMPOTENCIA = '{AgendamentoDoHolerite.Chave(holeriteId)}'");

    [Fact]
    public async Task A_versao_1_do_mensal_ainda_manda_o_e_mail_quando_a_versao_2_mais_nova_publicada_e_de_FERIAS()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api);
        var mensal = await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-04-05 10:00:00");
        await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "FERIAS", 2, "2026-04-06 10:00:00");

        await ExecutarAsync(api, mensal);

        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {mensal}"));
    }

    [Fact]
    public async Task A_recuperacao_agenda_o_que_foi_publicado_nos_ultimos_7_dias_inclusive_o_limite_e_deixa_o_mais_antigo()
    {
        var api = await ApiAsync(); // agora: 2026-10-07 09:42:00 no Brasil
        var pessoa = await NovaAsync(api);
        var recente = await SemearAsync(api, pessoa.FuncionarioId, 2026, 3, "MENSAL", 1, "2026-10-04 10:00:00");
        var noLimite = await SemearAsync(api, pessoa.FuncionarioId, 2026, 4, "MENSAL", 1, "2026-09-30 09:42:00"); // exatamente 7 dias antes
        var antigo = await SemearAsync(api, pessoa.FuncionarioId, 2026, 5, "MENSAL", 1, "2026-09-29 09:42:00");
        var agendado = await SemearAsync(api, pessoa.FuncionarioId, 2026, 6, "MENSAL", 1, "2026-10-20 00:00:00");

        await RecuperarAsync(api);

        Assert.Equal((1, 1, 0, 1), (await TarefasAsync(api, recente), await TarefasAsync(api, noLimite), await TarefasAsync(api, antigo), await TarefasAsync(api, agendado)));
    }
}

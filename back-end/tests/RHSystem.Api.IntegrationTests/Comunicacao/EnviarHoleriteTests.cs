using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// O envio individual (<c>POST /api/rh/funcionarios/{id}/holerites</c>) e a nova versão (<c>POST /api/rh/holerites/{id}/versoes</c>), no banco REAL com o relógio em 2026-10-07T12:42Z e o armazenamento em memória. O arquivo só é guardado depois de a competência e o
/// resto passarem, e é apagado se a gravação falhar (o banco nunca apaga holerite: uma linha sem arquivo ficaria para sempre). A maior parte dos testes usa o tipo <c>FERIAS</c> com uma competência futura própria (a única que pode ser futura), para cada teste ter a sua
/// e não esbarrar no <c>HOLERITE_JA_EXISTE</c> dos outros.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EnviarHoleriteTests(MySqlFixture mysql)
{
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync(int? diaDoPagamento = 6)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EnviarHoleriteTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        api.Api.Armazenamento.Limpar();
        if (diaDoPagamento is { } dia)
        {
            await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE CONFIGURACOES_EMPRESA SET DIA_PAGAMENTO = {dia}");
        }
        else
        {
            await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        }

        return api;
    }

    /// <summary>Uma competência futura só deste teste (FERIAS aceita futuro): do ano 2040 em diante, um mês por chamada.</summary>
    private static (string Mes, string Ano) Competencia()
    {
        var n = Interlocked.Increment(ref _sequencia);
        return ((n % 12 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), (2040 + n / 12).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private static List<(string Campo, string Valor)> Campos(string mes, string ano, string tipo = "FERIAS", string? publicarEm = null, string? noDiaDoPagamento = null)
    {
        var campos = new List<(string, string)> { ("mes", mes), ("ano", ano), ("tipo", tipo) };
        if (publicarEm is not null)
        {
            campos.Add(("publicarEm", publicarEm));
        }

        if (noDiaDoPagamento is not null)
        {
            campos.Add(("noDiaDoPagamento", noDiaDoPagamento));
        }

        return campos;
    }

    private static MultipartFormDataContent Formulario(IEnumerable<(string Campo, string Valor)> campos, byte[]? arquivo, string nome = "holerite.pdf", string campoDoArquivo = "arquivo")
    {
        var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
        {
            formulario.Add(new StringContent(valor), campo);
        }

        if (arquivo is not null)
        {
            var parte = new ByteArrayContent(arquivo);
            parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            formulario.Add(parte, campoDoArquivo, nome);
        }

        return formulario;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(ApiDeAcesso api, int funcionarioId, HttpContent formulario) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/funcionarios/{funcionarioId}/holerites", formulario);

    private static async Task<HttpResponseMessage> SubstituirAsync(ApiDeAcesso api, int holeriteId, HttpContent formulario) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/holerites/{holeriteId}/versoes", formulario);

    private static async Task<int> EnviarComSucessoAsync(ApiDeAcesso api, int funcionarioId, string mes, string ano, string tipo = "FERIAS", string? publicarEm = null)
    {
        var resposta = await EnviarAsync(api, funcionarioId, Formulario(Campos(mes, ano, tipo, publicarEm), ArquivosDeTeste.Pdf(2000)));
        Assert.Equal(201, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
    }

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    private static Task<long> LinhasAsync(ApiDeAcesso api, int funcionarioId, string mes, string ano, string tipo = "FERIAS") =>
        ContarAsync(api, $"SELECT COUNT(*) FROM HOLERITES WHERE ID_FUNCIONARIO = {funcionarioId} AND ANO = {ano} AND MES = {mes} AND TIPO_HOLERITE = '{tipo}'");

    // ---------------------------------------------------------------- publicar agora

    [Fact]
    public async Task Publicar_agora_guarda_o_pdf_em_holerites_grava_a_linha_com_a_data_do_relogio_agenda_a_tarefa_do_e_mail_e_audita_sem_o_caminho()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA;
        var (mes, ano) = Competencia();
        var pdf = ArquivosDeTeste.Pdf(3000);

        var resposta = await EnviarAsync(api, pessoa.FuncionarioId, Formulario(Campos(mes, ano), pdf, "holerite-diego.pdf"));
        var texto = await resposta.Content.ReadAsStringAsync();
        var holerite = JsonSerializer.Deserialize<JsonElement>(texto);

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = holerite.GetProperty("id").GetInt32();
        Assert.Equal((pessoa.FuncionarioId, "FERIAS", int.Parse(mes), int.Parse(ano), 1, true, "PUBLICADO", "2026-10-07T12:42:00Z"), (holerite.GetProperty("funcionarioId").GetInt32(), holerite.GetProperty("tipo").GetString(), holerite.GetProperty("mes").GetInt32(), holerite.GetProperty("ano").GetInt32(), holerite.GetProperty("versao").GetInt32(), holerite.GetProperty("versaoAtual").GetBoolean(), holerite.GetProperty("situacao").GetString(), holerite.GetProperty("publicacaoEmUtc").GetString()));
        Assert.DoesNotContain("holerites/", texto, StringComparison.Ordinal); // o caminho do arquivo nunca sai
        var caminho = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM HOLERITES WHERE ID_HOLERITE = {id}"))!;
        Assert.StartsWith("holerites/", caminho, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", caminho, StringComparison.Ordinal);
        Assert.Equal(caminho, Assert.Single(api.Api.Armazenamento.Gravados));
        Assert.Equal("application/pdf", api.Api.Armazenamento.TipoDeConteudo(caminho));
        Assert.Equal(pdf, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal("2026-10-07 09:42:00", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_PUBLICACAO FROM HOLERITES WHERE ID_HOLERITE = {id}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND CHAVE_IDEMPOTENCIA = 'PUBLICAR_HOLERITE:holerite={id}' AND SITUACAO = 'PENDENTE'"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "HOLERITES", id));
        Assert.Equal((AuditoriaArea.Holerites, AuditoriaAcao.Criou), (evento.Area, evento.Acao));
        Assert.Contains("(versão 1)", evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain("holerites/", evento.Descricao + evento.RegistroAfetado, StringComparison.Ordinal);
        Assert.Equal(0, await ContarAsync(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'HOLERITE_PUBLICADO' AND REFERENCIA_ID = {id}")); // o e-mail sai pela tarefa, nunca no pedido
    }

    [Fact]
    public async Task Publicar_com_a_data_de_hoje_e_publicar_agora_e_a_mesma_pessoa_pode_ter_tipos_diferentes_na_mesma_competencia()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.GestorDoFinanceiro.FuncionarioId; // competência passada e própria deste teste: fevereiro de 2026

        var hoje = await EnviarAsync(api, pessoa, Formulario(Campos("2", "2026", "FERIAS", "2026-10-07"), ArquivosDeTeste.Pdf()));
        var outroTipo = await EnviarAsync(api, pessoa, Formulario(Campos("2", "2026", "DECIMO_TERCEIRO"), ArquivosDeTeste.Pdf()));
        var mensal = await EnviarAsync(api, pessoa, Formulario(Campos("2", "2026", "MENSAL"), ArquivosDeTeste.Pdf()));

        Assert.Equal([201, 201, 201], new[] { hoje, outroTipo, mensal }.Select(r => (int)r.StatusCode));
        Assert.Equal(("PUBLICADO", "2026-10-07T12:42:00Z"), ((await CorpoAsync(hoje)).GetProperty("situacao").GetString(), (await CorpoAsync(hoje)).GetProperty("publicacaoEmUtc").GetString()));
    }

    // ---------------------------------------------------------------- agendar

    [Fact]
    public async Task Agendar_numa_data_futura_publica_no_comeco_do_dia_em_menos_3_horas_e_a_tarefa_espera_ate_la()
    {
        var api = await ApiAsync();
        var (mes, ano) = Competencia();

        var resposta = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Formulario(Campos(mes, ano, "FERIAS", "2026-11-06"), ArquivosDeTeste.Pdf()));
        var holerite = await CorpoAsync(resposta);

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(("AGENDADO", "2026-11-06T03:00:00Z"), (holerite.GetProperty("situacao").GetString(), holerite.GetProperty("publicacaoEmUtc").GetString())); // o exemplo do contrato
        var id = holerite.GetProperty("id").GetInt32();
        Assert.Equal("2026-11-06 00:00:00", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_PUBLICACAO FROM HOLERITES WHERE ID_HOLERITE = {id}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
        Assert.Equal("2026-11-06 03:00:00", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT EXECUTAR_APOS FROM TAREFAS_AGENDADAS WHERE CHAVE_IDEMPOTENCIA = 'PUBLICAR_HOLERITE:holerite={id}'")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public async Task No_dia_do_pagamento_o_sistema_calcula_a_data_e_se_hoje_e_o_dia_publica_agora()
    {
        var api = await ApiAsync(6); // 06/11/2026 é sexta
        var (mes, ano) = Competencia();
        var (mes2, ano2) = Competencia();

        var agendado = await CorpoAsync(await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Formulario(Campos(mes, ano, noDiaDoPagamento: "true"), ArquivosDeTeste.Pdf())));
        api.Api.Relogio.Fixar(new DateTime(2026, 11, 6, 15, 0, 0, DateTimeKind.Utc));
        var noDia = await CorpoAsync(await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Formulario(Campos(mes2, ano2, noDiaDoPagamento: "true"), ArquivosDeTeste.Pdf())));

        Assert.Equal(("AGENDADO", "2026-11-06T03:00:00Z"), (agendado.GetProperty("situacao").GetString(), agendado.GetProperty("publicacaoEmUtc").GetString()));
        Assert.Equal(("PUBLICADO", "2026-11-06T15:00:00Z"), (noDia.GetProperty("situacao").GetString(), noDia.GetProperty("publicacaoEmUtc").GetString()));
    }

    [Fact]
    public async Task No_dia_do_pagamento_sem_o_dia_configurado_e_422_EMPRESA_NAO_CONFIGURADA_e_nada_e_guardado()
    {
        var api = await ApiAsync(diaDoPagamento: null);
        var (mes, ano) = Competencia();

        var resposta = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Formulario(Campos(mes, ano, noDiaDoPagamento: "true"), ArquivosDeTeste.Pdf()));
        var problema = await CorpoAsync(resposta);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(("EMPRESA_NAO_CONFIGURADA", "A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, await LinhasAsync(api, api.Elenco.MembroA.FuncionarioId, mes, ano));
    }

    // ---------------------------------------------------------------- o que o contrato recusa com 400

    [Fact]
    public async Task Os_campos_invalidos_vem_juntos_num_400_e_nada_e_guardado()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var antes = await ContarAsync(api, "SELECT COUNT(*) FROM HOLERITES");

        var invalidos = await EnviarAsync(api, pessoa, Formulario(Campos("13", "1999", "SALARIO", "07/10/2026"), ArquivosDeTeste.Pdf()));
        var semArquivo = await EnviarAsync(api, pessoa, Formulario(Campos("1", "2040"), null));
        var osDois = await EnviarAsync(api, pessoa, Formulario(Campos("1", "2040", publicarEm: "2026-11-06", noDiaDoPagamento: "true"), ArquivosDeTeste.Pdf()));
        var diaPassado = await EnviarAsync(api, pessoa, Formulario(Campos("1", "2040", publicarEm: "2026-10-06"), ArquivosDeTeste.Pdf()));

        Assert.Equal([400, 400, 400, 400], new[] { invalidos, semArquivo, osDois, diaPassado }.Select(r => (int)r.StatusCode));
        Assert.Equal(["ano", "mes", "publicarEm", "tipo"], (await CorpoAsync(invalidos)).GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
        Assert.Equal("O arquivo é obrigatório.", (await CorpoAsync(semArquivo)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("Escolha a data de publicação ou o dia do pagamento, não os dois.", (await CorpoAsync(osDois)).GetProperty("errors").GetProperty("noDiaDoPagamento")[0].GetString());
        Assert.Equal("A data de publicação não pode ser anterior a hoje.", (await CorpoAsync(diaPassado)).GetProperty("errors").GetProperty("publicarEm")[0].GetString());
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(antes, await ContarAsync(api, "SELECT COUNT(*) FROM HOLERITES"));
    }

    [Fact]
    public async Task So_pdf_pelo_conteudo_ate_5_MiB_o_resto_e_400_em_arquivo_e_nada_e_guardado()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();
        const int CincoMiB = 5 * 1024 * 1024;

        var grande = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf(CincoMiB + 1)));
        var png = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Png(), "holerite.pdf"));
        var jpeg = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Jpeg(), "holerite.pdf"));
        var exe = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Exe(), "holerite.pdf"));
        var vazio = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), []));
        var dois = Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf());
        dois.Add(new ByteArrayContent(ArquivosDeTeste.Pdf()), "outro", "outro.pdf");
        var doisArquivos = await EnviarAsync(api, pessoa, dois);

        Assert.All(new[] { grande, png, jpeg, exe, vazio, doisArquivos }, r => Assert.Equal(400, (int)r.StatusCode));
        Assert.Equal("O arquivo pode ter até 5 MB.", (await CorpoAsync(grande)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("Envie um arquivo PDF.", (await CorpoAsync(png)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("Envie um arquivo PDF.", (await CorpoAsync(jpeg)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("Envie um arquivo PDF.", (await CorpoAsync(exe)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("O arquivo está vazio.", (await CorpoAsync(vazio)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Equal("Envie um arquivo só.", (await CorpoAsync(doisArquivos)).GetProperty("errors").GetProperty("arquivo")[0].GetString());
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, await LinhasAsync(api, pessoa, mes, ano));
    }

    [Fact]
    public async Task Cinco_MiB_exatos_valem_e_o_corpo_que_nao_e_multipart_e_415()
    {
        var api = await ApiAsync();
        var (mes, ano) = Competencia();

        var exatos = await EnviarAsync(api, api.Elenco.MembroA.FuncionarioId, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf(5 * 1024 * 1024)));
        var json = await (await api.ClienteDoRhAgoraAsync()).PostAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/holerites", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal((201, 415), ((int)exatos.StatusCode, (int)json.StatusCode));
    }

    // ---------------------------------------------------------------- a competência e a pessoa

    [Fact]
    public async Task Pessoa_que_nao_existe_e_404_antes_de_olhar_o_corpo_e_o_RH_envia_holerite_de_inativo()
    {
        var api = await ApiAsync();

        var inexistente = await EnviarAsync(api, int.MaxValue, Formulario(Campos("13", "1999", "X"), null));
        var inativo = await EnviarAsync(api, api.Elenco.Inativo.FuncionarioId, Formulario(Campos("1", "2026", "MENSAL"), ArquivosDeTeste.Pdf()));

        Assert.Equal(404, (int)inexistente.StatusCode);
        Assert.Equal(201, (int)inativo.StatusCode); // vale para inativo (o contrato não recusa)
        Assert.Single(api.Api.Armazenamento.Gravados);
    }

    [Fact]
    public async Task Competencia_futura_so_em_ferias_anterior_a_admissao_e_posterior_ao_desligamento_sao_422_com_a_regra_do_banco()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroB.FuncionarioId;

        var futura = await EnviarAsync(api, pessoa, Formulario(Campos("11", "2026", "MENSAL"), ArquivosDeTeste.Pdf()));
        var decimoFuturo = await EnviarAsync(api, pessoa, Formulario(Campos("12", "2026", "DECIMO_TERCEIRO"), ArquivosDeTeste.Pdf()));
        var anterior = await EnviarAsync(api, pessoa, Formulario(Campos("12", "2025", "MENSAL"), ArquivosDeTeste.Pdf())); // admitida em 2026-01-02
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-03-31' WHERE ID_FUNCIONARIO = {pessoa}");
        try
        {
            var posterior = await EnviarAsync(api, pessoa, Formulario(Campos("5", "2026", "MENSAL"), ArquivosDeTeste.Pdf()));

            Assert.All(new[] { futura, decimoFuturo, anterior, posterior }, r => Assert.Equal(422, (int)r.StatusCode));
            var c1 = await CorpoAsync(futura);
            Assert.Equal(("HOLERITE_COMPETENCIA_FUTURA", "Só o holerite de férias pode ter competência futura.", "Nao e possivel cadastrar holerite de competencia futura."), (Codigo(c1), c1.GetProperty("detail").GetString(), c1.GetProperty("regraDoBanco").GetString()));
            Assert.Equal("HOLERITE_COMPETENCIA_FUTURA", Codigo(await CorpoAsync(decimoFuturo)));
            var c3 = await CorpoAsync(anterior);
            Assert.Equal(("HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", "A competencia do holerite e anterior a admissao."), (Codigo(c3), c3.GetProperty("regraDoBanco").GetString()));
            var c4 = await CorpoAsync(posterior);
            Assert.Equal(("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", "A competência do holerite é posterior ao desligamento da pessoa."), (Codigo(c4), c4.GetProperty("detail").GetString()));
            Assert.Empty(api.Api.Armazenamento.Gravados);
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE, DATA_DESLIGAMENTO = NULL WHERE ID_FUNCIONARIO = {pessoa}");
        }
    }

    [Fact]
    public async Task Quando_o_banco_recusa_depois_de_a_aplicacao_aprovar_o_arquivo_e_apagado_e_nenhuma_linha_fica()
    {
        var api = await ApiAsync();
        api.Api.Relogio.Fixar(new DateTime(2100, 6, 1, 12, 0, 0, DateTimeKind.Utc)); // para a aplicação 01/2100 é passado; para o trigger, que usa o CURDATE() real, é futuro
        var pessoa = api.Elenco.MembroA.FuncionarioId;

        var resposta = await EnviarAsync(api, pessoa, Formulario(Campos("1", "2100", "MENSAL"), ArquivosDeTeste.Pdf()));
        var problema = await CorpoAsync(resposta);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(("HOLERITE_COMPETENCIA_FUTURA", "Nao e possivel cadastrar holerite de competencia futura."), (Codigo(problema), problema.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(api.Api.Armazenamento.Gravados, api.Api.Armazenamento.Apagados); // o arquivo foi guardado antes da linha e apagado quando o banco recusou
        Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, api.Api.Armazenamento.Quantidade);
        Assert.Equal(0, await LinhasAsync(api, pessoa, "1", "2100", "MENSAL"));
        Assert.Equal(0, await ContarAsync(api, "SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND CHAVE_IDEMPOTENCIA IN (SELECT CONCAT('PUBLICAR_HOLERITE:holerite=', ID_HOLERITE) FROM HOLERITES WHERE ANO = 2100)"));
    }

    // ---------------------------------------------------------------- o que já existe

    [Fact]
    public async Task Enviar_de_novo_a_mesma_competencia_e_tipo_e_409_HOLERITE_JA_EXISTE_com_o_texto_por_extenso_e_o_segundo_arquivo_nao_fica()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();
        await EnviarComSucessoAsync(api, pessoa, mes, ano);

        var segunda = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf(2500)));
        var problema = await CorpoAsync(segunda);

        Assert.Equal(409, (int)segunda.StatusCode);
        Assert.Equal("HOLERITE_JA_EXISTE", Codigo(problema));
        Assert.StartsWith("Já existe um holerite de férias de ", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.EndsWith($" de {ano} para esta pessoa. Para corrigir, envie uma nova versão.", problema.GetProperty("detail").GetString(), StringComparison.Ordinal);
        Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal(1, await LinhasAsync(api, pessoa, mes, ano));
    }

    [Fact]
    public async Task Dois_envios_da_mesma_competencia_ao_mesmo_tempo_deixam_um_holerite_so_e_um_arquivo_so()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf(4000)))));

        Assert.Equal([201, 409, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(1, await LinhasAsync(api, pessoa, mes, ano));
        Assert.Equal(1, api.Api.Armazenamento.Quantidade); // os arquivos dos que perderam a corrida foram apagados (ou nem chegaram a ser guardados)
        Assert.Equal(api.Api.Armazenamento.Gravados.Count - 1, api.Api.Armazenamento.Apagados.Count);
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS t JOIN HOLERITES h ON t.CHAVE_IDEMPOTENCIA = CONCAT('PUBLICAR_HOLERITE:holerite=', h.ID_HOLERITE) WHERE h.ID_FUNCIONARIO = {pessoa} AND h.ANO = {ano} AND h.MES = {mes}"));
    }

    // ---------------------------------------------------------------- nova versão

    [Fact]
    public async Task A_nova_versao_e_a_seguinte_da_mesma_competencia_a_antiga_fica_como_estava_e_so_a_atual_aceita_outra()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();
        var v1 = await EnviarComSucessoAsync(api, pessoa, mes, ano);

        var resposta = await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Pdf(3500), "corrigido.pdf"));
        var v2 = await CorpoAsync(resposta);
        var desatualizada = await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Pdf(3600)));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal((pessoa, "FERIAS", 2, true, "PUBLICADO"), (v2.GetProperty("funcionarioId").GetInt32(), v2.GetProperty("tipo").GetString(), v2.GetProperty("versao").GetInt32(), v2.GetProperty("versaoAtual").GetBoolean(), v2.GetProperty("situacao").GetString()));
        Assert.NotEqual(v1, v2.GetProperty("id").GetInt32());
        Assert.Equal(2, await LinhasAsync(api, pessoa, mes, ano));
        Assert.Equal(2, api.Api.Armazenamento.Gravados.Count);
        var problema = await CorpoAsync(desatualizada);
        Assert.Equal((409, "HOLERITE_VERSAO_DESATUALIZADA", "Este holerite já foi substituído por uma versão mais nova. Atualize a lista."), ((int)desatualizada.StatusCode, Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(2, await LinhasAsync(api, pessoa, mes, ano)); // a recusada não deixou linha nem arquivo
        Assert.Equal(2, api.Api.Armazenamento.Gravados.Count);
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE CHAVE_IDEMPOTENCIA = 'PUBLICAR_HOLERITE:holerite={v2.GetProperty("id").GetInt32()}'"));
    }

    [Fact]
    public async Task A_nova_versao_agendada_deixa_a_antiga_valendo_ate_o_dia_e_duas_substituicoes_ao_mesmo_tempo_dao_uma_versao_so()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();
        var v1 = await EnviarComSucessoAsync(api, pessoa, mes, ano);

        var agendada = await CorpoAsync(await SubstituirAsync(api, v1, Formulario([("publicarEm", "2026-11-06")], ArquivosDeTeste.Pdf())));
        var (mes2, ano2) = Competencia();
        var outra = await EnviarComSucessoAsync(api, pessoa, mes2, ano2);
        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => SubstituirAsync(api, outra, Formulario([], ArquivosDeTeste.Pdf(4000)))));

        Assert.Equal(("AGENDADO", "2026-11-06T03:00:00Z"), (agendada.GetProperty("situacao").GetString(), agendada.GetProperty("publicacaoEmUtc").GetString()));
        Assert.Equal("2026-10-07 09:42:00", Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_PUBLICACAO FROM HOLERITES WHERE ID_HOLERITE = {v1}")).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture)); // a v1 segue como estava
        Assert.Equal([201, 409, 409, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(2, await LinhasAsync(api, pessoa, mes2, ano2));
    }

    [Fact]
    public async Task A_nova_versao_confere_o_arquivo_o_dia_do_pagamento_e_a_pessoa_e_o_que_nao_existe_e_404()
    {
        var api = await ApiAsync(diaDoPagamento: null);
        var pessoa = api.Elenco.MembroB.FuncionarioId;
        var (mes, ano) = Competencia();
        var v1 = await EnviarComSucessoAsync(api, pessoa, mes, ano);
        var gravadosAntes = api.Api.Armazenamento.Gravados.Count;

        var semArquivo = await SubstituirAsync(api, v1, Formulario([], null));
        var osDois = await SubstituirAsync(api, v1, Formulario([("publicarEm", "2026-11-06"), ("noDiaDoPagamento", "true")], ArquivosDeTeste.Pdf()));
        var png = await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Png()));
        var semEmpresa = await SubstituirAsync(api, v1, Formulario([("noDiaDoPagamento", "true")], ArquivosDeTeste.Pdf()));
        var inexistente = await SubstituirAsync(api, int.MaxValue, Formulario([], ArquivosDeTeste.Pdf()));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-03-31' WHERE ID_FUNCIONARIO = {pessoa}");
        try
        {
            // O holerite de férias já existia; a pessoa foi desligada antes da competência: a nova versão também é recusada.
            var posterior = await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Pdf()));

            Assert.Equal([400, 400, 400, 422, 404, 422], new[] { semArquivo, osDois, png, semEmpresa, inexistente, posterior }.Select(r => (int)r.StatusCode));
            Assert.Equal("EMPRESA_NAO_CONFIGURADA", Codigo(await CorpoAsync(semEmpresa)));
            Assert.Equal("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", Codigo(await CorpoAsync(posterior)));
            Assert.Equal(gravadosAntes, api.Api.Armazenamento.Gravados.Count);
            Assert.Equal(1, await LinhasAsync(api, pessoa, mes, ano));
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = TRUE, DATA_DESLIGAMENTO = NULL WHERE ID_FUNCIONARIO = {pessoa}");
        }
    }

    // ---------------------------------------------------------------- limpeza de sobras, perfis e contrato

    [Fact]
    public async Task A_limpeza_de_sobras_conhece_o_prefixo_holerites_e_so_o_que_a_tabela_referencia_fica()
    {
        var api = await ApiAsync();
        var (mes, ano) = Competencia();
        var id = await EnviarComSucessoAsync(api, api.Elenco.MembroA.FuncionarioId, mes, ano);
        var caminho = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM HOLERITES WHERE ID_HOLERITE = {id}"))!;
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>().Single(f => f.Prefixo == "holerites/");

        var referenciados = await fonte.ReferenciadosAsync([caminho, "holerites/sobra-de-um-envio-que-caiu.pdf"], CancellationToken.None);

        Assert.Equal([caminho], referenciados);
    }

    [Fact]
    public async Task Gestor_funcionario_e_sem_login_nao_enviam_holerite()
    {
        var api = await ApiAsync();
        var (mes, ano) = Competencia();
        var v1 = await EnviarComSucessoAsync(api, api.Elenco.MembroA.FuncionarioId, mes, ano);

        foreach (var pessoa in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
            Assert.Equal(403, (int)(await cliente.PostAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/holerites", Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf()))).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsync($"/api/rh/holerites/{v1}/versoes", Formulario([], ArquivosDeTeste.Pdf()))).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.CreateClient().PostAsync($"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/holerites", Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf()))).StatusCode);
    }

    [Fact]
    public async Task As_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();

        var criado = await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf()));
        var v1 = (await CorpoAsync(criado)).GetProperty("id").GetInt32();

        await criado.AfirmarConformeAoContratoAsync("enviarHolerite");
        await (await EnviarAsync(api, pessoa, Formulario(Campos("13", "1999", "X"), null))).AfirmarConformeAoContratoAsync("enviarHolerite"); // 400
        await (await EnviarAsync(api, int.MaxValue, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf()))).AfirmarConformeAoContratoAsync("enviarHolerite"); // 404
        await (await EnviarAsync(api, pessoa, Formulario(Campos(mes, ano), ArquivosDeTeste.Pdf()))).AfirmarConformeAoContratoAsync("enviarHolerite"); // 409
        await (await EnviarAsync(api, pessoa, Formulario(Campos("11", "2026", "MENSAL"), ArquivosDeTeste.Pdf()))).AfirmarConformeAoContratoAsync("enviarHolerite"); // 422
        var nova = await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Pdf()));
        await nova.AfirmarConformeAoContratoAsync("substituirHolerite");
        await (await SubstituirAsync(api, v1, Formulario([], ArquivosDeTeste.Pdf()))).AfirmarConformeAoContratoAsync("substituirHolerite"); // 409
        await (await SubstituirAsync(api, v1, Formulario([], null))).AfirmarConformeAoContratoAsync("substituirHolerite"); // 400
        await (await SubstituirAsync(api, int.MaxValue, Formulario([], ArquivosDeTeste.Pdf()))).AfirmarConformeAoContratoAsync("substituirHolerite"); // 404
    }
}

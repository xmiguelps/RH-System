using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>POST /api/eu/ponto/atestados</c> (enviar) e <c>GET /api/eu/ponto/atestados</c> (os meus), no banco REAL, com o relógio fixo em 2026-10-07T12:42Z. Qualquer perfil envia e vê só os atestados dele. O arquivo (PDF, JPG ou PNG, até 5 MiB, o tipo pelos primeiros bytes) só é guardado
/// depois de os campos passarem; o atestado nasce <c>ENVIADO</c>; dado de saúde: o nome do arquivo e o conteúdo não vão para a auditoria.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EnviarAtestadoTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/ponto/atestados";
    private const long CincoMiB = 5L * 1024 * 1024;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EnviarAtestadoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(ApiDeAcesso api, PessoaDeTeste pessoa, HttpContent formulario) =>
        await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsync(Rota, formulario);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static long Contar(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql).GetAwaiter().GetResult();

    private static List<int> Ids(JsonElement pagina) => [.. pagina.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    // ---------------------------------------------------------------- enviar

    [Fact]
    public async Task Atestado_de_dias_inteiros_nasce_ENVIADO_com_o_nome_do_arquivo_a_hora_do_relogio_e_o_arquivo_guardado()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var bytes = ArquivosDeTeste.Pdf(3000);
        api.Api.Armazenamento.Limpar();

        var resposta = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-01", "2026-10-02", null, null, new AtestadosDeTeste.Parte("arquivo", "atestado-diego.pdf", bytes)));

        Assert.Equal(201, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        var atestado = JsonSerializer.Deserialize<JsonElement>(texto);
        var id = atestado.GetProperty("id").GetInt32();
        Assert.Equal((diego.FuncionarioId, "Diego Farias", true), (atestado.GetProperty("funcionario").GetProperty("id").GetInt32(), atestado.GetProperty("funcionario").GetProperty("nome").GetString(), atestado.GetProperty("funcionario").GetProperty("ativo").GetBoolean()));
        Assert.Equal(("2026-10-01", "2026-10-02", JsonValueKind.Null, JsonValueKind.Null, "ENVIADO", "atestado-diego.pdf", "2026-10-07T12:42:00Z"),
            (atestado.GetProperty("dataInicio").GetString(), atestado.GetProperty("dataFim").GetString(), atestado.GetProperty("horaInicio").ValueKind, atestado.GetProperty("horaFim").ValueKind, atestado.GetProperty("situacao").GetString(), atestado.GetProperty("nomeArquivo").GetString(), atestado.GetProperty("enviadoEmUtc").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (atestado.GetProperty("decididoEmUtc").ValueKind, atestado.GetProperty("decididoPorNome").ValueKind, atestado.GetProperty("motivoRecusa").ValueKind));
        Assert.DoesNotContain("atestados/", texto, StringComparison.Ordinal); // o caminho do arquivo nunca sai
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Matches(@"^atestados/[0-9a-f]{32}\.pdf$", caminho);
        Assert.Equal(bytes, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta); // o arquivo vai para o armazenamento sem transação aberta
        Assert.Equal((diego.FuncionarioId, "2026-10-01", "2026-10-02", "ENVIADO", caminho, "atestado-diego.pdf", "2026-10-07 09:42:00"), // o banco guarda o horário de Brasília
            (
                (int)Contar(api, $"SELECT ID_FUNCIONARIO FROM ATESTADOS WHERE ID_ATESTADO = {id}"),
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(DATA_INICIO, '%Y-%m-%d') FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!,
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(DATA_FIM, '%Y-%m-%d') FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!,
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_ATESTADO FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!,
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!,
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_ARQUIVO_ORIGINAL FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!,
                (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(DATA_ENVIO, '%Y-%m-%d %H:%i:%s') FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", id));
        Assert.Equal((AuditoriaArea.Atestados, AuditoriaAcao.Criou, "Enviou o atestado de 01/10/2026 a 02/10/2026.", "Atestado de Diego Farias", diego.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.DoesNotContain("atestado-diego", evento.Descricao + evento.RegistroAfetado, StringComparison.Ordinal); // dado de saúde: o nome do arquivo não vai para a auditoria
    }

    [Fact]
    public async Task Atestado_de_algumas_horas_guarda_as_horas_e_o_dia_so()
    {
        var api = await ApiAsync();

        var resposta = await EnviarAsync(api, api.Elenco.MembroB, AtestadosDeTeste.Horas("2026-09-25", "13:00", "17:48"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var atestado = await CorpoAsync(resposta);
        Assert.Equal(("2026-09-25", "2026-09-25", "13:00", "17:48"), (atestado.GetProperty("dataInicio").GetString(), atestado.GetProperty("dataFim").GetString(), atestado.GetProperty("horaInicio").GetString(), atestado.GetProperty("horaFim").GetString()));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", atestado.GetProperty("id").GetInt32()));
        Assert.Equal("Enviou o atestado de 25/09/2026 das 13:00 às 17:48.", evento.Descricao);
    }

    [Fact]
    public async Task Um_dia_so_tem_a_data_sem_o_intervalo_no_evento()
    {
        var api = await ApiAsync();

        var resposta = await EnviarAsync(api, api.Elenco.MembroB, AtestadosDeTeste.Dias("2026-10-06", "2026-10-06"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", (await CorpoAsync(resposta)).GetProperty("id").GetInt32()));
        Assert.Equal("Enviou o atestado de 06/10/2026.", evento.Descricao);
    }

    [Theory]
    [InlineData("jpg")]
    [InlineData("png")]
    public async Task JPG_e_PNG_valem_e_a_extensao_vem_do_conteudo_nao_do_nome(string extensao)
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var bytes = extensao == "jpg" ? ArquivosDeTeste.Jpeg(2000) : ArquivosDeTeste.Png(2000);

        var resposta = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Envio("2026-09-10", "2026-09-10", null, null, new AtestadosDeTeste.Parte("arquivo", "foto-do-atestado.pdf", bytes))); // o nome diz pdf, o conteúdo diz outra coisa

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Matches($@"^atestados/[0-9a-f]{{32}}\.{extensao}$", Assert.Single(api.Api.Armazenamento.Gravados));
        Assert.Equal("foto-do-atestado.pdf", (await CorpoAsync(resposta)).GetProperty("nomeArquivo").GetString()); // o nome é o que a pessoa enviou
    }

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task Qualquer_perfil_envia_o_proprio_atestado(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = perfil switch { Perfil.Gestor => api.Elenco.Gestor, Perfil.RH => api.Elenco.Rh, _ => api.Elenco.MembroB };

        var resposta = await EnviarAsync(api, pessoa, AtestadosDeTeste.Dias("2026-08-03", "2026-08-03"));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(pessoa.FuncionarioId, (await CorpoAsync(resposta)).GetProperty("funcionario").GetProperty("id").GetInt32());
    }

    // ---------------------------------------------------------------- enviar: as recusas

    [Theory]
    [InlineData("2026-10-05", "2026-10-03", null, null, "dataFim")] // fim antes do início
    [InlineData("2026-10-05", "2026-10-05", "13:00", null, "horaFim")] // hora sem o par
    [InlineData("2026-10-05", "2026-10-05", null, "17:00", "horaInicio")]
    [InlineData("2026-10-05", "2026-10-06", "13:00", "17:00", "dataFim")] // horas com mais de um dia
    [InlineData("2026-10-05", "2026-10-05", "17:00", "13:00", "horaFim")] // a hora de fim tem de ser depois
    [InlineData("2026-10-05", "2026-10-05", "13:00", "13:00", "horaFim")]
    [InlineData("2026-10-05", "2026-10-05", "25:00", "26:00", "horaInicio")] // fora do HH:mm
    [InlineData("2026-10-05", "2026-10-05", "08:00", "9h", "horaFim")]
    [InlineData(null, "2026-10-05", null, null, "dataInicio")]
    [InlineData("2026-10-05", null, null, null, "dataFim")]
    [InlineData("05/10/2026", "2026-10-05", null, null, "dataInicio")]
    [InlineData("2026-10-05", "2026-02-30", null, null, "dataFim")]
    public async Task Campo_invalido_e_400_com_o_nome_do_campo_e_nada_e_guardado(string? inicio, string? fim, string? horaInicio, string? horaFim, string campo)
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = Contar(api, "SELECT COUNT(*) FROM ATESTADOS");

        var resposta = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Envio(inicio, fim, horaInicio, horaFim, new AtestadosDeTeste.Parte("arquivo", "a.pdf", ArquivosDeTeste.Pdf())));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _), $"faltou o erro de {campo}");
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(antes, Contar(api, "SELECT COUNT(*) FROM ATESTADOS"));
    }

    [Fact]
    public async Task Os_erros_de_campo_e_de_arquivo_vem_juntos()
    {
        var api = await ApiAsync();

        var resposta = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Envio("2026-10-05", "2026-10-03", "13:00", null, new AtestadosDeTeste.Parte("arquivo", "virus.exe", ArquivosDeTeste.Exe())));

        Assert.Equal(400, (int)resposta.StatusCode);
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");
        Assert.Equal(["arquivo", "dataFim", "horaFim"], erros.EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Arquivo_de_exatamente_5_MiB_vale_e_um_byte_a_mais_e_400_com_o_texto_de_5_MB()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();

        var noLimite = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Dias("2026-07-01", "2026-07-01", "limite.pdf", ArquivosDeTeste.Pdf((int)CincoMiB)));
        var passou = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Dias("2026-07-02", "2026-07-02", "grande.pdf", ArquivosDeTeste.Pdf((int)CincoMiB + 1)));

        Assert.Equal(201, (int)noLimite.StatusCode);
        Assert.Equal(400, (int)passou.StatusCode);
        Assert.Equal(["O arquivo pode ter até 5 MB."], (await CorpoAsync(passou)).GetProperty("errors").GetProperty("arquivo").EnumerateArray().Select(m => m.GetString()!));
        Assert.Single(api.Api.Armazenamento.Gravados); // só o do limite
    }

    [Fact]
    public async Task Sem_arquivo_vazio_de_outro_tipo_ou_mais_de_um_e_400_em_arquivo()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var diego = api.Elenco.MembroA;

        var semArquivo = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05"));
        var vazio = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05", null, null, new AtestadosDeTeste.Parte("arquivo", "vazio.pdf", [])));
        var executavel = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05", null, null, new AtestadosDeTeste.Parte("arquivo", "atestado.pdf", ArquivosDeTeste.Exe())));
        var gif = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05", null, null, new AtestadosDeTeste.Parte("arquivo", "a.gif", ArquivosDeTeste.Gif())));
        var dois = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05", null, null, new AtestadosDeTeste.Parte("arquivo", "a.pdf", ArquivosDeTeste.Pdf()), new AtestadosDeTeste.Parte("arquivo", "b.pdf", ArquivosDeTeste.Pdf())));
        var emOutroCampo = await EnviarAsync(api, diego, AtestadosDeTeste.Envio("2026-10-05", "2026-10-05", null, null, new AtestadosDeTeste.Parte("anexo", "a.pdf", ArquivosDeTeste.Pdf())));

        (HttpResponseMessage Resposta, string Mensagem)[] esperados =
        [
            (semArquivo, "O arquivo é obrigatório."),
            (vazio, "O arquivo está vazio."),
            (executavel, "Envie um arquivo PDF, JPG ou PNG."),
            (gif, "Envie um arquivo PDF, JPG ou PNG."),
            (dois, "Envie um arquivo só."),
            (emOutroCampo, "O arquivo é obrigatório."),
        ];
        foreach (var (resposta, mensagem) in esperados)
        {
            Assert.Equal(400, (int)resposta.StatusCode);
            Assert.Equal([mensagem], (await CorpoAsync(resposta)).GetProperty("errors").GetProperty("arquivo").EnumerateArray().Select(m => m.GetString()!));
        }

        Assert.Empty(api.Api.Armazenamento.Gravados);
    }

    [Fact]
    public async Task Corpo_que_nao_e_multipart_e_415_e_sem_login_e_401()
    {
        var api = await ApiAsync();

        var json = await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).PostAsJsonAsync(Rota, new { dataInicio = "2026-10-05", dataFim = "2026-10-05" });
        var anonimo = await api.Api.ClienteAnonimo().PostAsync(Rota, AtestadosDeTeste.Dias("2026-10-05", "2026-10-05"));

        Assert.Equal((415, 401), ((int)json.StatusCode, (int)anonimo.StatusCode));
    }

    [Fact]
    public async Task Falha_ao_guardar_o_arquivo_e_500_sem_linha_e_sem_evento()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();
        var antes = Contar(api, "SELECT COUNT(*) FROM ATESTADOS");
        var eventosAntes = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS")).Count;
        api.Api.Armazenamento.FalharNaGravacao = new IOException("disco cheio");
        try
        {
            var resposta = await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Dias("2026-10-05", "2026-10-05"));

            Assert.Equal(500, (int)resposta.StatusCode);
        }
        finally
        {
            api.Api.Armazenamento.FalharNaGravacao = null;
        }

        Assert.Equal(antes, Contar(api, "SELECT COUNT(*) FROM ATESTADOS"));
        Assert.Equal(eventosAntes, (await AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS")).Count);
    }

    // ---------------------------------------------------------------- meus atestados

    [Fact]
    public async Task A_lista_e_so_da_pessoa_do_mais_recente_ao_mais_antigo_e_traz_a_decisao_e_o_motivo()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;
        var rh = api.Elenco.Rh;
        var validado = await AtestadosDeTeste.SemearAsync(api.Conexao, julia.FuncionarioId, "2026-06-01", "2026-06-02", "VALIDADO", rh.FuncionarioId, enviadoEm: "2026-06-01 12:00:00", nome: "medico.pdf");
        var recusado = await AtestadosDeTeste.SemearAsync(api.Conexao, julia.FuncionarioId, "2026-06-10", "2026-06-10", "RECUSADO", rh.FuncionarioId, enviadoEm: "2026-06-10 15:30:00", nome: "consulta.jpg", horaInicio: "13:00", horaFim: "17:48", motivo: "O documento não informa o horário da consulta.");
        var aguardando = await AtestadosDeTeste.SemearAsync(api.Conexao, julia.FuncionarioId, "2026-06-20", "2026-06-20", enviadoEm: "2026-06-20 09:00:00");
        var deOutra = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-06-15", "2026-06-15", enviadoEm: "2026-06-15 09:00:00");
        var cliente = await api.ClienteAgoraAsync(julia.UsuarioId);

        var todos = await CorpoAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=100"));

        var meus = Ids(todos).Where(id => new[] { validado, recusado, aguardando, deOutra }.Contains(id)).ToList();
        Assert.Equal([aguardando, recusado, validado], meus); // só os dela, mais recente primeiro
        Assert.All(todos.GetProperty("itens").EnumerateArray(), a => Assert.Equal(julia.FuncionarioId, a.GetProperty("funcionario").GetProperty("id").GetInt32()));
        var itemRecusado = todos.GetProperty("itens").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == recusado);
        Assert.Equal(("RECUSADO", "13:00", "17:48", "consulta.jpg", "2026-10-06T13:00:00Z", "Ana Souza", "O documento não informa o horário da consulta."),
            (itemRecusado.GetProperty("situacao").GetString(), itemRecusado.GetProperty("horaInicio").GetString(), itemRecusado.GetProperty("horaFim").GetString(), itemRecusado.GetProperty("nomeArquivo").GetString(), itemRecusado.GetProperty("decididoEmUtc").GetString(), itemRecusado.GetProperty("decididoPorNome").GetString(), itemRecusado.GetProperty("motivoRecusa").GetString()));
        Assert.Equal("2026-06-20T12:00:00Z", todos.GetProperty("itens").EnumerateArray().Single(a => a.GetProperty("id").GetInt32() == aguardando).GetProperty("enviadoEmUtc").GetString());
    }

    [Fact]
    public async Task A_lista_filtra_por_situacao_e_pagina()
    {
        var api = await ApiAsync();
        var bruno = api.Elenco.DeOutroSetor; // só este teste usa o Bruno
        var rh = api.Elenco.Rh;
        var a = await AtestadosDeTeste.SemearAsync(api.Conexao, bruno.FuncionarioId, "2026-05-01", "2026-05-01", "VALIDADO", rh.FuncionarioId, enviadoEm: "2026-05-01 10:00:00");
        var b = await AtestadosDeTeste.SemearAsync(api.Conexao, bruno.FuncionarioId, "2026-05-02", "2026-05-02", enviadoEm: "2026-05-02 10:00:00");
        var c = await AtestadosDeTeste.SemearAsync(api.Conexao, bruno.FuncionarioId, "2026-05-03", "2026-05-03", enviadoEm: "2026-05-03 10:00:00");
        var cliente = await api.ClienteAgoraAsync(bruno.UsuarioId);

        var aValidar = await CorpoAsync(await cliente.GetAsync($"{Rota}?situacao=ENVIADO"));
        var primeira = await CorpoAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=2"));
        var segunda = await CorpoAsync(await cliente.GetAsync($"{Rota}?tamanhoPagina=2&pagina=2"));

        Assert.Equal([c, b], Ids(aValidar));
        Assert.Equal([c, b], Ids(primeira));
        Assert.Equal([a], Ids(segunda));
        Assert.Equal((3, 2, 2), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32()));
        Assert.Equal(2, aValidar.GetProperty("total").GetInt32());
    }

    [Theory]
    [InlineData("?situacao=PENDENTE", "situacao")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    public async Task Filtro_invalido_da_lista_e_400_com_o_nome_do_campo(string consulta, string campo)
    {
        var api = await ApiAsync();

        var resposta = await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync($"{Rota}{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Fact]
    public async Task Sem_login_a_lista_e_401_e_as_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId);
        await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-04-01", "2026-04-01", "RECUSADO", api.Elenco.Rh.FuncionarioId, enviadoEm: "2026-04-01 10:00:00");

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode);
        await (await cliente.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarMeusAtestados");
        await (await cliente.GetAsync($"{Rota}?situacao=NADA")).AfirmarConformeAoContratoAsync("listarMeusAtestados"); // 400
        await (await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Dias("2026-04-02", "2026-04-02"))).AfirmarConformeAoContratoAsync("enviarAtestado");
        await (await EnviarAsync(api, api.Elenco.MembroA, AtestadosDeTeste.Dias("2026-04-05", "2026-04-02"))).AfirmarConformeAoContratoAsync("enviarAtestado"); // 400
    }
}

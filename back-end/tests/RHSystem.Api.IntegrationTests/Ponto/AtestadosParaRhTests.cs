using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// Os atestados do lado do RH (<c>/api/rh/ponto/atestados</c>), no banco REAL, com o relógio fixo em 2026-10-07T12:42Z. Só o RH lista a empresa toda, baixa o arquivo e DECIDE: <c>ENVIADO</c> vira <c>VALIDADO</c> ou <c>RECUSADO</c> (com motivo) e a decisão é final (409
/// <c>ATESTADO_JA_DECIDIDO</c>, decidida por um UPDATE condicional: duas abas, uma só vale). Validar cobre o dia no espelho e manda recalcular os dias já fechados; recusar não muda nada. Nenhum e-mail sai. O RH pode validar o próprio atestado.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AtestadosParaRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/ponto/atestados";

    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static bool _cargoDado;

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AtestadosParaRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await Trava.WaitAsync();
        try
        {
            if (!_cargoDado)
            {
                var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, "Cargo do atestado", 44m);
                await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({api.Elenco.MembroA.FuncionarioId}, {api.Elenco.MembroB.FuncionarioId})");
                _cargoDado = true;
            }
        }
        finally
        {
            Trava.Release();
        }

        return api;
    }

    private static async Task<HttpResponseMessage> ComoRhAsync(ApiDeAcesso api, string caminho) => await (await api.ClienteDoRhAgoraAsync()).GetAsync(caminho);

    private static async Task<HttpResponseMessage> DecidirAsync(ApiDeAcesso api, int id, string acao, object? corpo = null) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{Rota}/{id}/{acao}", corpo ?? new { });

    private static List<int> Ids(JsonElement pagina) => [.. pagina.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int atestadoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "ATESTADOS", atestadoId);

    private static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql))?.ToString();

    private static async Task<string> SituacaoAsync(ApiDeAcesso api, int id) => (await TextoAsync(api, $"SELECT STATUS_ATESTADO FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!;

    // ---------------------------------------------------------------- a lista

    [Fact]
    public async Task A_lista_e_da_empresa_toda_do_mais_recente_ao_mais_antigo_e_pagina()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rh = e.Rh.FuncionarioId;
        var a = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-03-02", "2026-03-02", "VALIDADO", rh, "2026-03-02 10:00:00");
        var b = await AtestadosDeTeste.SemearAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-03-03", "2026-03-03", enviadoEm: "2026-03-03 10:00:00");
        var c = await AtestadosDeTeste.SemearAsync(api.Conexao, e.Gestor.FuncionarioId, "2026-03-04", "2026-03-04", enviadoEm: "2026-03-04 10:00:00");
        var mesmaHora1 = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-03-05", "2026-03-05", enviadoEm: "2026-03-05 10:00:00");
        var mesmaHora2 = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-03-06", "2026-03-06", enviadoEm: "2026-03-05 10:00:00");

        var pagina = await CorpoAsync(await ComoRhAsync(api, $"{Rota}?tamanhoPagina=100"));
        var primeiraPagina = await CorpoAsync(await ComoRhAsync(api, $"{Rota}?tamanhoPagina=2"));
        var segundaPagina = await CorpoAsync(await ComoRhAsync(api, $"{Rota}?tamanhoPagina=2&pagina=2"));

        var nossos = Ids(pagina).Where(id => new[] { a, b, c, mesmaHora1, mesmaHora2 }.Contains(id)).ToList();
        Assert.Equal([mesmaHora2, mesmaHora1, c, b, a], nossos); // o instante de envio, e o id maior primeiro no empate
        Assert.Equal(Ids(pagina).Take(2), Ids(primeiraPagina));
        Assert.Equal(Ids(pagina).Skip(2).Take(2), Ids(segundaPagina));
        Assert.Equal(pagina.GetProperty("total").GetInt32(), primeiraPagina.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task A_lista_filtra_por_situacao_por_pessoa_e_busca_no_nome()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rh = e.Rh.FuncionarioId;
        var enviado = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-02-02", "2026-02-02", enviadoEm: "2026-02-02 10:00:00");
        var validado = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-02-03", "2026-02-03", "VALIDADO", rh, "2026-02-03 10:00:00");
        var recusado = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-02-04", "2026-02-04", "RECUSADO", rh, "2026-02-04 10:00:00");

        var aValidar = Ids(await CorpoAsync(await ComoRhAsync(api, $"{Rota}?situacao=ENVIADO&tamanhoPagina=100")));
        var deDiego = Ids(await CorpoAsync(await ComoRhAsync(api, $"{Rota}?funcionarioId={e.MembroA.FuncionarioId}&tamanhoPagina=100")));
        var buscaPeloNome = Ids(await CorpoAsync(await ComoRhAsync(api, $"{Rota}?busca=j%C3%BAlia&tamanhoPagina=100")));
        var buscaDeDiegoRecusado = Ids(await CorpoAsync(await ComoRhAsync(api, $"{Rota}?busca=Diego&situacao=RECUSADO&tamanhoPagina=100")));

        Assert.Contains(enviado, aValidar);
        Assert.DoesNotContain(validado, aValidar);
        Assert.DoesNotContain(recusado, aValidar);
        Assert.Contains(enviado, deDiego);
        Assert.Contains(validado, deDiego);
        Assert.DoesNotContain(recusado, deDiego);
        Assert.Contains(recusado, buscaPeloNome); // "júlia" acha "Júlia Nunes" (sem diferenciar maiúscula)
        Assert.DoesNotContain(enviado, buscaPeloNome);
        Assert.DoesNotContain(recusado, buscaDeDiegoRecusado); // o recusado é da Júlia, não do Diego
    }

    [Theory]
    [InlineData("?situacao=PENDENTE", "situacao")]
    [InlineData("?pagina=0", "pagina")]
    [InlineData("?tamanhoPagina=101", "tamanhoPagina")]
    public async Task Filtro_invalido_e_400_com_o_nome_do_campo(string consulta, string campo)
    {
        var api = await ApiAsync();

        var resposta = await ComoRhAsync(api, $"{Rota}{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    // ---------------------------------------------------------------- o download

    [Fact]
    public async Task Baixar_devolve_o_link_e_grava_Baixou_antes_e_inexistente_e_404()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-10-01", "2026-10-01", nome: "consulta do João.pdf", horaInicio: "13:00", horaFim: "17:48");
        var caminho = (await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM ATESTADOS WHERE ID_ATESTADO = {id}"))!;

        var resposta = await ComoRhAsync(api, $"{Rota}/{id}/download");
        var inexistente = await ComoRhAsync(api, $"{Rota}/{int.MaxValue}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
        var link = await CorpoAsync(resposta);
        Assert.Equal(($"https://arquivos.exemplo.test/{caminho}?nome=consulta%20do%20Jo%C3%A3o.pdf", "2026-10-07T12:47:00Z"), (link.GetProperty("url").GetString(), link.GetProperty("expiraEmUtc").GetString()));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Atestados, AuditoriaAcao.Baixou, "Baixou o atestado de Júlia Nunes (01/10/2026 das 13:00 às 17:48).", "Atestado de Júlia Nunes", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_de_download_nao_devolve_link()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-10-01", "2026-10-01");
        var cliente = AtestadosDeTeste.ComAuditorQueFalhaNoDownload(api.Api).CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;

        var resposta = await cliente.GetAsync($"{Rota}/{id}/download");

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain("arquivos.exemplo.test", await resposta.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        Assert.Empty(await EventosAsync(api, id));
    }

    // ---------------------------------------------------------------- validar

    [Fact]
    public async Task Validar_muda_ENVIADO_para_VALIDADO_com_quem_decidiu_e_quando_e_grava_Aprovou_sem_o_nome_do_arquivo()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var emails = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, diego.FuncionarioId, "2026-01-12", "2026-01-13", nome: "exame.pdf");

        var resposta = await DecidirAsync(api, id, "validar");

        Assert.Equal(200, (int)resposta.StatusCode);
        var atestado = await CorpoAsync(resposta);
        Assert.Equal((id, "VALIDADO", "Ana Souza", "2026-10-07T12:42:00Z", JsonValueKind.Null, "exame.pdf", "Diego Farias"),
            (atestado.GetProperty("id").GetInt32(), atestado.GetProperty("situacao").GetString(), atestado.GetProperty("decididoPorNome").GetString(), atestado.GetProperty("decididoEmUtc").GetString(), atestado.GetProperty("motivoRecusa").ValueKind, atestado.GetProperty("nomeArquivo").GetString(), atestado.GetProperty("funcionario").GetProperty("nome").GetString()));
        Assert.Equal(("VALIDADO", api.Elenco.Rh.FuncionarioId.ToString(), "2026-10-07 09:42:00"), ( // o banco guarda o horário de Brasília
            await SituacaoAsync(api, id),
            await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM ATESTADOS WHERE ID_ATESTADO = {id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_DECISAO, '%Y-%m-%d %H:%i:%s') FROM ATESTADOS WHERE ID_ATESTADO = {id}")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Atestados, AuditoriaAcao.Aprovou, "Validou o atestado de Diego Farias (12/01/2026 a 13/01/2026).", "Atestado de Diego Farias", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
        Assert.DoesNotContain("exame", evento.Descricao + evento.RegistroAfetado, StringComparison.Ordinal);
        Assert.Equal(emails, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS")); // o atestado não tem e-mail (lista fechada)
    }

    [Fact]
    public async Task Validar_cobre_o_dia_no_espelho_e_recusar_nao()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        var julia = api.Elenco.MembroB;
        var validado = await AtestadosDeTeste.SemearAsync(api.Conexao, diego.FuncionarioId, "2026-10-01", "2026-10-01");
        var recusado = await AtestadosDeTeste.SemearAsync(api.Conexao, julia.FuncionarioId, "2026-10-01", "2026-10-01");

        var antes = await DiaDoEspelhoAsync(api, diego.FuncionarioId, "2026-10-01");
        await DecidirAsync(api, validado, "validar");
        await DecidirAsync(api, recusado, "recusar", new { motivo = "Documento ilegível." });
        var depoisDiego = await DiaDoEspelhoAsync(api, diego.FuncionarioId, "2026-10-01");
        var depoisJulia = await DiaDoEspelhoAsync(api, julia.FuncionarioId, "2026-10-01");

        Assert.Equal("SEM_REGISTRO", antes); // enquanto está ENVIADO o dia segue como falta
        Assert.Equal(("FALTA_COMPENSADA", "SEM_REGISTRO"), (depoisDiego, depoisJulia)); // validado cobre o dia; recusado segue como falta
    }

    private static async Task<string> DiaDoEspelhoAsync(ApiDeAcesso api, int funcionarioId, string data)
    {
        var espelho = await CorpoAsync(await ComoRhAsync(api, $"/api/rh/funcionarios/{funcionarioId}/ponto?ano=2026&mes=10"));
        return espelho.GetProperty("dias").EnumerateArray().Single(d => d.GetProperty("data").GetString() == data).GetProperty("situacao").GetString()!;
    }

    [Fact]
    public async Task Validar_manda_recalcular_os_dias_ja_fechados_e_recusar_nao_manda_nada()
    {
        var api = await ApiAsync();
        var espiao = new RecalculoEspiao();
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IRecalculoDoBancoDeHoras>();
            servicos.AddSingleton<IRecalculoDoBancoDeHoras>(espiao);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        var pessoa = api.Elenco.DeOutroSetor.FuncionarioId; // o Bruno só aparece aqui: os dias dele não cruzam com os do teste do espelho
        var passadoAteDepoisDeHoje = await AtestadosDeTeste.SemearAsync(api.Conexao, pessoa, "2026-09-30", "2026-10-09"); // hoje é 07/10: o dia de hoje e os futuros não entram
        var deHoras = await AtestadosDeTeste.SemearAsync(api.Conexao, pessoa, "2026-09-25", "2026-09-25", horaInicio: "13:00", horaFim: "17:48");
        var recusado = await AtestadosDeTeste.SemearAsync(api.Conexao, pessoa, "2026-09-20", "2026-09-21");

        await cliente.PostAsJsonAsync($"{Rota}/{passadoAteDepoisDeHoje}/validar", new { });
        await cliente.PostAsJsonAsync($"{Rota}/{deHoras}/validar", new { });
        await cliente.PostAsJsonAsync($"{Rota}/{recusado}/recusar", new { motivo = "Sem carimbo." });

        Assert.Equal(
            new[] { "2026-09-25", "2026-09-30", "2026-10-01", "2026-10-02", "2026-10-03", "2026-10-04", "2026-10-05", "2026-10-06" }.Select(d => (pessoa, d)),
            espiao.Chamadas.Select(c => (c.Funcionario, c.Dia.ToString("yyyy-MM-dd"))).Order());
    }

    [Fact]
    public async Task Validar_um_periodo_que_comeca_antes_da_admissao_so_recalcula_a_partir_do_dia_da_admissao()
    {
        var api = await ApiAsync();
        var espiao = new RecalculoEspiao();
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IRecalculoDoBancoDeHoras>();
            servicos.AddSingleton<IRecalculoDoBancoDeHoras>(espiao);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        var pessoa = api.Elenco.DeOutroSetor.FuncionarioId;
        var admissaoOriginal = Convert.ToDateTime(await Sql.EscalarAsync(api.Conexao, $"SELECT DATA_ADMISSAO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa}"), System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '2026-09-28' WHERE ID_FUNCIONARIO = {pessoa}");
        try
        {
            var atestado = await AtestadosDeTeste.SemearAsync(api.Conexao, pessoa, "0001-01-01", "2026-10-02"); // o ano digitado errado: dias sem ponto nenhum, antes de a pessoa existir na empresa

            var resposta = await cliente.PostAsJsonAsync($"{Rota}/{atestado}/validar", new { });

            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.Equal(
                new[] { "2026-09-28", "2026-09-29", "2026-09-30", "2026-10-01", "2026-10-02" }.Select(d => (pessoa, d)),
                espiao.Chamadas.Select(c => (c.Funcionario, c.Dia.ToString("yyyy-MM-dd"))).Order());
        }
        finally
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '{admissaoOriginal}' WHERE ID_FUNCIONARIO = {pessoa}");
        }
    }

    [Fact]
    public async Task Atestado_ja_decidido_e_409_ATESTADO_JA_DECIDIDO_e_nada_muda()
    {
        var api = await ApiAsync();
        var validado = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-01-20", "2026-01-20");
        var recusado = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-01-21", "2026-01-21");
        await DecidirAsync(api, validado, "validar");
        await DecidirAsync(api, recusado, "recusar", new { motivo = "Ilegível." });
        var eventosAntes = (await EventosAsync(api, validado)).Count + (await EventosAsync(api, recusado)).Count;

        var tentativas = new[]
        {
            await DecidirAsync(api, validado, "validar"),
            await DecidirAsync(api, validado, "recusar", new { motivo = "Agora não." }),
            await DecidirAsync(api, recusado, "validar"),
            await DecidirAsync(api, recusado, "recusar", new { motivo = "De novo." }),
        };

        foreach (var resposta in tentativas)
        {
            Assert.Equal(409, (int)resposta.StatusCode);
            var problema = await CorpoAsync(resposta);
            Assert.Equal(("ATESTADO_JA_DECIDIDO", "Este atestado já foi respondido. Atualize a lista."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        }

        Assert.Equal(("VALIDADO", "RECUSADO"), (await SituacaoAsync(api, validado), await SituacaoAsync(api, recusado)));
        Assert.Equal(eventosAntes, (await EventosAsync(api, validado)).Count + (await EventosAsync(api, recusado)).Count);
    }

    [Fact]
    public async Task Tres_validacoes_ao_mesmo_tempo_valem_uma_so_e_as_outras_sao_409()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-01-26", "2026-01-26");
        var cliente = await api.ClienteDoRhAgoraAsync();
        var portao = new TaskCompletionSource();

        var tentativas = Enumerable.Range(0, 3).Select(async i =>
        {
            await portao.Task;
            return i == 1 ? await cliente.PostAsJsonAsync($"{Rota}/{id}/recusar", new { motivo = "Ilegível." }) : await cliente.PostAsJsonAsync($"{Rota}/{id}/validar", new { });
        }).ToList();
        portao.SetResult();
        var respostas = await Task.WhenAll(tentativas);

        Assert.Equal([200, 409, 409], respostas.Select(r => (int)r.StatusCode).Order()); // uma decisão só, qualquer que seja
        Assert.Single(await EventosAsync(api, id));
    }

    [Fact]
    public async Task O_RH_valida_o_proprio_atestado()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.Rh.FuncionarioId, "2026-01-27", "2026-01-27");

        var resposta = await DecidirAsync(api, id, "validar");

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("VALIDADO", "Ana Souza"), ((await CorpoAsync(resposta)).GetProperty("situacao").GetString(), (await CorpoAsync(resposta)).GetProperty("decididoPorNome").GetString()));
    }

    // ---------------------------------------------------------------- recusar

    [Fact]
    public async Task Recusar_muda_para_RECUSADO_com_o_motivo_aparado_e_grava_Recusou_sem_o_motivo()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-01-14", "2026-01-14", horaInicio: "13:00", horaFim: "17:48", nome: "declaracao.jpg");
        var emails = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        var resposta = await DecidirAsync(api, id, "recusar", new { motivo = "  O documento não informa o horário da consulta. Envie outro com o horário.  " });

        Assert.Equal(200, (int)resposta.StatusCode);
        var atestado = await CorpoAsync(resposta);
        Assert.Equal(("RECUSADO", "O documento não informa o horário da consulta. Envie outro com o horário.", "Ana Souza", "2026-10-07T12:42:00Z"),
            (atestado.GetProperty("situacao").GetString(), atestado.GetProperty("motivoRecusa").GetString(), atestado.GetProperty("decididoPorNome").GetString(), atestado.GetProperty("decididoEmUtc").GetString()));
        Assert.Equal(("RECUSADO", "O documento não informa o horário da consulta. Envie outro com o horário."), (await SituacaoAsync(api, id), await TextoAsync(api, $"SELECT MOTIVO_RECUSA FROM ATESTADOS WHERE ID_ATESTADO = {id}")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Atestados, AuditoriaAcao.Recusou, "Recusou o atestado de Júlia Nunes (14/01/2026 das 13:00 às 17:48).", "Atestado de Júlia Nunes"), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado));
        Assert.DoesNotContain("horário da consulta", evento.Descricao, StringComparison.Ordinal);
        Assert.Equal(emails, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
    }

    [Fact]
    public async Task Motivo_em_branco_ausente_ou_com_mais_de_500_caracteres_e_400_em_motivo_e_nada_muda()
    {
        var api = await ApiAsync();
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-01-15", "2026-01-15");
        var cliente = await api.ClienteDoRhAgoraAsync();

        var respostas = new[]
        {
            await DecidirAsync(api, id, "recusar", new { motivo = "" }),
            await DecidirAsync(api, id, "recusar", new { motivo = "   " }),
            await DecidirAsync(api, id, "recusar", new { motivo = new string('x', 501) }),
            await DecidirAsync(api, id, "recusar", new { }),
            await cliente.PostAsync($"{Rota}/{id}/recusar", content: null),
        };

        foreach (var resposta in respostas)
        {
            Assert.Equal(400, (int)resposta.StatusCode);
        }

        Assert.All(respostas.Take(4), r => Assert.True(CorpoAsync(r).GetAwaiter().GetResult().GetProperty("errors").TryGetProperty("motivo", out _)));
        Assert.Equal("ENVIADO", await SituacaoAsync(api, id));
        Assert.Equal(200, (int)(await DecidirAsync(api, id, "recusar", new { motivo = new string('x', 500) })).StatusCode); // 500 vale
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task Inexistente_e_404_e_so_o_RH_entra_nas_quatro_rotas()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var id = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-01-28", "2026-01-28");

        var validarInexistente = await DecidirAsync(api, int.MaxValue, "validar");
        var recusarInexistente = await DecidirAsync(api, int.MaxValue, "recusar", new { motivo = "x" });
        var baixarInexistente = await ComoRhAsync(api, $"{Rota}/{int.MaxValue}/download");
        Assert.Equal((404, 404, 404), ((int)validarInexistente.StatusCode, (int)recusarInexistente.StatusCode, (int)baixarInexistente.StatusCode));
        foreach (var intruso in new[] { e.Gestor, e.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(intruso.UsuarioId);
            var statuses = new[]
            {
                (int)(await cliente.GetAsync(Rota)).StatusCode,
                (int)(await cliente.GetAsync($"{Rota}/{id}/download")).StatusCode,
                (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/validar", new { })).StatusCode,
                (int)(await cliente.PostAsJsonAsync($"{Rota}/{id}/recusar", new { motivo = "x" })).StatusCode,
            };
            Assert.Equal(new[] { 403, 403, 403, 403 }, statuses);
        }

        var anonimo = api.Api.ClienteAnonimo();
        var semLogin = new[] { (int)(await anonimo.GetAsync(Rota)).StatusCode, (int)(await anonimo.GetAsync($"{Rota}/{id}/download")).StatusCode, (int)(await anonimo.PostAsJsonAsync($"{Rota}/{id}/validar", new { })).StatusCode, (int)(await anonimo.PostAsJsonAsync($"{Rota}/{id}/recusar", new { motivo = "x" })).StatusCode };
        Assert.Equal(new[] { 401, 401, 401, 401 }, semLogin);
        Assert.Equal("ENVIADO", await SituacaoAsync(api, id)); // nenhum intruso decidiu
    }

    [Fact]
    public async Task As_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var paraValidar = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-01-29", "2026-01-29");
        var paraRecusar = await AtestadosDeTeste.SemearAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-01-30", "2026-01-30");

        await (await ComoRhAsync(api, Rota)).AfirmarConformeAoContratoAsync("listarAtestados");
        await (await ComoRhAsync(api, $"{Rota}?situacao=NADA")).AfirmarConformeAoContratoAsync("listarAtestados"); // 400
        await (await ComoRhAsync(api, $"{Rota}/{paraValidar}/download")).AfirmarConformeAoContratoAsync("baixarAtestado");
        await (await ComoRhAsync(api, $"{Rota}/{int.MaxValue}/download")).AfirmarConformeAoContratoAsync("baixarAtestado"); // 404
        await (await DecidirAsync(api, paraValidar, "validar")).AfirmarConformeAoContratoAsync("validarAtestado");
        await (await DecidirAsync(api, paraValidar, "validar")).AfirmarConformeAoContratoAsync("validarAtestado"); // 409
        await (await DecidirAsync(api, paraRecusar, "recusar", new { motivo = "" })).AfirmarConformeAoContratoAsync("recusarAtestado"); // 400
        await (await DecidirAsync(api, paraRecusar, "recusar", new { motivo = "Ilegível." })).AfirmarConformeAoContratoAsync("recusarAtestado");
        await (await DecidirAsync(api, paraRecusar, "recusar", new { motivo = "Ilegível." })).AfirmarConformeAoContratoAsync("recusarAtestado"); // 409
    }

    [Fact]
    public async Task FonteDeReferenciasDosAtestados_marca_como_referenciados_so_os_caminhos_com_linha()
    {
        var api = await ApiAsync();
        var comLinha1 = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-01-21", "2026-01-21");
        var comLinha2 = await AtestadosDeTeste.SemearAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-01-22", "2026-01-22");
        var caminho1 = Convert.ToString(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM ATESTADOS WHERE ID_ATESTADO = {comLinha1}"))!;
        var caminho2 = Convert.ToString(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM ATESTADOS WHERE ID_ATESTADO = {comLinha2}"))!;
        var semLinha = $"atestados/{Guid.NewGuid():N}.pdf";

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = Assert.Single(escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>(), f => f.Prefixo == "atestados/");
        var referenciados = await fonte.ReferenciadosAsync([caminho1, caminho2, semLinha], CancellationToken.None);

        Assert.Equal(new[] { caminho1, caminho2 }.Order(), referenciados.Order());
        Assert.Empty(await fonte.ReferenciadosAsync([semLinha], CancellationToken.None));
    }

    private sealed class RecalculoEspiao : IRecalculoDoBancoDeHoras
    {
        private readonly ConcurrentQueue<(int Funcionario, DateOnly Dia)> _chamadas = new();

        public IReadOnlyCollection<(int Funcionario, DateOnly Dia)> Chamadas => _chamadas;

        public Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct)
        {
            _chamadas.Enqueue((funcionarioId, dia));
            return Task.CompletedTask;
        }
    }
}

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// O envio em lote (<c>POST /api/rh/holerites/lote</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. Um envio com vários PDFs e a lista <c>itens</c> que liga cada arquivo a uma pessoa; cada item é validado e gravado SOZINHO (sem tudo ou nada) e a resposta é
/// sempre 200 com o resultado de cada um. O que vale para o lote todo (campos, <c>itens</c>, arquivos sem par, tamanho, dia do pagamento sem configuração) recusa o envio inteiro antes de qualquer efeito. O lote não cria versões: quem já tem o holerite volta
/// <c>HOLERITE_JA_EXISTE</c>. Os testes usam o tipo <c>FERIAS</c> com uma competência futura própria de cada um.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class HoleritesEmLoteTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/holerites/lote";
    private static int _sequencia;

    private async Task<ApiDeAcesso> ApiAsync(int? diaDoPagamento = 6)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(HoleritesEmLoteTests));
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

    /// <summary>Uma competência futura só deste teste (FERIAS aceita futuro): do ano 2080 em diante, um mês por chamada.</summary>
    private static (string Mes, string Ano) Competencia()
    {
        var n = Interlocked.Increment(ref _sequencia);
        return ((n % 12 + 1).ToString(System.Globalization.CultureInfo.InvariantCulture), (2080 + n / 12).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private sealed record Item(int FuncionarioId, string NomeDoArquivo);

    private sealed record Arquivo(string Nome, byte[] Bytes, string Campo = "arquivos");

    private static MultipartFormDataContent Lote(IEnumerable<(string Campo, string Valor)> campos, IEnumerable<Item>? itens, IEnumerable<Arquivo> arquivos, string? itensBrutos = null)
    {
        var formulario = new MultipartFormDataContent();
        foreach (var (campo, valor) in campos)
        {
            formulario.Add(new StringContent(valor), campo);
        }

        if (itensBrutos is not null)
        {
            formulario.Add(new StringContent(itensBrutos, Encoding.UTF8, "application/json"), "itens");
        }
        else if (itens is not null)
        {
            formulario.Add(new StringContent(JsonSerializer.Serialize(itens.Select(i => new { funcionarioId = i.FuncionarioId, nomeDoArquivo = i.NomeDoArquivo })), Encoding.UTF8, "application/json"), "itens");
        }

        foreach (var arquivo in arquivos)
        {
            var parte = new ByteArrayContent(arquivo.Bytes);
            parte.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            formulario.Add(parte, arquivo.Campo, arquivo.Nome);
        }

        return formulario;
    }

    private static List<(string, string)> Campos(string mes, string ano, string tipo = "FERIAS", string? publicarEm = null, string? noDia = null)
    {
        var campos = new List<(string, string)> { ("mes", mes), ("ano", ano), ("tipo", tipo) };
        if (publicarEm is not null)
        {
            campos.Add(("publicarEm", publicarEm));
        }

        if (noDia is not null)
        {
            campos.Add(("noDiaDoPagamento", noDia));
        }

        return campos;
    }

    private static async Task<HttpResponseMessage> EnviarAsync(ApiDeAcesso api, HttpContent formulario) => await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota, formulario);

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    private static Task<long> LinhasDaCompetenciaAsync(ApiDeAcesso api, string mes, string ano) => ContarAsync(api, $"SELECT COUNT(*) FROM HOLERITES WHERE ANO = {ano} AND MES = {mes} AND TIPO_HOLERITE = 'FERIAS'");

    private static IEnumerable<Item> Itens(params (int Funcionario, string Nome)[] itens) => itens.Select(i => new Item(i.Funcionario, i.Nome));

    // ---------------------------------------------------------------- o caminho feliz

    [Fact]
    public async Task Tres_pdfs_viram_tres_holerites_na_ordem_dos_itens_com_um_evento_de_resumo_do_lote()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var (mes, ano) = Competencia();
        var pessoas = new[] { e.MembroA.FuncionarioId, e.MembroB.FuncionarioId, e.Gestor.FuncionarioId };

        var resposta = await EnviarAsync(api, Lote(Campos(mes, ano), Itens((pessoas[0], "a.pdf"), (pessoas[1], "b.pdf"), (pessoas[2], "c.pdf")), [new Arquivo("c.pdf", ArquivosDeTeste.Pdf(1500)), new Arquivo("a.pdf", ArquivosDeTeste.Pdf(1600)), new Arquivo("b.pdf", ArquivosDeTeste.Pdf(1700))]));
        var lote = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((3, 3, 0), (lote.GetProperty("total").GetInt32(), lote.GetProperty("enviados").GetInt32(), lote.GetProperty("comErro").GetInt32()));
        var itens = lote.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(pessoas, itens.Select(i => i.GetProperty("funcionarioId").GetInt32())); // a ordem dos itens, não a dos arquivos
        Assert.Equal(["a.pdf", "b.pdf", "c.pdf"], itens.Select(i => i.GetProperty("nomeDoArquivo").GetString()!));
        Assert.All(itens, i => Assert.Equal((true, JsonValueKind.Null, JsonValueKind.Null, "PUBLICADO", 1), (i.GetProperty("sucesso").GetBoolean(), i.GetProperty("codigoDoErro").ValueKind, i.GetProperty("mensagemDoErro").ValueKind, i.GetProperty("holerite").GetProperty("situacao").GetString(), i.GetProperty("holerite").GetProperty("versao").GetInt32())));
        Assert.Equal(3, await LinhasDaCompetenciaAsync(api, mes, ano));
        Assert.Equal(3, api.Api.Armazenamento.Quantidade);
        var ids = itens.Select(i => i.GetProperty("holerite").GetProperty("id").GetInt32()).ToList();
        Assert.Equal(3, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'PUBLICAR_HOLERITE' AND REFERENCIA_ID IN ({string.Join(", ", ids)}) AND SITUACAO = 'PENDENTE'"));
        Assert.All(ids, id => Assert.Single(AuditoriaDeTeste.EventosAsync(api.Conexao, "HOLERITES", id).GetAwaiter().GetResult()));
        var resumo = (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, e.Rh.FuncionarioId)).Single(ev => ev.Entidade is null && ev.Descricao.Contains("lote de 3 holerites", StringComparison.Ordinal));
        Assert.Equal((AuditoriaArea.Holerites, AuditoriaAcao.Criou), (resumo.Area, resumo.Acao));
        Assert.Contains("3 holerites", resumo.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_que_vale_para_o_lote_vale_para_todos_agendar_numa_data_e_no_dia_do_pagamento()
    {
        var api = await ApiAsync(6);
        var e = api.Elenco;
        var (mes, ano) = Competencia();
        var (mes2, ano2) = Competencia();

        var numaData = await CorpoAsync(await EnviarAsync(api, Lote(Campos(mes, ano, publicarEm: "2026-11-06"), Itens((e.MembroA.FuncionarioId, "a.pdf"), (e.MembroB.FuncionarioId, "b.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf()), new Arquivo("b.pdf", ArquivosDeTeste.Pdf())])));
        var noDia = await CorpoAsync(await EnviarAsync(api, Lote(Campos(mes2, ano2, noDia: "true"), Itens((e.MembroA.FuncionarioId, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())])));

        Assert.All(numaData.GetProperty("itens").EnumerateArray(), i => Assert.Equal(("AGENDADO", "2026-11-06T03:00:00Z"), (i.GetProperty("holerite").GetProperty("situacao").GetString(), i.GetProperty("holerite").GetProperty("publicacaoEmUtc").GetString())));
        Assert.Equal(("AGENDADO", "2026-11-06T03:00:00Z"), (noDia.GetProperty("itens")[0].GetProperty("holerite").GetProperty("situacao").GetString(), noDia.GetProperty("itens")[0].GetProperty("holerite").GetProperty("publicacaoEmUtc").GetString())); // 06/11/2026: o próximo pagamento
    }

    // ---------------------------------------------------------------- cada item sozinho

    [Fact]
    public async Task Um_item_ruim_nao_impede_os_outros_cada_erro_volta_no_proprio_item_e_o_que_falhou_nao_deixa_arquivo()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var (mes, ano) = Competencia();
        var jaTem = e.MembroB.FuncionarioId;
        var primeiro = await EnviarAsync(api, Lote(Campos(mes, ano), Itens((jaTem, "antes.pdf")), [new Arquivo("antes.pdf", ArquivosDeTeste.Pdf())]));
        Assert.Equal(200, (int)primeiro.StatusCode);
        api.Api.Armazenamento.Limpar();

        var resposta = await EnviarAsync(api, Lote(
            Campos(mes, ano),
            Itens((e.MembroA.FuncionarioId, "bom.pdf"), (jaTem, "repetido.pdf"), (int.MaxValue, "fantasma.pdf"), (e.Gestor.FuncionarioId, "png.pdf"), (e.Rh.FuncionarioId, "grande.pdf")),
            [new Arquivo("bom.pdf", ArquivosDeTeste.Pdf()), new Arquivo("repetido.pdf", ArquivosDeTeste.Pdf()), new Arquivo("fantasma.pdf", ArquivosDeTeste.Pdf()), new Arquivo("png.pdf", ArquivosDeTeste.Png()), new Arquivo("grande.pdf", ArquivosDeTeste.Pdf(5 * 1024 * 1024 + 1))]));
        var lote = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((5, 1, 4), (lote.GetProperty("total").GetInt32(), lote.GetProperty("enviados").GetInt32(), lote.GetProperty("comErro").GetInt32()));
        var itens = lote.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal([true, false, false, false, false], itens.Select(i => i.GetProperty("sucesso").GetBoolean()));
        Assert.Equal(["HOLERITE_JA_EXISTE", "FUNCIONARIO_NAO_ENCONTRADO", "ARQUIVO_INVALIDO", "ARQUIVO_INVALIDO"], itens.Skip(1).Select(i => i.GetProperty("codigoDoErro").GetString()!));
        Assert.Contains("Já existe um holerite de férias de ", itens[1].GetProperty("mensagemDoErro").GetString(), StringComparison.Ordinal);
        Assert.EndsWith(" para esta pessoa.", itens[1].GetProperty("mensagemDoErro").GetString(), StringComparison.Ordinal); // sem a sugestão de nova versão: o lote não cria versão
        Assert.Equal("Esta pessoa não foi encontrada.", itens[2].GetProperty("mensagemDoErro").GetString());
        Assert.Equal("Envie um arquivo PDF.", itens[3].GetProperty("mensagemDoErro").GetString());
        Assert.Equal("O arquivo pode ter até 5 MB.", itens[4].GetProperty("mensagemDoErro").GetString());
        Assert.All(itens.Skip(1), i => Assert.Equal(JsonValueKind.Null, i.GetProperty("holerite").ValueKind));
        Assert.Equal(2, await LinhasDaCompetenciaAsync(api, mes, ano)); // o de antes e o item bom
        Assert.Equal(1, api.Api.Armazenamento.Quantidade);
    }

    [Fact]
    public async Task A_competencia_recusada_pelo_banco_vale_para_cada_item_com_o_codigo_da_operacao_individual()
    {
        var api = await ApiAsync();
        var e = api.Elenco;

        var futura = await CorpoAsync(await EnviarAsync(api, Lote(Campos("11", "2026", "MENSAL"), Itens((e.MembroA.FuncionarioId, "a.pdf"), (e.MembroB.FuncionarioId, "b.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf()), new Arquivo("b.pdf", ArquivosDeTeste.Pdf())])));
        var anterior = await CorpoAsync(await EnviarAsync(api, Lote(Campos("12", "2025", "MENSAL"), Itens((e.MembroA.FuncionarioId, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())])));

        Assert.Equal((2, 0, 2), (futura.GetProperty("total").GetInt32(), futura.GetProperty("enviados").GetInt32(), futura.GetProperty("comErro").GetInt32()));
        Assert.All(futura.GetProperty("itens").EnumerateArray(), i => Assert.Equal(("HOLERITE_COMPETENCIA_FUTURA", "Só o holerite de férias pode ter competência futura."), (i.GetProperty("codigoDoErro").GetString(), i.GetProperty("mensagemDoErro").GetString())));
        Assert.Equal("HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", anterior.GetProperty("itens")[0].GetProperty("codigoDoErro").GetString());
        Assert.Equal(0, api.Api.Armazenamento.Quantidade);
    }

    [Fact]
    public async Task Repetir_o_mesmo_lote_devolve_HOLERITE_JA_EXISTE_nos_que_ja_entraram_e_ninguem_ganha_versao()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var (mes, ano) = Competencia();
        Item[] itens = [new(e.MembroA.FuncionarioId, "a.pdf"), new(e.MembroB.FuncionarioId, "b.pdf")];
        Arquivo[] arquivos = [new("a.pdf", ArquivosDeTeste.Pdf()), new("b.pdf", ArquivosDeTeste.Pdf())];
        await EnviarAsync(api, Lote(Campos(mes, ano), itens, arquivos));

        var repetido = await CorpoAsync(await EnviarAsync(api, Lote(Campos(mes, ano), itens, arquivos)));

        Assert.Equal((2, 0, 2), (repetido.GetProperty("total").GetInt32(), repetido.GetProperty("enviados").GetInt32(), repetido.GetProperty("comErro").GetInt32()));
        Assert.All(repetido.GetProperty("itens").EnumerateArray(), i => Assert.Equal("HOLERITE_JA_EXISTE", i.GetProperty("codigoDoErro").GetString()));
        Assert.Equal(2, await LinhasDaCompetenciaAsync(api, mes, ano));
        Assert.Equal(2, api.Api.Armazenamento.Quantidade);
    }

    // ---------------------------------------------------------------- o que recusa o lote todo

    [Fact]
    public async Task Erros_do_lote_inteiro_sao_400_antes_de_qualquer_efeito_um_por_campo()
    {
        var api = await ApiAsync();
        var a = api.Elenco.MembroA.FuncionarioId;
        var (mes, ano) = Competencia();
        var pdf = ArquivosDeTeste.Pdf();
        var casos = new Dictionary<string, HttpContent>
        {
            ["campos"] = Lote(Campos("13", "1999", "SALARIO", "2026-10-06"), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf)]),
            ["os dois"] = Lote(Campos(mes, ano, publicarEm: "2026-11-06", noDia: "true"), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf)]),
            ["itens ausente"] = Lote(Campos(mes, ano), null, [new Arquivo("a.pdf", pdf)]),
            ["itens que não é JSON"] = Lote(Campos(mes, ano), null, [new Arquivo("a.pdf", pdf)], "isto não é json"),
            ["itens vazio"] = Lote(Campos(mes, ano), [], [new Arquivo("a.pdf", pdf)]),
            ["itens que não é lista"] = Lote(Campos(mes, ano), null, [new Arquivo("a.pdf", pdf)], "{\"funcionarioId\": 1}"),
            ["item sem nome"] = Lote(Campos(mes, ano), null, [new Arquivo("a.pdf", pdf)], "[{\"funcionarioId\": 1, \"nomeDoArquivo\": \"\"}]"),
            ["item sem pessoa"] = Lote(Campos(mes, ano), null, [new Arquivo("a.pdf", pdf)], "[{\"nomeDoArquivo\": \"a.pdf\"}]"),
            ["nome repetido nos itens"] = Lote(Campos(mes, ano), Itens((a, "a.pdf"), (a, "a.pdf")), [new Arquivo("a.pdf", pdf)]),
            ["arquivo sem item"] = Lote(Campos(mes, ano), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf), new Arquivo("sobra.pdf", pdf)]),
            ["item sem arquivo"] = Lote(Campos(mes, ano), Itens((a, "a.pdf"), (a, "falta.pdf")), [new Arquivo("a.pdf", pdf)]),
            ["nome repetido nos arquivos"] = Lote(Campos(mes, ano), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf), new Arquivo("a.pdf", pdf)]),
            ["sem arquivos"] = Lote(Campos(mes, ano), Itens((a, "a.pdf")), []),
            ["arquivo no campo errado"] = Lote(Campos(mes, ano), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf, "arquivo")]),
        };

        foreach (var (nome, formulario) in casos)
        {
            var resposta = await EnviarAsync(api, formulario);
            Assert.True((int)resposta.StatusCode == 400, $"{nome}: esperava 400 e veio {(int)resposta.StatusCode}");
        }

        Assert.Equal(0, await LinhasDaCompetenciaAsync(api, mes, ano));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        var campos = await CorpoAsync(await EnviarAsync(api, Lote(Campos("13", "1999", "SALARIO", "2026-10-06"), Itens((a, "a.pdf")), [new Arquivo("a.pdf", pdf)])));
        Assert.Equal(["ano", "mes", "publicarEm", "tipo"], campos.GetProperty("errors").EnumerateObject().Select(p => p.Name).Order());
    }

    [Fact]
    public async Task Mais_de_200_itens_ou_arquivos_e_400_e_200_valem()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var (mes, ano) = Competencia();
        var pessoas = new[] { e.MembroA.FuncionarioId, e.MembroB.FuncionarioId, e.Gestor.FuncionarioId };
        var nomes = Enumerable.Range(1, 201).Select(n => $"holerite-{n}.pdf").ToList();
        var pdf = ArquivosDeTeste.Pdf(1200);

        var duzentos = await EnviarAsync(api, Lote(Campos(mes, ano), nomes.Take(200).Select((n, i) => new Item(pessoas[i % 3], n)), nomes.Take(200).Select(n => new Arquivo(n, pdf))));
        var duzentosEUm = await EnviarAsync(api, Lote(Campos(mes, ano), nomes.Select((n, i) => new Item(pessoas[i % 3], n)), nomes.Select(n => new Arquivo(n, pdf))));

        var lote = await CorpoAsync(duzentos);
        Assert.Equal((200, 200, 400), ((int)duzentos.StatusCode, lote.GetProperty("total").GetInt32(), (int)duzentosEUm.StatusCode));
        Assert.Equal((3, 197), (lote.GetProperty("enviados").GetInt32(), lote.GetProperty("comErro").GetInt32())); // três pessoas: o primeiro item de cada uma entra, o resto é HOLERITE_JA_EXISTE
        Assert.Equal(3, await LinhasDaCompetenciaAsync(api, mes, ano));
        Assert.Equal(3, api.Api.Armazenamento.Quantidade);
    }

    [Fact]
    public async Task O_total_dos_arquivos_passa_de_100_MiB_e_400_e_o_item_de_5_MiB_exatos_vale()
    {
        var api = await ApiAsync();
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var casoDeUso = escopo.ServiceProvider.GetRequiredService<EnviarHoleritesEmLoteCasoDeUso>();
        var (mes, ano) = Competencia();
        ArquivoRecebido Grande(string nome, long tamanho) => new(nome, tamanho, () => new MemoryStream(ArquivosDeTeste.Pdf(64)));
        string Itens(int quantidade) => JsonSerializer.Serialize(Enumerable.Range(1, quantidade).Select(n => new { funcionarioId = api.Elenco.MembroA.FuncionarioId, nomeDoArquivo = $"f{n}.pdf" }));

        // 21 arquivos de 5 MiB (105 MiB) passam do total; 20 (100 MiB) cabem. O tamanho é o declarado: nenhum byte grande é criado.
        var passa = await Assert.ThrowsAsync<ErroDeValidacaoException>(() => casoDeUso.ExecutarAsync(new NovoLoteDeHoleritesBruto(mes, ano, "FERIAS", null, null, Itens(21), [.. Enumerable.Range(1, 21).Select(n => Grande($"f{n}.pdf", 5L * 1024 * 1024))], 21), CancellationToken.None));
        var cabe = await casoDeUso.ExecutarAsync(new NovoLoteDeHoleritesBruto(mes, ano, "FERIAS", null, null, Itens(20), [.. Enumerable.Range(1, 20).Select(n => Grande($"f{n}.pdf", 5L * 1024 * 1024))], 20), CancellationToken.None);

        Assert.Equal("O total dos arquivos pode ter até 100 MB.", Assert.Single(passa.Erros["arquivos"]));
        Assert.Equal(20, cabe.Total);
    }

    [Fact]
    public async Task No_dia_do_pagamento_sem_o_dia_configurado_o_lote_inteiro_e_recusado_com_422_e_nenhum_item_e_processado()
    {
        var api = await ApiAsync(diaDoPagamento: null);
        var (mes, ano) = Competencia();

        var resposta = await EnviarAsync(api, Lote(Campos(mes, ano, noDia: "true"), Itens((api.Elenco.MembroA.FuncionarioId, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())]));
        var problema = await CorpoAsync(resposta);

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal(("EMPRESA_NAO_CONFIGURADA", "A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data."), (Codigo(problema), problema.GetProperty("detail").GetString()));
        Assert.Equal(0, await LinhasDaCompetenciaAsync(api, mes, ano));
        Assert.Empty(api.Api.Armazenamento.Gravados);
    }

    // ---------------------------------------------------------------- quem pode e contrato

    [Fact]
    public async Task Gestor_funcionario_e_sem_login_nao_enviam_lote()
    {
        var api = await ApiAsync();
        var (mes, ano) = Competencia();

        foreach (var pessoa in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);
            Assert.Equal(403, (int)(await cliente.PostAsync(Rota, Lote(Campos(mes, ano), Itens((pessoa.FuncionarioId, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())]))).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.CreateClient().PostAsync(Rota, Lote(Campos(mes, ano), Itens((1, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())]))).StatusCode);
        Assert.Equal(415, (int)(await (await api.ClienteDoRhAgoraAsync()).PostAsync(Rota, new StringContent("{}", Encoding.UTF8, "application/json"))).StatusCode);
    }

    [Fact]
    public async Task As_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var (mes, ano) = Competencia();

        var misto = await EnviarAsync(api, Lote(Campos(mes, ano), Itens((e.MembroA.FuncionarioId, "a.pdf"), (int.MaxValue, "b.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf()), new Arquivo("b.pdf", ArquivosDeTeste.Pdf())]));
        await misto.AfirmarConformeAoContratoAsync("enviarHoleritesEmLote");
        await (await EnviarAsync(api, Lote(Campos("13", "1999", "X"), Itens((1, "a.pdf")), []))).AfirmarConformeAoContratoAsync("enviarHoleritesEmLote"); // 400

        await ApiAsync(diaDoPagamento: null);
        await (await EnviarAsync(api, Lote(Campos(mes, ano, noDia: "true"), Itens((e.MembroA.FuncionarioId, "a.pdf")), [new Arquivo("a.pdf", ArquivosDeTeste.Pdf())]))).AfirmarConformeAoContratoAsync("enviarHoleritesEmLote"); // 422
    }
}

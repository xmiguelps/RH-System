using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>obterResultadoFormulario</c> e <c>exportarRespostasFormulario</c> (só o RH), no banco REAL com o relógio em 2026-10-07T12:42Z. O resultado vem por pergunta (alternativas todas, escala, textos em ordem alfabética, arquivo só a quantidade); no ANÔNIMO só depois de encerrado de verdade (o prazo vencido com a tarefa parada
/// vale), com o mínimo de 3 respostas por pergunta (a de menos some e conta como oculta) e, nas avaliações, por avaliado com 3 ou mais; com nome sai em qualquer situação e sem mínimo. O CSV é seguro (BOM, <c>;</c>, célula que começa com <c>=</c>, <c>+</c>, <c>-</c> ou <c>@</c> neutralizada), uma linha por resposta com nome e só o
/// resultado geral no anônimo, sem nenhum dado de pessoa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ResultadoEExportacaoTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ResultadoEExportacaoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static string RotaDoResultado(int formulario, string consulta = "") => $"/api/rh/formularios/{formulario}/resultado{(consulta.Length == 0 ? "" : "?" + consulta)}";

    private static async Task<HttpResponseMessage> PedirResultadoAsync(ApiDeAcesso api, int formulario, string consulta = "") => await (await api.ClienteDoRhAgoraAsync()).GetAsync(RotaDoResultado(formulario, consulta));

    private static async Task<HttpResponseMessage> ExportarAsync(ApiDeAcesso api, int formulario) => await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{formulario}/exportacao");

    private static async Task<string> CsvAsync(HttpResponseMessage resposta)
    {
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3]); // BOM
        return Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3);
    }

    private static string[] Linhas(string csv) => csv.TrimEnd('\r', '\n').Split("\r\n");

    private static Task EncerrarAsync(ApiDeAcesso api, int formulario) => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {formulario}");

    private static Task AnonimaAsync(ApiDeAcesso api, int formulario, int pergunta, string? texto, int? alternativa = null, int? avaliado = null) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, RESPOSTA, DATA_RESPOSTA, ID_AVALIADO, ID_ALTERNATIVA, ANONIMO) VALUES ({pergunta}, {formulario}, {Valor(texto)}, '2026-10-01 00:00:00', {(avaliado is null ? "NULL" : avaliado.Value.ToString())}, {(alternativa is null ? "NULL" : alternativa.Value.ToString())}, 1)");

    private static Task ParticipacaoAsync(ApiDeAcesso api, int formulario, int pessoa, int? avaliado = null) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PARTICIPACOES (ID_PARTICIPACAO, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ('{Guid.NewGuid()}', {formulario}, {pessoa}, {(avaliado is null ? "NULL" : avaliado.Value.ToString())})");

    private static Task NomeadaAsync(ApiDeAcesso api, int formulario, int pergunta, int pessoa, string texto, string instante, int? alternativa = null, int? avaliado = null) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ID_ALTERNATIVA, ANONIMO) VALUES ({pergunta}, {formulario}, {pessoa}, {(avaliado is null ? "NULL" : avaliado.Value.ToString())}, {Valor(texto)}, '{instante}', {(alternativa is null ? "NULL" : alternativa.Value.ToString())}, 0)");

    private static async Task<int[]> AlternativasAsync(ApiDeAcesso api, int pergunta) => [.. (await Sql.ColunaAsync(api.Conexao, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta} ORDER BY ORDEM")).Select(int.Parse)];

    private sealed record Clima(int Id, int Recomenda, int Nota, int Comentario, int Opcional, int Sim, int Nao);

    /// <summary>Um anônimo encerrado de 4 pessoas (4 participações): múltipla (4 respostas), escala (4), comentário longo (3, fora de ordem) e uma curta opcional (2 respostas: abaixo do mínimo).</summary>
    private static async Task<Clima> ClimaAsync(ApiDeAcesso api, string prefixo, string situacao = "ENCERRADO", string? prazo = null)
    {
        var id = await SemearAsync(api, Titulo(prefixo), situacao == "ENCERRADO" ? "PUBLICADO" : situacao, anonimo: true, alvo: "PESSOAS", prazo: prazo);
        var recomenda = await PerguntaAsync(api, id, "Recomendaria?", "MULTIPLA_ESCOLHA", 1, true, "Sim", "Não");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 2, true);
        var comentario = await PerguntaAsync(api, id, "O que melhorar?", "RESPOSTA_LONGA", 3);
        var opcional = await PerguntaAsync(api, id, "Algo mais?", "RESPOSTA_CURTA", 4);
        var alt = await AlternativasAsync(api, recomenda);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo(prefixo + " setor"));
        for (var i = 0; i < 4; i++)
        {
            var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo($"{prefixo} Pessoa"), Perfil.Funcionario, setor);
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES ({id}, {pessoa})");
            await ParticipacaoAsync(api, id, pessoa);
            await AnonimaAsync(api, id, recomenda, i < 3 ? "Sim" : "Não", i < 3 ? alt[0] : alt[1]);
            await AnonimaAsync(api, id, nota, (i + 2).ToString()); // 2, 3, 4, 5
        }

        foreach (var texto in new[] { "Zeladoria", "acesso", "Mais clareza" })
        {
            await AnonimaAsync(api, id, comentario, texto);
        }

        await AnonimaAsync(api, id, opcional, "um");
        await AnonimaAsync(api, id, opcional, "dois");
        if (situacao == "ENCERRADO")
        {
            await EncerrarAsync(api, id); // o trigger recusa resposta em formulário inativo: encerra DEPOIS de gravar
        }

        return new Clima(id, recomenda, nota, comentario, opcional, alt[0], alt[1]);
    }

    // ---------------------------------------------------------------- resultado: anônimo

    [Fact]
    public async Task O_resultado_do_anonimo_encerrado_traz_o_minimo_por_pergunta_as_contas_e_os_textos_em_ordem_alfabetica()
    {
        var api = await ApiAsync();
        var c = await ClimaAsync(api, "ZRes1");

        var resposta = await PedirResultadoAsync(api, c.Id);
        var r = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterResultadoFormulario");
        Assert.Equal((true, "ENCERRADO", 4, 3, 1, JsonValueKind.Null), (r.GetProperty("anonimo").GetBoolean(), r.GetProperty("situacao").GetString(), r.GetProperty("totalRespostas").GetInt32(), r.GetProperty("minimoPorPergunta").GetInt32(), r.GetProperty("perguntasOcultas").GetInt32(), r.GetProperty("avaliado").ValueKind));
        var perguntas = r.GetProperty("perguntas").EnumerateArray().ToList();
        Assert.Equal([c.Recomenda, c.Nota, c.Comentario], perguntas.Select(p => p.GetProperty("perguntaId").GetInt32())); // a opcional com 2 respostas some
        Assert.Equal([("Sim", 3, 75m), ("Não", 1, 25m)], perguntas[0].GetProperty("alternativas").EnumerateArray().Select(a => (a.GetProperty("texto").GetString()!, a.GetProperty("total").GetInt32(), a.GetProperty("percentual").GetDecimal())));
        var escala = perguntas[1].GetProperty("escala");
        Assert.Equal(3.5m, escala.GetProperty("media").GetDecimal());
        Assert.Equal([0, 1, 1, 1, 1], escala.GetProperty("distribuicao").EnumerateArray().Select(v => v.GetProperty("total").GetInt32()));
        Assert.Equal(["acesso", "Mais clareza", "Zeladoria"], perguntas[2].GetProperty("textos").EnumerateArray().Select(t => t.GetString()));
        Assert.Equal(3, perguntas[2].GetProperty("totalRespostas").GetInt32());
        Assert.All(perguntas, p => Assert.Equal(["alternativas", "escala", "ordem", "perguntaId", "texto", "textos", "tipo", "totalRespostas"], p.EnumerateObject().Select(x => x.Name).Order()));
    }

    [Fact]
    public async Task O_resultado_do_anonimo_so_abre_encerrado_de_verdade_e_o_prazo_vencido_com_a_tarefa_parada_ja_conta()
    {
        var api = await ApiAsync();
        var aberto = await ClimaAsync(api, "ZRes2 aberto", "PUBLICADO");
        var vencido = await ClimaAsync(api, "ZRes2 vencido", "PUBLICADO", "2026-10-06");

        var doAberto = await PedirResultadoAsync(api, aberto.Id);
        var doVencido = await PedirResultadoAsync(api, vencido.Id);

        Assert.Equal(409, (int)doAberto.StatusCode);
        Assert.Equal(("RESULTADO_ANONIMO_INDISPONIVEL", "O resultado de um formulário anônimo só aparece depois que ele é encerrado."), (Codigo(await CorpoAsync(doAberto)), (await CorpoAsync(doAberto)).GetProperty("detail").GetString()));
        Assert.Equal(200, (int)doVencido.StatusCode);
        Assert.Equal("ENCERRADO", (await CorpoAsync(doVencido)).GetProperty("situacao").GetString());
    }

    [Fact]
    public async Task Sem_nenhuma_pergunta_com_3_respostas_o_anonimo_devolve_perguntas_vazio_e_conta_as_ocultas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZRes3"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "Algo?");
        await AnonimaAsync(api, id, curta, "um");
        await AnonimaAsync(api, id, curta, "dois");
        await EncerrarAsync(api, id);

        var r = await CorpoAsync(await PedirResultadoAsync(api, id));

        Assert.Equal((0, 1), (r.GetProperty("perguntas").GetArrayLength(), r.GetProperty("perguntasOcultas").GetInt32()));
    }

    // ---------------------------------------------------------------- resultado: avaliação anônima

    [Fact]
    public async Task Na_avaliacao_anonima_o_avaliado_e_obrigatorio_precisa_de_3_respostas_e_so_aparece_o_dele()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var gestorB = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("ZRes4"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        for (var i = 0; i < 3; i++)
        {
            var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
            await ParticipacaoAsync(api, id, membro.Id, setor.GestorFuncionarioId);
            await AnonimaAsync(api, id, nota, (i + 3).ToString(), avaliado: setor.GestorFuncionarioId);
        }

        for (var i = 0; i < 2; i++)
        {
            var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, gestorB);
            await ParticipacaoAsync(api, id, membro.Id, gestorB.GestorFuncionarioId);
            await AnonimaAsync(api, id, nota, "1", avaliado: gestorB.GestorFuncionarioId);
        }

        await EncerrarAsync(api, id);
        var semAvaliado = await PedirResultadoAsync(api, id);
        var liberado = await PedirResultadoAsync(api, id, $"avaliadoId={setor.GestorFuncionarioId}");
        var abaixo = await PedirResultadoAsync(api, id, $"avaliadoId={gestorB.GestorFuncionarioId}");
        var inexistente = await PedirResultadoAsync(api, id, "avaliadoId=99999999");

        Assert.Equal((400, 200, 422, 404), ((int)semAvaliado.StatusCode, (int)liberado.StatusCode, (int)abaixo.StatusCode, (int)inexistente.StatusCode));
        Assert.True((await CorpoAsync(semAvaliado)).GetProperty("errors").TryGetProperty("avaliadoId", out _));
        var r = await CorpoAsync(liberado);
        Assert.Equal((3, setor.GestorFuncionarioId, 4.0m), (r.GetProperty("totalRespostas").GetInt32(), r.GetProperty("avaliado").GetProperty("id").GetInt32(), r.GetProperty("perguntas")[0].GetProperty("escala").GetProperty("media").GetDecimal()));
        var recusa = await CorpoAsync(abaixo);
        Assert.Equal(("RESULTADO_SEM_RESPOSTAS_SUFICIENTES", "Esta pessoa recebeu menos de 3 avaliações, então o resultado dela não aparece."), (Codigo(recusa), recusa.GetProperty("detail").GetString()));
        Assert.DoesNotContain("totalRespostas", await abaixo.Content.ReadAsStringAsync(), StringComparison.Ordinal); // a recusa não diz quantas respostas houve
    }

    // ---------------------------------------------------------------- resultado: com nome

    [Fact]
    public async Task Com_nome_o_resultado_sai_em_qualquer_situacao_sem_minimo_com_todas_as_perguntas_e_conta_os_envios()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("ZRes5"), "PUBLICADO", alvo: "PESSOAS");
        var recomenda = await PerguntaAsync(api, id, "Gostou?", "MULTIPLA_ESCOLHA", 1, true, "Sim", "Não");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 2);
        var texto = await PerguntaAsync(api, id, "Comentário", "RESPOSTA_CURTA", 3);
        var alt = await AlternativasAsync(api, recomenda);
        await NomeadaAsync(api, id, recomenda, a.Id, "Sim", "2026-10-05 10:00:00", alt[0]);
        await NomeadaAsync(api, id, texto, a.Id, "Bom", "2026-10-05 10:00:00");
        await NomeadaAsync(api, id, recomenda, b.Id, "Não", "2026-10-06 10:00:00", alt[1]);

        var resposta = await PedirResultadoAsync(api, id);
        var r = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterResultadoFormulario");
        Assert.Equal((false, "PUBLICADO", 2, JsonValueKind.Null, 0), (r.GetProperty("anonimo").GetBoolean(), r.GetProperty("situacao").GetString(), r.GetProperty("totalRespostas").GetInt32(), r.GetProperty("minimoPorPergunta").ValueKind, r.GetProperty("perguntasOcultas").GetInt32()));
        var perguntas = r.GetProperty("perguntas").EnumerateArray().ToList();
        Assert.Equal([recomenda, nota, texto], perguntas.Select(p => p.GetProperty("perguntaId").GetInt32())); // todas, até a escala sem resposta
        Assert.Equal([("Sim", 1, 50m), ("Não", 1, 50m)], perguntas[0].GetProperty("alternativas").EnumerateArray().Select(x => (x.GetProperty("texto").GetString()!, x.GetProperty("total").GetInt32(), x.GetProperty("percentual").GetDecimal())));
        Assert.Equal((JsonValueKind.Null, 0), (perguntas[1].GetProperty("escala").ValueKind, perguntas[1].GetProperty("totalRespostas").GetInt32()));
        Assert.Equal(["Bom"], perguntas[2].GetProperty("textos").EnumerateArray().Select(t => t.GetString()));
    }

    [Fact]
    public async Task O_resultado_so_e_do_RH_o_formulario_tem_de_existir_e_avaliado_fora_de_avaliacao_e_400()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZRes6"), "PUBLICADO", alvo: "PESSOAS");
        await PerguntaAsync(api, id, "Algo?");

        var inexistente = await PedirResultadoAsync(api, 99999999);
        var comAvaliado = await PedirResultadoAsync(api, id, "avaliadoId=5");
        var gestor = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(RotaDoResultado(id));
        var semLogin = await api.Api.ClienteAnonimo().GetAsync(RotaDoResultado(id));
        var gestorExporta = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync($"/api/rh/formularios/{id}/exportacao");

        Assert.Equal([404, 400, 403, 401, 403], new[] { inexistente, comAvaliado, gestor, semLogin, gestorExporta }.Select(r => (int)r.StatusCode));
        Assert.True((await CorpoAsync(comAvaliado)).GetProperty("errors").TryGetProperty("avaliadoId", out _));
    }

    // ---------------------------------------------------------------- exportação com nome

    [Fact]
    public async Task A_exportacao_com_nome_tem_uma_linha_por_resposta_com_os_dados_de_quem_respondeu_e_neutraliza_formula()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZExp1 setor"));
        var julia = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "ZExp1 Júlia Nunes", Perfil.Funcionario, setor);
        var id = await SemearAsync(api, "ZExp1 Segurança; no trabalho", "PUBLICADO", alvo: "PESSOAS");
        var gostou = await PerguntaAsync(api, id, "Recebeu o equipamento?", "MULTIPLA_ESCOLHA", 1, true, "Sim", "Não");
        var livre = await PerguntaAsync(api, id, "Comentário", "RESPOSTA_LONGA", 2);
        var alt = await AlternativasAsync(api, gostou);
        await NomeadaAsync(api, id, livre, julia, "=SOMA(1;1)", "2026-10-02 11:10:00");
        await NomeadaAsync(api, id, gostou, julia, "Sim", "2026-10-02 11:10:00", alt[0]);

        var resposta = await ExportarAsync(api, id);
        var linhas = Linhas(await CsvAsync(resposta));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("text/csv", "utf-8", $"attachment; filename=\"formulario-{id}-respostas.csv\"", "no-store"), (resposta.Content.Headers.ContentType!.MediaType, resposta.Content.Headers.ContentType.CharSet, resposta.Content.Headers.ContentDisposition!.ToString(), resposta.Headers.CacheControl!.ToString()));
        Assert.Equal("formulario;respondente;email;setor;avaliado;pergunta;resposta;respondido_em", linhas[0]);
        Assert.Equal(3, linhas.Length);
        var email = await TextoAsync(api, $"SELECT EMAIL_CORPORATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {julia}");
        var setorNome = await TextoAsync(api, $"SELECT NOME_SETOR FROM SETORES WHERE ID_SETOR = {setor}");
        Assert.Equal($"\"ZExp1 Segurança; no trabalho\";ZExp1 Júlia Nunes;{email};{setorNome};;Recebeu o equipamento?;Sim;2026-10-02 11:10", linhas[1]); // o título com ; vai entre aspas; a data em -03:00
        Assert.EndsWith(";Comentário;'=SOMA(1;1);2026-10-02 11:10", linhas[2].Replace("\"'=SOMA(1;1)\"", "'=SOMA(1;1)"), StringComparison.Ordinal); // a fórmula virou texto (com o apóstrofo) e foi entre aspas por causa do ;
    }

    [Fact]
    public async Task A_exportacao_da_avaliacao_com_nome_traz_o_avaliado_e_a_do_arquivo_traz_o_nome()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor, prefixo: "ZExp2 Membro");
        var id = await SemearAsync(api, Titulo("ZExp2"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await NomeadaAsync(api, id, nota, setor.GestorFuncionarioId, "4", "2026-10-05 10:00:00", avaliado: membro.Id);
        var arquivos = await SemearAsync(api, Titulo("ZExp2 arquivos"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var anexo = await PerguntaAsync(api, arquivos, "Comprovante", "ARQUIVO", 1);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL) VALUES ({anexo}, {arquivos}, {membro.Id}, 'residencia.pdf', '2026-10-05 10:00:00', 0, 'formularios/{Guid.NewGuid():N}.pdf', 'residencia.pdf')");

        var daAvaliacao = Linhas(await CsvAsync(await ExportarAsync(api, id)));
        var doArquivo = Linhas(await CsvAsync(await ExportarAsync(api, arquivos)));

        Assert.Contains($";{membro.Nome};Nota;4;2026-10-05 10:00", daAvaliacao[1], StringComparison.Ordinal);
        Assert.EndsWith(";Comprovante;residencia.pdf;2026-10-05 10:00", doArquivo[1], StringComparison.Ordinal);
        Assert.DoesNotContain("formularios/", string.Join("\n", doArquivo), StringComparison.Ordinal); // o caminho nunca entra no arquivo
    }

    // ---------------------------------------------------------------- exportação anônima

    [Fact]
    public async Task A_exportacao_do_anonimo_traz_so_o_resultado_geral_das_perguntas_com_3_respostas_sem_nenhum_dado_de_pessoa()
    {
        var api = await ApiAsync();
        var c = await ClimaAsync(api, "ZExp3");
        var nomes = await Sql.ColunaAsync(api.Conexao, $"SELECT F.NOME FROM FORMULARIOS_DESTINATARIOS D JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = D.ID_FUNCIONARIO WHERE D.ID_FORMULARIO = {c.Id}");

        var resposta = await ExportarAsync(api, c.Id);
        var csv = await CsvAsync(resposta);
        var linhas = Linhas(csv);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("pergunta;opcao;total;percentual", linhas[0]);
        Assert.Equal(
            [
                "Recomendaria?;Sim;3;75,0",
                "Recomendaria?;Não;1;25,0",
                "Nota;1;0;0,0",
                "Nota;2;1;25,0",
                "Nota;3;1;25,0",
                "Nota;4;1;25,0",
                "Nota;5;1;25,0",
                "O que melhorar?;acesso;;",
                "O que melhorar?;Mais clareza;;",
                "O que melhorar?;Zeladoria;;",
            ],
            linhas[1..]); // a opcional com 2 respostas ficou de fora, sem aviso; os textos em ordem alfabética
        Assert.All(nomes, nome => Assert.DoesNotContain(nome, csv, StringComparison.Ordinal));
        Assert.DoesNotContain("@", csv, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_exportacao_do_anonimo_aberto_e_409_e_sem_nenhuma_pergunta_com_3_respostas_e_422()
    {
        var api = await ApiAsync();
        var aberto = await ClimaAsync(api, "ZExp4", "PUBLICADO");
        var pequeno = await SemearAsync(api, Titulo("ZExp4 pequeno"), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, pequeno, "Algo?");
        await AnonimaAsync(api, pequeno, curta, "um");
        await EncerrarAsync(api, pequeno);

        var doAberto = await ExportarAsync(api, aberto.Id);
        var semMinimo = await ExportarAsync(api, pequeno);

        Assert.Equal((409, 422), ((int)doAberto.StatusCode, (int)semMinimo.StatusCode));
        Assert.Equal("RESULTADO_ANONIMO_INDISPONIVEL", Codigo(await CorpoAsync(doAberto)));
        var recusa = await CorpoAsync(semMinimo);
        Assert.Equal(("RESULTADO_SEM_RESPOSTAS_SUFICIENTES", "Nenhuma pergunta deste formulário anônimo teve 3 respostas ou mais, então não há resultado para exportar."), (Codigo(recusa), recusa.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task A_exportacao_da_avaliacao_anonima_tem_uma_secao_por_avaliado_com_3_respostas_e_deixa_de_fora_os_outros_sem_aviso()
    {
        var api = await ApiAsync();
        var liberado = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var abaixo = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("ZExp5"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        foreach (var (setor, quantas) in new[] { (liberado, 3), (abaixo, 2) })
        {
            for (var i = 0; i < quantas; i++)
            {
                var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
                await ParticipacaoAsync(api, id, membro.Id, setor.GestorFuncionarioId);
                await AnonimaAsync(api, id, nota, "4", avaliado: setor.GestorFuncionarioId);
            }
        }

        await EncerrarAsync(api, id);
        var csv = await CsvAsync(await ExportarAsync(api, id));
        var linhas = Linhas(csv);

        Assert.Equal("avaliado;pergunta;opcao;total;percentual", linhas[0]);
        Assert.Equal(5, linhas.Length - 1);
        Assert.All(linhas[1..], l => Assert.StartsWith($"{liberado.GestorNome};Nota;", l, StringComparison.Ordinal));
        Assert.DoesNotContain(abaixo.GestorNome, csv, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- o evento

    [Fact]
    public async Task Exportar_grava_o_evento_BAIXOU_so_com_o_titulo_igual_no_anonimo_e_no_com_nome()
    {
        var api = await ApiAsync();
        var c = await ClimaAsync(api, "ZExp6");
        var simples = await SemearAsync(api, "ZExp6 com nome", "PUBLICADO", alvo: "PESSOAS");
        await PerguntaAsync(api, simples, "Algo?");

        await ExportarAsync(api, c.Id);
        await ExportarAsync(api, simples);

        var doAnonimo = Assert.Single(await EventosAsync(api, c.Id));
        var doComNome = Assert.Single(await EventosAsync(api, simples));
        var titulo = await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {c.Id}");
        Assert.Equal((AuditoriaArea.Formularios, AuditoriaAcao.Baixou, $"Exportou as respostas do formulário \"{titulo}\" em CSV."), (doAnonimo.Area, doAnonimo.Acao, doAnonimo.Descricao));
        Assert.Equal((AuditoriaAcao.Baixou, "Exportou as respostas do formulário \"ZExp6 com nome\" em CSV."), (doComNome.Acao, doComNome.Descricao));
    }
}

using System.Text.RegularExpressions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// <c>responderFormulario</c> em formulário ANÔNIMO, no banco REAL (relógio em 2026-10-07T12:42Z; o formulário foi publicado em 2026-10-01 08:00 locais). A resposta é gravada SEM autor, sem candidato, sem arquivo e com a data do DIA da publicação da rodada (00:00 -03:00), nunca a hora do envio; a participação
/// (a pessoa e o avaliado, sem data e sem ligação com as linhas) leva um UUID v4 da aplicação, devolvido no recibo; uma participação por pessoa (e por avaliado) vale também com dois envios ao mesmo tempo; NADA é gravado na auditoria; as recusas têm o mesmo corpo para quem já participou e para quem não.
/// </summary>
[Collection(MySqlCollection.Nome)]
public partial class ResponderAnonimoTests(MySqlFixture mysql)
{
    private static readonly Regex UuidV4 = UuidV4Regex();

    [GeneratedRegex("^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$")]
    private static partial Regex UuidV4Regex();

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ResponderAnonimoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private sealed record Anonimo(int Id, int Curta, int Escala);

    /// <summary>Um formulário anônimo publicado para as pessoas (as linhas de destinatário são a fotografia): uma curta obrigatória e uma escala obrigatória.</summary>
    private static async Task<Anonimo> NovoAsync(ApiDeAcesso api, string prefixo, params int[] pessoas)
    {
        var id = await SemearAsync(api, Titulo(prefixo), "PUBLICADO", anonimo: true, alvo: "PESSOAS");
        var curta = await PerguntaAsync(api, id, "O que acha da empresa?", "RESPOSTA_CURTA", 1, true);
        var escala = await PerguntaAsync(api, id, "De 1 a 5", "ESCALA", 2, true);
        await EscolherPessoasAsync(api, id, pessoas);
        return new Anonimo(id, curta, escala);
    }

    private static string Corpo(Anonimo a, string texto = "Gosto muito.") => Json(new { perguntaId = a.Curta, texto }, new { perguntaId = a.Escala, valorEscala = 5 });

    private static async Task<long> ParticipacoesAsync(ApiDeAcesso api, int formularioId) => await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {formularioId}");

    [Fact]
    public async Task Responder_anonimo_grava_as_linhas_sem_autor_com_a_data_do_dia_da_publicacao_e_a_participacao_sem_rastro_na_auditoria()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var a = await NovoAsync(api, "Zanon1", eu);
        var auditoriaAntes = await AuditoriaAsync(api);

        var resposta = await ResponderAsync(cliente, a.Id, Envio(Corpo(a)));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("responderFormulario");
        var recibo = await CorpoAsync(resposta);
        var participacaoId = recibo.GetProperty("participacaoId").GetString()!;
        Assert.Matches(UuidV4, participacaoId);
        Assert.Equal((a.Id, 0), (recibo.GetProperty("formularioId").GetInt32(), recibo.GetProperty("restantes").GetInt32()));
        Assert.Equal(2, await LinhasAsync(api, a.Id));
        Assert.Equal("0,0,2,0", await TextoAsync(api, $"SELECT CONCAT(COUNT(ID_FUNCIONARIO), ',', COUNT(ID_FORMULARIO_CANDIDATO), ',', SUM(ANONIMO), ',', COUNT(CAMINHO_ARQUIVO)) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {a.Id}")); // sem autor, sem candidato, anônima, sem arquivo
        Assert.Equal("2026-10-01 00:00:00", await TextoAsync(api, $"SELECT GROUP_CONCAT(DISTINCT DATE_FORMAT(DATA_RESPOSTA, '%Y-%m-%d %H:%i:%s')) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {a.Id}")); // o dia da publicação, nunca a hora do envio
        Assert.Equal($"1,{eu},{participacaoId}", await TextoAsync(api, $"SELECT CONCAT(COUNT(*), ',', MIN(ID_FUNCIONARIO), ',', MIN(ID_PARTICIPACAO)) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {a.Id}"));
        Assert.Equal(auditoriaAntes, await AuditoriaAsync(api)); // nem o autor, nem a hora, nem a participação
    }

    [Fact]
    public async Task Duas_abas_dao_um_201_e_um_409_com_uma_participacao_so_e_responder_de_novo_e_o_mesmo_409_do_formulario_com_nome()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var a = await NovoAsync(api, "Zanon2", eu);

        var juntas = await Task.WhenAll(ResponderAsync(cliente, a.Id, Envio(Corpo(a))), ResponderAsync(cliente, a.Id, Envio(Corpo(a))));
        var depois = await ResponderAsync(cliente, a.Id, Envio(Corpo(a)));

        Assert.Equal([201, 409], juntas.Select(r => (int)r.StatusCode).Order());
        var conflito = await CorpoAsync(juntas.Single(r => (int)r.StatusCode == 409));
        Assert.Equal(("JA_RESPONDIDO", "Você já respondeu este formulário."), (Codigo(conflito), conflito.GetProperty("detail").GetString()));
        Assert.Equal(409, (int)depois.StatusCode);
        Assert.Equal((1, 2), (await ParticipacoesAsync(api, a.Id), await LinhasAsync(api, a.Id))); // uma participação e as duas linhas de UMA resposta
    }

    [Fact]
    public async Task Pessoa_inativa_na_fotografia_e_barrada_pela_aplicacao_com_422_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var a = await NovoAsync(api, "Zanon3", eu);
        await AdvertenciasDeTeste.InativarAsync(api, eu);

        var resposta = await ResponderAsync(cliente, a.Id, Envio(Corpo(a)));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal((0, 0), (await LinhasAsync(api, a.Id), await ParticipacoesAsync(api, a.Id)));
    }

    [Fact]
    public async Task Na_avaliacao_do_gestor_anonima_cada_pessoa_da_equipe_participa_uma_vez_e_so_avalia_o_gestor_da_fotografia()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membro = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, membro.Id);
        var estranho = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var id = await SemearAsync(api, Titulo("Zanon4 avaliação"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor.SetorId})");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ({id}, {membro.Id}, {setor.GestorFuncionarioId})"); // a fotografia
        var corpo = Json(new { perguntaId = nota, valorEscala = 2 });

        var errado = await ResponderAsync(cliente, id, Envio(corpo, estranho.GestorFuncionarioId));
        var primeira = await ResponderAsync(cliente, id, Envio(corpo, setor.GestorFuncionarioId));
        var repetida = await ResponderAsync(cliente, id, Envio(corpo, setor.GestorFuncionarioId));

        Assert.Equal((422, 201, 409), ((int)errado.StatusCode, (int)primeira.StatusCode, (int)repetida.StatusCode));
        Assert.Equal("AVALIADO_INVALIDO", Codigo(await CorpoAsync(errado)));
        Assert.Matches(UuidV4, (await CorpoAsync(primeira)).GetProperty("participacaoId").GetString()!);
        Assert.Equal(0, (await CorpoAsync(primeira)).GetProperty("restantes").GetInt32());
        Assert.Equal($"1,{setor.GestorFuncionarioId}", await TextoAsync(api, $"SELECT CONCAT(COUNT(*), ',', MIN(ID_AVALIADO)) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}"));
        Assert.Equal($"1,{setor.GestorFuncionarioId}", await TextoAsync(api, $"SELECT CONCAT(COUNT(*), ',', MIN(ID_AVALIADO)) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Arquivo_em_formulario_anonimo_e_400_em_arquivos_e_nada_e_gravado_nem_guardado()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var a = await NovoAsync(api, "Zanon5", eu);
        var guardadosAntes = api.Api.Armazenamento.Gravados.Count;

        var resposta = await ResponderAsync(cliente, a.Id, Envio(Corpo(a), null, ("anexo.pdf", ArquivosDeTeste.Pdf())));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await ErrosAsync(resposta)).TryGetProperty("arquivos", out _));
        Assert.Equal((0, 0), (await LinhasAsync(api, a.Id), await ParticipacoesAsync(api, a.Id)));
        Assert.Equal(guardadosAntes, api.Api.Armazenamento.Gravados.Count);
    }

    [Fact]
    public async Task Encerrar_na_hora_de_responder_e_422_e_nao_grava_resposta_nem_participacao()
    {
        var api = await ApiAsync();
        var a = await NovoAsync(api, "Zanon6", api.Elenco.Rh.FuncionarioId);
        var cliente = await ClienteComOutraAbaAsync(api, () => Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {a.Id}"));

        var resposta = await ResponderAsync(cliente, a.Id, Envio(Corpo(a)));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FORMULARIO_ENCERRADO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal((0, 0), (await LinhasAsync(api, a.Id), await ParticipacoesAsync(api, a.Id)));
    }

    [Fact]
    public async Task As_recusas_tem_o_mesmo_corpo_para_quem_ja_participou_e_para_quem_nao()
    {
        var api = await ApiAsync();
        var (participou, a) = await PessoaAsync(api);
        var (nao, b) = await PessoaAsync(api);
        var f = await NovoAsync(api, "Zanon7", a, b);
        await ResponderAsync(participou, f.Id, Envio(Corpo(f)));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {f.Id}");

        var daQuemParticipou = await ResponderAsync(participou, f.Id, Envio(Corpo(f)));
        var daQuemNao = await ResponderAsync(nao, f.Id, Envio(Corpo(f)));
        var minhasDeQuemParticipou = await participou.GetAsync($"/api/eu/formularios/{f.Id}/minhas-respostas");
        var minhasDeQuemNao = await nao.GetAsync($"/api/eu/formularios/{f.Id}/minhas-respostas");

        Assert.Equal((422, 422), ((int)daQuemParticipou.StatusCode, (int)daQuemNao.StatusCode));
        Assert.Equal(Sem(await CorpoAsync(daQuemParticipou)), Sem(await CorpoAsync(daQuemNao)));
        Assert.Equal((422, 422), ((int)minhasDeQuemParticipou.StatusCode, (int)minhasDeQuemNao.StatusCode));
        Assert.Equal(Sem(await CorpoAsync(minhasDeQuemParticipou)), Sem(await CorpoAsync(minhasDeQuemNao)));
        Assert.Equal("FORMULARIO_ANONIMO", Codigo(await CorpoAsync(minhasDeQuemParticipou)));
    }

    /// <summary>O corpo da recusa sem o <c>traceId</c> (que muda de requisição para requisição).</summary>
    private static string Sem(System.Text.Json.JsonElement problema) => string.Join('|', problema.EnumerateObject().Where(p => p.Name != "traceId").Select(p => $"{p.Name}={p.Value}"));

    [Fact]
    public async Task Quem_participou_ve_o_cartao_como_respondido_sem_data_e_sem_ver_minhas_respostas()
    {
        var api = await ApiAsync();
        var (cliente, eu) = await PessoaAsync(api);
        var a = await NovoAsync(api, "Zanon8", eu);
        await ResponderAsync(cliente, a.Id, Envio(Corpo(a)));

        var lista = await CorpoAsync(await cliente.GetAsync("/api/eu/formularios?situacao=RESPONDIDO&busca=Zanon8"));
        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray());

        Assert.Equal(("RESPONDIDO", 1, 1, false, System.Text.Json.JsonValueKind.Null), (item.GetProperty("situacao").GetString(), item.GetProperty("respondidos").GetInt32(), item.GetProperty("total").GetInt32(), item.GetProperty("podeVerMinhasRespostas").GetBoolean(), item.GetProperty("respondidoEmUtc").ValueKind));
    }
}

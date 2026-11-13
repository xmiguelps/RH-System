using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>listarParticipantesDeTreinamento</c> e <c>exportarParticipantesDeTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. "Quem fez" lista só quem tem linha (inativos continuam; <c>REPROVADO</c> e <c>CANCELADO</c> nunca), com o instante da conclusão (o do último módulo, ou o dia gravado às 00:00 em -03:00 quando não há progresso), sem nota; as contagens ignoram a aba e respeitam setor e busca; o nulo de início e conclusão fica por
/// último nos dois sentidos; o CSV tem BOM, <c>;</c>, datas <c>dd/MM/aaaa</c>, situação em português, célula neutralizada e o evento <c>BAIXOU</c> sem nomes. Cada treinamento tem os SEUS participantes, então os testes da classe não se misturam.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ParticipantesDoTreinamentoTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento, string sufixo = "") => $"/api/rh/treinamentos/{treinamento}/participantes{sufixo}";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ParticipantesDoTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, setor, ativa, new DateOnly(2026, 1, 2));

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null, string? conclusao = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO, DATA_CONCLUSAO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")}, {(conclusao is null ? "NULL" : $"'{conclusao}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api, int treinamento, string consulta = "") => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota(treinamento)}{(consulta.Length == 0 ? string.Empty : "?" + consulta)}"));

    private static string[] Nomes(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()!)];

    private static (int, int, int, int) Contagens(JsonElement lista)
    {
        var c = lista.GetProperty("contagens");
        return (c.GetProperty("total").GetInt32(), c.GetProperty("concluidos").GetInt32(), c.GetProperty("emAndamento").GetInt32(), c.GetProperty("naoIniciados").GetInt32());
    }

    /// <summary>Um treinamento obrigatório que emite, com 4 módulos, e cinco participantes: A concluiu (com progresso), B concluiu antes dos módulos (só o dia), C não iniciou, D em andamento com 1 módulo, E (inativa) não iniciou; F está REPROVADO e G CANCELADO (nunca aparecem).</summary>
    private static async Task<(int Treinamento, Dictionary<string, int> Pessoas, int[] Modulos)> CenarioAsync(ApiDeAcesso api, string prefixo, int? setorA = null, int? setorB = null)
    {
        var t = await SemearAsync(api, Titulo(prefixo), "EM_ANDAMENTO", obrigatorio: true, emite: true);
        var modulos = new int[4];
        for (var i = 0; i < 4; i++)
        {
            modulos[i] = await ModuloAsync(api, t, i + 1, "TEXTO", duracao: 10);
        }

        var pessoas = new Dictionary<string, int>
        {
            ["A"] = await PessoaAsync(api, $"{prefixo} A Ana", setorA),
            ["B"] = await PessoaAsync(api, $"{prefixo} B Bia", setorB),
            ["C"] = await PessoaAsync(api, $"{prefixo} C Caio", setorA),
            ["D"] = await PessoaAsync(api, $"{prefixo} D Dani", setorB),
            ["E"] = await PessoaAsync(api, $"{prefixo} E Edu"),
            ["F"] = await PessoaAsync(api, $"{prefixo} F Fabi"),
            ["G"] = await PessoaAsync(api, $"{prefixo} G Gabi"),
        };
        await LinhaAsync(api, t, pessoas["A"], "CONCLUIDO", "2026-08-02", "2026-09-15");
        foreach (var m in modulos)
        {
            await ProgressoAsync(api, m, pessoas["A"], m == modulos[3] ? "2026-09-15 11:05:00" : "2026-08-02 10:00:00");
        }

        await LinhaAsync(api, t, pessoas["B"], "CONCLUIDO", "2026-08-05", "2026-08-20"); // concluiu antes dos módulos existirem: sem progresso
        await LinhaAsync(api, t, pessoas["C"], "INSCRITO");
        await LinhaAsync(api, t, pessoas["D"], "EM_ANDAMENTO", "2026-09-12");
        await ProgressoAsync(api, modulos[0], pessoas["D"], "2026-09-12 09:00:00");
        await LinhaAsync(api, t, pessoas["E"], "INSCRITO");
        await AdvertenciasDeTeste.InativarAsync(api, pessoas["E"]);
        await LinhaAsync(api, t, pessoas["F"], "REPROVADO");
        await LinhaAsync(api, t, pessoas["G"], "CANCELADO");
        return (t, pessoas, modulos);
    }

    [Fact]
    public async Task A_lista_traz_so_quem_tem_linha_valida_com_inativos_o_instante_da_conclusao_e_o_cabecalho()
    {
        var api = await ApiAsync();
        var (t, pessoas, _) = await CenarioAsync(api, Titulo("ZPar1"));

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(t));
        var corpo = await CorpoAsync(resposta);
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarParticipantesDeTreinamento");
        Assert.Equal((5, 2, 1, 2), Contagens(corpo)); // F e G nunca entram
        Assert.Equal(5, corpo.GetProperty("total").GetInt32());
        var cabecalho = corpo.GetProperty("treinamento");
        Assert.Equal((t, true, true, "EM_ANDAMENTO", "TODOS", 4), (cabecalho.GetProperty("id").GetInt32(), cabecalho.GetProperty("obrigatorio").GetBoolean(), cabecalho.GetProperty("emiteCertificado").GetBoolean(), cabecalho.GetProperty("situacao").GetString(), cabecalho.GetProperty("publico").GetString(), cabecalho.GetProperty("totalModulos").GetInt32()));
        Assert.Equal([pessoas["A"], pessoas["B"], pessoas["C"], pessoas["D"], pessoas["E"]], itens.Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32())); // por nome
        Assert.Equal((pessoas["A"], "CONCLUIDO", "2026-08-02", "2026-09-15T14:05:00Z", 4, true), (itens[0].GetProperty("funcionario").GetProperty("id").GetInt32(), itens[0].GetProperty("situacao").GetString(), itens[0].GetProperty("iniciadoEm").GetString(), itens[0].GetProperty("concluidoEmUtc").GetString(), itens[0].GetProperty("modulosConcluidos").GetInt32(), itens[0].GetProperty("certificadoDisponivel").GetBoolean()));
        Assert.Equal(("CONCLUIDO", "2026-08-20T03:00:00Z", 0, true), (itens[1].GetProperty("situacao").GetString(), itens[1].GetProperty("concluidoEmUtc").GetString(), itens[1].GetProperty("modulosConcluidos").GetInt32(), itens[1].GetProperty("certificadoDisponivel").GetBoolean())); // sem progresso: o dia às 00:00 em -03:00
        Assert.Equal(("INSCRITO", JsonValueKind.Null, JsonValueKind.Null, 0, false), (itens[2].GetProperty("situacao").GetString(), itens[2].GetProperty("iniciadoEm").ValueKind, itens[2].GetProperty("concluidoEmUtc").ValueKind, itens[2].GetProperty("modulosConcluidos").GetInt32(), itens[2].GetProperty("certificadoDisponivel").GetBoolean()));
        Assert.Equal(("EM_ANDAMENTO", "2026-09-12", JsonValueKind.Null, 1, false), (itens[3].GetProperty("situacao").GetString(), itens[3].GetProperty("iniciadoEm").GetString(), itens[3].GetProperty("concluidoEmUtc").ValueKind, itens[3].GetProperty("modulosConcluidos").GetInt32(), itens[3].GetProperty("certificadoDisponivel").GetBoolean()));
        Assert.False(itens[4].GetProperty("funcionario").GetProperty("ativo").GetBoolean()); // inativo continua na lista
        Assert.DoesNotContain("nota", itens[0].EnumerateObject().Select(p => p.Name));
    }

    [Fact]
    public async Task Cada_aba_o_setor_e_a_busca_filtram_e_as_contagens_ignoram_a_aba_mas_respeitam_o_setor_e_a_busca()
    {
        var api = await ApiAsync();
        var setorA = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPar2 setor A"));
        var setorB = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPar2 setor B"));
        var (t, pessoas, _) = await CenarioAsync(api, Titulo("ZPar2"), setorA, setorB);

        var concluido = await ListarAsync(api, t, "situacao=CONCLUIDO");
        var andamento = await ListarAsync(api, t, "situacao=EM_ANDAMENTO");
        var inscrito = await ListarAsync(api, t, "situacao=INSCRITO");
        var doSetorA = await ListarAsync(api, t, $"setorId={setorA}");
        var buscaCaio = await ListarAsync(api, t, "busca=caio");
        var abaESetor = await ListarAsync(api, t, $"situacao=CONCLUIDO&setorId={setorB}");

        Assert.Equal([pessoas["A"], pessoas["B"]], concluido.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal([pessoas["D"]], andamento.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal([pessoas["C"], pessoas["E"]], inscrito.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal((5, 2, 1, 2), Contagens(concluido)); // a aba não muda as contagens
        Assert.Equal((2, 1, 0, 1), Contagens(doSetorA)); // A (concluída) e C (não iniciada)
        Assert.Equal([pessoas["A"], pessoas["C"]], doSetorA.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal([pessoas["C"]], buscaCaio.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal((1, 0, 0, 1), Contagens(buscaCaio));
        Assert.Equal([pessoas["B"]], abaESetor.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()));
        Assert.Equal((2, 1, 1, 0), Contagens(abaESetor)); // setor B: B (concluída) e D (em andamento)
    }

    [Fact]
    public async Task A_ordem_por_nome_setor_situacao_inicio_e_conclusao_com_o_nulo_sempre_por_ultimo_e_a_pagina_corta_a_lista()
    {
        var api = await ApiAsync();
        var setorA = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPar3 A setor"));
        var setorB = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZPar3 B setor"));
        var (t, pessoas, _) = await CenarioAsync(api, Titulo("ZPar3"), setorA, setorB);
        int[] Ids(JsonElement l) => [.. l.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32())];
        var (a, b, c, d, e) = (pessoas["A"], pessoas["B"], pessoas["C"], pessoas["D"], pessoas["E"]);

        Assert.Equal([a, b, c, d, e], Ids(await ListarAsync(api, t))); // NOME ASC é o padrão
        Assert.Equal([e, d, c, b, a], Ids(await ListarAsync(api, t, "direcao=DESC")));
        Assert.Equal([a, c, b, d, e], Ids(await ListarAsync(api, t, "ordenarPor=SETOR"))); // setor A (A, C), setor B (B, D), sem setor por último
        Assert.Equal([b, d, a, c, e], Ids(await ListarAsync(api, t, "ordenarPor=SETOR&direcao=DESC"))); // sem setor continua por último
        Assert.Equal([c, e, d, a, b], Ids(await ListarAsync(api, t, "ordenarPor=SITUACAO"))); // não iniciado (C, E), em andamento (D), concluído (A, B)
        Assert.Equal([a, b, d, c, e], Ids(await ListarAsync(api, t, "ordenarPor=SITUACAO&direcao=DESC")));
        Assert.Equal([a, b, d, c, e], Ids(await ListarAsync(api, t, "ordenarPor=INICIO"))); // 02/08, 05/08, 12/09; sem início por último
        Assert.Equal([d, b, a, c, e], Ids(await ListarAsync(api, t, "ordenarPor=INICIO&direcao=DESC"))); // sem início continua por último
        Assert.Equal([b, a, c, d, e], Ids(await ListarAsync(api, t, "ordenarPor=CONCLUSAO"))); // 20/08, 15/09; sem conclusão por último
        Assert.Equal([a, b, c, d, e], Ids(await ListarAsync(api, t, "ordenarPor=CONCLUSAO&direcao=DESC"))); // 15/09, 20/08; sem conclusão continua por último
        var segunda = await ListarAsync(api, t, "tamanhoPagina=2&pagina=2");
        Assert.Equal([c, d], Ids(segunda));
        Assert.Equal((5, 3, 2), (segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32(), segunda.GetProperty("pagina").GetInt32()));
    }

    [Fact]
    public async Task Rascunho_e_lista_vazia_inexistente_e_404_e_o_encerrado_continua_consultavel_com_quem_nao_comecou_e_quem_terminou()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZPar4 rascunho"));
        var encerrado = await SemearAsync(api, Titulo("ZPar4 encerrado"), "ENCERRADO", obrigatorio: true);
        var naoComecou = await PessoaAsync(api, Titulo("ZPar4 nao comecou"));
        var terminou = await PessoaAsync(api, Titulo("ZPar4 terminou"));
        await LinhaAsync(api, encerrado, naoComecou, "INSCRITO");
        await LinhaAsync(api, encerrado, terminou, "CONCLUIDO", "2026-09-05", "2026-10-03"); // terminou depois do encerramento

        var vazio = await ListarAsync(api, rascunho);
        var depoisDoEncerramento = await ListarAsync(api, encerrado);
        var inexistente = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(987654));

        Assert.Equal((0, 0), (vazio.GetProperty("itens").GetArrayLength(), vazio.GetProperty("total").GetInt32()));
        Assert.Equal((0, 0, 0, 0), Contagens(vazio));
        Assert.Equal("RASCUNHO", vazio.GetProperty("treinamento").GetProperty("situacao").GetString());
        Assert.Equal((2, 1, 0, 1), Contagens(depoisDoEncerramento));
        Assert.Equal(("ENCERRADO", JsonValueKind.String), (depoisDoEncerramento.GetProperty("treinamento").GetProperty("situacao").GetString(), depoisDoEncerramento.GetProperty("treinamento").GetProperty("encerradoEmUtc").ValueKind));
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    [Fact]
    public async Task Parametro_invalido_devolve_400_com_todos_os_campos_juntos_e_so_o_RH_acessa_as_duas_rotas()
    {
        var api = await ApiAsync();
        var t = await SemearAsync(api, Titulo("ZPar5"), "EM_ANDAMENTO", obrigatorio: true);
        var rh = await api.ClienteDoRhAgoraAsync();

        var invalido = await rh.GetAsync($"{Rota(t)}?situacao=XPTO&ordenarPor=NOTA&direcao=PARA_O_LADO&pagina=0");
        var exportacaoInvalida = await rh.GetAsync($"{Rota(t, "/exportacao")}?situacao=XPTO");
        var respostas = new[]
        {
            await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync(Rota(t)), await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync(Rota(t, "/exportacao")),
            await api.Api.ClienteAnonimo().GetAsync(Rota(t)), await api.Api.ClienteAnonimo().GetAsync(Rota(t, "/exportacao")),
        };

        Assert.Equal(400, (int)invalido.StatusCode);
        await invalido.AfirmarConformeAoContratoAsync("listarParticipantesDeTreinamento");
        Assert.Equal(["direcao", "ordenarPor", "pagina", "situacao"], (await CorpoAsync(invalido)).GetProperty("errors").EnumerateObject().Select(e => e.Name).Order(StringComparer.Ordinal));
        Assert.Equal(400, (int)exportacaoInvalida.StatusCode);
        Assert.Equal([403, 403, 401, 401], respostas.Select(r => (int)r.StatusCode));
    }

    // ---------------------------------------------------------------- CSV

    [Fact]
    public async Task O_CSV_tem_BOM_ponto_e_virgula_datas_em_dia_mes_ano_situacao_em_portugues_e_o_nome_do_arquivo_com_o_titulo()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Financeiro ZPar6");
        var titulo = "LGPD na prática ZPar6";
        var t = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true);
        var bruno = await PessoaAsync(api, "Bruno Lima ZPar6", setor);
        var diego = await PessoaAsync(api, "Diego Farias ZPar6");
        var joao = await PessoaAsync(api, "João Pedro ZPar6", setor);
        _ = await ModuloAsync(api, t, 1);
        await LinhaAsync(api, t, bruno, "CONCLUIDO", "2026-08-02", "2026-09-15");
        await ProgressoAsync(api, (int)await ContarAsync(api, $"SELECT ID_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {t}"), bruno, "2026-09-15 22:30:00"); // 22:30 em -03:00 = 01:30 do dia seguinte em UTC: o CSV mostra o dia LOCAL
        await LinhaAsync(api, t, diego, "INSCRITO");
        await LinhaAsync(api, t, joao, "EM_ANDAMENTO", "2026-09-12");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(t, "/exportacao"));
        var bytes = await resposta.Content.ReadAsByteArrayAsync();
        var texto = new UTF8Encoding(false).GetString(bytes[3..]);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal([0xEF, 0xBB, 0xBF], bytes[..3].Select(b => (int)b)); // BOM
        Assert.StartsWith("text/csv", resposta.Content.Headers.ContentType!.ToString(), StringComparison.Ordinal);
        Assert.Equal("attachment; filename=\"treinamento-lgpd-na-pratica-zpar6.csv\"", resposta.Content.Headers.ContentDisposition!.ToString());
        Assert.Contains("no-store", resposta.Headers.CacheControl!.ToString(), StringComparison.Ordinal);
        Assert.Equal("Pessoa;Setor;Situação;Início;Conclusão\r\nBruno Lima ZPar6;Financeiro ZPar6;Concluído;02/08/2026;15/09/2026\r\nDiego Farias ZPar6;;Não iniciado;;\r\nJoão Pedro ZPar6;Financeiro ZPar6;Em andamento;12/09/2026;\r\n", texto);
        var evento = Assert.Single(await EventosAsync(api, t));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Baixou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain("Bruno", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_CSV_aceita_os_mesmos_filtros_da_lista_devolve_todas_as_linhas_e_neutraliza_a_celula_que_comeca_com_formula()
    {
        var api = await ApiAsync();
        var t = await SemearAsync(api, Titulo("ZPar7"), "EM_ANDAMENTO", obrigatorio: true);
        var pessoas = new List<int>();
        for (var i = 1; i <= 25; i++)
        {
            var id = await PessoaAsync(api, $"ZPar7 pessoa {i:00}");
            pessoas.Add(id);
            await LinhaAsync(api, t, id, i % 5 == 0 ? "CONCLUIDO" : "INSCRITO", i % 5 == 0 ? "2026-09-01" : null, i % 5 == 0 ? "2026-09-02" : null);
        }

        var perigosa = await PessoaAsync(api, "=SOMA(1;1) ZPar7");
        await LinhaAsync(api, t, perigosa, "INSCRITO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var tudo = await (await rh.GetAsync(Rota(t, "/exportacao"))).Content.ReadAsStringAsync();
        var soConcluidos = await (await rh.GetAsync($"{Rota(t, "/exportacao")}?situacao=CONCLUIDO")).Content.ReadAsStringAsync();
        var porBusca = await (await rh.GetAsync($"{Rota(t, "/exportacao")}?busca=pessoa%2007")).Content.ReadAsStringAsync();

        Assert.Equal(27, tudo.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // cabeçalho e 26 linhas: sem paginação (mais de 20)
        Assert.Equal(6, soConcluidos.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // cabeçalho e 5 concluídos
        Assert.Equal(2, porBusca.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.DoesNotContain("\r\n=SOMA", tudo, StringComparison.Ordinal); // a célula não começa mais com "="
        Assert.Contains("'=SOMA(1;1) ZPar7", tudo, StringComparison.Ordinal);
    }
}

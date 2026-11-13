using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>listarTreinamentosDeFuncionario</c> (a aba da ficha do RH) e as três rotas do gestor (<c>listarTreinamentosDaEquipe</c>, <c>listarPessoasDaEquipeNoTreinamento</c>, <c>listarTreinamentosDePessoaDaEquipe</c>), no banco REAL com o relógio em 2026-10-07T12:42Z. O gestor só vê a equipe de hoje (ativos, admissão começada, do setor Comercial, SEM ele) e NUNCA certificado, código nem nota;
/// a pendência é a definição única (obrigatório em andamento em que a pessoa ativa está <code>INSCRITO</code> ou <code>EM_ANDAMENTO</code>) e é o mesmo número do indicador <c>treinamentosPendentes</c> da ficha. Os testes da classe dividem o banco: cada um filtra o que lê pelo prefixo do título.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EquipeETreinamentosTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EquipeETreinamentosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpClient> GestorAsync(ApiDeAcesso api) => await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId);

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null, string? conclusao = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO, DATA_CONCLUSAO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")}, {(conclusao is null ? "NULL" : $"'{conclusao}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    private static async Task PublicoDoSetorAsync(ApiDeAcesso api, int treinamento, int setor) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({treinamento}, {setor})");

    private static async Task PublicoDaPessoaAsync(ApiDeAcesso api, int treinamento, int pessoa) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({treinamento}, {pessoa})");

    private static List<JsonElement> Itens(JsonElement lista, string prefixo) => [.. lista.GetProperty("itens").EnumerateArray().Where(i => i.GetProperty("titulo").GetString()!.StartsWith(prefixo, StringComparison.Ordinal))];

    // ---------------------------------------------------------------- a lista da equipe (#28)

    [Fact]
    public async Task A_lista_do_gestor_traz_so_publicados_que_alcancam_a_equipe_com_os_numeros_dela_sem_o_gestor_e_a_ordem_fixa()
    {
        var api = await ApiAsync();
        var p = Titulo("ZEqu1");
        var (gestor, a, b, outroSetor, comercial, financeiro) = (api.Elenco.Gestor.FuncionarioId, api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroB.FuncionarioId, api.Elenco.DeOutroSetor.FuncionarioId, api.Elenco.SetorComercialId, api.Elenco.SetorFinanceiroId);
        var obrigatorio = await SemearAsync(api, $"{p} B obrigatorio", "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, obrigatorio, a, "CONCLUIDO");
        await LinhaAsync(api, obrigatorio, b, "EM_ANDAMENTO", "2026-10-02");
        await LinhaAsync(api, obrigatorio, gestor, "INSCRITO"); // o gestor nunca entra nas contas
        await LinhaAsync(api, obrigatorio, outroSetor, "CONCLUIDO"); // outro setor também não
        var antigo = await SemearAsync(api, $"{p} A obrigatorio antigo", "EM_ANDAMENTO", obrigatorio: true);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '2026-08-01' WHERE ID_TREINAMENTO = {antigo}");
        await LinhaAsync(api, antigo, a, "INSCRITO");
        var opcionalDoSetor = await SemearAsync(api, $"{p} C opcional agendado", "AGENDADO", obrigatorio: false, publico: "SETORES");
        await PublicoDoSetorAsync(api, opcionalDoSetor, comercial);
        var opcionalDaPessoa = await SemearAsync(api, $"{p} D opcional da pessoa", "EM_ANDAMENTO", obrigatorio: false, publico: "PESSOAS");
        await PublicoDaPessoaAsync(api, opcionalDaPessoa, a);
        await LinhaAsync(api, opcionalDaPessoa, a, "INSCRITO");
        var doFinanceiro = await SemearAsync(api, $"{p} E so do financeiro", "EM_ANDAMENTO", obrigatorio: false, publico: "SETORES");
        await PublicoDoSetorAsync(api, doFinanceiro, financeiro);
        _ = await SemearAsync(api, $"{p} F rascunho", obrigatorio: true);
        var encerrado = await SemearAsync(api, $"{p} G encerrado", "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, encerrado, b, "CONCLUIDO");

        var tamanhoDaEquipe = (await CorpoAsync(await (await GestorAsync(api)).GetAsync("/api/gestor/equipe"))).GetProperty("total").GetInt32(); // outros testes da classe podem pôr gente no setor
        var resposta = await (await GestorAsync(api)).GetAsync("/api/gestor/treinamentos");
        var itens = Itens(await CorpoAsync(resposta), p);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarTreinamentosDaEquipe");
        Assert.Equal([antigo, encerrado, obrigatorio, opcionalDaPessoa, opcionalDoSetor], itens.Select(i => i.GetProperty("id").GetInt32())); // obrigatórios pelo comecaEm (01/08, 01/09, 01/10), depois os opcionais (01/10, 20/10); sem o do financeiro e sem o rascunho
        (int, int, int, string) Numeros(int posicao) => (itens[posicao].GetProperty("totalNaEquipe").GetInt32(), itens[posicao].GetProperty("concluiram").GetInt32(), itens[posicao].GetProperty("inscritos").GetInt32(), itens[posicao].GetProperty("situacao").GetString()!);
        Assert.Equal((1, 0, 1, "EM_ANDAMENTO"), Numeros(0)); // só A tem linha no antigo
        Assert.Equal((1, 1, 1, "ENCERRADO"), Numeros(1));
        Assert.Equal((2, 1, 2, "EM_ANDAMENTO"), Numeros(2)); // A concluiu, B em andamento; o gestor e o outro setor ficam de fora
        Assert.Equal((1, 0, 1, "EM_ANDAMENTO"), Numeros(3)); // opcional da pessoa: só ela está no público e se inscreveu
        Assert.Equal((tamanhoDaEquipe, 0, 0, "AGENDADO"), Numeros(4)); // opcional do setor: a equipe inteira, ninguém inscrito
        Assert.DoesNotContain("certificado", await resposta.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task O_gestor_sem_setor_recebe_lista_vazia_e_404_nas_rotas_de_pessoa_e_de_treinamento()
    {
        var api = await ApiAsync();
        var t = await SemearAsync(api, Titulo("ZEqu2"), "EM_ANDAMENTO", obrigatorio: true);
        var semSetor = await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId);

        var lista = await semSetor.GetAsync("/api/gestor/treinamentos");
        var equipe = await semSetor.GetAsync($"/api/gestor/treinamentos/{t}/equipe");
        var ficha = await semSetor.GetAsync($"/api/gestor/equipe/{api.Elenco.MembroA.FuncionarioId}/treinamentos");

        Assert.Equal([200, 404, 404], new[] { lista, equipe, ficha }.Select(r => (int)r.StatusCode));
        Assert.Equal(0, (await CorpoAsync(lista)).GetProperty("itens").GetArrayLength());
    }

    // ---------------------------------------------------------------- quem da equipe fez (#29)

    [Fact]
    public async Task Quem_da_equipe_fez_traz_so_quem_tem_linha_e_e_da_equipe_por_nome_sem_nota_nem_certificado_e_com_as_ferias_de_hoje()
    {
        var api = await ApiAsync();
        var (gestor, a, b, outroSetor, emAdmissao) = (api.Elenco.Gestor.FuncionarioId, api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroB.FuncionarioId, api.Elenco.DeOutroSetor.FuncionarioId, api.Elenco.EmAdmissao.FuncionarioId);
        var t = await SemearAsync(api, Titulo("ZEqu3"), "EM_ANDAMENTO", obrigatorio: true, emite: true);
        var modulos = new int[4];
        for (var i = 0; i < 4; i++)
        {
            modulos[i] = await ModuloAsync(api, t, i + 1, "TEXTO", duracao: 10);
        }

        await LinhaAsync(api, t, a, "CONCLUIDO", "2026-08-02", "2026-09-15");
        foreach (var m in modulos)
        {
            await ProgressoAsync(api, m, a, "2026-09-15 11:05:00");
        }

        await LinhaAsync(api, t, b, "EM_ANDAMENTO", "2026-10-02");
        await ProgressoAsync(api, modulos[0], b, "2026-10-02 09:00:00");
        await ProgressoAsync(api, modulos[1], b, "2026-10-03 09:00:00");
        await LinhaAsync(api, t, gestor, "INSCRITO");
        await LinhaAsync(api, t, outroSetor, "CONCLUIDO");
        await LinhaAsync(api, t, emAdmissao, "INSCRITO"); // em admissão: ainda fora da equipe
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS) VALUES ({b}, '2026-01-02', '2026-10-04', '2026-10-05', '2026-10-17', 'APROVADA')");

        var resposta = await (await GestorAsync(api)).GetAsync($"/api/gestor/treinamentos/{t}/equipe");
        var corpo = await CorpoAsync(resposta);
        var itens = corpo.GetProperty("itens").EnumerateArray().ToList();

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("listarPessoasDaEquipeNoTreinamento");
        Assert.Equal((1, 1), (corpo.GetProperty("concluiram").GetInt32(), corpo.GetProperty("faltamConcluir").GetInt32()));
        var nomes = itens.Select(i => i.GetProperty("funcionario").GetProperty("nome").GetString()).ToList();
        Assert.Equal(nomes.Order(StringComparer.Ordinal), nomes); // por nome
        Assert.Equal(new[] { a, b }.Order(), itens.Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32()).Order()); // só a equipe com linha: nem o gestor, nem o outro setor, nem quem está em admissão
        var daA = itens.Single(i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == a);
        var daB = itens.Single(i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == b);
        Assert.Equal(("CONCLUIDO", "2026-09-15T14:05:00Z", 4, 4, JsonValueKind.Null, JsonValueKind.Null), (daA.GetProperty("situacao").GetString(), daA.GetProperty("concluidoEmUtc").GetString(), daA.GetProperty("modulosConcluidos").GetInt32(), daA.GetProperty("totalModulos").GetInt32(), daA.GetProperty("moduloAtual").ValueKind, daA.GetProperty("emFeriasAte").ValueKind));
        Assert.Equal(("EM_ANDAMENTO", JsonValueKind.Null, 2, 3, "2026-10-17"), (daB.GetProperty("situacao").GetString(), daB.GetProperty("concluidoEmUtc").ValueKind, daB.GetProperty("modulosConcluidos").GetInt32(), daB.GetProperty("moduloAtual").GetInt32(), daB.GetProperty("emFeriasAte").GetString()));
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("nota", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("certificado", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("codigoValidacao", texto, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Rascunho_que_nao_alcanca_a_equipe_e_inexistente_sao_404_e_o_gestor_so_vai_ate_ali()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZEqu4 rascunho"), obrigatorio: true);
        var doFinanceiro = await SemearAsync(api, Titulo("ZEqu4 financeiro"), "EM_ANDAMENTO", obrigatorio: false, publico: "SETORES");
        await PublicoDoSetorAsync(api, doFinanceiro, api.Elenco.SetorFinanceiroId);
        var gestor = await GestorAsync(api);

        var respostas = new[] { await gestor.GetAsync($"/api/gestor/treinamentos/{rascunho}/equipe"), await gestor.GetAsync($"/api/gestor/treinamentos/{doFinanceiro}/equipe"), await gestor.GetAsync("/api/gestor/treinamentos/987654/equipe") };

        Assert.Equal([404, 404, 404], respostas.Select(r => (int)r.StatusCode));
    }

    // ---------------------------------------------------------------- a ficha (#18 e #30) e o indicador

    [Fact]
    public async Task A_ficha_do_rh_e_a_do_gestor_trazem_as_mesmas_linhas_o_gestor_sem_certificado_e_a_pendencia_e_a_definicao_unica()
    {
        var api = await ApiAsync();
        var p = Titulo("ZEqu5");
        var a = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZEqu5 Pessoa"), Perfil.Funcionario, api.Elenco.SetorComercialId, true, new DateOnly(2026, 1, 2)); // uma pessoa só desta prova: o resumo conta tudo o que ela tem
        var concluido = await SemearAsync(api, $"{p} concluido", "EM_ANDAMENTO", obrigatorio: true, emite: true);
        await ModuloAsync(api, concluido, 1);
        await LinhaAsync(api, concluido, a, "CONCLUIDO", "2026-08-10", "2026-08-21");
        var pendente = await SemearAsync(api, $"{p} pendente", "EM_ANDAMENTO", obrigatorio: true);
        await ModuloAsync(api, pendente, 1);
        await ModuloAsync(api, pendente, 2);
        await LinhaAsync(api, pendente, a, "INSCRITO");
        var emAndamento = await SemearAsync(api, $"{p} em andamento", "EM_ANDAMENTO", obrigatorio: true);
        var primeiro = await ModuloAsync(api, emAndamento, 1);
        await ModuloAsync(api, emAndamento, 2);
        await LinhaAsync(api, emAndamento, a, "EM_ANDAMENTO", "2026-10-05");
        await ProgressoAsync(api, primeiro, a, "2026-10-05 10:00:00");
        var opcionalInscrito = await SemearAsync(api, $"{p} opcional", "EM_ANDAMENTO", obrigatorio: false);
        await LinhaAsync(api, opcionalInscrito, a, "INSCRITO"); // opcional nunca é pendência
        var agendado = await SemearAsync(api, $"{p} agendado", "AGENDADO", obrigatorio: true);
        await LinhaAsync(api, agendado, a, "INSCRITO"); // ainda não liberado: a pessoa não o vê e não é pendência
        var encerrado = await SemearAsync(api, $"{p} encerrado", "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, encerrado, a, "INSCRITO"); // encerrado e nunca começou: some e não é pendência
        var reprovado = await SemearAsync(api, $"{p} reprovado", "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, reprovado, a, "REPROVADO");

        var rh = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{a}/treinamentos");
        var doGestor = await (await GestorAsync(api)).GetAsync($"/api/gestor/equipe/{a}/treinamentos");
        var indicadores = await (await GestorAsync(api)).GetAsync($"/api/gestor/equipe/{a}/indicadores");
        var corpoRh = await CorpoAsync(rh);
        var corpoGestor = await CorpoAsync(doGestor);

        Assert.Equal([200, 200, 200], new[] { rh, doGestor, indicadores }.Select(r => (int)r.StatusCode));
        await rh.AfirmarConformeAoContratoAsync("listarTreinamentosDeFuncionario");
        await doGestor.AfirmarConformeAoContratoAsync("listarTreinamentosDePessoaDaEquipe");
        var meus = Itens(corpoRh, p);
        Assert.Equal(["concluido", "em andamento", "pendente", "opcional"], meus.Select(i => i.GetProperty("titulo").GetString()![(p.Length + 1)..])); // obrigatórios (o mesmo dia: por título), depois o opcional
        var resumoRh = corpoRh.GetProperty("resumo");
        Assert.Equal(2, resumoRh.GetProperty("pendentes").GetInt32()); // "pendente" (INSCRITO) e "em andamento" (EM_ANDAMENTO): só obrigatório em andamento; opcional, agendado, encerrado e REPROVADO fora
        Assert.Equal((1, 1), (resumoRh.GetProperty("emAndamento").GetInt32(), resumoRh.GetProperty("concluidos").GetInt32()));
        var concluidoRh = meus.Single(i => i.GetProperty("treinamentoId").GetInt32() == concluido);
        Assert.Equal(("CONCLUIDO", "2026-08-10", 1, true), (concluidoRh.GetProperty("situacao").GetString(), concluidoRh.GetProperty("iniciadoEm").GetString(), concluidoRh.GetProperty("totalModulos").GetInt32(), concluidoRh.GetProperty("certificadoDisponivel").GetBoolean()));
        Assert.Equal(corpoRh.GetProperty("resumo").GetProperty("pendentes").GetInt32(), corpoGestor.GetProperty("resumo").GetProperty("pendentes").GetInt32());
        Assert.Equal(meus.Select(i => i.GetProperty("treinamentoId").GetInt32()), Itens(corpoGestor, p).Select(i => i.GetProperty("treinamentoId").GetInt32())); // a mesma visibilidade e a mesma ordem
        Assert.DoesNotContain("certificado", await doGestor.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase); // o gestor nunca recebe certificado
        Assert.Equal(resumoRh.GetProperty("pendentes").GetInt32(), (await CorpoAsync(indicadores)).GetProperty("treinamentosPendentes").GetInt32()); // o indicador da ficha é o mesmo número
    }

    [Fact]
    public async Task O_gestor_so_ve_a_ficha_da_equipe_de_hoje_e_o_rh_ve_ate_o_inativo_com_zero_pendencias()
    {
        var api = await ApiAsync();
        var inativo = api.Elenco.Inativo.FuncionarioId;
        var gestor = await GestorAsync(api);

        var respostasDoGestor = new[]
        {
            await gestor.GetAsync($"/api/gestor/equipe/{api.Elenco.DeOutroSetor.FuncionarioId}/treinamentos"), await gestor.GetAsync($"/api/gestor/equipe/{api.Elenco.Gestor.FuncionarioId}/treinamentos"),
            await gestor.GetAsync($"/api/gestor/equipe/{api.Elenco.EmAdmissao.FuncionarioId}/treinamentos"), await gestor.GetAsync($"/api/gestor/equipe/{inativo}/treinamentos"), await gestor.GetAsync("/api/gestor/equipe/987654/treinamentos"),
        };
        var rh = await api.ClienteDoRhAgoraAsync();
        var doInativo = await rh.GetAsync($"/api/rh/funcionarios/{inativo}/treinamentos");
        var inexistente = await rh.GetAsync("/api/rh/funcionarios/987654/treinamentos");

        Assert.Equal([404, 404, 404, 404, 404], respostasDoGestor.Select(r => (int)r.StatusCode));
        Assert.Equal(200, (int)doInativo.StatusCode);
        Assert.Equal(0, (await CorpoAsync(doInativo)).GetProperty("resumo").GetProperty("pendentes").GetInt32());
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    [Fact]
    public async Task So_o_gestor_acessa_as_rotas_do_gestor_so_o_rh_a_ficha_do_rh_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var a = api.Elenco.MembroA.FuncionarioId;
        var t = await SemearAsync(api, Titulo("ZEqu7"), "EM_ANDAMENTO", obrigatorio: true);
        var rotasDoGestor = new[] { "/api/gestor/treinamentos", $"/api/gestor/treinamentos/{t}/equipe", $"/api/gestor/equipe/{a}/treinamentos" };
        var rh = await api.ClienteDoRhAgoraAsync();
        var membro = api.Api.ClienteComo(api.Elenco.MembroA);

        var codigos = new List<int>();
        foreach (var rota in rotasDoGestor)
        {
            codigos.Add((int)(await rh.GetAsync(rota)).StatusCode);
            codigos.Add((int)(await membro.GetAsync(rota)).StatusCode);
            codigos.Add((int)(await api.Api.ClienteAnonimo().GetAsync(rota)).StatusCode);
        }

        codigos.Add((int)(await (await GestorAsync(api)).GetAsync($"/api/rh/funcionarios/{a}/treinamentos")).StatusCode);
        codigos.Add((int)(await membro.GetAsync($"/api/rh/funcionarios/{a}/treinamentos")).StatusCode);
        codigos.Add((int)(await api.Api.ClienteAnonimo().GetAsync($"/api/rh/funcionarios/{a}/treinamentos")).StatusCode);

        Assert.Equal([403, 403, 401, 403, 403, 401, 403, 403, 401, 403, 403, 401], codigos);
    }
}

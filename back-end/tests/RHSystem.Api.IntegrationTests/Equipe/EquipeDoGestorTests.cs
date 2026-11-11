using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Equipe;

/// <summary>
/// <c>GET /api/gestor/equipe</c> pela API de verdade, no banco REAL com o relógio em 2026-10-07T12:42Z (quarta). A equipe de Carlos Ribeiro (Comercial) é Diego Farias e Júlia Nunes; Rafael Torres
/// (admissão em 2026-10-13) e Henrique Alves (inativo) estão no setor mas fora da equipe, e Bruno Lima é do Financeiro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EquipeDoGestorTests(MySqlFixture mysql)
{
    private static readonly DateTime Quarta = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
    private static int _extra;

    private sealed class FonteDeFatosFalsa(CalendarioDoDia calendario, Dictionary<int, FatosDaPessoaNoDia> fatos) : IFonteDeFatosDoDia
    {
        public Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct) => Task.FromResult(calendario);

        public Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, FatosDaPessoaNoDia>>(fatos);
    }

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EquipeDoGestorTests));
        api.Api.Relogio.Fixar(Quarta); // cada teste começa na quarta, 09:42 em Brasília
        return api;
    }

    private static async Task<JsonElement> ListarAsync(HttpClient cliente) => await CorpoAsync(await cliente.GetAsync("/api/gestor/equipe"));

    private static string[] Nomes(JsonElement equipe) => [.. equipe.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("nome").GetString()!)];

    private static int[] Contagens(JsonElement equipe)
    {
        var c = equipe.GetProperty("contagensHoje");
        return [.. new[] { "trabalhando", "ferias", "atestado", "semRegistro", "aindaNaoComecou", "semExpediente" }.Select(n => c.GetProperty(n).GetInt32())];
    }

    [Fact]
    public async Task Lista_so_a_propria_equipe_em_ordem_de_nome_sem_o_gestor_sem_em_admissao_sem_inativo_e_sem_outro_setor()
    {
        var api = await ApiAsync();

        var resposta = await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync("/api/gestor/equipe");

        Assert.Equal(200, (int)resposta.StatusCode);
        var equipe = await CorpoAsync(resposta);
        Assert.Equal(["Diego Farias", "Júlia Nunes"], Nomes(equipe)); // A a Z; sem Carlos, Rafael (em admissão), Henrique (inativo) nem Bruno (Financeiro)
        Assert.Equal((api.Elenco.SetorComercialId, "Comercial"), (equipe.GetProperty("setor").GetProperty("id").GetInt32(), equipe.GetProperty("setor").GetProperty("nome").GetString()));
        Assert.Equal(2, equipe.GetProperty("total").GetInt32());
        var primeiro = equipe.GetProperty("itens")[0];
        Assert.Equal((api.Elenco.MembroA.FuncionarioId, true, "Comercial"), (primeiro.GetProperty("id").GetInt32(), primeiro.GetProperty("ativo").GetBoolean(), primeiro.GetProperty("setor").GetString()));
    }

    [Fact]
    public async Task Contagens_de_hoje_somam_o_total_e_a_fonte_padrao_deixa_todos_AINDA_NAO_COMECOU_num_dia_util()
    {
        var api = await ApiAsync();

        var equipe = await ListarAsync(api.Api.ClienteComo(api.Elenco.Gestor));

        Assert.True(equipe.GetProperty("diaUtil").GetBoolean());
        Assert.Equal([0, 0, 0, 0, 2, 0], Contagens(equipe)); // trabalhando, férias, atestado, sem registro, ainda não começou, sem expediente
        Assert.Equal(equipe.GetProperty("total").GetInt32(), Contagens(equipe).Sum()); // a soma dos seis é o total da lista
        Assert.All(equipe.GetProperty("itens").EnumerateArray(), i => Assert.Equal("AINDA_NAO_COMECOU", i.GetProperty("situacaoHoje").GetString())); // a fonte padrão só conhece o fim de semana
    }

    [Fact]
    public async Task Sabado_diaUtil_false_e_todos_SEM_EXPEDIENTE_exceto_quem_tem_entrada_registrada_pela_fonte_falsa()
    {
        var api = await ApiAsync();
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 12, 42, 0, DateTimeKind.Utc)); // sábado
        var gestor = await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId);

        var semSetorNoSabado = await ListarAsync(await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId));
        Assert.False(semSetorNoSabado.GetProperty("diaUtil").GetBoolean()); // a equipe vazia também diz que sábado não é dia útil
        var padrao = await ListarAsync(gestor);
        Assert.False(padrao.GetProperty("diaUtil").GetBoolean());
        Assert.All(padrao.GetProperty("itens").EnumerateArray(), i => Assert.Equal("SEM_EXPEDIENTE", i.GetProperty("situacaoHoje").GetString())); // a fonte padrão: fim de semana, ninguém trabalha

        var fatos = new Dictionary<int, FatosDaPessoaNoDia> { [api.Elenco.MembroA.FuncionarioId] = new(JaRegistrouEntrada: true, EmFerias: false, ComAtestadoDeDiaInteiro: false) };
        await using var comFonte = api.Api.WithWebHostBuilder(b => b.ConfigureServices(s => s.AddScoped<IFonteDeFatosDoDia>(_ => new FonteDeFatosFalsa(new CalendarioDoDia(false, null), fatos))));
        var cliente = comFonte.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = gestor.DefaultRequestHeaders.Authorization;

        var equipe = await ListarAsync(cliente);

        Assert.False(equipe.GetProperty("diaUtil").GetBoolean());
        var porNome = equipe.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("nome").GetString()!, i => i.GetProperty("situacaoHoje").GetString());
        Assert.Equal("TRABALHANDO", porNome["Diego Farias"]); // quem tem entrada registrada trabalha, mesmo no sábado
        Assert.Equal("SEM_EXPEDIENTE", porNome["Júlia Nunes"]);
        Assert.Equal(equipe.GetProperty("total").GetInt32(), Contagens(equipe).Sum());
        Assert.Equal((1, 1), (Contagens(equipe)[0], Contagens(equipe)[5]));
    }

    [Fact]
    public async Task Nomes_iguais_ficam_em_ordem_de_id()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _extra);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo homonimo {n}", 44m);
        var gestorId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {api.Elenco.Gestor.FuncionarioId}");
        var primeiro = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Diego Farias", $"homonimo.a.{n}@equipe.com.br", new DateOnly(2026, 1, 5), setorId: api.Elenco.SetorComercialId, cargoId: cargo, gestorId: gestorId);
        var segundo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Diego Farias", $"homonimo.b.{n}@equipe.com.br", new DateOnly(2026, 1, 5), setorId: api.Elenco.SetorComercialId, cargoId: cargo, gestorId: gestorId);

        var equipe = await ListarAsync(api.Api.ClienteComo(api.Elenco.Gestor));

        var ids = equipe.GetProperty("itens").EnumerateArray().Where(i => i.GetProperty("nome").GetString() == "Diego Farias").Select(i => i.GetProperty("id").GetInt32()).ToArray();
        Assert.Equal([api.Elenco.MembroA.FuncionarioId, primeiro, segundo], ids); // o mesmo nome: pelo id, sempre na mesma ordem
        var doPrimeiro = equipe.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == primeiro);
        Assert.Equal($"Cargo homonimo {n}", doPrimeiro.GetProperty("cargo").GetString()); // o cargo da pessoa aparece na linha

        // Saem da equipe depois do teste: o banco da classe é um só e os outros testes contam a equipe de Carlos.
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO IN ({primeiro}, {segundo})");
    }

    [Fact]
    public async Task Gestor_sem_setor_devolve_equipe_vazia_com_setor_null_total_0_e_contagens_zeradas_e_nao_erro()
    {
        var api = await ApiAsync();

        var resposta = await api.Api.ClienteComo(api.Elenco.GestorSemSetor).GetAsync("/api/gestor/equipe");

        Assert.Equal(200, (int)resposta.StatusCode);
        var equipe = await CorpoAsync(resposta);
        Assert.Equal(JsonValueKind.Null, equipe.GetProperty("setor").ValueKind);
        Assert.Equal(0, equipe.GetProperty("total").GetInt32());
        Assert.Equal([0, 0, 0, 0, 0, 0], Contagens(equipe));
        Assert.Equal(0, equipe.GetProperty("itens").GetArrayLength());
        Assert.True(equipe.GetProperty("diaUtil").GetBoolean()); // o calendário vale também para a equipe vazia
        await resposta.AfirmarConformeAoContratoAsync("listarEquipeDoGestor");
    }

    [Fact]
    public async Task Admissao_em_08_10_vista_as_22h_de_07_10_em_Brasilia_nao_esta_na_equipe_e_as_00h_de_08_10_esta()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _extra);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo equipe virada {n}", 44m);
        var gestorId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {api.Elenco.Gestor.FuncionarioId}");
        var nova = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Zélia Virada {n}", $"zelia.virada.{n}@equipe.com.br", new DateOnly(2026, 10, 8), setorId: api.Elenco.SetorComercialId, cargoId: cargo, gestorId: gestorId);

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 1, 0, 0, DateTimeKind.Utc)); // 22:00 de 07/10 em Brasília
        var antes = await ListarAsync(await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc)); // 00:00 de 08/10
        var depois = await ListarAsync(await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId));

        Assert.DoesNotContain($"Zélia Virada {n}", Nomes(antes));
        Assert.Contains($"Zélia Virada {n}", Nomes(depois));
        Assert.Equal(Nomes(depois).Length, depois.GetProperty("total").GetInt32());

        // A pessoa sai da equipe depois do teste: o banco da classe é um só e os outros testes contam a equipe de Carlos.
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-08' WHERE ID_FUNCIONARIO = {nova}");
    }

    [Fact]
    public async Task Gestor_que_mudou_de_setor_passa_a_ver_a_equipe_nova_sem_novo_login()
    {
        var api = await ApiAsync();
        var n = Interlocked.Increment(ref _extra);
        var pessoa = await api.NovaPessoaAsync($"equipemudou{n}", login: TipoDeLogin.SenhaDefinida);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_NIVEL_ACESSO = (SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = 'Funcionario Gestor') WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        await api.NovoGestorAsync(pessoa.FuncionarioId);
        var gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo equipe mudou {n}", 44m);
        var primeiro = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor equipe mudou A {n}", gestor);
        var segundo = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor equipe mudou B {n}");
        await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Membro do primeiro {n}", $"primeiro.{n}@equipe.com.br", new DateOnly(2026, 1, 5), setorId: primeiro, cargoId: cargo, gestorId: gestor);
        var segundoMembro = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Membro do segundo {n}", $"segundo.{n}@equipe.com.br", new DateOnly(2026, 1, 5), setorId: segundo, cargoId: cargo);
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId!.Value); // UM token só, antes e depois

        Assert.Equal([$"Membro do primeiro {n}"], Nomes(await ListarAsync(cliente)));

        // O RH troca o gestor do setor (a 1c): o primeiro fica sem gestor e o segundo passa a ser dele.
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = NULL WHERE ID_SETOR = {primeiro}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {segundo}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {gestor} WHERE ID_FUNCIONARIO = {segundoMembro}");

        var depois = await ListarAsync(cliente);
        Assert.Equal([$"Membro do segundo {n}"], Nomes(depois)); // a equipe nova, sem novo login
        Assert.Equal($"Setor equipe mudou B {n}", depois.GetProperty("setor").GetProperty("nome").GetString());
    }

    [Fact]
    public async Task RH_e_Funcionario_recebem_403_nas_tres_rotas_e_anonimo_401()
    {
        var api = await ApiAsync();
        var id = api.Elenco.MembroA.FuncionarioId;
        var rotas = new[] { "/api/gestor/equipe", $"/api/gestor/equipe/{id}", $"/api/gestor/equipe/{id}/indicadores" };

        foreach (var rota in rotas)
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.Rh).GetAsync(rota)).StatusCode); // RH recebe 403, não 404
            Assert.Equal(403, (int)(await api.Api.ClienteComo(api.Elenco.MembroA).GetAsync(rota)).StatusCode);
            Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(rota)).StatusCode);
        }
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();

        await (await api.Api.ClienteComo(api.Elenco.Gestor).GetAsync("/api/gestor/equipe")).AfirmarConformeAoContratoAsync("listarEquipeDoGestor"); // o 200 com equipe
        await (await api.Api.ClienteComo(api.Elenco.Rh).GetAsync("/api/gestor/equipe")).AfirmarConformeAoContratoAsync("listarEquipeDoGestor"); // o 403
        await (await api.Api.ClienteAnonimo().GetAsync("/api/gestor/equipe")).AfirmarConformeAoContratoAsync("listarEquipeDoGestor"); // o 401
    }
}

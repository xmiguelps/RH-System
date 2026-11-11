using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// A "situação de hoje" pela API de verdade (a equipe do gestor e a lista do RH), agora com os dados do ponto: batida, férias, atestado, feriado e o horário da empresa (08:00 com 10 minutos de tolerância). A equipe de Carlos
/// (Comercial) é Diego Farias e Júlia Nunes; o relógio é fixado em cada teste.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class SituacaoDeHojeComPontoTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync(DateTime agoraUtc)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(SituacaoDeHojeComPontoTests));
        await ConfigurarEmpresaAsync(api.Conexao);
        api.Api.Relogio.Fixar(agoraUtc);
        return api;
    }

    private static async Task<Dictionary<string, string>> SituacoesDaEquipeAsync(ApiDeAcesso api) =>
        (await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync("/api/gestor/equipe")))
        .GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("nome").GetString()!, i => i.GetProperty("situacaoHoje").GetString()!);

    private static async Task<Dictionary<string, string>> SituacoesDaListaDoRhAsync(ApiDeAcesso api) =>
        (await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios?setorId={api.Elenco.SetorComercialId}")))
        .GetProperty("itens").EnumerateArray().Where(i => i.GetProperty("situacaoHoje").ValueKind == System.Text.Json.JsonValueKind.String)
        .ToDictionary(i => i.GetProperty("nome").GetString()!, i => i.GetProperty("situacaoHoje").GetString()!);

    [Fact]
    public async Task Quem_bateu_entrada_trabalha_e_quem_esta_de_ferias_aprovadas_fica_FERIAS()
    {
        var api = await ApiAsync(new DateTime(2026, 3, 4, 12, 42, 0, DateTimeKind.Utc)); // quarta, 09:42 em Brasília
        await BaterAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, new DateOnly(2026, 3, 4), (TipoDeMarcacao.Entrada, 11));
        await NovasFeriasAsync(api.Conexao, api.Elenco.MembroB.FuncionarioId, "2026-03-02", "2026-03-10", "APROVADA");

        var equipe = await SituacoesDaEquipeAsync(api);
        var listaDoRh = await SituacoesDaListaDoRhAsync(api);

        Assert.Equal("TRABALHANDO", equipe["Diego Farias"]);
        Assert.Equal("FERIAS", equipe["Júlia Nunes"]);
        Assert.Equal("TRABALHANDO", listaDoRh["Diego Farias"]); // a ficha e a lista do RH usam a mesma situação
        Assert.Equal("FERIAS", listaDoRh["Júlia Nunes"]);
    }

    [Fact]
    public async Task Atestado_validado_de_dia_inteiro_fica_ATESTADO_e_quem_nao_bateu_depois_do_limite_fica_SEM_REGISTRO()
    {
        var api = await ApiAsync(new DateTime(2026, 5, 5, 12, 42, 0, DateTimeKind.Utc)); // terça, 09:42 em Brasília
        await NovoAtestadoAsync(api.Conexao, api.Elenco.MembroA.FuncionarioId, "2026-05-05", "2026-05-06", "VALIDADO", api.Elenco.Rh.FuncionarioId);

        var equipe = await SituacoesDaEquipeAsync(api);

        Assert.Equal("ATESTADO", equipe["Diego Farias"]);
        Assert.Equal("SEM_REGISTRO", equipe["Júlia Nunes"]);
    }

    [Fact]
    public async Task O_limite_e_o_horario_mais_a_tolerancia_e_so_vale_estritamente_depois()
    {
        var api = await ApiAsync(new DateTime(2026, 5, 6, 11, 10, 0, DateTimeKind.Utc)); // quarta, 08:10 em Brasília: dentro da tolerância
        var noLimite = await SituacoesDaEquipeAsync(api);
        api.Api.Relogio.Fixar(new DateTime(2026, 5, 6, 11, 11, 0, DateTimeKind.Utc)); // 08:11
        var depois = await SituacoesDaEquipeAsync(api);

        Assert.Equal("AINDA_NAO_COMECOU", noLimite["Júlia Nunes"]);
        Assert.Equal("SEM_REGISTRO", depois["Júlia Nunes"]);
    }

    [Fact]
    public async Task Feriado_e_dia_sem_expediente_para_todos_e_a_equipe_diz_que_nao_e_dia_util()
    {
        var api = await ApiAsync(new DateTime(2026, 9, 7, 12, 42, 0, DateTimeKind.Utc)); // segunda, Independência

        var equipe = await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync("/api/gestor/equipe"));

        Assert.False(equipe.GetProperty("diaUtil").GetBoolean());
        Assert.All(equipe.GetProperty("itens").EnumerateArray(), i => Assert.Equal("SEM_EXPEDIENTE", i.GetProperty("situacaoHoje").GetString()));
    }
}

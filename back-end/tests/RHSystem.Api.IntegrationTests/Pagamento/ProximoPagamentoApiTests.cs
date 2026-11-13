using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Pagamento;

/// <summary>
/// <c>GET /api/eu/pagamento/proximo</c> e o campo <c>proximoPagamento</c> da ficha do RH no banco REAL: a data do próximo pagamento (de hoje em diante, hoje inclusive), a competência (o mês anterior) e a exceção do mês que vence a regra. O padrão é hoje = 2026-10-07T12:42Z (quarta) e dia 6; os testes de data
/// movem o relógio para 2031, sem feriado importado.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ProximoPagamentoApiTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/pagamento/proximo";

    private async Task<ApiDeAcesso> ApiAsync(int? diaDoPagamento = 6, DateTime? agora = null)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ProximoPagamentoApiTests));
        api.Api.Relogio.Fixar(agora ?? ElencoDeTeste.Hoje);
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM PAGAMENTOS_EXCECOES");
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM FERIADOS WHERE YEAR(DATA_FERIADO) >= 2031");
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

    private static DateTime Em(int ano, int mes, int dia) => new(ano, mes, dia, 12, 42, 0, DateTimeKind.Utc);

    private static async Task<System.Text.Json.JsonElement> ObterAsync(ApiDeAcesso api, bool conferir = false)
    {
        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        if (conferir)
        {
            await resposta.AfirmarConformeAoContratoAsync("obterProximoPagamento");
        }

        return await CorpoAsync(resposta);
    }

    private static (int Ano, int Mes, string Data, string Origem, string Ajustes, int AnoDaCompetencia, int MesDaCompetencia) Ler(System.Text.Json.JsonElement corpo)
    {
        var pagamento = corpo.GetProperty("pagamento");
        var competencia = corpo.GetProperty("competencia");
        return (pagamento.GetProperty("ano").GetInt32(), pagamento.GetProperty("mes").GetInt32(), pagamento.GetProperty("data").GetString()!, pagamento.GetProperty("origem").GetString()!, string.Join("+", pagamento.GetProperty("ajustes").EnumerateArray().Select(a => a.GetString()!)), competencia.GetProperty("ano").GetInt32(), competencia.GetProperty("mes").GetInt32());
    }

    [Fact]
    public async Task O_exemplo_do_contrato_dia_6_em_7_de_outubro_de_2026_paga_em_6_de_novembro_e_o_holerite_e_de_outubro()
    {
        var api = await ApiAsync();

        var corpo = await ObterAsync(api, conferir: true);

        Assert.Equal((2026, 11, "2026-11-06", "REGRA", "", 2026, 10), Ler(corpo));
    }

    [Fact]
    public async Task Se_hoje_e_o_dia_do_pagamento_o_proximo_e_hoje()
    {
        var api = await ApiAsync(6, Em(2031, 3, 6));

        Assert.Equal((2031, 3, "2031-03-06", "REGRA", "", 2031, 2), Ler(await ObterAsync(api)));
    }

    [Fact]
    public async Task Passado_o_dia_o_proximo_e_o_do_mes_seguinte_e_antecipa_o_fim_de_semana()
    {
        var api = await ApiAsync(6, Em(2031, 3, 7));

        Assert.Equal((2031, 4, "2031-04-04", "REGRA", "ANTECIPADO_FIM_DE_SEMANA", 2031, 3), Ler(await ObterAsync(api)));
    }

    [Fact]
    public async Task A_excecao_do_mes_vence_a_regra_e_vale_como_esta_num_sabado()
    {
        var api = await ApiAsync(6, Em(2031, 4, 10));
        await Sql.ExecutarAsync(api.Conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 5, '2031-05-17')"); // sábado

        Assert.Equal((2031, 5, "2031-05-17", "EXCECAO", "", 2031, 4), Ler(await ObterAsync(api)));
    }

    [Fact]
    public async Task O_pagamento_de_janeiro_tem_o_holerite_de_dezembro_do_ano_anterior()
    {
        var api = await ApiAsync(6, Em(2031, 12, 10));

        Assert.Equal((2032, 1, "2032-01-06", "REGRA", "", 2031, 12), Ler(await ObterAsync(api)));
    }

    [Fact]
    public async Task Sem_dia_de_pagamento_e_sem_excecao_os_dois_campos_vem_nulos_com_200_e_so_com_excecao_ela_vale()
    {
        var api = await ApiAsync(diaDoPagamento: null, agora: Em(2031, 4, 10));

        var vazio = await ObterAsync(api, conferir: true);
        await Sql.ExecutarAsync(api.Conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 6, '2031-06-13')");
        var comExcecao = await ObterAsync(api);

        Assert.Equal((System.Text.Json.JsonValueKind.Null, System.Text.Json.JsonValueKind.Null), (vazio.GetProperty("pagamento").ValueKind, vazio.GetProperty("competencia").ValueKind));
        Assert.Equal((2031, 6, "2031-06-13", "EXCECAO", "", 2031, 5), Ler(comExcecao));
    }

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task Todos_os_perfis_veem_o_mesmo_cartao(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = await FeriasDeTeste.PessoaAsync(api, "Cartao do pagamento " + perfil, new DateOnly(2024, 1, 2), perfil: perfil);

        var resposta = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((2026, 11, "2026-11-06", "REGRA", "", 2026, 10), Ler(await CorpoAsync(resposta)));
    }

    [Fact]
    public async Task A_ficha_do_RH_mostra_o_mesmo_dia_no_campo_proximoPagamento_e_nulo_sem_a_regra()
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();
        var rota = $"/api/rh/funcionarios/{api.Elenco.MembroA.FuncionarioId}/indicadores";

        var comRegra = await CorpoAsync(await rh.GetAsync(rota));
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        var semRegra = await CorpoAsync(await rh.GetAsync(rota));

        Assert.Equal("2026-11-06", comRegra.GetProperty("proximoPagamento").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, semRegra.GetProperty("proximoPagamento").ValueKind);
    }
}

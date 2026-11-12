using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Comunicacao;
using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// A porta <c>IProximoPagamento</c> no banco REAL: o dia de pagamento da empresa (<c>CONFIGURACOES_EMPRESA</c>), as exceções por mês (<c>PAGAMENTOS_EXCECOES</c>) e os feriados que valem para a empresa (os mesmos do ponto: nacionais, da empresa, estaduais da UF
/// e municipais do município). Os testes usam datas de 2031, sem feriado importado, e inserem só o que cada caso precisa. "Hoje" vem do parâmetro, não do relógio.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ProximoPagamentoTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync(int? diaDoPagamento = 6)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ProximoPagamentoTests));
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

    private static async Task<PagamentoDoMes?> ObterAsync(ApiDeAcesso api, DateOnly hoje)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IProximoPagamento>().ObterAsync(hoje, Ct);
    }

    [Fact]
    public async Task Sem_a_empresa_configurada_e_sem_excecao_nao_ha_proximo_pagamento()
    {
        var api = await ApiAsync(diaDoPagamento: null);

        Assert.Null(await ObterAsync(api, new DateOnly(2031, 3, 1)));
    }

    [Fact]
    public async Task O_dia_da_empresa_vale_de_hoje_em_diante_e_antecipa_o_fim_de_semana()
    {
        var api = await ApiAsync(6); // 06/03/2031 é quinta; 06/04/2031 é domingo (a sexta é 04/04)

        var hoje = await ObterAsync(api, new DateOnly(2031, 3, 6));
        var depois = await ObterAsync(api, new DateOnly(2031, 3, 7));

        Assert.Equal((new DateOnly(2031, 3, 6), OrigemDoPagamento.Regra, 3), (hoje!.Data, hoje.Origem, hoje.Mes));
        Assert.Empty(hoje.Ajustes);
        Assert.Equal((new DateOnly(2031, 4, 4), 4, 2031, (2031, 3)), (depois!.Data, depois.Mes, depois.Ano, depois.Competencia));
        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], depois.Ajustes);
    }

    [Fact]
    public async Task A_excecao_do_mes_vale_como_esta_e_vence_a_regra()
    {
        var api = await ApiAsync(6);
        await Sql.ExecutarAsync(api.Conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 5, '2031-05-17')"); // sábado: a exceção não antecipa

        var pagamento = await ObterAsync(api, new DateOnly(2031, 5, 2));

        Assert.Equal((new DateOnly(2031, 5, 17), OrigemDoPagamento.Excecao), (pagamento!.Data, pagamento.Origem));
        Assert.Empty(pagamento.Ajustes);
    }

    [Fact]
    public async Task Feriado_que_vale_para_a_empresa_antecipa_e_o_de_outra_cidade_ou_de_outro_estado_nao()
    {
        var api = await ApiAsync(10); // 10/06/2031 é terça
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Municipal de São Paulo", new DateOnly(2031, 6, 10), "MUNICIPAL", "SP", "São Paulo");
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Municipal de Campinas", new DateOnly(2031, 7, 10), "MUNICIPAL", "SP", "Campinas"); // 10/07/2031 é quinta
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Estadual do RJ", new DateOnly(2031, 8, 11), "ESTADUAL", "RJ"); // 10/08/2031 é domingo: a sexta é 08/08

        var emJunho = await ObterAsync(api, new DateOnly(2031, 6, 1));
        var emJulho = await ObterAsync(api, new DateOnly(2031, 6, 11));
        var emAgosto = await ObterAsync(api, new DateOnly(2031, 7, 11));

        Assert.Equal(new DateOnly(2031, 6, 9), emJunho!.Data);
        Assert.Equal([AjusteDoPagamento.AntecipadoFeriado], emJunho.Ajustes);
        Assert.Equal((new DateOnly(2031, 7, 10), 0), (emJulho!.Data, emJulho.Ajustes.Count)); // o feriado de Campinas não vale para a empresa de São Paulo
        Assert.Equal(new DateOnly(2031, 8, 8), emAgosto!.Data); // o do RJ não vale para SP, mas 10/08 é domingo
        Assert.Equal([AjusteDoPagamento.AntecipadoFimDeSemana], emAgosto.Ajustes);
    }

    [Fact]
    public async Task A_excecao_marcada_para_o_proprio_dia_de_hoje_no_primeiro_dia_do_mes_conta()
    {
        var api = await ApiAsync(6);
        await Sql.ExecutarAsync(api.Conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 5, '2031-05-01')");

        var pagamento = await ObterAsync(api, new DateOnly(2031, 5, 1));

        Assert.Equal((new DateOnly(2031, 5, 1), OrigemDoPagamento.Excecao), (pagamento!.Data, pagamento.Origem));
    }

    [Fact]
    public async Task Feriado_no_primeiro_dia_do_mes_de_hoje_conta_para_a_regra_que_cai_nele()
    {
        var api = await ApiAsync(1); // 01/09/2031 é segunda
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Municipal no dia 1", new DateOnly(2031, 9, 1), "MUNICIPAL", "SP", "São Paulo");

        var pagamento = await ObterAsync(api, new DateOnly(2031, 9, 1));

        Assert.Equal(new DateOnly(2031, 10, 1), pagamento!.Data); // 01/09 vira sexta 29/08 (já passou): o próximo é o de outubro, quarta
    }

    [Fact]
    public async Task Feriado_dois_meses_a_frente_antecipa_quando_o_proximo_pagamento_e_o_daquele_mes()
    {
        var api = await ApiAsync(1); // 01/06/2031 é domingo (vira 30/05, já passou em 31/05); 01/07/2031 é terça
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Municipal em 1º de julho", new DateOnly(2031, 7, 1), "MUNICIPAL", "SP", "São Paulo");

        var pagamento = await ObterAsync(api, new DateOnly(2031, 5, 31));

        Assert.Equal((new DateOnly(2031, 6, 30), 7), (pagamento!.Data, pagamento.Mes)); // a regra de julho, antecipada pelo feriado, sai na segunda 30/06
        Assert.Equal([AjusteDoPagamento.AntecipadoFeriado], pagamento.Ajustes);
    }

    [Fact]
    public async Task Feriado_desativado_nao_antecipa()
    {
        var api = await ApiAsync(10);
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Municipal desativado", new DateOnly(2031, 9, 10), "MUNICIPAL", "SP", "São Paulo", ativo: false); // 10/09/2031 é quarta

        var pagamento = await ObterAsync(api, new DateOnly(2031, 9, 1));

        Assert.Equal(new DateOnly(2031, 9, 10), pagamento!.Data);
    }
}

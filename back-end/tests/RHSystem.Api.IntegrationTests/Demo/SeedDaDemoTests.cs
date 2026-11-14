using System.Net.Http.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Demo.DemoDeTeste;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>O seed fictício da demo (<c>Banco de Dados/Demo/seed_demo.sql</c>): roda inteiro, cria o que promete e deixa as telas principais com o que mostrar.</summary>
[Collection(MySqlCollection.Nome)]
public class SeedDaDemoTests(MySqlFixture mysql)
{
    private static async Task<long> ContarAsync(ApiFactory api, string sql) => await Sql.ContarAsync(api.Conexao, sql);

    [Fact]
    public async Task O_seed_roda_inteiro_e_cria_o_elenco_com_os_dados_fictícios()
    {
        var api = await ApiAsync(mysql);

        Assert.Equal((12L, 12L, 2L, 3L), (
            await ContarAsync(api, "SELECT COUNT(*) FROM FUNCIONARIOS"),
            await ContarAsync(api, "SELECT COUNT(*) FROM USUARIOS"),
            await ContarAsync(api, "SELECT COUNT(*) FROM GESTORES"),
            await ContarAsync(api, "SELECT COUNT(*) FROM SETORES")));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO NOT LIKE '%@{Dominio}'"));
        Assert.Equal(12L, await ContarAsync(api, "SELECT COUNT(DISTINCT MATRICULA) FROM FUNCIONARIOS WHERE MATRICULA REGEXP '^[0-9]{5}$'"));
        Assert.Equal(12L, await ContarAsync(api, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM HOLERITES"));
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS"));
    }

    [Fact]
    public async Task O_seed_deixa_ponto_ferias_beneficios_formularios_e_treinamentos_coerentes_com_as_regras_do_banco()
    {
        var api = await ApiAsync(mysql);

        // ponto: cada dia tem as quatro batidas em ordem e as horas batem com elas; o Thiago falta em um dia e a Vanessa, de férias, não bate nos dias do gozo
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM PONTOS p WHERE p.DATA < '2026-10-07' AND (SELECT COUNT(*) FROM PONTOS_MARCACOES m WHERE m.ID_PONTO = p.ID_PONTO) <> 4"));
        Assert.Equal(0L, await ContarAsync(api, "SELECT COUNT(*) FROM PONTOS p WHERE p.DATA < '2026-10-07' AND p.HORAS_TRABALHADAS NOT BETWEEN 7 AND 9"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM PONTOS p JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = p.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = '{Email("vanessa.lima")}' AND p.DATA >= '2026-10-05'"));
        Assert.Equal(9L, await ContarAsync(api, $"SELECT COUNT(*) FROM PONTOS p JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = p.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = '{Email("thiago.mello")}' AND p.DATA < '2026-10-07'")); // 10 dias úteis, menos o da falta
        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM PONTOS_MARCACOES m JOIN PONTOS p ON p.ID_PONTO = m.ID_PONTO JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = p.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = '{Email("diego.farias")}' AND m.TIPO_MARCACAO = 'ENTRADA' AND TIME(m.DATA_HORA_MARCACAO) > '08:10:00' AND TIME(m.DATA_HORA_MARCACAO) < '12:00:00'"));
        // férias: uma em gozo, uma esperando o gestor, uma esperando o RH e uma concluída
        Assert.Equal("CONCLUIDA,EM_GOZO,SOLICITADA,SOLICITADA", string.Join(',', (await Sql.ColunaAsync(api.Conexao, "SELECT STATUS_FERIAS FROM FERIAS ORDER BY STATUS_FERIAS, ID_FERIAS"))));
        // benefícios, formulários e treinamentos
        Assert.Equal(6L, await ContarAsync(api, "SELECT COUNT(*) FROM BENEFICIOS"));
        Assert.Equal(3L, await ContarAsync(api, "SELECT COUNT(*) FROM FORMULARIOS WHERE ATIVO = TRUE"));
        Assert.Equal(2L, await ContarAsync(api, "SELECT COUNT(*) FROM TREINAMENTOS WHERE PUBLICADO = TRUE"));
        // a conclusão da Camila gerou o certificado, e o código é um UUID v4 (a conferência pública só aceita v4)
        Assert.Equal(1L, await ContarAsync(api, "SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE CODIGO_VALIDACAO REGEXP '^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$'"));
    }

    [Theory]
    [InlineData("2026-10-10")] // sábado
    [InlineData("2026-10-12")] // segunda-feira, feriado nacional
    [InlineData("2025-12-31")] // virada de ano: a janela de ponto e o pagamento do mês seguinte cruzam o ano (antes do ano de feriados semeado pelo V001)
    [InlineData("2026-01-02")] // logo depois da virada
    public async Task O_seed_roda_em_qualquer_dia_ate_hoje_porque_todas_as_datas_saem_de_hoje(string hoje)
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();

        await RodarOSeedAsync(conexao, DateOnly.Parse(hoje), Dominio);

        Assert.Equal(12L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.Equal(4L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FERIAS"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM PAGAMENTOS_EXCECOES"));
    }

    [Fact]
    public async Task Rodar_o_seed_de_novo_falha_logo_no_primeiro_comando_e_nao_duplica_nada()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await RodarOSeedAsync(conexao, Hoje, Dominio);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => RodarOSeedAsync(conexao, Hoje, Dominio));

        Assert.Contains("O seed falhou", erro.Message, StringComparison.Ordinal);
        Assert.Equal((12L, 8L), (await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"), await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS")));
    }

    [Theory]
    [InlineData("helena.martins")]
    [InlineData("rafael.costa")]
    [InlineData("beatriz.nogueira")]
    [InlineData("lucas.pereira")]
    [InlineData("julia.andrade")]
    public async Task Cada_pessoa_da_demo_entra_com_a_senha_de_demonstracao_e_a_senha_errada_nao_entra(string local)
    {
        var api = await ApiAsync(mysql);

        var cliente = await EntrarComoAsync(api, local);
        var eu = await GetAsync(cliente, "/api/auth/eu");

        Assert.Equal(Email(local), eu.GetProperty("email").GetString());
        var errada = await api.CreateClient().PostAsJsonAsync("/api/auth/entrar", new { email = Email(local), senha = "outra-senha" });
        Assert.Equal(401, (int)errada.StatusCode);
    }

    [Fact]
    public async Task O_painel_do_RH_mostra_as_pendencias_da_demo()
    {
        var api = await ApiAsync(mysql);
        var rh = await EntrarComoAsync(api, "helena.martins");

        var painel = await GetAsync(rh, "/api/rh/painel");

        var cartoes = painel.GetProperty("pendencias").EnumerateArray().ToDictionary(p => p.GetProperty("tipo").GetString()!, p => p.GetProperty("quantidade").GetInt32());
        Assert.Equal(1, cartoes["FERIAS"]); // a Camila: o gestor aprovou, falta o RH
        Assert.Equal(1, cartoes["ADMISSAO_COM_DOCUMENTOS_PENDENTES"]); // a Júlia começa em 5 dias e ainda não enviou o formulário de admissão
        Assert.Equal(2, cartoes["BENEFICIOS"]); // o plano odontológico do Diego e o novo trajeto da Vanessa
        Assert.False(cartoes.ContainsKey("ATESTADO"));
        Assert.NotEmpty(painel.GetProperty("treinamentosEmAndamento").EnumerateArray());
    }

    [Fact]
    public async Task O_painel_do_gestor_mostra_a_equipe_e_o_pedido_de_ferias_esperando()
    {
        var api = await ApiAsync(mysql);
        var gestor = await EntrarComoAsync(api, "rafael.costa");

        var painel = await GetAsync(gestor, "/api/gestor/painel");

        Assert.Equal("Tecnologia", painel.GetProperty("setor").GetProperty("nome").GetString());
        Assert.Equal(3, painel.GetProperty("totalPessoas").GetInt32()); // Lucas, Camila e Diego (a Júlia ainda não começou: não é da equipe de hoje)
        var ferias = painel.GetProperty("aguardandoAprovacao").GetProperty("ferias").EnumerateArray().ToList();
        Assert.Equal(["Lucas Pereira"], ferias.Select(f => f.GetProperty("funcionario").GetProperty("nome").GetString()!));
    }

    [Fact]
    public async Task A_pessoa_ve_o_painel_dela_com_ponto_ferias_beneficio_e_avisos()
    {
        var api = await ApiAsync(mysql);
        var lucas = await EntrarComoAsync(api, "lucas.pereira");

        var painel = await GetAsync(lucas, "/api/eu/painel");
        var avisos = await GetAsync(lucas, "/api/eu/avisos");
        var treinamentos = await GetAsync(lucas, "/api/eu/treinamentos");

        Assert.True(painel.TryGetProperty("ferias", out _));
        Assert.NotEmpty(avisos.GetProperty("itens").EnumerateArray());
        Assert.NotEmpty(treinamentos.GetProperty("itens").EnumerateArray());
    }
}

using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O extrato do banco de horas (<c>/api/eu/ponto/banco-de-horas</c> da própria pessoa e <c>/api/rh/funcionarios/{id}/ponto/banco-de-horas</c> do RH), no banco REAL. <c>saldoMinutos</c> é a soma dos minutos DE CADA LINHA (convertida uma a uma de horas com 2 casas, com
/// arredondamento) de TODOS os lançamentos, nunca o dos filtrados nem o da página; a lista vem do dia de referência mais recente para o mais antigo (ordem fixa), filtra por <c>de</c>, <c>ate</c> (inclusive) e <c>tipo</c> e pagina; <c>atualizadoAte</c> é o último dia consolidado,
/// global. Cada pessoa tem o seu banco neste teste (um por teste), porque o saldo soma tudo.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ExtratoDoBancoDeHorasTests(MySqlFixture mysql)
{
    private const string Eu = "/api/eu/ponto/banco-de-horas";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ExtratoDoBancoDeHorasTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = 'CONSOLIDAR_BANCO_HORAS'");
        return api;
    }

    /// <summary>O saldo soma TUDO da pessoa: cada teste começa com o banco das pessoas que usa vazio.</summary>
    private static Task ZerarAsync(ApiDeAcesso api, params PessoaDeTeste[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"DELETE FROM BANCOS_HORAS WHERE ID_FUNCIONARIO IN ({string.Join(", ", pessoas.Select(p => p.FuncionarioId))})");

    private static Task LinhaAsync(ApiDeAcesso api, int funcionarioId, string dia, string tipo, string horas, string observacao) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, OBSERVACAO, DIA_REFERENCIA) VALUES ({funcionarioId}, '{dia}', '{tipo}', {horas}, '{observacao}', '{dia}')");

    private static async Task<HttpResponseMessage> MeuAsync(ApiDeAcesso api, PessoaDeTeste pessoa, string consulta = "") =>
        await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(consulta.Length == 0 ? Eu : $"{Eu}?{consulta}");

    private static async Task<HttpResponseMessage> ComoRhAsync(ApiDeAcesso api, int funcionarioId, string consulta = "") =>
        await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas" + (consulta.Length == 0 ? string.Empty : $"?{consulta}"));

    private static List<string> Dias(JsonElement extrato) => [.. extrato.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("diaReferencia").GetString()!)];

    private static async Task SemearOExemploDoContratoAsync(ApiDeAcesso api, int funcionarioId)
    {
        await LinhaAsync(api, funcionarioId, "2026-09-01", "CREDITO", "10.00", "Hora extra"); // +600
        await LinhaAsync(api, funcionarioId, "2026-09-02", "CREDITO", "0.07", "Hora extra"); // 4,2 min: +4
        await LinhaAsync(api, funcionarioId, "2026-09-25", "DEBITO", "4.00", "Saldo negativo do dia"); // -240
        await LinhaAsync(api, funcionarioId, "2026-10-01", "CREDITO", "0.10", "Hora extra"); // +6
        await LinhaAsync(api, funcionarioId, "2026-10-05", "DEBITO", "0.03", "Saldo negativo do dia"); // 1,8 min: -2
        await LinhaAsync(api, funcionarioId, "2026-10-06", "CREDITO", "0.20", "Hora extra"); // +12
    }

    // ---------------------------------------------------------------- o saldo e a lista

    [Fact]
    public async Task O_exemplo_do_contrato_saldo_380_os_quatro_ultimos_lancamentos_e_o_total_de_6_linhas()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var diego = api.Elenco.MembroA;
        await SemearOExemploDoContratoAsync(api, diego.FuncionarioId);

        var resposta = await MeuAsync(api, diego, "tamanhoPagina=4");
        var extrato = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal((380, JsonValueKind.Null, 1, 4, 6, 2), (extrato.GetProperty("saldoMinutos").GetInt32(), extrato.GetProperty("atualizadoAte").ValueKind, extrato.GetProperty("pagina").GetInt32(), extrato.GetProperty("tamanhoPagina").GetInt32(), extrato.GetProperty("total").GetInt32(), extrato.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal(
            [("2026-10-06", "CREDITO", 12, "Hora extra"), ("2026-10-05", "DEBITO", 2, "Saldo negativo do dia"), ("2026-10-01", "CREDITO", 6, "Hora extra"), ("2026-09-25", "DEBITO", 240, "Saldo negativo do dia")],
            extrato.GetProperty("itens").EnumerateArray().Select(i => (i.GetProperty("diaReferencia").GetString()!, i.GetProperty("tipo").GetString()!, i.GetProperty("quantidadeMinutos").GetInt32(), i.GetProperty("descricao").GetString()!)));
        Assert.True(extrato.GetProperty("itens")[0].GetProperty("id").GetInt32() > 0);
    }

    [Fact]
    public async Task O_saldo_soma_os_minutos_de_cada_linha_e_nao_as_horas_somadas()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var julia = api.Elenco.MembroB;
        foreach (var dia in new[] { "2026-08-03", "2026-08-04", "2026-08-05" })
        {
            await LinhaAsync(api, julia.FuncionarioId, dia, "CREDITO", "0.02", "Hora extra"); // 1,2 min cada: 1
        }

        var extrato = await CorpoAsync(await MeuAsync(api, julia));

        Assert.Equal(3, extrato.GetProperty("saldoMinutos").GetInt32()); // 0,06 h somadas dariam 3,6: 4 minutos
    }

    [Fact]
    public async Task Sem_nenhuma_linha_o_saldo_e_zero_e_a_lista_vazia()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);

        var extrato = await CorpoAsync(await MeuAsync(api, api.Elenco.GestorSemSetor));

        Assert.Equal((0, 0, 0, 0), (extrato.GetProperty("saldoMinutos").GetInt32(), extrato.GetProperty("itens").GetArrayLength(), extrato.GetProperty("total").GetInt32(), extrato.GetProperty("totalPaginas").GetInt32()));
    }

    [Fact]
    public async Task A_pagina_dois_traz_as_duas_linhas_que_sobram_e_a_ordem_no_mesmo_dia_e_pelo_id_maior()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var gestor = api.Elenco.Gestor;
        await SemearOExemploDoContratoAsync(api, gestor.FuncionarioId);

        var primeira = await CorpoAsync(await MeuAsync(api, gestor, "tamanhoPagina=4&pagina=1"));
        var segunda = await CorpoAsync(await MeuAsync(api, gestor, "tamanhoPagina=4&pagina=2"));
        var alem = await CorpoAsync(await MeuAsync(api, gestor, "tamanhoPagina=4&pagina=3"));

        Assert.Equal(["2026-10-06", "2026-10-05", "2026-10-01", "2026-09-25"], Dias(primeira));
        Assert.Equal(["2026-09-02", "2026-09-01"], Dias(segunda));
        Assert.Equal((380, 6, 2), (segunda.GetProperty("saldoMinutos").GetInt32(), segunda.GetProperty("total").GetInt32(), segunda.GetProperty("totalPaginas").GetInt32())); // o saldo não depende da página
        Assert.Empty(Dias(alem));
    }

    [Fact]
    public async Task De_e_ate_sao_inclusivos_o_tipo_filtra_e_o_saldo_continua_o_de_todas_as_linhas()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var rh = api.Elenco.Rh;
        await SemearOExemploDoContratoAsync(api, rh.FuncionarioId);

        var intervalo = await CorpoAsync(await MeuAsync(api, rh, "de=2026-09-25&ate=2026-10-05"));
        var creditos = await CorpoAsync(await MeuAsync(api, rh, "tipo=CREDITO"));
        var debitosDoIntervalo = await CorpoAsync(await MeuAsync(api, rh, "tipo=DEBITO&de=2026-10-05"));
        var soDe = await CorpoAsync(await MeuAsync(api, rh, "de=2026-10-01"));
        var soAte = await CorpoAsync(await MeuAsync(api, rh, "ate=2026-09-02"));

        Assert.Equal(["2026-10-05", "2026-10-01", "2026-09-25"], Dias(intervalo));
        Assert.Equal(["2026-10-06", "2026-10-01", "2026-09-02", "2026-09-01"], Dias(creditos));
        Assert.Equal(["2026-10-05"], Dias(debitosDoIntervalo));
        Assert.Equal(["2026-10-06", "2026-10-05", "2026-10-01"], Dias(soDe));
        Assert.Equal(["2026-09-02", "2026-09-01"], Dias(soAte));
        Assert.All(new[] { intervalo, creditos, debitosDoIntervalo, soDe, soAte }, e => Assert.Equal(380, e.GetProperty("saldoMinutos").GetInt32()));
        Assert.Equal((3, 4, 1), (intervalo.GetProperty("total").GetInt32(), creditos.GetProperty("total").GetInt32(), debitosDoIntervalo.GetProperty("total").GetInt32())); // o total é o dos filtrados
    }

    [Fact]
    public async Task Cada_um_ve_so_o_proprio_extrato_e_o_atualizadoAte_e_o_ultimo_dia_consolidado_global()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var diego = api.Elenco.MembroA;
        var julia = api.Elenco.MembroB;
        await LinhaAsync(api, diego.FuncionarioId, "2026-07-01", "CREDITO", "1.00", "Hora extra");
        await LinhaAsync(api, julia.FuncionarioId, "2026-07-02", "DEBITO", "2.00", "Falta");
        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            var agendador = escopo.ServiceProvider.GetRequiredService<IAgendador>();
            await agendador.RegistrarComoConcluidaAsync("CONSOLIDAR_BANCO_HORAS", "CONSOLIDAR_BANCO_HORAS:dia=2026-10-04", new DateOnly(2026, 10, 4), CancellationToken.None);
            await agendador.RegistrarComoConcluidaAsync("CONSOLIDAR_BANCO_HORAS", "CONSOLIDAR_BANCO_HORAS:dia=2026-10-06", new DateOnly(2026, 10, 6), CancellationToken.None);
        }

        var doDiego = await CorpoAsync(await MeuAsync(api, diego));
        var daJulia = await CorpoAsync(await MeuAsync(api, julia));

        Assert.Equal((60, "2026-10-06", "2026-07-01"), (doDiego.GetProperty("saldoMinutos").GetInt32(), doDiego.GetProperty("atualizadoAte").GetString(), Assert.Single(Dias(doDiego))));
        Assert.Equal((-120, "2026-10-06", "2026-07-02"), (daJulia.GetProperty("saldoMinutos").GetInt32(), daJulia.GetProperty("atualizadoAte").GetString(), Assert.Single(Dias(daJulia))));
    }

    // ---------------------------------------------------------------- filtros inválidos

    [Theory]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=0", "tamanhoPagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    [InlineData("tipo=TALVEZ", "tipo")]
    [InlineData("de=2026-10-05&ate=2026-10-04", "ate")]
    public async Task Filtro_invalido_da_400_com_o_nome_do_campo(string consulta, string campo)
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);

        var resposta = await MeuAsync(api, api.Elenco.MembroA, consulta);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    [Theory]
    [InlineData("de=2026-02-30")]
    [InlineData("ate=ontem")]
    public async Task Data_que_nao_existe_ou_nao_e_data_da_400(string consulta)
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);

        Assert.Equal(400, (int)(await MeuAsync(api, api.Elenco.MembroA, consulta)).StatusCode);
    }

    // ---------------------------------------------------------------- o RH, os perfis e o contrato

    [Fact]
    public async Task O_RH_ve_o_extrato_de_qualquer_pessoa_e_o_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var julia = api.Elenco.MembroB;
        await LinhaAsync(api, julia.FuncionarioId, "2026-06-01", "DEBITO", "1.50", "Falta");

        var extrato = await CorpoAsync(await ComoRhAsync(api, julia.FuncionarioId));
        var inexistente = await ComoRhAsync(api, int.MaxValue);

        Assert.Equal((-90, 1), (extrato.GetProperty("saldoMinutos").GetInt32(), extrato.GetProperty("total").GetInt32()));
        Assert.Equal(404, (int)inexistente.StatusCode);
    }

    [Fact]
    public async Task So_o_RH_entra_na_rota_do_RH_qualquer_perfil_entra_na_propria_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var e = api.Elenco;

        foreach (var intruso in new[] { e.Gestor, e.MembroA })
        {
            Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(intruso.UsuarioId)).GetAsync($"/api/rh/funcionarios/{e.MembroB.FuncionarioId}/ponto/banco-de-horas")).StatusCode);
        }

        foreach (var pessoa in new[] { e.Rh, e.Gestor, e.MembroA })
        {
            Assert.Equal(200, (int)(await MeuAsync(api, pessoa)).StatusCode);
        }

        var anonimo = api.Api.ClienteAnonimo();
        Assert.Equal((401, 401), ((int)(await anonimo.GetAsync(Eu)).StatusCode, (int)(await anonimo.GetAsync($"/api/rh/funcionarios/{e.MembroB.FuncionarioId}/ponto/banco-de-horas")).StatusCode));
    }

    [Fact]
    public async Task As_respostas_seguem_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var diego = api.Elenco.MembroA;
        await LinhaAsync(api, diego.FuncionarioId, "2026-05-04", "CREDITO", "0.50", "Hora extra");

        await (await MeuAsync(api, diego)).AfirmarConformeAoContratoAsync("obterMeuBancoDeHoras");
        await (await MeuAsync(api, diego, "tipo=TALVEZ")).AfirmarConformeAoContratoAsync("obterMeuBancoDeHoras"); // 400
        await (await ComoRhAsync(api, diego.FuncionarioId)).AfirmarConformeAoContratoAsync("obterBancoDeHorasDeFuncionario");
        await (await ComoRhAsync(api, int.MaxValue)).AfirmarConformeAoContratoAsync("obterBancoDeHorasDeFuncionario"); // 404
    }

    // ---------------------------------------------------------------- o indicador da ficha

    [Fact]
    public async Task O_indicador_de_banco_de_horas_da_ficha_e_o_saldo_e_nao_e_mais_nulo()
    {
        var api = await ApiAsync();
        await ZerarAsync(api, api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA, api.Elenco.MembroB, api.Elenco.GestorSemSetor);
        var diego = api.Elenco.MembroA;
        var semLinhas = api.Elenco.GestorSemSetor;
        await LinhaAsync(api, diego.FuncionarioId, "2026-04-06", "DEBITO", "2.00", "Falta");

        var doDiego = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{diego.FuncionarioId}/indicadores"));
        var daGestora = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{semLinhas.FuncionarioId}/indicadores"));
        var pelaEquipe = await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{diego.FuncionarioId}/indicadores"));

        Assert.Equal((-120, 0, -120), (doDiego.GetProperty("bancoDeHorasMinutos").GetInt32(), daGestora.GetProperty("bancoDeHorasMinutos").GetInt32(), pelaEquipe.GetProperty("bancoDeHorasMinutos").GetInt32()));
    }
}

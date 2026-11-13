using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Pagamento;

/// <summary>
/// O pagamento para o RH (<c>/api/rh/pagamento</c>) no banco REAL: a lista de exceções, o calendário do ano e o PUT e o DELETE da exceção de um mês. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). Os testes de calendário usam 2031 e 2032, sem feriado importado, e inserem só os feriados de que precisam; cada teste
/// começa sem exceção nenhuma e com o dia de pagamento que ele pede.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PagamentoRhTests(MySqlFixture mysql)
{
    private const string Excecoes = "/api/rh/pagamento/excecoes";

    private const string Calendario = "/api/rh/pagamento/calendario";

    private async Task<ApiDeAcesso> ApiAsync(int? diaDoPagamento = 6)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PagamentoRhTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
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

    private static Task Excecao(ApiDeAcesso api, int ano, int mes, string data) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES ({ano}, {mes}, '{data}')");

    private static string Rota(int ano, int mes) => $"{Excecoes}/{ano}/{mes}";

    private static long Linhas(ApiDeAcesso api) => Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM PAGAMENTOS_EXCECOES").GetAwaiter().GetResult();

    private static string? Data(ApiDeAcesso api, int ano, int mes) =>
        Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(DATA_PAGAMENTO, '%Y-%m-%d') FROM PAGAMENTOS_EXCECOES WHERE ANO = {ano} AND MES = {mes}").GetAwaiter().GetResult() as string;

    private static List<(int Ano, int Mes, string Data)> Itens(JsonElement lista) =>
        [.. lista.GetProperty("itens").EnumerateArray().Select(i => (i.GetProperty("ano").GetInt32(), i.GetProperty("mes").GetInt32(), i.GetProperty("dataPagamento").GetString()!))];

    /// <summary>Os ajustes saem juntos com "+" (vazio = nenhum): tupla com lista compara por referência.</summary>
    private static List<(int Mes, string Data, string Origem, string Ajustes)> Meses(JsonElement calendario) =>
        [.. calendario.GetProperty("meses").EnumerateArray().Select(m => (m.GetProperty("mes").GetInt32(), m.GetProperty("data").GetString()!, m.GetProperty("origem").GetString()!, string.Join("+", m.GetProperty("ajustes").EnumerateArray().Select(a => a.GetString()!))))];

    // ---------------------------------------------------------------- listarExcecoesDePagamento

    [Fact]
    public async Task Sem_ano_a_lista_traz_as_excecoes_do_mes_atual_em_diante_em_ordem_de_data_e_com_ano_as_daquele_ano()
    {
        var api = await ApiAsync();
        await Excecao(api, 2025, 12, "2025-12-19");
        await Excecao(api, 2026, 9, "2026-09-18");
        await Excecao(api, 2026, 10, "2026-10-05"); // o mês de hoje conta mesmo com a data já passada
        await Excecao(api, 2027, 2, "2027-02-19");
        await Excecao(api, 2026, 12, "2026-12-18");
        var rh = await api.ClienteDoRhAgoraAsync();

        var semAno = await rh.GetAsync(Excecoes);
        var de2026 = await CorpoAsync(await rh.GetAsync($"{Excecoes}?ano=2026"));
        var de2028 = await CorpoAsync(await rh.GetAsync($"{Excecoes}?ano=2028"));

        Assert.Equal(200, (int)semAno.StatusCode);
        await semAno.AfirmarConformeAoContratoAsync("listarExcecoesDePagamento");
        Assert.Equal([(2026, 10, "2026-10-05"), (2026, 12, "2026-12-18"), (2027, 2, "2027-02-19")], Itens(await CorpoAsync(semAno)));
        Assert.Equal([(2026, 9, "2026-09-18"), (2026, 10, "2026-10-05"), (2026, 12, "2026-12-18")], Itens(de2026));
        Assert.Empty(Itens(de2028));
    }

    [Theory]
    [InlineData("ano=2019", 400)]
    [InlineData("ano=2101", 400)]
    [InlineData("ano=abc", 400)]
    [InlineData("ano=2020", 200)]
    [InlineData("ano=2100", 200)]
    public async Task O_ano_da_lista_e_do_calendario_vale_de_2020_a_2100(string consulta, int esperado)
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();

        var lista = await rh.GetAsync($"{Excecoes}?{consulta}");
        var calendario = await rh.GetAsync($"{Calendario}?{consulta}");

        Assert.Equal((esperado, esperado), ((int)lista.StatusCode, (int)calendario.StatusCode));
        if (esperado == 400)
        {
            await lista.AfirmarConformeAoContratoAsync("listarExcecoesDePagamento");
            await calendario.AfirmarConformeAoContratoAsync("obterCalendarioDePagamento");
        }
    }

    // ---------------------------------------------------------------- obterCalendarioDePagamento

    [Fact]
    public async Task O_calendario_traz_os_12_meses_com_a_antecipacao_de_fim_de_semana_e_a_excecao_vale_como_esta()
    {
        var api = await ApiAsync(6);
        await Excecao(api, 2031, 11, "2031-11-14");
        await Excecao(api, 2031, 12, "2031-12-21"); // domingo: a exceção não antecipa
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync($"{Calendario}?ano=2031");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterCalendarioDePagamento");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(2031, corpo.GetProperty("ano").GetInt32());
        Assert.Equal(
            [
                (1, "2031-01-06", "REGRA", ""),
                (2, "2031-02-06", "REGRA", ""),
                (3, "2031-03-06", "REGRA", ""),
                (4, "2031-04-04", "REGRA", "ANTECIPADO_FIM_DE_SEMANA"),
                (5, "2031-05-06", "REGRA", ""),
                (6, "2031-06-06", "REGRA", ""),
                (7, "2031-07-04", "REGRA", "ANTECIPADO_FIM_DE_SEMANA"),
                (8, "2031-08-06", "REGRA", ""),
                (9, "2031-09-05", "REGRA", "ANTECIPADO_FIM_DE_SEMANA"),
                (10, "2031-10-06", "REGRA", ""),
                (11, "2031-11-14", "EXCECAO", ""),
                (12, "2031-12-21", "EXCECAO", ""),
            ],
            Meses(corpo));
        Assert.All(corpo.GetProperty("meses").EnumerateArray(), m => Assert.Equal(2031, m.GetProperty("ano").GetInt32()));
    }

    [Fact]
    public async Task Fevereiro_de_ano_bissexto_que_cai_no_domingo_traz_os_dois_ajustes_o_dia_30_vira_o_ultimo_e_antecipa()
    {
        var api = await ApiAsync(30);
        var rh = await api.ClienteDoRhAgoraAsync();

        var corpo = await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2032"));

        var fevereiro = Meses(corpo).Single(m => m.Mes == 2);
        Assert.Equal(("2032-02-27", "REGRA", "ULTIMO_DIA_DO_MES+ANTECIPADO_FIM_DE_SEMANA"), (fevereiro.Data, fevereiro.Origem, fevereiro.Ajustes));
        Assert.Equal("2032-05-28", Meses(corpo).Single(m => m.Mes == 5).Data);
    }

    [Fact]
    public async Task O_dia_31_em_fevereiro_de_ano_comum_vira_o_dia_28_e_o_feriado_antecipa_pelo_feriado()
    {
        var api = await ApiAsync(31);
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Feriado do pagamento", new DateOnly(2031, 1, 31), "EMPRESA");
        var rh = await api.ClienteDoRhAgoraAsync();

        var meses = Meses(await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2031")));

        Assert.Equal(("2031-01-30", "ANTECIPADO_FERIADO"), (meses.Single(m => m.Mes == 1).Data, meses.Single(m => m.Mes == 1).Ajustes));
        Assert.Equal(("2031-02-28", "ULTIMO_DIA_DO_MES"), (meses.Single(m => m.Mes == 2).Data, meses.Single(m => m.Mes == 2).Ajustes)); // 28/02/2031 é sexta
    }

    [Fact]
    public async Task A_antecipacao_pode_voltar_para_o_ano_anterior_e_conta_o_feriado_de_la()
    {
        var api = await ApiAsync(1);
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Confraternizacao", new DateOnly(2031, 1, 1), "EMPRESA");
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Feriado de fim de ano", new DateOnly(2030, 12, 31), "EMPRESA");
        var rh = await api.ClienteDoRhAgoraAsync();

        var janeiro = Meses(await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2031"))).Single(m => m.Mes == 1);

        Assert.Equal(("2030-12-30", "ANTECIPADO_FERIADO"), (janeiro.Data, janeiro.Ajustes)); // 01/01 e 31/12 são feriados: o dia útil anterior é 30/12
    }

    [Fact]
    public async Task Sem_dia_de_pagamento_o_calendario_so_traz_os_meses_com_excecao_e_sem_nada_vem_vazio()
    {
        var api = await ApiAsync(diaDoPagamento: null);
        var rh = await api.ClienteDoRhAgoraAsync();
        var vazio = await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2031"));
        await Excecao(api, 2031, 7, "2031-07-15");

        var comUma = await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2031"));

        Assert.Empty(Meses(vazio));
        Assert.Equal([(7, "2031-07-15", "EXCECAO", "")], Meses(comUma));
    }

    [Fact]
    public async Task Sem_ano_o_calendario_e_o_do_ano_de_hoje_e_a_excecao_de_outro_ano_nao_entra()
    {
        var api = await ApiAsync(6);
        await Excecao(api, 2027, 3, "2027-03-12");
        var rh = await api.ClienteDoRhAgoraAsync();

        var corpo = await CorpoAsync(await rh.GetAsync(Calendario));

        Assert.Equal(2026, corpo.GetProperty("ano").GetInt32());
        Assert.Equal(12, corpo.GetProperty("meses").GetArrayLength());
        Assert.DoesNotContain(Meses(corpo), m => m.Origem == "EXCECAO");
    }

    // ---------------------------------------------------------------- definirExcecaoDePagamento

    [Fact]
    public async Task O_PUT_cria_depois_substitui_a_data_do_mes_audita_e_vale_como_esta_mesmo_em_fim_de_semana()
    {
        var api = await ApiAsync(diaDoPagamento: null); // funciona sem a empresa ter o dia
        var rh = await api.ClienteDoRhAgoraAsync();

        var cria = await rh.PutAsJsonAsync(Rota(2031, 12), new { dataPagamento = "2031-12-18" });
        var troca = await rh.PutAsJsonAsync(Rota(2031, 12), new { dataPagamento = "2031-12-21" }); // domingo

        Assert.Equal((200, 200), ((int)cria.StatusCode, (int)troca.StatusCode));
        await cria.AfirmarConformeAoContratoAsync("definirExcecaoDePagamento");
        var corpo = await CorpoAsync(troca);
        Assert.Equal((2031, 12, "2031-12-21"), (corpo.GetProperty("ano").GetInt32(), corpo.GetProperty("mes").GetInt32(), corpo.GetProperty("dataPagamento").GetString()!));
        Assert.Equal((1L, "2031-12-21"), (Linhas(api), Data(api, 2031, 12)));
        var id = Convert.ToInt32(await Sql.EscalarAsync(api.Conexao, "SELECT ID_PAGAMENTO_EXCECAO FROM PAGAMENTOS_EXCECOES WHERE ANO = 2031 AND MES = 12"), System.Globalization.CultureInfo.InvariantCulture);
        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "PAGAMENTOS_EXCECOES", id);
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], eventos.Select(e => e.Acao));
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Cadastros, api.Elenco.Rh.FuncionarioId), (e.Area, e.FuncionarioAtorId)));
        Assert.Contains(eventos[1].Mudancas, m => m.Campo == "Data do pagamento");
    }

    [Theory]
    [InlineData(2031, 12, "2031-11-30", "dataPagamento")]
    [InlineData(2031, 12, "2032-12-18", "dataPagamento")]
    [InlineData(2031, 12, "2031-01-18", "dataPagamento")]
    [InlineData(2019, 12, "2019-12-18", "ano")]
    [InlineData(2101, 12, "2101-12-18", "ano")]
    [InlineData(2031, 13, "2031-12-18", "mes")]
    [InlineData(2031, 0, "2031-01-18", "mes")]
    public async Task O_PUT_com_data_fora_do_mes_ou_ano_e_mes_fora_da_faixa_e_400_no_campo_certo_e_nao_grava(int ano, int mes, string data, string campo)
    {
        var api = await ApiAsync();

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync(Rota(ano, mes), new { dataPagamento = data });

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("definirExcecaoDePagamento");
        Assert.Equal([campo], (await CorpoAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, Linhas(api));
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"dataPagamento\":\"18/12/2031\"}")]
    [InlineData("{\"dataPagamento\":null}")]
    public async Task O_PUT_sem_data_ou_com_data_mal_escrita_e_400(string corpo)
    {
        var api = await ApiAsync();

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsync(Rota(2031, 12), new StringContent(corpo, Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.Equal(0, Linhas(api));
    }

    [Fact]
    public async Task Dois_PUT_do_mesmo_mes_ao_mesmo_tempo_nao_dao_500_e_deixam_uma_linha_so()
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => rh.PutAsJsonAsync(Rota(2031, 12), new { dataPagamento = $"2031-12-{10 + i:00}" })));

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode));
        Assert.Equal(1, Linhas(api));
        Assert.Contains(Data(api, 2031, 12), Enumerable.Range(0, 8).Select(i => $"2031-12-{10 + i:00}"));
    }

    // ---------------------------------------------------------------- removerExcecaoDePagamento

    [Fact]
    public async Task O_DELETE_remove_a_excecao_audita_e_o_mes_volta_a_seguir_a_regra_e_remover_de_novo_e_404()
    {
        var api = await ApiAsync(6);
        await Excecao(api, 2031, 11, "2031-11-14");
        var id = Convert.ToInt32(await Sql.EscalarAsync(api.Conexao, "SELECT ID_PAGAMENTO_EXCECAO FROM PAGAMENTOS_EXCECOES WHERE ANO = 2031 AND MES = 11"), System.Globalization.CultureInfo.InvariantCulture);
        var rh = await api.ClienteDoRhAgoraAsync();

        var removeu = await rh.DeleteAsync(Rota(2031, 11));
        var depois = Meses(await CorpoAsync(await rh.GetAsync($"{Calendario}?ano=2031"))).Single(m => m.Mes == 11);
        var denovo = await rh.DeleteAsync(Rota(2031, 11));

        Assert.Equal(204, (int)removeu.StatusCode);
        await removeu.AfirmarConformeAoContratoAsync("removerExcecaoDePagamento");
        Assert.Equal(("2031-11-06", "REGRA"), (depois.Data, depois.Origem));
        Assert.Equal(404, (int)denovo.StatusCode);
        await denovo.AfirmarConformeAoContratoAsync("removerExcecaoDePagamento");
        Assert.Equal(0, Linhas(api));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "PAGAMENTOS_EXCECOES", id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Excluiu, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
    }

    [Theory]
    [InlineData(2019, 12)]
    [InlineData(2101, 12)]
    [InlineData(2031, 13)]
    [InlineData(2031, 0)]
    [InlineData(2031, 8)]
    public async Task O_DELETE_de_mes_sem_excecao_ou_fora_da_faixa_e_404_e_nao_mexe_nas_outras(int ano, int mes)
    {
        var api = await ApiAsync();
        await Excecao(api, 2031, 12, "2031-12-18");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync(Rota(ano, mes));

        Assert.Equal(404, (int)resposta.StatusCode);
        Assert.Equal(1, Linhas(api));
    }

    [Fact]
    public async Task Dois_DELETE_ao_mesmo_tempo_dao_um_204_e_o_resto_404_sem_500()
    {
        var api = await ApiAsync();
        await Excecao(api, 2031, 12, "2031-12-18");
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => rh.DeleteAsync(Rota(2031, 12))));

        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 204));
        Assert.All(respostas.Where(r => (int)r.StatusCode != 204), r => Assert.Equal(404, (int)r.StatusCode));
        Assert.Equal(0, Linhas(api));
    }

    // ---------------------------------------------------------------- quem pode

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    public async Task So_o_RH_usa_estas_rotas(Perfil perfil)
    {
        var api = await ApiAsync();
        await Excecao(api, 2031, 12, "2031-12-18");
        var pessoa = await FeriasDeTeste.PessoaAsync(api, "Sem permissao do pagamento " + perfil, new DateOnly(2024, 1, 2), perfil: perfil);
        var cliente = await api.ClienteAgoraAsync(pessoa.UsuarioId);

        var respostas = new[]
        {
            await cliente.GetAsync(Excecoes),
            await cliente.GetAsync(Calendario),
            await cliente.PutAsJsonAsync(Rota(2031, 11), new { dataPagamento = "2031-11-14" }),
            await cliente.DeleteAsync(Rota(2031, 12)),
        };

        Assert.All(respostas, r => Assert.Equal(403, (int)r.StatusCode));
        Assert.Equal(1, Linhas(api));
    }
}

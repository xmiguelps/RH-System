using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Ferias;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// As fichas de férias (gestor e RH), o registro direto do RH e a conferência dele, no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). O Bruno do contrato (admitido em 2022-03-14) tem 18 dias de saldo no período de 2025 a 2026.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class FichaERegistroDeFeriasTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(FichaERegistroDeFeriasTests));

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static string RotaDoRh(int pessoaId) => $"/api/rh/funcionarios/{pessoaId}/ferias";

    private static Task<HttpResponseMessage> RegistrarAsync(HttpClient rh, int pessoaId, object corpo) => rh.PostAsJsonAsync(RotaDoRh(pessoaId), corpo);

    private static object Registro(string inicio, string fim, bool abono = false, int diasAbono = 0, string? observacao = null) =>
        new { dataInicio = inicio, dataFim = fim, abonoPecuniario = abono, diasAbono, adiantar13Salario = false, observacao };

    private static async Task<(int SetorId, PessoaDeFerias Gestor, int GestorId, PessoaDeFerias Bruno)> BrunoNaEquipeAsync(ApiDeAcesso api, string nome, bool com340 = true)
    {
        var (setor, gestor, gestorId, bruno, _) = await EquipeAsync(api, nome);
        await SemearBrunoAsync(api, bruno.FuncionarioId, gestorId, api.Elenco.Rh.FuncionarioId, com340);
        return (setor, gestor, gestorId, bruno);
    }

    // ---------------------------------------------------------------- ficha do gestor

    [Fact]
    public async Task A_ficha_do_gestor_mostra_todos_os_pedidos_da_pessoa_da_equipe_inclusive_os_que_esperam_o_gestor()
    {
        var api = await ApiAsync();
        var (_, gestor, gestorId, bruno) = await BrunoNaEquipeAsync(api, "Ficha do gestor");
        _ = await SemearAsync(api, bruno.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");

        var resposta = await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{bruno.FuncionarioId}/ferias");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFeriasDaPessoaDaEquipe");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((bruno.FuncionarioId, 6), (corpo.GetProperty("funcionario").GetProperty("id").GetInt32(), corpo.GetProperty("ferias").GetArrayLength()));
        Assert.Equal(["SUGESTAO_DE_DATA", "COM_O_GESTOR"], corpo.GetProperty("ferias").EnumerateArray().Take(2).Select(f => f.GetProperty("situacao").GetString()!).OrderByDescending(s => s)); // os dois que esperam, no topo (os mais novos)
        var resumo = corpo.GetProperty("resumo");
        Assert.Equal((18, 2), (resumo.GetProperty("diasDisponiveis").GetInt32(), resumo.GetProperty("pedidosEmAnalise").GetInt32()));
        Assert.Equal(5, corpo.GetProperty("periodosAquisitivos").GetArrayLength());
    }

    [Fact]
    public async Task A_ficha_do_gestor_da_o_mesmo_404_para_pessoa_de_outro_setor_gestor_ele_mesmo_e_quem_nao_existe()
    {
        var api = await ApiAsync();
        var (_, gestor, _, _) = await BrunoNaEquipeAsync(api, "Ficha 404");
        var (_, outroGestor, _, deOutroSetor) = await BrunoNaEquipeAsync(api, "Ficha 404 alheia");
        var cliente = await api.ClienteAgoraAsync(gestor.UsuarioId);

        var respostas = new[]
        {
            await cliente.GetAsync($"/api/gestor/equipe/{deOutroSetor.FuncionarioId}/ferias"),
            await cliente.GetAsync($"/api/gestor/equipe/{outroGestor.FuncionarioId}/ferias"),
            await cliente.GetAsync($"/api/gestor/equipe/{gestor.FuncionarioId}/ferias"),
            await cliente.GetAsync("/api/gestor/equipe/999999/ferias"),
        };

        Assert.All(respostas, r => Assert.Equal(404, (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("obterFeriasDaPessoaDaEquipe");
    }

    // ---------------------------------------------------------------- ficha do RH

    [Fact]
    public async Task A_ficha_do_RH_nao_lista_o_que_esta_com_o_gestor_ou_em_sugestao_mas_o_resumo_os_conta_e_a_ordem_e_do_mais_novo_ao_mais_antigo()
    {
        var api = await ApiAsync();
        var (_, _, gestorId, bruno) = await BrunoNaEquipeAsync(api, "Ficha do RH");
        _ = await SemearAsync(api, bruno.FuncionarioId, "2025-03-14", "2026-03-13", "2027-02-08", "2027-02-22", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2027-02-15", sugestaoFim: "2027-03-01");
        var comORh = await SemearAsync(api, bruno.FuncionarioId, "2025-03-14", "2026-03-13", "2027-04-12", "2027-04-26", "SOLICITADA", "2026-10-07 09:30:00", gestorId, gestorAprovou: true);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(RotaDoRh(bruno.FuncionarioId));

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFeriasDaFichaDoFuncionario");
        var corpo = await CorpoAsync(resposta);
        var situacoes = corpo.GetProperty("ferias").EnumerateArray().Select(f => f.GetProperty("situacao").GetString()!).ToList();
        Assert.DoesNotContain("COM_O_GESTOR", situacoes);
        Assert.DoesNotContain("SUGESTAO_DE_DATA", situacoes);
        Assert.Equal(comORh, corpo.GetProperty("ferias")[0].GetProperty("id").GetInt32()); // o mais novo primeiro
        Assert.Equal(5, corpo.GetProperty("ferias").GetArrayLength()); // o COM_O_RH e os quatro concluídos
        Assert.Equal((18, 3), (corpo.GetProperty("resumo").GetProperty("diasDisponiveis").GetInt32(), corpo.GetProperty("resumo").GetProperty("pedidosEmAnalise").GetInt32()));
        var primeiro = corpo.GetProperty("ferias")[0];
        Assert.Equal(("COM_O_RH", true), (primeiro.GetProperty("situacao").GetString(), primeiro.GetProperty("podeDecidir").GetBoolean())); // o RH decide pela fila; a ficha só informa
        var inexistente = await (await api.ClienteDoRhAgoraAsync()).GetAsync(RotaDoRh(999999));
        Assert.Equal(404, (int)inexistente.StatusCode);
        await inexistente.AfirmarConformeAoContratoAsync("obterFeriasDaFichaDoFuncionario");
    }

    // ---------------------------------------------------------------- registrar

    [Fact]
    public async Task O_RH_registra_ferias_ja_aprovadas_sem_gestor_sem_decisoes_sem_e_mail_e_sem_a_antecedencia()
    {
        var api = await ApiAsync();
        var (_, _, _, bruno) = await BrunoNaEquipeAsync(api, "Registro", com340: false);

        var resposta = await RegistrarAsync(await api.ClienteDoRhAgoraAsync(), bruno.FuncionarioId, Registro("2026-10-13", "2026-10-27", observacao: "Combinado com o gestor em 06/10.")); // 15 dias, já na semana que vem (12/10 é feriado)

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("registrarFeriasPeloRh");
        var pedido = await CorpoAsync(resposta);
        Assert.Equal(("APROVADA", "CONCLUIDA", "APROVADA", 15, "Combinado com o gestor em 06/10.", "2025-03-14"), (
            pedido.GetProperty("status").GetString(),
            pedido.GetProperty("etapa").GetString(),
            pedido.GetProperty("situacao").GetString(),
            pedido.GetProperty("dias").GetInt32(),
            pedido.GetProperty("observacao").GetString(),
            pedido.GetProperty("periodoAquisitivo").GetProperty("inicio").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null, false), (pedido.GetProperty("gestorResponsavel").ValueKind, pedido.GetProperty("decisaoGestor").ValueKind, pedido.GetProperty("decisaoRh").ValueKind, pedido.GetProperty("podeDecidir").GetBoolean()));
        var id = pedido.GetProperty("id").GetInt32();
        Assert.Equal(("APROVADA", (string?)null, (string?)null, "2026-10-07 09:42:00"), (
            await TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT ID_FUNCIONARIO_RH FROM FERIAS WHERE ID_FERIAS = {id}"),
            await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_SOLICITACAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {id}")));
        Assert.Equal(0, EmailsDe(api, bruno.FuncionarioId));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "FERIAS", id));
        Assert.Equal((AuditoriaArea.Ferias, AuditoriaAcao.Registrou), (evento.Area, evento.Acao));
        Assert.DoesNotContain("Combinado com o gestor", evento.Descricao, StringComparison.Ordinal);

        // os 15 dias passam a ser tirados e o saldo da pessoa cai
        var minhas = await CorpoAsync(await (await api.ClienteAgoraAsync(bruno.UsuarioId)).GetAsync(Rota));
        Assert.Equal(3, minhas.GetProperty("resumo").GetProperty("diasDisponiveis").GetInt32());
        Assert.Equal("2026-10-13", minhas.GetProperty("resumo").GetProperty("proximasFerias").GetProperty("dataInicio").GetString());
    }

    [Fact]
    public async Task O_registro_no_passado_vale_entra_no_periodo_que_terminou_antes_do_inicio_e_recalcula_o_banco_de_horas_dos_dias_passados()
    {
        var api = await ApiAsync();
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        var pessoa = await PessoaAsync(api, "Registro passado", new DateOnly(2022, 3, 14), api.Elenco.SetorComercialId);
        // 08/09/2026 (terça) já passou: o dia foi consolidado como falta (débito de 8 h) e o registro das férias tem de desfazer isso
        await PontoDeTeste.NovoLancamentoAsync(api.Conexao, pessoa.FuncionarioId, "2026-09-08", "DEBITO", "8.00");
        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DIA_REFERENCIA = '2026-09-08'"));

        var no2023 = await RegistrarAsync(await api.ClienteDoRhAgoraAsync(), pessoa.FuncionarioId, Registro("2023-06-05", "2023-06-18")); // 14 dias; só o primeiro período terminou antes
        var recente = await RegistrarAsync(await api.ClienteDoRhAgoraAsync(), pessoa.FuncionarioId, Registro("2026-09-08", "2026-09-18")); // 07/09 é feriado

        Assert.Equal((201, 201), ((int)no2023.StatusCode, (int)recente.StatusCode));
        Assert.Equal("2022-03-14", (await CorpoAsync(no2023)).GetProperty("periodoAquisitivo").GetProperty("inicio").GetString());
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM BANCOS_HORAS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId} AND DIA_REFERENCIA = '2026-09-08'"));
    }

    [Fact]
    public async Task Registro_fora_das_regras_da_422_com_a_lista_inativo_e_sem_periodo_dao_os_422_de_codigo_e_o_corpo_invalido_da_400_sem_gravar()
    {
        var api = await ApiAsync();
        var (setor, _, _, bruno) = await BrunoNaEquipeAsync(api, "Registro invalido", com340: false);
        var comAprovadas = await PessoaAsync(api, "Com aprovadas", new DateOnly(2022, 3, 14), setor);
        await SemearAsync(api, comAprovadas.FuncionarioId, "2022-03-14", "2023-03-13", "2026-11-16", "2026-11-30", "APROVADA"); // 15 dias no período mais antigo: sobram 15
        var inativa = await PessoaAsync(api, "Inativa", new DateOnly(2022, 3, 14), ativa: false);
        var semAnoDeCasa = api.Elenco.MembroA;
        var rh = await api.ClienteDoRhAgoraAsync();
        var antes = LinhasDe(api, bruno.FuncionarioId);

        var passaDoSaldo = await RegistrarAsync(rh, bruno.FuncionarioId, Registro("2026-12-07", "2026-12-28")); // 22 dias para 18
        var sobrepoe = await RegistrarAsync(rh, comAprovadas.FuncionarioId, Registro("2026-11-23", "2026-12-07")); // 15 dias que coincidem com as férias de 16 a 30 de novembro (20/11 é feriado)
        var deInativa = await RegistrarAsync(rh, inativa.FuncionarioId, Registro("2026-12-07", "2026-12-18"));
        var semPeriodo = await RegistrarAsync(rh, semAnoDeCasa.FuncionarioId, Registro("2026-12-07", "2026-12-18"));
        var inexistente = await RegistrarAsync(rh, 999999, Registro("2026-12-07", "2026-12-18"));
        var invertido = await RegistrarAsync(rh, bruno.FuncionarioId, Registro("2026-12-18", "2026-12-07"));
        var observacaoGrande = await RegistrarAsync(rh, bruno.FuncionarioId, Registro("2026-12-07", "2026-12-18", observacao: new string('o', 501)));

        Assert.Equal((422, 422, 422, 422, 404, 400, 400), ((int)passaDoSaldo.StatusCode, (int)sobrepoe.StatusCode, (int)deInativa.StatusCode, (int)semPeriodo.StatusCode, (int)inexistente.StatusCode, (int)invertido.StatusCode, (int)observacaoGrande.StatusCode));
        await passaDoSaldo.AfirmarConformeAoContratoAsync("registrarFeriasPeloRh");
        var problema = await CorpoAsync(passaDoSaldo);
        Assert.Equal(("FERIAS_REGRAS_NAO_ATENDIDAS", "Registro de férias recusado."), (Codigo(problema), problema.GetProperty("title").GetString()));
        Assert.DoesNotContain(problema.GetProperty("regras").EnumerateArray(), r => r.GetProperty("codigo").GetString() == "ANTECEDENCIA_DE_30_DIAS");
        Assert.Equal(["SEM_SOBREPOSICAO"], (await CorpoAsync(sobrepoe)).GetProperty("regras").EnumerateArray().Where(r => !r.GetProperty("atendida").GetBoolean()).Select(r => r.GetProperty("codigo").GetString()!));
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(deInativa)));
        Assert.Equal("FERIAS_SEM_PERIODO_AQUISITIVO", Codigo(await CorpoAsync(semPeriodo)));
        Assert.Equal(antes, LinhasDe(api, bruno.FuncionarioId));
        await inexistente.AfirmarConformeAoContratoAsync("registrarFeriasPeloRh");
    }

    // ---------------------------------------------------------------- conferir o registro

    [Fact]
    public async Task A_conferencia_do_RH_nao_lista_a_antecedencia_traz_o_aviso_de_pagamento_e_nao_grava()
    {
        var api = await ApiAsync();
        var (_, _, _, bruno) = await BrunoNaEquipeAsync(api, "Conferencia do RH", com340: false);
        var antes = LinhasDe(api, bruno.FuncionarioId);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{RotaDoRh(bruno.FuncionarioId)}/conferencia", new { dataInicio = "2026-12-07", dataFim = "2026-12-21", abonoPecuniario = false, diasAbono = 0 });

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("conferirRegistroDeFerias");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((15, 3, true, "2026-12-05"), (corpo.GetProperty("totalDias").GetInt32(), corpo.GetProperty("diasQueSobram").GetInt32(), corpo.GetProperty("podeEnviar").GetBoolean(), corpo.GetProperty("pagamentoAte").GetString()));
        Assert.Equal(
            ["DURACAO_DA_PARTE", "CABE_NO_SALDO", "PARTE_DE_14_DIAS", "MAXIMO_DE_3_PARTES", "NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO", "SEM_SOBREPOSICAO"],
            corpo.GetProperty("regras").EnumerateArray().Select(r => r.GetProperty("codigo").GetString()!));
        Assert.Equal("Não coincide com outras férias da pessoa.", corpo.GetProperty("regras").EnumerateArray().Last().GetProperty("mensagem").GetString());
        Assert.Equal(antes, LinhasDe(api, bruno.FuncionarioId));

        var inexistente = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{RotaDoRh(999999)}/conferencia", new { dataInicio = "2026-12-07", dataFim = "2026-12-21" });
        var invalida = await (await api.ClienteDoRhAgoraAsync()).PostAsJsonAsync($"{RotaDoRh(bruno.FuncionarioId)}/conferencia", new { dataInicio = "2026-12-21", dataFim = "2026-12-07" });
        Assert.Equal((404, 400), ((int)inexistente.StatusCode, (int)invalida.StatusCode));
        await inexistente.AfirmarConformeAoContratoAsync("conferirRegistroDeFerias");
    }

    // ---------------------------------------------------------------- corrida

    [Fact]
    public async Task O_registro_do_RH_e_o_pedido_da_pessoa_ao_mesmo_tempo_nunca_estouram_o_saldo()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Corrida do registro", new DateOnly(2025, 3, 14), api.Elenco.SetorComercialId); // um período fechado com 30 dias
        var barreira = new BarreiraDeTeste();
        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IFeriasRepositorio>();
            s.AddScoped<FeriasRepositorio>();
            s.AddScoped<IFeriasRepositorio>(sp => new RepositorioDeFeriasComGancho(sp.GetRequiredService<FeriasRepositorio>()) { AntesDeCriar = barreira.AguardarAsync });
        }));
        var rh = fabrica.CreateClient();
        rh.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        var daPessoa = fabrica.CreateClient();
        daPessoa.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(pessoa.UsuarioId)).DefaultRequestHeaders.Authorization;

        // 16 + 16 = 32 > 30: só um dos dois entra, na ordem que for
        var respostas = await Task.WhenAll(RegistrarAsync(rh, pessoa.FuncionarioId, Registro("2026-11-16", "2026-12-01")), daPessoa.PostAsJsonAsync(Rota, Pedido("2027-01-11", "2027-01-26")));

        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 422));
        Assert.Equal(1, respostas.Count(r => (int)r.StatusCode == 201));
        Assert.Equal(16, Contar(api, $"SELECT SUM(DATEDIFF(DATA_FIM, DATA_INICIO) + 1) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    // ---------------------------------------------------------------- quais dias do banco de horas o registro recalcula

    private sealed class RecalculoEspiao(DateOnly? falhaNoDia = null) : IRecalculoDoBancoDeHoras
    {
        private readonly List<DateOnly> _dias = [];

        public IReadOnlyList<DateOnly> Dias
        {
            get
            {
                lock (_dias)
                {
                    return [.. _dias];
                }
            }
        }

        public Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct)
        {
            lock (_dias)
            {
                _dias.Add(dia);
            }

            return dia == falhaNoDia ? Task.FromException(new InvalidOperationException("falha de teste")) : Task.CompletedTask;
        }
    }

    private static async Task<(HttpResponseMessage Resposta, RecalculoEspiao Espiao)> RegistrarComEspiaoAsync(ApiDeAcesso api, string nome, string inicio, string fim, DateOnly? falhaNoDia = null)
    {
        var pessoa = await PessoaAsync(api, nome, new DateOnly(2022, 3, 14), api.Elenco.SetorComercialId);
        var espiao = new RecalculoEspiao(falhaNoDia);
        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IRecalculoDoBancoDeHoras>();
            s.AddSingleton<IRecalculoDoBancoDeHoras>(espiao);
        }));
        var rh = fabrica.CreateClient();
        rh.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        return (await RegistrarAsync(rh, pessoa.FuncionarioId, Registro(inicio, fim)), espiao);
    }

    [Fact]
    public async Task O_registro_inteiro_no_passado_recalcula_todos_os_dias_das_ferias_do_primeiro_ao_ultimo_e_so_eles()
    {
        var api = await ApiAsync();

        var (resposta, espiao) = await RegistrarComEspiaoAsync(api, "Recalculo no passado", "2026-09-14", "2026-09-27");

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(Enumerable.Range(0, 14).Select(i => new DateOnly(2026, 9, 14).AddDays(i)), espiao.Dias);
    }

    [Fact]
    public async Task O_registro_que_atravessa_hoje_recalcula_so_os_dias_que_ja_passaram_e_nao_hoje_nem_o_futuro()
    {
        var api = await ApiAsync();

        var (resposta, espiao) = await RegistrarComEspiaoAsync(api, "Recalculo atravessa hoje", "2026-10-05", "2026-10-18");

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal([new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 6)], espiao.Dias);
    }

    [Fact]
    public async Task A_falha_no_recalculo_de_um_dia_nao_derruba_o_registro_nem_os_outros_dias()
    {
        var api = await ApiAsync();

        var (resposta, espiao) = await RegistrarComEspiaoAsync(api, "Recalculo com falha", "2026-09-14", "2026-09-27", falhaNoDia: new DateOnly(2026, 9, 16));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(14, espiao.Dias.Count);
    }
}

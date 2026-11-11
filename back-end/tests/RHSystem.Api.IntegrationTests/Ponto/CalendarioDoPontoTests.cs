using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ponto;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O calendário do ponto e a fonte de fatos do dia, no banco REAL. A carga do <c>V001</c> traz os 10 nacionais de 2026 (07/09 Independência, 15/11 Proclamação, que cai num domingo...). Cada teste usa as suas datas e as suas pessoas
/// (os fatos de um dia não aparecem no outro), e a empresa é reconfigurada a cada teste que depende dela.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CalendarioDoPontoTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private async Task<ApiDeAcesso> ApiAsync() => await ApiDeAcesso.ObterAsync(mysql, nameof(CalendarioDoPontoTests));

    private static async Task<DiaDoCalendario> DiaAsync(ApiDeAcesso api, DateOnly dia)
    {
        using var escopo = api.Api.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<ICalendarioDoPonto>().DiaAsync(dia, Ct);
    }

    private static async Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> FatosAsync(ApiDeAcesso api, DateOnly dia, params int[] ids)
    {
        using var escopo = api.Api.Services.CreateScope();
        return await escopo.ServiceProvider.GetRequiredService<IFonteDeFatosDoDia>().ObterPessoasAsync(ids, dia, Ct);
    }

    // ---------------------------------------------------------------- calendário

    [Fact]
    public async Task Dia_util_e_segunda_a_sexta_sem_feriado_e_fim_de_semana_nao_tem_nome()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);

        Assert.Equal(new DiaDoCalendario(true, null), await DiaAsync(api, new DateOnly(2026, 10, 7))); // quarta
        Assert.Equal(new DiaDoCalendario(false, null), await DiaAsync(api, new DateOnly(2026, 10, 10))); // sábado
        Assert.Equal(new DiaDoCalendario(false, null), await DiaAsync(api, new DateOnly(2026, 10, 11))); // domingo
        Assert.Equal(new DiaDoCalendario(true, null), await DiaAsync(api, new DateOnly(2026, 10, 9))); // sexta
    }

    [Fact]
    public async Task Feriado_nacional_da_carga_tira_o_dia_util_e_traz_o_nome_e_no_domingo_o_nome_continua()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);

        Assert.Equal(new DiaDoCalendario(false, "Independencia do Brasil"), await DiaAsync(api, new DateOnly(2026, 9, 7))); // segunda
        Assert.Equal(new DiaDoCalendario(false, "Proclamacao da Republica"), await DiaAsync(api, new DateOnly(2026, 11, 15))); // domingo
    }

    [Fact]
    public async Task Feriado_estadual_so_vale_com_a_UF_da_empresa()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 7, 9);
        await NovoFeriadoAsync(api.Conexao, "Revolucao Constitucionalista", dia, "ESTADUAL", uf: "SP");

        await ConfigurarEmpresaAsync(api.Conexao, uf: "SP");
        var emSp = await DiaAsync(api, dia);
        await ConfigurarEmpresaAsync(api.Conexao, uf: "RJ", municipio: "Rio de Janeiro");
        var emRj = await DiaAsync(api, dia);

        Assert.Equal(new DiaDoCalendario(false, "Revolucao Constitucionalista"), emSp);
        Assert.Equal(new DiaDoCalendario(true, null), emRj);
    }

    [Fact]
    public async Task Feriado_municipal_so_vale_com_o_municipio_da_empresa_sem_distinguir_maiuscula_nem_acento()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 8, 12);
        await NovoFeriadoAsync(api.Conexao, "Aniversario da cidade", dia, "MUNICIPAL", uf: "SP", municipio: "Sao Paulo");

        await ConfigurarEmpresaAsync(api.Conexao, uf: "SP", municipio: "SÃO PAULO");
        var mesmaCidade = await DiaAsync(api, dia);
        await ConfigurarEmpresaAsync(api.Conexao, uf: "SP", municipio: "Campinas");
        var outraCidade = await DiaAsync(api, dia);
        await ConfigurarEmpresaAsync(api.Conexao, uf: "RJ", municipio: "Sao Paulo"); // o município de mesmo nome em outra UF não conta
        var outraUf = await DiaAsync(api, dia);

        Assert.Equal(new DiaDoCalendario(false, "Aniversario da cidade"), mesmaCidade);
        Assert.Equal(new DiaDoCalendario(true, null), outraCidade);
        Assert.Equal(new DiaDoCalendario(true, null), outraUf);
    }

    [Fact]
    public async Task Feriado_da_empresa_vale_e_o_inativo_e_ignorado()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        await NovoFeriadoAsync(api.Conexao, "Recesso da empresa", new DateOnly(2026, 8, 13), "EMPRESA");
        await NovoFeriadoAsync(api.Conexao, "Recesso cancelado", new DateOnly(2026, 8, 14), "EMPRESA", ativo: false);

        Assert.Equal(new DiaDoCalendario(false, "Recesso da empresa"), await DiaAsync(api, new DateOnly(2026, 8, 13)));
        Assert.Equal(new DiaDoCalendario(true, null), await DiaAsync(api, new DateOnly(2026, 8, 14)));
    }

    [Fact]
    public async Task Sem_a_empresa_configurada_valem_so_os_nacionais_e_os_da_empresa()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        await NovoFeriadoAsync(api.Conexao, "Estadual sem empresa", new DateOnly(2026, 6, 1), "ESTADUAL", uf: "SP");
        await NovoFeriadoAsync(api.Conexao, "Municipal sem empresa", new DateOnly(2026, 6, 2), "MUNICIPAL", uf: "SP", municipio: "Sao Paulo");
        await NovoFeriadoAsync(api.Conexao, "Empresa sem empresa", new DateOnly(2026, 6, 3), "EMPRESA");

        Assert.Equal(new DiaDoCalendario(true, null), await DiaAsync(api, new DateOnly(2026, 6, 1)));
        Assert.Equal(new DiaDoCalendario(true, null), await DiaAsync(api, new DateOnly(2026, 6, 2)));
        Assert.Equal(new DiaDoCalendario(false, "Empresa sem empresa"), await DiaAsync(api, new DateOnly(2026, 6, 3)));
        Assert.Equal(new DiaDoCalendario(false, "Independencia do Brasil"), await DiaAsync(api, new DateOnly(2026, 9, 7)));
    }

    [Fact]
    public async Task Com_mais_de_um_feriado_na_data_o_nome_e_o_do_tipo_mais_amplo_e_a_lista_vem_por_data_tipo_e_nome()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        var dia = new DateOnly(2026, 3, 16);
        await NovoFeriadoAsync(api.Conexao, "Ponte da empresa", dia, "EMPRESA");
        await NovoFeriadoAsync(api.Conexao, "Feriado da cidade", dia, "MUNICIPAL", uf: "SP", municipio: "Sao Paulo");
        await NovoFeriadoAsync(api.Conexao, "Feriado do estado", dia, "ESTADUAL", uf: "SP");
        await NovoFeriadoAsync(api.Conexao, "Feriado do pais", dia, "NACIONAL");
        await NovoFeriadoAsync(api.Conexao, "Antes de todos", dia.AddDays(-1), "EMPRESA");
        await NovoFeriadoAsync(api.Conexao, "Depois do periodo", dia.AddDays(2), "EMPRESA");

        using var escopo = api.Api.Services.CreateScope();
        var calendario = escopo.ServiceProvider.GetRequiredService<ICalendarioDoPonto>();
        var lista = await calendario.FeriadosQueValemAsync(dia.AddDays(-1), dia.AddDays(1), Ct);

        Assert.Equal(
            [("Antes de todos", TipoDeFeriado.Empresa), ("Feriado do pais", TipoDeFeriado.Nacional), ("Feriado do estado", TipoDeFeriado.Estadual), ("Feriado da cidade", TipoDeFeriado.Municipal), ("Ponte da empresa", TipoDeFeriado.Empresa)],
            lista.Select(f => (f.Nome, f.Tipo)));
        Assert.Equal("Feriado do pais", (await calendario.DiaAsync(dia, Ct)).NomeDoFeriado);
    }

    [Fact]
    public async Task A_lista_inclui_as_duas_pontas_do_periodo()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        using var escopo = api.Api.Services.CreateScope();
        var calendario = escopo.ServiceProvider.GetRequiredService<ICalendarioDoPonto>();

        var lista = await calendario.FeriadosQueValemAsync(new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15), Ct); // Finados (2/11) a Proclamação (15/11)

        Assert.Equal([new DateOnly(2026, 11, 2), new DateOnly(2026, 11, 15)], lista.Select(f => f.Data));
    }

    // ---------------------------------------------------------------- calendário da fonte (limite de entrada)

    [Fact]
    public async Task O_limite_de_entrada_e_o_horario_mais_a_tolerancia_e_sem_empresa_nao_ha_limite()
    {
        var api = await ApiAsync();
        using var escopo = api.Api.Services.CreateScope();
        var fonte = escopo.ServiceProvider.GetRequiredService<IFonteDeFatosDoDia>();
        var quarta = new DateOnly(2026, 10, 7);

        await ConfigurarEmpresaAsync(api.Conexao, tolerancia: 10);
        var comTolerancia = await fonte.ObterCalendarioAsync(quarta, Ct);
        await ConfigurarEmpresaAsync(api.Conexao, tolerancia: 0);
        var semTolerancia = await fonte.ObterCalendarioAsync(quarta, Ct);
        await ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(23, 50), tolerancia: 20);
        var viraODia = await fonte.ObterCalendarioAsync(quarta, Ct);
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        var semEmpresa = await fonte.ObterCalendarioAsync(quarta, Ct);

        Assert.Equal(new CalendarioDoDia(true, new TimeOnly(8, 10)), comTolerancia);
        Assert.Equal(new CalendarioDoDia(true, new TimeOnly(8, 0)), semTolerancia);
        Assert.Equal(new CalendarioDoDia(true, TimeOnly.MaxValue), viraODia); // 23:50 + 20 min não volta para 00:10: vale até o fim do dia
        Assert.Equal(new CalendarioDoDia(true, null), semEmpresa);
    }

    [Fact]
    public async Task O_calendario_da_fonte_diz_que_feriado_e_fim_de_semana_nao_tem_expediente()
    {
        var api = await ApiAsync();
        await ConfigurarEmpresaAsync(api.Conexao);
        using var escopo = api.Api.Services.CreateScope();
        var fonte = escopo.ServiceProvider.GetRequiredService<IFonteDeFatosDoDia>();

        Assert.False((await fonte.ObterCalendarioAsync(new DateOnly(2026, 9, 7), Ct)).DiaComExpediente);
        Assert.False((await fonte.ObterCalendarioAsync(new DateOnly(2026, 10, 10), Ct)).DiaComExpediente);
        Assert.True((await fonte.ObterCalendarioAsync(new DateOnly(2026, 10, 8), Ct)).DiaComExpediente);
    }

    // ---------------------------------------------------------------- fatos de cada pessoa

    [Fact]
    public async Task Quem_bateu_entrada_no_dia_trabalha_e_so_a_entrada_do_proprio_dia_conta()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 9, 1);
        var (comEntrada, soSaida, entradaDeOntem, semNada) = (api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroB.FuncionarioId, api.Elenco.Gestor.FuncionarioId, api.Elenco.DeOutroSetor.FuncionarioId);
        await BaterAsync(api.Conexao, comEntrada, dia, (TipoDeMarcacao.Entrada, 11), (TipoDeMarcacao.Saida, 15));
        await BaterAsync(api.Conexao, soSaida, dia, (TipoDeMarcacao.Saida, 11));
        await BaterAsync(api.Conexao, entradaDeOntem, dia.AddDays(-1), (TipoDeMarcacao.Entrada, 11));

        var fatos = await FatosAsync(api, dia, comEntrada, soSaida, entradaDeOntem, semNada);

        Assert.Equal([comEntrada], fatos.Keys); // quem não tem nenhum fato nem entra no dicionário
        Assert.Equal(new FatosDaPessoaNoDia(true, false, false), fatos[comEntrada]);
    }

    [Fact]
    public async Task So_ferias_aprovadas_em_gozo_ou_concluidas_que_cobrem_o_dia_tiram_a_pessoa_do_expediente()
    {
        var api = await ApiAsync();
        var dia = new DateOnly(2026, 3, 4);
        var e = api.Elenco;
        await NovasFeriasAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-03-02", "2026-03-10", "APROVADA");
        await NovasFeriasAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-03-02", "2026-03-10", "EM_GOZO");
        await NovasFeriasAsync(api.Conexao, e.Gestor.FuncionarioId, "2026-03-02", "2026-03-10", "CONCLUIDA");
        await NovasFeriasAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-03-02", "2026-03-10", "SOLICITADA");
        await NovasFeriasAsync(api.Conexao, e.GestorDoFinanceiro.FuncionarioId, "2026-03-02", "2026-03-10", "CANCELADA");
        await NovasFeriasAsync(api.Conexao, e.GestorSemSetor.FuncionarioId, "2026-02-23", "2026-03-03", "APROVADA"); // terminou na véspera

        var fatos = await FatosAsync(api, dia, e.MembroA.FuncionarioId, e.MembroB.FuncionarioId, e.Gestor.FuncionarioId, e.DeOutroSetor.FuncionarioId, e.GestorDoFinanceiro.FuncionarioId, e.GestorSemSetor.FuncionarioId);

        Assert.Equal(new[] { e.MembroA.FuncionarioId, e.MembroB.FuncionarioId, e.Gestor.FuncionarioId }.Order(), fatos.Keys.Order());
        Assert.All(fatos.Values, f => Assert.Equal(new FatosDaPessoaNoDia(false, true, false), f));
    }

    [Fact]
    public async Task As_ferias_incluem_o_primeiro_e_o_ultimo_dia()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.Rh.FuncionarioId;
        await NovasFeriasAsync(api.Conexao, pessoa, "2026-04-06", "2026-04-10", "APROVADA");

        var emFerias = new List<bool>();
        foreach (var dia in new[] { new DateOnly(2026, 4, 5), new DateOnly(2026, 4, 6), new DateOnly(2026, 4, 10), new DateOnly(2026, 4, 11) })
        {
            emFerias.Add((await FatosAsync(api, dia, pessoa)).ContainsKey(pessoa));
        }

        Assert.Equal([false, true, true, false], emFerias);
    }

    [Fact]
    public async Task So_atestado_validado_de_dia_inteiro_que_cobre_o_dia_conta_e_inclui_as_duas_pontas()
    {
        var api = await ApiAsync();
        var e = api.Elenco;
        var rh = e.Rh.FuncionarioId;
        await NovoAtestadoAsync(api.Conexao, e.MembroA.FuncionarioId, "2026-05-04", "2026-05-06", "VALIDADO", rh);
        await NovoAtestadoAsync(api.Conexao, e.MembroB.FuncionarioId, "2026-05-04", "2026-05-06", "ENVIADO", rh);
        await NovoAtestadoAsync(api.Conexao, e.Gestor.FuncionarioId, "2026-05-04", "2026-05-06", "RECUSADO", rh);
        await NovoAtestadoAsync(api.Conexao, e.DeOutroSetor.FuncionarioId, "2026-05-05", "2026-05-05", "VALIDADO", rh, "09:00", "11:00"); // de horas não é de dia inteiro
        var todos = new[] { e.MembroA.FuncionarioId, e.MembroB.FuncionarioId, e.Gestor.FuncionarioId, e.DeOutroSetor.FuncionarioId };

        var porDia = new Dictionary<string, int[]>();
        foreach (var dia in new[] { "2026-05-03", "2026-05-04", "2026-05-05", "2026-05-06", "2026-05-07" })
        {
            porDia[dia] = [.. (await FatosAsync(api, DateOnly.Parse(dia), todos)).Where(f => f.Value.ComAtestadoDeDiaInteiro).Select(f => f.Key)];
        }

        Assert.Equal([], porDia["2026-05-03"]);
        Assert.Equal([e.MembroA.FuncionarioId], porDia["2026-05-04"]);
        Assert.Equal([e.MembroA.FuncionarioId], porDia["2026-05-05"]); // o de 09:00 às 11:00 do mesmo dia não entra
        Assert.Equal([e.MembroA.FuncionarioId], porDia["2026-05-06"]);
        Assert.Equal([], porDia["2026-05-07"]);
    }

    [Fact]
    public async Task Sem_ids_a_fonte_devolve_vazio()
    {
        var api = await ApiAsync();

        Assert.Empty(await FatosAsync(api, new DateOnly(2026, 10, 7)));
    }

    [Fact]
    public async Task O_conteiner_entrega_a_fonte_o_calendario_e_as_ferias_do_ponto()
    {
        var api = await ApiAsync();
        using var escopo = api.Api.Services.CreateScope();

        Assert.IsType<FonteDeFatosDoDiaDoPonto>(escopo.ServiceProvider.GetRequiredService<IFonteDeFatosDoDia>());
        Assert.IsType<CalendarioDoPonto>(escopo.ServiceProvider.GetRequiredService<ICalendarioDoPonto>());
        Assert.IsType<FeriasDoPonto>(escopo.ServiceProvider.GetRequiredService<IFeriasDoPonto>());
    }
}

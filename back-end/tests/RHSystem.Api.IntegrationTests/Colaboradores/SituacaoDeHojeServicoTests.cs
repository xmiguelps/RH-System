using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Infrastructure.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>A situação de hoje de cada pessoa, com relógio falso e uma fonte de fatos falsa (sem banco). Um teste confere o registro no contêiner.</summary>
[Collection(MySqlCollection.Nome)]
public class SituacaoDeHojeServicoTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static RelogioDeTeste RelogioEm(DateTime utc)
    {
        var relogio = new RelogioDeTeste();
        relogio.Fixar(utc);
        return relogio;
    }

    private sealed class FonteFalsa(CalendarioDoDia calendario, Dictionary<int, FatosDaPessoaNoDia>? fatos = null) : IFonteDeFatosDoDia
    {
        public DateOnly? DiaPedido { get; private set; }

        public Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct)
        {
            DiaPedido = dia;
            return Task.FromResult(calendario);
        }

        public Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct) =>
            Task.FromResult<IReadOnlyDictionary<int, FatosDaPessoaNoDia>>(fatos ?? []);
    }

    [Fact]
    public async Task Quarta_2026_10_07_12h42Z_e_dia_util_e_sabado_2026_10_10_nao_e()
    {
        var quarta = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), new FonteDeFatosDoDiaSemRegistros()).ObterAsync([1], Ct);
        var sabado = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 10, 12, 42, 0, DateTimeKind.Utc)), new FonteDeFatosDoDiaSemRegistros()).ObterAsync([1], Ct);

        Assert.Equal((new DateOnly(2026, 10, 7), true), (quarta.Dia, quarta.DiaUtil));
        Assert.Equal((new DateOnly(2026, 10, 10), false), (sabado.Dia, sabado.DiaUtil));
    }

    [Fact]
    public async Task No_sabado_so_quem_tem_entrada_registrada_e_TRABALHANDO_e_diaUtil_e_false()
    {
        var fonte = new FonteFalsa(new CalendarioDoDia(false, null), new() { [1] = new FatosDaPessoaNoDia(true, false, false) });
        var servico = new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 10, 12, 42, 0, DateTimeKind.Utc)), fonte);

        var dia = await servico.ObterAsync([1, 2], Ct);

        Assert.False(dia.DiaUtil);
        Assert.Equal(SituacaoHoje.Trabalhando, dia.Situacoes[1]);
        Assert.Equal(SituacaoHoje.SemExpediente, dia.Situacoes[2]);
    }

    [Fact]
    public async Task Fonte_padrao_deixa_todo_ativo_AINDA_NAO_COMECOU_num_dia_util_e_nunca_SEM_REGISTRO()
    {
        // Decisão 1 do plano: sem ponto nem horário padrão (fatia 3 e 1c) ninguém está atrasado, nem às 22h59 de Brasília.
        var tardeDaNoite = RelogioEm(new DateTime(2026, 10, 8, 1, 59, 0, DateTimeKind.Utc));
        var dia = await new SituacaoDeHojeServico(tardeDaNoite, new FonteDeFatosDoDiaSemRegistros()).ObterAsync([1, 2, 3], Ct);

        Assert.True(dia.DiaUtil);
        Assert.Equal([SituacaoHoje.AindaNaoComecou, SituacaoHoje.AindaNaoComecou, SituacaoHoje.AindaNaoComecou], [dia.Situacoes[1], dia.Situacoes[2], dia.Situacoes[3]]);
    }

    [Fact]
    public async Task Virada_do_dia_2026_10_08T02h30Z_ainda_e_quarta_07_10_em_Brasilia()
    {
        var fonte = new FonteFalsa(new CalendarioDoDia(true, null));

        var dia = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc)), fonte).ObterAsync([1], Ct);

        Assert.Equal(new DateOnly(2026, 10, 7), dia.Dia);
        Assert.Equal(new DateOnly(2026, 10, 7), fonte.DiaPedido); // a fonte também recebe o dia de Brasília, não o de UTC
    }

    [Fact]
    public async Task Usa_o_horario_de_Brasilia_e_nao_o_UTC_para_o_limite_de_entrada()
    {
        var fonte = new FonteFalsa(new CalendarioDoDia(true, new TimeOnly(8, 10)));

        // 11:00Z = 08:00 em Brasília: ainda dentro do limite. Em UTC (11:00) a pessoa já estaria "sem registro".
        var cedo = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 7, 11, 0, 0, DateTimeKind.Utc)), fonte).ObterAsync([1], Ct);
        // 12:42Z = 09:42 em Brasília: passou do limite.
        var tarde = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), fonte).ObterAsync([1], Ct);

        Assert.Equal(SituacaoHoje.AindaNaoComecou, cedo.Situacoes[1]);
        Assert.Equal(SituacaoHoje.SemRegistro, tarde.Situacoes[1]);
    }

    [Fact]
    public async Task Pessoa_que_a_fonte_nao_lista_nao_tem_nenhum_fato_e_toda_pessoa_pedida_volta_no_resultado()
    {
        var fonte = new FonteFalsa(new CalendarioDoDia(true, null), new() { [7] = new FatosDaPessoaNoDia(false, true, false) });

        var dia = await new SituacaoDeHojeServico(RelogioEm(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), fonte).ObterAsync([7, 8], Ct);

        Assert.Equal(SituacaoHoje.Ferias, dia.Situacoes[7]);
        Assert.Equal(SituacaoHoje.AindaNaoComecou, dia.Situacoes[8]);
        Assert.Equal(2, dia.Situacoes.Count);
    }

    [Fact]
    public async Task O_conteiner_entrega_a_fonte_padrao_e_o_servico_por_escopo()
    {
        var api = await ApiCompartilhada.ObterAsync(mysql);
        using var escopo1 = api.Services.CreateScope();
        using var escopo2 = api.Services.CreateScope();

        Assert.IsType<FonteDeFatosDoDiaSemRegistros>(escopo1.ServiceProvider.GetRequiredService<IFonteDeFatosDoDia>());
        Assert.IsType<SituacaoDeHojeServico>(escopo1.ServiceProvider.GetRequiredService<ISituacaoDeHoje>());
        Assert.Same(escopo1.ServiceProvider.GetRequiredService<ISituacaoDeHoje>(), escopo1.ServiceProvider.GetRequiredService<ISituacaoDeHoje>());
        Assert.NotSame(escopo1.ServiceProvider.GetRequiredService<ISituacaoDeHoje>(), escopo2.ServiceProvider.GetRequiredService<ISituacaoDeHoje>());
    }
}

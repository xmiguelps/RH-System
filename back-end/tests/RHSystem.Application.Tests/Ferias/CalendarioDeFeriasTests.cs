using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>A janela do calendário da equipe: a segunda-feira da primeira semana (a do dia informado ou, sem ele, a de hoje), o domingo da última e a segunda de cada coluna (o exemplo do contrato: 13 semanas a partir de 05/10/2026).</summary>
public class CalendarioDeFeriasTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7); // quarta

    [Fact]
    public void Sem_inicio_comeca_na_segunda_da_semana_de_hoje_com_13_semanas_como_o_exemplo_do_contrato()
    {
        var janela = CalendarioDeFerias.Janela(null, 13, Hoje);

        Assert.Equal((new DateOnly(2026, 10, 5), new DateOnly(2027, 1, 3)), (janela.Inicio, janela.Fim));
        Assert.Equal(
            ["2026-10-05", "2026-10-12", "2026-10-19", "2026-10-26", "2026-11-02", "2026-11-09", "2026-11-16", "2026-11-23", "2026-11-30", "2026-12-07", "2026-12-14", "2026-12-21", "2026-12-28"],
            janela.Semanas.Select(s => s.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
    }

    [Theory]
    [InlineData(2026, 10, 5, 2026, 10, 5)] // segunda
    [InlineData(2026, 10, 6, 2026, 10, 5)] // terça
    [InlineData(2026, 10, 10, 2026, 10, 5)] // sábado
    [InlineData(2026, 10, 11, 2026, 10, 5)] // domingo: ainda é a semana que começou na segunda anterior
    [InlineData(2026, 10, 12, 2026, 10, 12)]
    [InlineData(2026, 1, 1, 2025, 12, 29)] // quinta, virada do ano
    public void Qualquer_dia_informado_vira_a_segunda_da_semana_dele(int a, int m, int d, int ea, int em, int ed) =>
        Assert.Equal(new DateOnly(ea, em, ed), CalendarioDeFerias.Janela(new DateOnly(a, m, d), 4, Hoje).Inicio);

    [Theory]
    [InlineData(1, 2026, 10, 11)]
    [InlineData(2, 2026, 10, 18)]
    [InlineData(26, 2027, 4, 4)]
    public void O_fim_e_o_domingo_da_ultima_semana(int semanas, int ea, int em, int ed)
    {
        var janela = CalendarioDeFerias.Janela(null, semanas, Hoje);

        Assert.Equal(new DateOnly(ea, em, ed), janela.Fim);
        Assert.Equal(semanas, janela.Semanas.Count);
        Assert.Equal(DayOfWeek.Sunday, janela.Fim.DayOfWeek);
    }

    [Theory]
    [InlineData(2026, 10, 4, 2026, 10, 4, false)] // domingo antes da janela
    [InlineData(2026, 10, 5, 2026, 10, 5, true)] // só o primeiro dia
    [InlineData(2026, 9, 20, 2026, 10, 5, true)] // termina no primeiro dia
    [InlineData(2026, 10, 11, 2026, 10, 18, true)]
    [InlineData(2026, 10, 12, 2026, 10, 18, false)] // começa depois do fim da janela de 1 semana
    [InlineData(2026, 9, 1, 2026, 12, 31, true)] // cobre a janela inteira
    public void O_item_entra_quando_encosta_na_janela_nem_que_seja_por_um_dia(int ia, int im, int id, int fa, int fm, int fd, bool entra)
    {
        var janela = CalendarioDeFerias.Janela(null, 1, Hoje); // 05/10 a 11/10

        Assert.Equal(entra, janela.Encosta(new DateOnly(ia, im, id), new DateOnly(fa, fm, fd)));
    }

    private static LinhaDoPedidoDeFerias Linha(StatusDasFerias status, int? gestorId = 8, ResultadoDoGestorNasFerias? resultado = null, RespostaDaSugestao? resposta = null) =>
        new(status, gestorId, 80, resultado, resultado is null ? null : new DateTime(2026, 10, 6, 13, 0, 0, DateTimeKind.Utc), false, null, null, null, null, resposta, null, null);

    [Theory]
    [InlineData(StatusDasFerias.Aprovada, SituacaoDoItemDoCalendario.Aprovada)]
    [InlineData(StatusDasFerias.EmGozo, SituacaoDoItemDoCalendario.Aprovada)]
    [InlineData(StatusDasFerias.Concluida, SituacaoDoItemDoCalendario.Aprovada)]
    [InlineData(StatusDasFerias.Rejeitada, null)]
    [InlineData(StatusDasFerias.Cancelada, null)]
    public void Os_status_finais_viram_aprovada_ou_ficam_fora_do_calendario(StatusDasFerias status, SituacaoDoItemDoCalendario? esperado) =>
        Assert.Equal(esperado, CalendarioDeFerias.Situacao(Linha(status), 8));

    [Fact]
    public void O_pedido_em_analise_vira_aguardando_voce_so_para_o_gestor_dele_com_o_RH_sem_etapa_e_sugestao_aceita_e_fora_a_sugestao_pendente()
    {
        Assert.Equal(SituacaoDoItemDoCalendario.AguardandoVoce, CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada), 8));
        Assert.Null(CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada), 9)); // esperando outro gestor (a pessoa trocou de setor): não é "você"
        Assert.Equal(SituacaoDoItemDoCalendario.ComORh, CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.Aprovou), 8));
        Assert.Equal(SituacaoDoItemDoCalendario.ComORh, CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada, gestorId: null), 8));
        Assert.Equal(SituacaoDoItemDoCalendario.ComORh, CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.SugeriuData, resposta: RespostaDaSugestao.Aceitou), 8));
        Assert.Null(CalendarioDeFerias.Situacao(Linha(StatusDasFerias.Solicitada, resultado: ResultadoDoGestorNasFerias.SugeriuData), 8));
    }
}

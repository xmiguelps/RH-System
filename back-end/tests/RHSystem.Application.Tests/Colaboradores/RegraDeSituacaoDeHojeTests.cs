using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

public class RegraDeSituacaoDeHojeTests
{
    private static readonly CalendarioDoDia DiaUtil = new(true, new TimeOnly(8, 10));
    private static readonly CalendarioDoDia DiaSemExpediente = new(false, new TimeOnly(8, 10));
    private static readonly FatosDaPessoaNoDia Nenhum = new(false, false, false);

    [Theory]
    [InlineData(true, false)] // sábado, domingo ou feriado
    [InlineData(true, true)]
    public void Entrada_registrada_vence_tudo_inclusive_sabado_e_ferias(bool sabado, bool emFerias)
    {
        var calendario = sabado ? DiaSemExpediente : DiaUtil;

        var situacao = RegraDeSituacaoDeHoje.Calcular(calendario, new FatosDaPessoaNoDia(JaRegistrouEntrada: true, EmFerias: emFerias, ComAtestadoDeDiaInteiro: true), new TimeOnly(23, 0));

        Assert.Equal(SituacaoHoje.Trabalhando, situacao);
    }

    [Fact]
    public void Sabado_domingo_ou_feriado_sem_entrada_e_SEM_EXPEDIENTE()
    {
        // O dia sem expediente vence férias e atestado: não há o que "estar de" no domingo.
        Assert.Equal(SituacaoHoje.SemExpediente, RegraDeSituacaoDeHoje.Calcular(DiaSemExpediente, Nenhum, new TimeOnly(9, 0)));
        Assert.Equal(SituacaoHoje.SemExpediente, RegraDeSituacaoDeHoje.Calcular(DiaSemExpediente, new FatosDaPessoaNoDia(false, true, true), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Ferias_vencem_atestado_e_atestado_de_dia_inteiro_e_ATESTADO()
    {
        Assert.Equal(SituacaoHoje.Ferias, RegraDeSituacaoDeHoje.Calcular(DiaUtil, new FatosDaPessoaNoDia(false, true, true), new TimeOnly(9, 0)));
        Assert.Equal(SituacaoHoje.Atestado, RegraDeSituacaoDeHoje.Calcular(DiaUtil, new FatosDaPessoaNoDia(false, false, true), new TimeOnly(9, 0)));
    }

    [Fact]
    public void Ferias_e_atestado_valem_mesmo_depois_do_limite_de_entrada()
    {
        // Quem está de férias às 18h não está "sem registro".
        Assert.Equal(SituacaoHoje.Ferias, RegraDeSituacaoDeHoje.Calcular(DiaUtil, new FatosDaPessoaNoDia(false, true, false), new TimeOnly(18, 0)));
        Assert.Equal(SituacaoHoje.Atestado, RegraDeSituacaoDeHoje.Calcular(DiaUtil, new FatosDaPessoaNoDia(false, false, true), new TimeOnly(18, 0)));
    }

    [Fact]
    public void Sem_entrada_as_09h42_com_limite_08h10_e_SEM_REGISTRO_e_as_08h10_ainda_e_AINDA_NAO_COMECOU_e_as_08h11_e_SEM_REGISTRO()
    {
        Assert.Equal(SituacaoHoje.SemRegistro, RegraDeSituacaoDeHoje.Calcular(DiaUtil, Nenhum, new TimeOnly(9, 42)));
        Assert.Equal(SituacaoHoje.AindaNaoComecou, RegraDeSituacaoDeHoje.Calcular(DiaUtil, Nenhum, new TimeOnly(8, 10))); // estritamente depois do limite
        Assert.Equal(SituacaoHoje.SemRegistro, RegraDeSituacaoDeHoje.Calcular(DiaUtil, Nenhum, new TimeOnly(8, 11)));
        Assert.Equal(SituacaoHoje.AindaNaoComecou, RegraDeSituacaoDeHoje.Calcular(DiaUtil, Nenhum, new TimeOnly(8, 9, 59)));
    }

    [Fact]
    public void Sem_limite_de_entrada_ninguem_esta_atrasado()
    {
        var semHorario = new CalendarioDoDia(true, null); // empresa sem horário padrão configurado

        Assert.Equal(SituacaoHoje.AindaNaoComecou, RegraDeSituacaoDeHoje.Calcular(semHorario, Nenhum, new TimeOnly(23, 59)));
    }
}

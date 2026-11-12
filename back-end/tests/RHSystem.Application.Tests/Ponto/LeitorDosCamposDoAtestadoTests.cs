using RHSystem.Application.Comum;
using RHSystem.Application.Ponto.Atestados;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>A leitura dos campos do envio de atestado e o texto do período da auditoria: as regras puras, sem banco, com os dias e horas do contrato.</summary>
public class LeitorDosCamposDoAtestadoTests
{
    private static (CamposDoAtestado? Campos, IReadOnlyDictionary<string, string[]> Erros) Ler(string? dataInicio, string? dataFim, string? horaInicio = null, string? horaFim = null)
    {
        var coletor = new ColetorDeErros();
        var campos = LeitorDosCamposDoAtestado.Ler(coletor, dataInicio, dataFim, horaInicio, horaFim);
        if (!coletor.TemErros)
        {
            return (campos, new Dictionary<string, string[]>());
        }

        Assert.Null(campos); // com erro, nunca devolve campos
        return (campos, Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros);
    }

    [Fact]
    public void Dias_inteiros_sem_horas_valem()
    {
        var (campos, erros) = Ler("2026-10-01", "2026-10-02");

        Assert.Empty(erros);
        Assert.Equal(new CamposDoAtestado(new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 2), null, null), campos);
    }

    [Fact]
    public void Um_dia_so_vale_com_inicio_igual_ao_fim()
    {
        var (campos, erros) = Ler("2026-10-06", "2026-10-06");

        Assert.Empty(erros);
        Assert.Equal(new DateOnly(2026, 10, 6), campos!.DataInicio);
        Assert.Equal(new DateOnly(2026, 10, 6), campos.DataFim);
    }

    [Fact]
    public void Algumas_horas_de_um_dia_valem_e_espacos_em_volta_sao_aparados()
    {
        var (campos, erros) = Ler(" 2026-09-25 ", " 2026-09-25 ", " 13:00 ", " 17:48 ");

        Assert.Empty(erros);
        Assert.Equal(new CamposDoAtestado(new DateOnly(2026, 9, 25), new DateOnly(2026, 9, 25), new TimeOnly(13, 0), new TimeOnly(17, 48)), campos);
    }

    [Theory]
    [InlineData(null, "2026-10-02", "dataInicio", "Informe a data de início.")]
    [InlineData("", "2026-10-02", "dataInicio", "Informe a data de início.")]
    [InlineData("   ", "2026-10-02", "dataInicio", "Informe a data de início.")]
    [InlineData("2026-10-01", null, "dataFim", "Informe a data de fim.")]
    [InlineData("2026-10-01", " ", "dataFim", "Informe a data de fim.")]
    [InlineData("01/10/2026", "2026-10-02", "dataInicio", "Use a data de início no formato AAAA-MM-DD.")]
    [InlineData("2026-10-01", "2026-13-02", "dataFim", "Use a data de fim no formato AAAA-MM-DD.")]
    [InlineData("2026-10-01", "2026-02-30", "dataFim", "Use a data de fim no formato AAAA-MM-DD.")]
    [InlineData("2026-10-1", "2026-10-02", "dataInicio", "Use a data de início no formato AAAA-MM-DD.")]
    public void Data_ausente_ou_fora_do_formato_da_o_erro_no_campo_dela(string? inicio, string? fim, string campo, string mensagem)
    {
        var (campos, erros) = Ler(inicio, fim);

        Assert.Null(campos);
        Assert.Equal([campo], erros.Keys);
        Assert.Equal([mensagem], erros[campo]);
    }

    [Fact]
    public void Data_de_fim_antes_da_de_inicio_da_o_erro_em_dataFim()
    {
        var (campos, erros) = Ler("2026-10-02", "2026-10-01");

        Assert.Null(campos);
        Assert.Equal(["dataFim"], erros.Keys);
        Assert.Equal(["A data de fim não pode ser antes da data de início."], erros["dataFim"]);
    }

    [Fact]
    public void Os_dois_erros_de_data_vem_juntos()
    {
        var (_, erros) = Ler("amanhã", "ontem");

        Assert.Equal(["dataFim", "dataInicio"], erros.Keys.Order());
    }

    [Theory]
    [InlineData("13:00", null, "horaFim")]
    [InlineData("13:00", "", "horaFim")]
    [InlineData(null, "17:48", "horaInicio")]
    [InlineData("  ", "17:48", "horaInicio")]
    public void As_horas_vem_juntas_e_a_que_falta_leva_o_erro(string? horaInicio, string? horaFim, string campo)
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-25", horaInicio, horaFim);

        Assert.Null(campos);
        Assert.Equal([campo], erros.Keys);
        Assert.Equal(["Informe a hora de início e a hora de fim juntas."], erros[campo]);
    }

    [Theory]
    [InlineData("1:00", "17:48", "horaInicio")]
    [InlineData("13:00", "17h48", "horaFim")]
    [InlineData("24:00", "17:48", "horaInicio")]
    [InlineData("13:00", "17:60", "horaFim")]
    [InlineData("13:00:00", "17:48", "horaInicio")]
    public void Hora_fora_do_formato_HH_mm_da_o_erro_no_campo_dela(string horaInicio, string horaFim, string campo)
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-25", horaInicio, horaFim);

        Assert.Null(campos);
        Assert.Equal([campo], erros.Keys);
        Assert.Equal(["Use a hora no formato HH:mm."], erros[campo]);
    }

    [Fact]
    public void Horas_so_valem_num_dia_e_o_erro_vai_em_dataFim()
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-26", "13:00", "17:48");

        Assert.Null(campos);
        Assert.Equal(["dataFim"], erros.Keys);
        Assert.Equal(["O atestado de algumas horas vale para um dia só: use a mesma data de início e de fim."], erros["dataFim"]);
    }

    [Theory]
    [InlineData("17:48", "13:00")]
    [InlineData("13:00", "13:00")] // o banco também exige o fim DEPOIS do início
    public void Hora_de_fim_que_nao_vem_depois_da_de_inicio_da_o_erro_em_horaFim(string horaInicio, string horaFim)
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-25", horaInicio, horaFim);

        Assert.Null(campos);
        Assert.Equal(["horaFim"], erros.Keys);
        Assert.Equal(["A hora de fim precisa ser depois da hora de início."], erros["horaFim"]);
    }

    [Fact]
    public void Um_minuto_de_atestado_vale()
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-25", "13:00", "13:01");

        Assert.Empty(erros);
        Assert.Equal(new TimeOnly(13, 1), campos!.HoraFim);
    }

    [Fact]
    public void Dia_ruim_com_hora_ruim_leva_os_dois_erros_de_uma_vez()
    {
        var (campos, erros) = Ler("2026-09-25", "2026-09-26", "17:48", "13:00");

        Assert.Null(campos);
        Assert.Equal(["dataFim", "horaFim"], erros.Keys.Order());
    }

    [Fact]
    public void Hora_com_formato_ruim_nao_gera_tambem_o_erro_de_ordem()
    {
        var (_, erros) = Ler("2026-09-25", "2026-09-25", "abc", "13:00");

        Assert.Equal(["horaInicio"], erros.Keys);
    }

    // ---------------------------------------------------------------- o texto do período (auditoria)

    [Theory]
    [InlineData("2026-10-01", "2026-10-02", null, null, "01/10/2026 a 02/10/2026")]
    [InlineData("2026-10-06", "2026-10-06", null, null, "06/10/2026")]
    [InlineData("2026-09-25", "2026-09-25", "13:00", "17:48", "25/09/2026 das 13:00 às 17:48")]
    public void O_periodo_sai_no_texto_da_auditoria(string inicio, string fim, string? horaInicio, string? horaFim, string esperado) =>
        Assert.Equal(
            esperado,
            PeriodoDoAtestado.Texto(DateOnly.Parse(inicio), DateOnly.Parse(fim), horaInicio is null ? null : TimeOnly.Parse(horaInicio), horaFim is null ? null : TimeOnly.Parse(horaFim)));
}

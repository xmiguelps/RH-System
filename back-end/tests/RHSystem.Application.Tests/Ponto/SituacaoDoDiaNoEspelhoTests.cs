using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Tests.Ponto;

public class SituacaoDoDiaNoEspelhoTests
{
    private static EntradaDaSituacaoDoDia Dia(
        bool ehHoje = false,
        bool temPedidoPendente = false,
        bool temBatidas = false,
        bool sequenciaFechada = true,
        bool temAtraso = false,
        bool ehFimDeSemana = false,
        bool ehFeriado = false,
        bool estaDeFerias = false,
        bool temAtestadoDeDiaInteiro = false) =>
        new(ehHoje, temPedidoPendente, temBatidas, sequenciaFechada, temAtraso, ehFimDeSemana, ehFeriado, estaDeFerias, temAtestadoDeDiaInteiro);

    [Fact]
    public void Pedido_pendente_vence_tudo_mesmo_sem_batida()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.AjusteEmAnalise, SituacaoDoDiaNoEspelho.Calcular(Dia(temPedidoPendente: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.AjusteEmAnalise, SituacaoDoDiaNoEspelho.Calcular(Dia(temPedidoPendente: true, temBatidas: true, ehHoje: true, sequenciaFechada: false)));
        Assert.Equal(SituacaoDoDiaNoPonto.AjusteEmAnalise, SituacaoDoDiaNoEspelho.Calcular(Dia(temPedidoPendente: true, ehFimDeSemana: true, ehFeriado: true, estaDeFerias: true)));
    }

    [Fact]
    public void Hoje_com_batida_esta_em_andamento_mesmo_com_a_sequencia_aberta_ou_fechada()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.EmAndamento, SituacaoDoDiaNoEspelho.Calcular(Dia(ehHoje: true, temBatidas: true, sequenciaFechada: false)));
        Assert.Equal(SituacaoDoDiaNoPonto.EmAndamento, SituacaoDoDiaNoEspelho.Calcular(Dia(ehHoje: true, temBatidas: true, sequenciaFechada: true, temAtraso: true)));
    }

    [Fact]
    public void Dia_passado_com_sequencia_quebrada_e_incompleto_mesmo_com_atraso()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.Incompleto, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true, sequenciaFechada: false, temAtraso: true)));
    }

    [Fact]
    public void Dia_passado_com_atraso_e_atraso_e_sem_atraso_e_normal()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.Atraso, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true, temAtraso: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Normal, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true)));
    }

    [Fact]
    public void Batida_em_dia_de_folga_feriado_ou_ferias_conta_como_dia_com_batida()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.Normal, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true, ehFimDeSemana: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Normal, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true, ehFeriado: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Normal, SituacaoDoDiaNoEspelho.Calcular(Dia(temBatidas: true, estaDeFerias: true)));
    }

    [Fact]
    public void Sem_batida_a_precedencia_e_folga_feriado_ferias_falta_compensada_e_sem_registro()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.Folga, SituacaoDoDiaNoEspelho.Calcular(Dia(ehFimDeSemana: true, ehFeriado: true, estaDeFerias: true, temAtestadoDeDiaInteiro: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Feriado, SituacaoDoDiaNoEspelho.Calcular(Dia(ehFeriado: true, estaDeFerias: true, temAtestadoDeDiaInteiro: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Ferias, SituacaoDoDiaNoEspelho.Calcular(Dia(estaDeFerias: true, temAtestadoDeDiaInteiro: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.FaltaCompensada, SituacaoDoDiaNoEspelho.Calcular(Dia(temAtestadoDeDiaInteiro: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.SemRegistro, SituacaoDoDiaNoEspelho.Calcular(Dia()));
    }

    [Fact]
    public void Hoje_sem_batida_e_sem_registro_a_qualquer_hora()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.SemRegistro, SituacaoDoDiaNoEspelho.Calcular(Dia(ehHoje: true)));
    }

    [Fact]
    public void Hoje_sem_batida_em_fim_de_semana_ou_feriado_segue_a_precedencia_do_dia_sem_batida()
    {
        Assert.Equal(SituacaoDoDiaNoPonto.Folga, SituacaoDoDiaNoEspelho.Calcular(Dia(ehHoje: true, ehFimDeSemana: true)));
        Assert.Equal(SituacaoDoDiaNoPonto.Feriado, SituacaoDoDiaNoEspelho.Calcular(Dia(ehHoje: true, ehFeriado: true)));
    }

    [Fact]
    public void Os_nomes_no_JSON_sao_os_do_contrato()
    {
        var nomes = Enum.GetValues<SituacaoDoDiaNoPonto>().Select(s => System.Text.Json.JsonNamingPolicy.SnakeCaseUpper.ConvertName(s.ToString())).Order().ToList();

        Assert.Equal(["AJUSTE_EM_ANALISE", "ATRASO", "EM_ANDAMENTO", "FALTA_COMPENSADA", "FERIADO", "FERIAS", "FOLGA", "INCOMPLETO", "NORMAL", "SEM_REGISTRO"], nomes);
    }
}

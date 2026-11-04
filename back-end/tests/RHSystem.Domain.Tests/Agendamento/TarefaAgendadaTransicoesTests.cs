using RHSystem.Domain.Agendamento;

namespace RHSystem.Domain.Tests.Agendamento;

/// <summary>As saídas de uma tarefa mantêm o que os <c>CHECK</c> do <c>V001</c> exigem (reserva só em EM_EXECUCAO, hora só em CONCLUIDA), sempre num <c>UPDATE</c> só.</summary>
public class TarefaAgendadaTransicoesTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static TarefaAgendada Pendente() => TarefaAgendada.Criar("EFETIVAR_DESLIGAMENTO", "EFETIVAR_DESLIGAMENTO:func=1", Agora, referenciaId: 1);

    [Fact]
    public void Reservar_poe_EM_EXECUCAO_com_a_reserva_a_hora_da_tentativa_e_uma_tentativa_a_mais()
    {
        var tarefa = Pendente();

        tarefa.Reservar(Agora.AddMinutes(10), Agora);

        Assert.Equal((SituacaoDaTarefa.EmExecucao, Agora.AddMinutes(10), (DateTime?)Agora, 1), (tarefa.Situacao, tarefa.ReservadaAte, tarefa.UltimaTentativaEm, tarefa.Tentativas));
    }

    [Fact]
    public void Concluida_grava_a_hora_limpa_a_reserva_e_o_erro()
    {
        var tarefa = Pendente();
        tarefa.Reservar(Agora.AddMinutes(10), Agora);
        tarefa.AgendarNovaTentativa(Agora, "InvalidOperationException: antes falhou");
        tarefa.Reservar(Agora.AddMinutes(10), Agora);

        tarefa.MarcarComoConcluida(Agora.AddSeconds(2));

        Assert.Equal((SituacaoDaTarefa.Concluida, (DateTime?)Agora.AddSeconds(2), (DateTime?)null, (string?)null, 2), (tarefa.Situacao, tarefa.ConcluidaEm, tarefa.ReservadaAte, tarefa.UltimoErro, tarefa.Tentativas));
    }

    [Fact]
    public void Nova_tentativa_volta_a_PENDENTE_para_a_hora_dada_e_limpa_a_reserva()
    {
        var tarefa = Pendente();
        tarefa.Reservar(Agora.AddMinutes(10), Agora);

        tarefa.AgendarNovaTentativa(Agora.AddMinutes(1), "InvalidOperationException: falhou");

        Assert.Equal((SituacaoDaTarefa.Pendente, Agora.AddMinutes(1), (DateTime?)null, "InvalidOperationException: falhou", 1), (tarefa.Situacao, tarefa.ExecutarApos, tarefa.ReservadaAte, tarefa.UltimoErro, tarefa.Tentativas));
    }

    [Fact]
    public void Falhou_limpa_a_reserva_e_guarda_o_erro_sem_somar_tentativa()
    {
        var tarefa = Pendente();
        tarefa.Reservar(Agora.AddMinutes(10), Agora);

        tarefa.MarcarComoFalhou("SemExecutor:EFETIVAR_DESLIGAMENTO");

        Assert.Equal((SituacaoDaTarefa.Falhou, (DateTime?)null, "SemExecutor:EFETIVAR_DESLIGAMENTO", 1), (tarefa.Situacao, tarefa.ReservadaAte, tarefa.UltimoErro, tarefa.Tentativas)); // a tentativa já foi contada ao reservar
    }

    [Fact]
    public void Ultimo_erro_e_cortado_em_500_caracteres()
    {
        var tarefa = Pendente();

        tarefa.AgendarNovaTentativa(Agora, new string('x', 900));

        Assert.Equal(500, tarefa.UltimoErro!.Length);
    }
}

using RHSystem.Domain.Emails;

namespace RHSystem.Domain.Tests.Emails;

/// <summary>As saídas da fila mantêm o que os <c>CHECK</c> do <c>V001</c> exigem, sempre num <c>UPDATE</c> só (as mudanças são todas de uma vez, na entidade).</summary>
public class EmailNaFilaTransicoesTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static EmailNaFila Pendente(bool sigiloso = false) =>
        EmailNaFila.Criar("CONVITE", "CONVITE:func=1", "ana@empresa.com.br", Agora, funcionarioId: sigiloso ? null : 1, sigiloso: sigiloso, carga: "{\"nome\":\"Ana\"}");

    [Fact]
    public void Reservar_poe_EM_ENVIO_com_a_reserva()
    {
        var email = Pendente();

        email.Reservar(Agora.AddMinutes(5));

        Assert.Equal((SituacaoDoEmail.EmEnvio, Agora.AddMinutes(5)), (email.Situacao, email.ReservadoAte));
    }

    [Fact]
    public void Enviado_apaga_a_carga_limpa_a_reserva_e_grava_a_data()
    {
        var email = Pendente();
        email.Reservar(Agora.AddMinutes(5));

        email.MarcarComoEnviado(Agora.AddSeconds(3));

        Assert.Equal((SituacaoDoEmail.Enviado, Agora.AddSeconds(3), (string?)null, (DateTime?)null, (string?)null), (email.Situacao, email.EnviadoEm, email.Carga, email.ReservadoAte, email.UltimoErro));
    }

    [Fact]
    public void Falha_temporaria_soma_tentativa_volta_a_PENDENTE_e_limpa_a_reserva()
    {
        var email = Pendente();
        email.Reservar(Agora.AddMinutes(5));

        email.AgendarNovaTentativa(Agora.AddMinutes(1), "FalhaDeEnvioException:SmtpCommandException 451");

        Assert.Equal((SituacaoDoEmail.Pendente, 1, Agora.AddMinutes(1), (DateTime?)null, "FalhaDeEnvioException:SmtpCommandException 451"), (email.Situacao, email.Tentativas, email.ProximaTentativaEm, email.ReservadoAte, email.UltimoErro));
        Assert.NotNull(email.Carga); // a mensagem ainda espera: a carga fica
    }

    [Fact]
    public void Falhou_apaga_a_carga_limpa_a_reserva_e_soma_tentativa()
    {
        var email = Pendente();
        email.Reservar(Agora.AddMinutes(5));

        email.MarcarComoFalhou("FalhaDeEnvioException 550");

        Assert.Equal((SituacaoDoEmail.Falhou, 1, (string?)null, (DateTime?)null), (email.Situacao, email.Tentativas, email.Carga, email.ReservadoAte));
    }

    [Fact]
    public void Sigilosa_nao_vira_ENVIADO_nem_FALHOU()
    {
        var email = Pendente(sigiloso: true);

        Assert.Throws<InvalidOperationException>(() => email.MarcarComoEnviado(Agora));
        Assert.Throws<InvalidOperationException>(() => email.MarcarComoFalhou("x"));
        Assert.Equal(SituacaoDoEmail.Pendente, email.Situacao); // nada mudou
    }

    [Fact]
    public void Ultimo_erro_e_cortado_em_500_caracteres()
    {
        var email = Pendente();

        email.AgendarNovaTentativa(Agora, new string('x', 900));

        Assert.Equal(500, email.UltimoErro!.Length);
    }
}

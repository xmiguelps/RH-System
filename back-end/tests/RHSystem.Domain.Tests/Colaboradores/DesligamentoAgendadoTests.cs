using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Tests.Colaboradores;

public class DesligamentoAgendadoTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static DesligamentoAgendado Agendado() => DesligamentoAgendado.Criar(1, new DateOnly(2026, 11, 13), Agora.AddDays(-1));

    [Fact]
    public void DesligamentoAgendado_Efetivar_Cancelar_e_Reverter_so_valem_nas_transicoes_do_V001_e_Reverter_mantem_EfetivadoEm()
    {
        // Agendado -> Efetivado (EFETIVADO_EM) -> Revertido (REVERTIDO_EM; o CHECK do V001 exige que EFETIVADO_EM continue preenchido).
        var efetivado = Agendado();
        efetivado.Efetivar(Agora);
        Assert.Equal((SituacaoDoDesligamento.Efetivado, (DateTime?)Agora, (DateTime?)null, (DateTime?)null), (efetivado.Situacao, efetivado.EfetivadoEm, efetivado.CanceladoEm, efetivado.RevertidoEm));

        var depois = Agora.AddDays(30);
        efetivado.Reverter(depois);
        Assert.Equal((SituacaoDoDesligamento.Revertido, (DateTime?)Agora, (DateTime?)depois), (efetivado.Situacao, efetivado.EfetivadoEm, efetivado.RevertidoEm));

        // Agendado -> Cancelado (CANCELADO_EM), sem EFETIVADO_EM.
        var cancelado = Agendado();
        cancelado.Cancelar(Agora);
        Assert.Equal((SituacaoDoDesligamento.Cancelado, (DateTime?)Agora, (DateTime?)null), (cancelado.Situacao, cancelado.CanceladoEm, cancelado.EfetivadoEm));

        // Fora dessas transições nada vale (nem de leve: quem já terminou não volta).
        Assert.Throws<InvalidOperationException>(() => efetivado.Efetivar(Agora));
        Assert.Throws<InvalidOperationException>(() => efetivado.Cancelar(Agora));
        Assert.Throws<InvalidOperationException>(() => cancelado.Efetivar(Agora));
        Assert.Throws<InvalidOperationException>(() => cancelado.Cancelar(Agora));
        Assert.Throws<InvalidOperationException>(() => cancelado.Reverter(Agora));
        Assert.Throws<InvalidOperationException>(() => Agendado().Reverter(Agora)); // só quem está efetivado se reverte
        var jaRevertido = Agendado();
        jaRevertido.Efetivar(Agora);
        jaRevertido.Reverter(Agora);
        Assert.Throws<InvalidOperationException>(() => jaRevertido.Reverter(Agora));

        // O instante é UTC: um instante sem Kind faria o banco guardar a hora errada em silêncio.
        Assert.Throws<ArgumentException>(() => Agendado().Efetivar(DateTime.SpecifyKind(Agora, DateTimeKind.Unspecified)));
        Assert.Throws<ArgumentException>(() => Agendado().Cancelar(DateTime.SpecifyKind(Agora, DateTimeKind.Local)));
    }
}

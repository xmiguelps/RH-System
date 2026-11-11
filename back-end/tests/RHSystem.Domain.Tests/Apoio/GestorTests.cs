using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Tests.Apoio;

public class GestorTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private static Funcionario Pessoa() => Funcionario.Criar("Carlos Ribeiro", "carlos.ribeiro@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso("Funcionario Gestor"));

    [Fact]
    public void Gestor_AusenteEm_vale_ate_o_dia_anterior_a_volta_e_sem_data_vale_enquanto_marcado()
    {
        var gestor = Gestor.Criar(Pessoa());
        Assert.False(gestor.AusenteEm(new DateOnly(2026, 10, 18))); // não marcado: nunca ausente

        gestor.MarcarAusente(new DateOnly(2026, 10, 19), Hoje);

        Assert.True(gestor.AusenteEm(Hoje));
        Assert.True(gestor.AusenteEm(new DateOnly(2026, 10, 18))); // o dia anterior à volta ainda vale
        Assert.False(gestor.AusenteEm(new DateOnly(2026, 10, 19))); // no dia da volta já não
        Assert.False(gestor.AusenteEm(new DateOnly(2026, 10, 20)));

        gestor.MarcarAusente(null, Hoje); // sem data de volta: vale enquanto estiver marcado
        Assert.Null(gestor.AusenteAte);
        Assert.True(gestor.AusenteEm(new DateOnly(2030, 1, 1)));
    }

    [Fact]
    public void Gestor_MarcarAusente_com_volta_hoje_ou_antes_lanca_e_DesmarcarAusencia_zera_a_data()
    {
        var gestor = Gestor.Criar(Pessoa());

        Assert.Throws<ArgumentException>(() => gestor.MarcarAusente(Hoje, Hoje)); // a volta tem de ser depois de hoje
        Assert.Throws<ArgumentException>(() => gestor.MarcarAusente(Hoje.AddDays(-1), Hoje));
        Assert.False(gestor.Ausente); // a recusa não marca nada

        gestor.MarcarAusente(Hoje.AddDays(1), Hoje);
        Assert.Equal((true, (DateOnly?)Hoje.AddDays(1)), (gestor.Ausente, gestor.AusenteAte));

        gestor.DesmarcarAusencia();

        Assert.Equal((false, (DateOnly?)null), (gestor.Ausente, gestor.AusenteAte)); // o CHECK do V001 não aceita data com a marca desligada
    }

    [Fact]
    public void Gestor_diz_o_nome_da_pessoa_e_o_registro_afetado()
    {
        var gestor = Gestor.Criar(Pessoa());

        Assert.Equal("Carlos Ribeiro", gestor.NomeParaAuditoria);
        Assert.Equal("Gestor Carlos Ribeiro", gestor.RegistroAfetado);
    }
}

using RHSystem.Domain.Beneficios;

namespace RHSystem.Domain.Tests.Apoio;

public class BeneficioTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(false, true, false)]
    [InlineData(false, false, true)]
    [InlineData(true, true, true)]
    public void Beneficio_Criar_e_Atualizar_guardam_as_tres_marcas_como_informadas(bool opcional, bool dependentes, bool trajeto)
    {
        var criado = Beneficio.Criar("Plano de saúde", "Cobertura nacional", opcional, dependentes, trajeto);
        Assert.Equal((opcional, dependentes, trajeto), (criado.Opcional, criado.AceitaDependentes, criado.AceitaTrajeto));

        var atualizado = Beneficio.Criar("Vale-refeição", null, !opcional, !dependentes, !trajeto);
        atualizado.Atualizar("Vale-alimentação", "Cartão", opcional, dependentes, trajeto);

        Assert.Equal(("Vale-alimentação", "Cartão", opcional, dependentes, trajeto), (atualizado.Nome, atualizado.Descricao, atualizado.Opcional, atualizado.AceitaDependentes, atualizado.AceitaTrajeto));
    }

    [Fact]
    public void Beneficio_normaliza_o_nome_com_Trim_e_recusa_nome_vazio_ou_acima_de_100_e_descricao_acima_de_255()
    {
        var beneficio = Beneficio.Criar("  Plano de saúde  ", "  Cobertura  ", false, false, false);

        Assert.Equal(("Plano de saúde", "Cobertura"), (beneficio.Nome, beneficio.Descricao));
        Assert.Equal(new string('a', 100), Beneficio.Criar(new string('a', 100), null, false, false, false).Nome);
        Assert.Null(Beneficio.Criar("Plano", "   ", false, false, false).Descricao);
        foreach (var nome in new[] { string.Empty, "   ", new string('a', 101) })
        {
            Assert.ThrowsAny<ArgumentException>(() => Beneficio.Criar(nome, null, false, false, false));
            Assert.ThrowsAny<ArgumentException>(() => beneficio.Atualizar(nome, null, false, false, false));
        }

        Assert.Throws<ArgumentException>(() => Beneficio.Criar("Plano", new string('d', 256), false, false, false));
        Assert.Equal("Plano de saúde", beneficio.Nome); // o que foi recusado não trocou nada
    }
}

using System.Globalization;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Apoio;

public class CargoTests
{
    [Theory]
    [InlineData("44", true)]
    [InlineData("44.5", true)]
    [InlineData("168", true)]
    [InlineData("0.01", true)]
    [InlineData("0", false)]
    [InlineData("-1", false)]
    [InlineData("168.01", false)]
    [InlineData("44.555", false)]
    public void Cargo_aceita_carga_ate_168_com_duas_casas_e_recusa_zero_negativa_acima_de_168_e_tres_casas(string texto, bool valida)
    {
        var carga = decimal.Parse(texto, CultureInfo.InvariantCulture);
        var existente = Cargo.Criar("Analista", null, 40m);

        if (valida)
        {
            Assert.Equal(carga, Cargo.Criar("Gerente", null, carga).CargaSemanalHoras);
            existente.Atualizar("Analista", null, carga);
            Assert.Equal(carga, existente.CargaSemanalHoras);
        }
        else
        {
            Assert.Throws<ArgumentException>(() => Cargo.Criar("Gerente", null, carga));
            Assert.Throws<ArgumentException>(() => existente.Atualizar("Analista", null, carga));
            Assert.Equal(40m, existente.CargaSemanalHoras); // recusar não deixa a metade gravada
        }
    }

    [Fact]
    public void Cargo_e_Setor_normalizam_o_nome_com_Trim_e_recusam_nome_vazio_ou_acima_de_100()
    {
        var cem = new string('a', 100);
        Assert.Equal("Analista", Cargo.Criar("  Analista  ", null, 44m).Nome);
        Assert.Equal("Compras", Setor.Criar("  Compras ", null).Nome);
        Assert.Equal(cem, Cargo.Criar($" {cem} ", null, 44m).Nome); // o Trim vem antes de contar: 100 caracteres com espaços nas pontas valem
        Assert.Equal(cem, Setor.Criar($" {cem} ", null).Nome);

        var cargo = Cargo.Criar("Analista", null, 44m);
        var setor = Setor.Criar("Compras", null);
        foreach (var nome in new[] { string.Empty, "   ", new string('a', 101), $" {new string('a', 101)} " })
        {
            Assert.ThrowsAny<ArgumentException>(() => Cargo.Criar(nome, null, 44m));
            Assert.ThrowsAny<ArgumentException>(() => Setor.Criar(nome, null));
            Assert.ThrowsAny<ArgumentException>(() => cargo.Atualizar(nome, null, 44m));
            Assert.ThrowsAny<ArgumentException>(() => setor.Atualizar(nome, null));
        }

        Assert.ThrowsAny<ArgumentException>(() => Cargo.Criar(null!, null, 44m));
        Assert.ThrowsAny<ArgumentException>(() => Setor.Criar(null!, null));
        Assert.Equal(("Analista", "Compras"), (cargo.Nome, setor.Nome)); // o nome recusado não troca o que estava
    }

    [Fact]
    public void Cargo_e_Setor_aceitam_descricao_de_ate_255_caracteres_e_a_vazia_vira_nula()
    {
        var duzentosECinquentaECinco = new string('d', 255);

        Assert.Equal(duzentosECinquentaECinco, Cargo.Criar("Analista", duzentosECinquentaECinco, 44m).Descricao);
        Assert.Equal(duzentosECinquentaECinco, Setor.Criar("Compras", duzentosECinquentaECinco).Descricao);
        Assert.Equal("Cuida das compras", Setor.Criar("Compras", "  Cuida das compras ").Descricao);
        Assert.Null(Cargo.Criar("Analista", "   ", 44m).Descricao);
        Assert.Null(Setor.Criar("Compras", string.Empty).Descricao);
        Assert.Throws<ArgumentException>(() => Cargo.Criar("Analista", new string('d', 256), 44m));
        Assert.Throws<ArgumentException>(() => Setor.Criar("Compras", new string('d', 256)));
    }
}

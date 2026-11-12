using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>Os feriados calculados: os nacionais (com a Páscoa) e os estaduais de data fixa. A carga do <c>V001</c> traz os nacionais de 2026 e é o oráculo desses dias.</summary>
public class FeriadosNacionaisTests
{
    [Theory]
    [InlineData(2020, 4, 12)]
    [InlineData(2021, 4, 4)]
    [InlineData(2022, 4, 17)]
    [InlineData(2023, 4, 9)]
    [InlineData(2024, 3, 31)]
    [InlineData(2025, 4, 20)]
    [InlineData(2026, 4, 5)]
    [InlineData(2027, 3, 28)]
    [InlineData(2028, 4, 16)]
    [InlineData(2029, 4, 1)]
    [InlineData(2030, 4, 21)]
    public void A_pascoa_cai_no_domingo_certo(int ano, int mes, int dia) =>
        Assert.Equal(new DateOnly(ano, mes, dia), FeriadosNacionais.Pascoa(ano));

    [Fact]
    public void Os_nacionais_de_2026_sao_os_dez_da_carga_do_banco_na_ordem_da_data()
    {
        var esperados = new (string Nome, DateOnly Data)[]
        {
            ("Confraternização Universal", new DateOnly(2026, 1, 1)),
            ("Sexta-feira Santa", new DateOnly(2026, 4, 3)),
            ("Tiradentes", new DateOnly(2026, 4, 21)),
            ("Dia do Trabalho", new DateOnly(2026, 5, 1)),
            ("Independência do Brasil", new DateOnly(2026, 9, 7)),
            ("Nossa Senhora Aparecida", new DateOnly(2026, 10, 12)),
            ("Finados", new DateOnly(2026, 11, 2)),
            ("Proclamação da República", new DateOnly(2026, 11, 15)),
            ("Dia da Consciência Negra", new DateOnly(2026, 11, 20)),
            ("Natal", new DateOnly(2026, 12, 25)),
        };

        Assert.Equal(esperados, FeriadosNacionais.Do(2026).Select(f => (f.Nome, f.Data)));
    }

    [Theory]
    [InlineData(2027, 3, 26)]
    [InlineData(2028, 4, 14)]
    [InlineData(2029, 3, 30)]
    public void A_sexta_feira_santa_acompanha_a_pascoa_de_cada_ano(int ano, int mes, int dia) =>
        Assert.Equal(new DateOnly(ano, mes, dia), Assert.Single(FeriadosNacionais.Do(ano), f => f.Nome == "Sexta-feira Santa").Data);

    [Theory]
    [InlineData(2026)]
    [InlineData(2028)]
    public void Todos_caem_no_ano_pedido_e_carnaval_e_corpus_christi_nao_entram(int ano)
    {
        var feriados = FeriadosNacionais.Do(ano);

        Assert.Equal(10, feriados.Count);
        Assert.All(feriados, f => Assert.Equal(ano, f.Data.Year));
        Assert.DoesNotContain(feriados, f => f.Nome.Contains("Carnaval", StringComparison.OrdinalIgnoreCase) || f.Nome.Contains("Corpus", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(feriados.OrderBy(f => f.Data).ToList(), feriados);
    }

    [Fact]
    public void O_estadual_de_sao_paulo_e_a_revolucao_constitucionalista_e_a_sigla_vale_em_qualquer_caixa()
    {
        var esperado = new FeriadoCalculado("Revolução Constitucionalista", new DateOnly(2026, 7, 9));

        Assert.Equal([esperado], TabelaDeFeriadosEstaduais.Da("SP", 2026));
        Assert.Equal([esperado], TabelaDeFeriadosEstaduais.Da("sp", 2026));
        Assert.Equal([esperado with { Data = new DateOnly(2028, 7, 9) }], TabelaDeFeriadosEstaduais.Da(" Sp ", 2028));
    }

    [Theory]
    [InlineData("XX")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("MG")] // estado sem feriado estadual de data fixa na tabela
    public void Uf_sem_feriado_estadual_na_tabela_devolve_lista_vazia(string? uf) => Assert.Empty(TabelaDeFeriadosEstaduais.Da(uf, 2026));

    [Fact]
    public void Estado_com_dois_feriados_devolve_os_dois_na_ordem_da_data()
    {
        var rondonia = TabelaDeFeriadosEstaduais.Da("RO", 2026);

        Assert.Equal([new DateOnly(2026, 1, 4), new DateOnly(2026, 6, 18)], rondonia.Select(f => f.Data));
    }

    [Fact]
    public void Nenhum_estadual_repete_a_data_de_um_nacional_nem_cai_fora_do_ano()
    {
        var nacionais = FeriadosNacionais.Do(2026).Select(f => f.Data).ToHashSet();
        foreach (var uf in new[] { "AC", "AL", "AM", "AP", "BA", "CE", "DF", "MA", "MS", "PA", "PB", "PE", "PI", "PR", "RJ", "RN", "RO", "RR", "RS", "SC", "SE", "SP", "TO" })
        {
            var estaduais = TabelaDeFeriadosEstaduais.Da(uf, 2026);
            Assert.NotEmpty(estaduais);
            Assert.All(estaduais, f =>
            {
                Assert.Equal(2026, f.Data.Year);
                Assert.DoesNotContain(f.Data, nacionais);
            });
        }
    }
}

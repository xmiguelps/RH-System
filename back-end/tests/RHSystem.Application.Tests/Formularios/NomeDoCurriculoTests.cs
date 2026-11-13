using RHSystem.Application.Formularios.Acompanhar;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// O nome com que o RH baixa o currículo, sem banco: <c>curriculo-&lt;nome&gt;&lt;extensão&gt;</c>; barra e contrabarra do nome viram <c>-</c>, aspas e caracteres de controle somem, e o nome é cortado ANTES da extensão para ela nunca se perder no limite de 100 caracteres.
/// </summary>
public class NomeDoCurriculoTests
{
    [Fact]
    public void Junta_o_prefixo_o_nome_e_a_extensao()
    {
        Assert.Equal("curriculo-Mariana Teixeira.pdf", NomeDoCurriculo.De("Mariana Teixeira", ".pdf"));
        Assert.Equal("curriculo-Caio.docx", NomeDoCurriculo.De("Caio", ".docx"));
    }

    [Fact]
    public void Barra_e_contrabarra_viram_hifen_e_nao_viram_diretorio()
    {
        Assert.Equal("curriculo-A-B-C.pdf", NomeDoCurriculo.De("A/B\\C", ".pdf"));
        Assert.Equal("curriculo--..-segredo.pdf", NomeDoCurriculo.De("/../segredo", ".pdf"));
    }

    [Fact]
    public void Aspas_e_quebra_de_linha_somem_para_nao_quebrar_o_cabecalho()
    {
        Assert.Equal("curriculo-Ana Maria.pdf", NomeDoCurriculo.De("Ana \"Maria\"", ".pdf"));
        Assert.Equal("curriculo-AnaX-Injetado: 1.pdf", NomeDoCurriculo.De("Ana\r\nX-Injetado: 1", ".pdf"));
    }

    [Fact]
    public void Nome_comprido_e_cortado_antes_da_extensao_e_o_total_nunca_passa_de_100()
    {
        var nome = NomeDoCurriculo.De(new string('n', 300), ".docx");

        Assert.Equal(100, nome.Length);
        Assert.StartsWith("curriculo-nnn", nome, StringComparison.Ordinal);
        Assert.EndsWith("n.docx", nome, StringComparison.Ordinal);
    }

    [Fact]
    public void O_corte_nao_deixa_um_par_substituto_pela_metade_nem_espaco_antes_da_extensao()
    {
        var comEmoji = NomeDoCurriculo.De(new string('n', 85) + "😀resto", ".pdf"); // cabem 86 caracteres do nome: o emoji (duas posições) começaria na 86ª e o corte o deixaria pela metade
        var comEspaco = NomeDoCurriculo.De(new string('n', 85) + " fim", ".pdf");

        Assert.EndsWith(new string('n', 85) + ".pdf", comEmoji, StringComparison.Ordinal);
        Assert.DoesNotContain(" .pdf", comEspaco, StringComparison.Ordinal);
        Assert.True(comEmoji.Length <= 100 && comEspaco.Length <= 100);
    }

    [Fact]
    public void Nome_so_de_pontos_ou_espacos_vira_documento_com_o_prefixo()
    {
        Assert.Equal("curriculo-documento.pdf", NomeDoCurriculo.De("  ..  ", ".pdf"));
    }
}

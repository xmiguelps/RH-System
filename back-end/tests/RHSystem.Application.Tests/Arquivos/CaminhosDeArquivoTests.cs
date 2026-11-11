using System.Text.RegularExpressions;
using RHSystem.Application.Arquivos;

namespace RHSystem.Application.Tests.Arquivos;

public class CaminhosDeArquivoTests
{
    private const string Guid32 = "0123456789abcdef0123456789abcdef";

    [Fact]
    public void CaminhosDeArquivo_Novo_gera_pasta_barra_guid_ponto_extensao_e_EhValido_recusa_traversal_barra_inicial_maiuscula_e_barra_invertida()
    {
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");

        Assert.Matches(new Regex(@"^documentos/[0-9a-f]{32}\.pdf\z"), caminho);
        Assert.NotEqual(caminho, CaminhosDeArquivo.Novo("documentos", ".pdf")); // o nome é um GUID novo a cada vez, nunca vem do usuário
        Assert.True(CaminhosDeArquivo.EhValido(caminho));

        // Pasta e extensão saem em minúsculas; a extensão pode vir com ou sem o ponto.
        Assert.Matches(new Regex(@"^documentos/[0-9a-f]{32}\.jpg\z"), CaminhosDeArquivo.Novo("Documentos", "JPG"));
        Assert.Matches(new Regex(@"^holerites/2026-10/[0-9a-f]{32}\.png\z"), CaminhosDeArquivo.Novo("holerites/2026-10", ".png"));

        // Válidos: um ou mais segmentos [a-z0-9-], 32 hex e a extensão.
        Assert.True(CaminhosDeArquivo.EhValido($"documentos/{Guid32}.pdf"));
        Assert.True(CaminhosDeArquivo.EhValido($"holerites/2026-10/{Guid32}.xlsx"));

        // Recusados: traversal, barra inicial, maiúscula, barra invertida, nome que não é GUID, sem pasta, sem extensão, segmento vazio, quebra de linha no fim.
        foreach (var invalido in new[]
        {
            $"../documentos/{Guid32}.pdf",
            $"documentos/../{Guid32}.pdf",
            $"documentos/..%2f{Guid32}.pdf",
            $"/documentos/{Guid32}.pdf",
            $"Documentos/{Guid32}.pdf",
            $"documentos/{Guid32}.PDF",
            $"documentos/{Guid32.ToUpperInvariant()}.pdf",
            $"documentos\\{Guid32}.pdf",
            $"documentos/{Guid32[..31]}.pdf",
            $"documentos/{Guid32}0.pdf",
            $"documentos/{Guid32}",
            $"documentos/{Guid32}.",
            $"{Guid32}.pdf",
            $"documentos//{Guid32}.pdf",
            $"docu mentos/{Guid32}.pdf",
            $"documentos/{Guid32}.pdf\n",
            $"documentos/{Guid32}.pdf ",
            "documentos/contrato-do-joao.pdf",
            string.Empty,
            "   ",
        })
        {
            Assert.False(CaminhosDeArquivo.EhValido(invalido), $"\"{invalido}\" devia ser recusado");
        }

        Assert.False(CaminhosDeArquivo.EhValido(null!));
    }

    [Theory]
    [InlineData("", ".pdf")]
    [InlineData("../documentos", ".pdf")]
    [InlineData("documentos/..", ".pdf")]
    [InlineData("/documentos", ".pdf")]
    [InlineData("documentos\\x", ".pdf")]
    [InlineData("docu mentos", ".pdf")]
    [InlineData("documentos", "")]
    [InlineData("documentos", ".")]
    [InlineData("documentos", ".p/f")]
    [InlineData("documentos", ".pdf.exe")]
    public void Novo_recusa_pasta_ou_extensao_invalida_com_ArgumentException(string pasta, string extensao)
    {
        Assert.Throws<ArgumentException>(() => CaminhosDeArquivo.Novo(pasta, extensao));
    }
}

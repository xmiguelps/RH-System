using System.Xml.Linq;

namespace RHSystem.Domain.Tests;

/// <summary>
/// Regra de dependência da Clean Architecture, conferida nos próprios .csproj:
/// Domain não depende de nada; Application depende só de Domain; Infrastructure e Api dependem de Application.
/// </summary>
public class ArquiteturaTests
{
    private static string PastaBackEnd()
    {
        var pasta = new DirectoryInfo(AppContext.BaseDirectory);
        while (pasta is not null && !File.Exists(Path.Combine(pasta.FullName, "RHSystem.slnx")))
        {
            pasta = pasta.Parent;
        }

        return pasta?.FullName
            ?? throw new InvalidOperationException($"Pasta back-end (a que tem RHSystem.slnx) não encontrada acima de {AppContext.BaseDirectory}.");
    }

    private static XDocument Csproj(string projeto) =>
        XDocument.Load(Path.Combine(PastaBackEnd(), "src", projeto, projeto + ".csproj"));

    private static string[] ProjetosReferenciados(XDocument arquivo) =>
        arquivo.Descendants("ProjectReference")
            .Select(e => ((string?)e.Attribute("Include") ?? string.Empty).Replace('\\', '/'))
            .Select(caminho => Path.GetFileNameWithoutExtension(caminho))
            .Order()
            .ToArray();

    [Fact]
    public void Domain_csproj_nao_tem_ProjectReference_PackageReference_nem_FrameworkReference()
    {
        var domain = Csproj("RHSystem.Domain");

        Assert.Empty(domain.Descendants("ProjectReference"));
        Assert.Empty(domain.Descendants("PackageReference"));
        Assert.Empty(domain.Descendants("FrameworkReference"));
    }

    [Fact]
    public void Application_referencia_apenas_Domain()
    {
        Assert.Equal(["RHSystem.Domain"], ProjetosReferenciados(Csproj("RHSystem.Application")));
    }

    [Fact]
    public void Infrastructure_referencia_Application_e_nao_referencia_Api()
    {
        var referencias = ProjetosReferenciados(Csproj("RHSystem.Infrastructure"));

        Assert.Contains("RHSystem.Application", referencias);
        Assert.DoesNotContain("RHSystem.Api", referencias);
    }

    [Fact]
    public void Api_referencia_Application_e_Infrastructure()
    {
        var referencias = ProjetosReferenciados(Csproj("RHSystem.Api"));

        Assert.Contains("RHSystem.Application", referencias);
        Assert.Contains("RHSystem.Infrastructure", referencias);
    }

    [Fact]
    public void Assembly_do_Domain_nao_referencia_EntityFrameworkCore_nem_AspNetCore()
    {
        var referenciados = typeof(RHSystem.Domain.Autenticacao.Perfil).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name ?? string.Empty)
            .ToList();

        Assert.DoesNotContain(referenciados, nome => nome.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        Assert.DoesNotContain(referenciados, nome => nome.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
    }

    [Fact]
    public void Directory_Build_props_e_Directory_Packages_props_nao_tem_ProjectReference()
    {
        foreach (var arquivo in new[] { "Directory.Build.props", "Directory.Packages.props" })
        {
            var props = XDocument.Load(Path.Combine(PastaBackEnd(), arquivo));

            Assert.Empty(props.Descendants("ProjectReference"));
        }
    }
}

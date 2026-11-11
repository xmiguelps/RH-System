using System.Text.RegularExpressions;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// Os rótulos da tela de auditoria (e do CSV): todo valor de enum tem o seu, e os de área e ação batem com o texto que o próprio contrato escreve nas
/// descrições de <c>AuditoriaArea</c> e <c>AuditoriaAcao</c> (<c>`ACESSO` "Acesso"</c>), lido do <c>openapi.yaml</c>, e não de uma cópia no teste.
/// </summary>
public partial class RotulosDeAuditoriaTests
{
    [GeneratedRegex("`([A-Z_]+)`\\s+\"([^\"]+)\"")]
    private static partial Regex CodigoERotulo();

    /// <summary>Os pares <c>CODIGO → "Rótulo"</c> da descrição do esquema, com as quebras de linha da descrição trocadas por espaço.</summary>
    private static Dictionary<string, string> ParesDoContrato(string esquema)
    {
        var descricao = CarregadorDeContrato.Contrato()["components"]!["schemas"]![esquema]!["description"]!.GetValue<string>();
        var normalizada = Regex.Replace(descricao, @"\s+", " ");
        var pares = new Dictionary<string, string>();
        foreach (Match m in CodigoERotulo().Matches(normalizada))
        {
            pares[m.Groups[1].Value] = m.Groups[2].Value;
        }

        return pares;
    }

    [Fact]
    public void Todo_valor_de_AuditoriaArea_AuditoriaAcao_Perfil_e_CanalDeOrigem_tem_rotulo_e_os_literais_do_contrato_conferem()
    {
        foreach (var area in Enum.GetValues<AuditoriaArea>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RotulosDeAuditoria.Area(area)), $"sem rótulo de área para {area}");
        }

        foreach (var acao in Enum.GetValues<AuditoriaAcao>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RotulosDeAuditoria.Acao(acao)), $"sem rótulo de ação para {acao}");
        }

        foreach (var perfil in Enum.GetValues<Perfil>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RotulosDeAuditoria.Perfil(perfil)), $"sem rótulo de perfil para {perfil}");
        }

        foreach (var canal in Enum.GetValues<CanalDeOrigem>())
        {
            Assert.False(string.IsNullOrWhiteSpace(RotulosDeAuditoria.Origem(canal)), $"sem rótulo de origem para {canal}");
        }

        // Os literais que o plano e o contrato citam.
        string[] esperado =
            ["Colaborador", "Importação", "Férias", "Pediu ajuste", "Acesso negado", "Enviou e-mail", "Portal no navegador", "Automático", "RH", "Gestor", string.Empty];
        string[] obtido =
            [
                RotulosDeAuditoria.Perfil(Perfil.Funcionario),
                RotulosDeAuditoria.Area(AuditoriaArea.Importacao),
                RotulosDeAuditoria.Area(AuditoriaArea.Ferias),
                RotulosDeAuditoria.Acao(AuditoriaAcao.PediuAjuste),
                RotulosDeAuditoria.Acao(AuditoriaAcao.AcessoNegado),
                RotulosDeAuditoria.Acao(AuditoriaAcao.EnviouEmail),
                RotulosDeAuditoria.Origem(CanalDeOrigem.Portal),
                RotulosDeAuditoria.Origem(CanalDeOrigem.Sistema),
                RotulosDeAuditoria.Perfil(Perfil.RH),
                RotulosDeAuditoria.Perfil(Perfil.Gestor),
                RotulosDeAuditoria.Perfil(null),
            ];
        Assert.Equal(esperado, obtido);
    }

    [Fact]
    public void Os_rotulos_de_area_e_acao_batem_com_o_texto_do_contrato()
    {
        var areas = ParesDoContrato("AuditoriaArea");
        var acoes = ParesDoContrato("AuditoriaAcao");

        // Controle positivo: o leitor achou todos os pares (um regex que não casa daria dicionário vazio e o teste passaria sem provar nada).
        Assert.Equal(Enum.GetValues<AuditoriaArea>().Length, areas.Count);
        Assert.Equal(Enum.GetValues<AuditoriaAcao>().Length, acoes.Count);
        foreach (var area in Enum.GetValues<AuditoriaArea>())
        {
            Assert.Equal(areas[NomesDoContrato.Enum(area)], RotulosDeAuditoria.Area(area));
        }

        foreach (var acao in Enum.GetValues<AuditoriaAcao>())
        {
            Assert.Equal(acoes[NomesDoContrato.Enum(acao)], RotulosDeAuditoria.Acao(acao));
        }
    }
}

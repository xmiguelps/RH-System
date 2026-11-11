using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Comum;

public class EnumsDeConsultaTests
{
    [Fact]
    public void Ler_aceita_o_nome_do_contrato_ACESSO_NEGADO_e_devolve_AcessoNegado()
    {
        Assert.Equal(AuditoriaAcao.AcessoNegado, EnumsDeConsulta.Ler<AuditoriaAcao>("ACESSO_NEGADO", "acao"));
        Assert.Equal(AuditoriaAcao.Entrou, EnumsDeConsulta.Ler<AuditoriaAcao>("ENTROU", "acao"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Ler_texto_nulo_ou_vazio_devolve_null(string? texto)
    {
        Assert.Null(EnumsDeConsulta.Ler<AuditoriaAcao>(texto, "acao"));
    }

    [Theory]
    [InlineData("AcessoNegado")]
    [InlineData("acesso_negado")]
    [InlineData("XYZ")]
    public void Ler_o_nome_do_CSharp_ou_texto_desconhecido_lanca_ErroDeValidacao_com_o_campo_e_os_valores_aceitos(string texto)
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => EnumsDeConsulta.Ler<AuditoriaAcao>(texto, "acao"));

        Assert.Equal(["acao"], erro.Erros.Keys);
        var mensagem = Assert.Single(erro.Erros["acao"]);
        Assert.Contains("ACESSO_NEGADO", mensagem);
        Assert.Contains("ENTROU", mensagem);
    }

    [Theory]
    [InlineData("Funcionario", Perfil.Funcionario)]
    [InlineData("Gestor", Perfil.Gestor)]
    [InlineData("RH", Perfil.RH)]
    public void Ler_Perfil_aceita_Funcionario_Gestor_RH(string texto, Perfil esperado)
    {
        Assert.Equal(esperado, EnumsDeConsulta.Ler<Perfil>(texto, "perfil"));
    }

    [Theory]
    [InlineData("FUNCIONARIO")]
    [InlineData("Rh")]
    [InlineData("rh")]
    public void Ler_Perfil_nao_aceita_outra_grafia(string texto)
    {
        // O contrato fixa a exceção: perfil é Funcionario, Gestor ou RH, e não MAIÚSCULAS_COM_SUBLINHADO.
        Assert.Throws<ErroDeValidacaoException>(() => EnumsDeConsulta.Ler<Perfil>(texto, "perfil"));
    }

    [Fact]
    public void Todo_valor_dos_enums_da_auditoria_faz_a_viagem_de_ida_e_volta_pelo_nome_do_contrato()
    {
        foreach (var valor in Enum.GetValues<AuditoriaAcao>())
        {
            Assert.Equal(valor, EnumsDeConsulta.Ler<AuditoriaAcao>(NomesDoContrato.Enum(valor), "acao"));
        }

        foreach (var valor in Enum.GetValues<AuditoriaArea>())
        {
            Assert.Equal(valor, EnumsDeConsulta.Ler<AuditoriaArea>(NomesDoContrato.Enum(valor), "area"));
        }
    }
}

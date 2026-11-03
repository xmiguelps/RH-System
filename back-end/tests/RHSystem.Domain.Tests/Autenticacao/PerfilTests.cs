using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class PerfilTests
{
    [Theory]
    [InlineData("RH", Perfil.RH)]
    [InlineData("Funcionario", Perfil.Funcionario)]
    [InlineData("Funcionario Gestor", Perfil.Gestor)]
    public void PerfilPorNome_mapeia_RH_Funcionario_e_Funcionario_Gestor(string nome, Perfil esperado)
    {
        Assert.Equal(esperado, Perfis.PerfilPorNome(nome));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Gestor")]
    [InlineData("Administrador")]
    public void PerfilPorNome_devolve_null_para_nulo_ou_outro_nome(string? nome)
    {
        Assert.Null(Perfis.PerfilPorNome(nome));
    }
}

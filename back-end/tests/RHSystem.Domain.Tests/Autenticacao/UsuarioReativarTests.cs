using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class UsuarioReativarTests
{
    [Fact]
    public void Usuario_Reativar_volta_a_ativar_quem_foi_inativado()
    {
        var funcionario = Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));
        var usuario = Usuario.Criar(funcionario, new string('x', 60));

        usuario.Inativar();
        Assert.False(usuario.Ativo);

        usuario.Reativar();
        Assert.True(usuario.Ativo);
    }
}

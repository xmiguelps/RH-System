using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class UsuarioSenhaProvisoriaTests
{
    private static Funcionario NovoFuncionario() =>
        Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));

    [Fact]
    public void Usuario_Criar_com_senhaProvisoria_true_e_DefinirSenha_zera_a_marca()
    {
        var usuario = Usuario.Criar(NovoFuncionario(), "hash-provisorio", senhaProvisoria: true);
        Assert.True(usuario.SenhaProvisoria);
        Assert.Equal("hash-provisorio", usuario.SenhaHash);

        usuario.DefinirSenha("hash-escolhido-pela-pessoa");

        Assert.False(usuario.SenhaProvisoria);
        Assert.Equal("hash-escolhido-pela-pessoa", usuario.SenhaHash);

        // Sem o argumento, a senha não é provisória (a fatia 0 e os convites já aceitos).
        Assert.False(Usuario.Criar(NovoFuncionario(), "hash").SenhaProvisoria);
    }
}

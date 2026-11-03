using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class FuncionarioMatriculaTests
{
    private static Funcionario Novo() =>
        Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));

    [Fact]
    public void Funcionario_AtribuirMatricula_aceita_5_digitos_e_recusa_outro_formato()
    {
        var funcionario = Novo();
        Assert.Null(funcionario.Matricula);

        funcionario.AtribuirMatricula("00482");

        Assert.Equal("00482", funcionario.Matricula);

        // O banco confere ^[0-9]{5}$ (só dígitos ASCII, sem espaço nem quebra de linha): a entidade recusa antes.
        foreach (var invalida in new[] { "482", "ABCDE", "004821", string.Empty, " 0482", "0048 ", "0048\n", "٠٠٤٨٢", null! })
        {
            Assert.Throws<ArgumentException>(() => Novo().AtribuirMatricula(invalida));
        }
    }
}

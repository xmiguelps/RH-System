using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class FuncionarioTests
{
    [Fact]
    public void Funcionario_Criar_normaliza_o_email()
    {
        var funcionario = Funcionario.Criar("Ana Souza", "  Ana@Empresa.com ", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));

        Assert.Equal("ana@empresa.com", funcionario.EmailCorporativo);
        Assert.Equal("Ana Souza", funcionario.Nome);
        Assert.Equal(new DateOnly(2026, 1, 2), funcionario.DataAdmissao);
        Assert.True(funcionario.Ativo);
        Assert.Equal(Perfil.RH, funcionario.Perfil);
    }

    [Fact]
    public void Funcionario_Inativar_marca_como_inativo()
    {
        var funcionario = Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));

        funcionario.Inativar();

        Assert.False(funcionario.Ativo);
    }

    [Fact]
    public void Funcionario_sem_nivel_de_acesso_nao_tem_perfil()
    {
        // O banco aceita ID_NIVEL_ACESSO nulo; um funcionário assim não entra (o login recusa).
        var funcionario = Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("Nivel Desconhecido"));

        Assert.Null(funcionario.Perfil);
    }
}

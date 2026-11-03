using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class UsuarioTests
{
    private static Funcionario NovoFuncionario() =>
        Funcionario.Criar("Ana Souza", "ana@empresa.com", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));

    [Fact]
    public void Usuario_PodeEntrar_e_false_se_o_usuario_ou_o_funcionario_esta_inativo()
    {
        var usuarioInativo = Usuario.Criar(NovoFuncionario(), "hash");
        Assert.True(usuarioInativo.PodeEntrar);
        usuarioInativo.Inativar();
        Assert.False(usuarioInativo.PodeEntrar);

        var funcionario = NovoFuncionario();
        var usuario = Usuario.Criar(funcionario, "hash");
        funcionario.Inativar();
        Assert.False(usuario.PodeEntrar);
    }

    [Theory]
    [InlineData(700)]
    [InlineData(999)]
    public void Usuario_RegistrarLogin_trunca_o_instante_ao_segundo(int milissegundos)
    {
        // USUARIOS.DATA_HORA_ULTIMO_LOGIN é DATETIME sem fração: nunca arredonda 12:42:00.999 para 12:42:01.
        var usuario = Usuario.Criar(NovoFuncionario(), "hash");

        usuario.RegistrarLogin(new DateTime(2026, 10, 7, 12, 42, 0, milissegundos, DateTimeKind.Utc));

        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), usuario.UltimoLoginEm);
        Assert.Equal(DateTimeKind.Utc, usuario.UltimoLoginEm!.Value.Kind);
    }
}

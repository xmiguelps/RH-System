using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Autenticacao;

public class ObterUsuarioLogadoCasoDeUsoTests
{
    [Fact]
    public async Task Devolve_o_usuario_com_perfil_cargo_e_setor()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario(nivel: "Funcionario Gestor", cargo: "Analista de RH", setor: "Recursos Humanos");

        var resposta = await new ObterUsuarioLogadoCasoDeUso(cenario.Usuarios).ExecutarAsync(usuario.Id, CancellationToken.None);

        Assert.Equal(new UsuarioResposta(usuario.Id, usuario.FuncionarioId, "Ana Souza", "ana@empresa.com.br", Perfil.Gestor, "Analista de RH", "Recursos Humanos"), resposta);
    }

    [Fact]
    public async Task Devolve_cargo_e_setor_nulos_quando_o_funcionario_nao_tem()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();

        var resposta = await new ObterUsuarioLogadoCasoDeUso(cenario.Usuarios).ExecutarAsync(usuario.Id, CancellationToken.None);

        Assert.Null(resposta!.Cargo);
        Assert.Null(resposta.Setor);
    }

    [Theory]
    [InlineData("inexistente")]
    [InlineData("usuario inativo")]
    [InlineData("funcionario inativo")]
    [InlineData("sem perfil")]
    public async Task Devolve_null_para_usuario_inexistente_inativo_ou_sem_perfil(string caso)
    {
        var cenario = new Cenario();
        var usuario = caso switch
        {
            "usuario inativo" => cenario.NovoUsuario(usuarioAtivo: false),
            "funcionario inativo" => cenario.NovoUsuario(funcionarioAtivo: false),
            "sem perfil" => cenario.NovoUsuario(nivel: null),
            _ => null,
        };

        var resposta = await new ObterUsuarioLogadoCasoDeUso(cenario.Usuarios).ExecutarAsync(usuario?.Id ?? 999, CancellationToken.None);

        Assert.Null(resposta);
    }
}

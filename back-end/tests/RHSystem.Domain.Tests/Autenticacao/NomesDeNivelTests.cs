using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Autenticacao;

public class NomesDeNivelTests
{
    [Theory]
    [InlineData(Perfil.RH, "RH")]
    [InlineData(Perfil.Funcionario, "Funcionario")]
    [InlineData(Perfil.Gestor, "Funcionario Gestor")]
    public void NomesDeNivel_DoPerfil_e_PerfilPorNome_sao_inversos_para_os_tres_perfis(Perfil perfil, string nomeEsperado)
    {
        Assert.Equal(nomeEsperado, NomesDeNivel.DoPerfil(perfil));
        Assert.Equal(perfil, Perfis.PerfilPorNome(NomesDeNivel.DoPerfil(perfil)));
    }

    [Fact]
    public void NomesDeNivel_DoPerfil_de_valor_fora_do_enum_lanca()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => NomesDeNivel.DoPerfil((Perfil)99));
    }

    [Theory]
    [InlineData("RH", "RH")]
    [InlineData("Funcionario", "Colaborador")]
    [InlineData("Funcionario Gestor", "Gestor")]
    public void NivelDeAcesso_diz_o_nome_para_a_auditoria_como_a_tela_mostra(string nomeNoBanco, string nomeNaTela)
    {
        var nivel = new NivelDeAcesso(nomeNoBanco);

        Assert.Equal(nomeNaTela, nivel.NomeParaAuditoria);
        Assert.Equal($"Nível de acesso {nomeNaTela}", nivel.RegistroAfetado);
    }
}

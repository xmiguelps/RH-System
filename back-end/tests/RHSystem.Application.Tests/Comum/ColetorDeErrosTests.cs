using RHSystem.Application.Comum;

namespace RHSystem.Application.Tests.Comum;

public class ColetorDeErrosTests
{
    [Fact]
    public void ColetorDeErros_acumula_campos_e_LancarSeHouver_lanca_ErroDeValidacao_com_todos()
    {
        var coletor = new ColetorDeErros();
        coletor.Adicionar("nome", "O nome é obrigatório.");
        coletor.Adicionar("dataNascimento", "A data de nascimento não pode ser no futuro.");
        coletor.Adicionar("nome", "O nome é longo demais.");

        Assert.True(coletor.TemErros);
        var erro = Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver);

        Assert.Equal(["dataNascimento", "nome"], erro.Erros.Keys.Order());
        Assert.Equal(["O nome é obrigatório.", "O nome é longo demais."], erro.Erros["nome"]);
        Assert.Equal(["A data de nascimento não pode ser no futuro."], erro.Erros["dataNascimento"]);
    }

    [Fact]
    public void ColetorDeErros_sem_erros_nao_lanca()
    {
        var coletor = new ColetorDeErros();

        Assert.False(coletor.TemErros);
        coletor.LancarSeHouver();
    }

    [Fact]
    public void ErroDeValidacaoException_com_campo_e_mensagem_vira_um_mapa_de_um_campo()
    {
        var erro = new ErroDeValidacaoException("email", "O e-mail é obrigatório.");

        Assert.Equal(["email"], erro.Erros.Keys);
        Assert.Equal(["O e-mail é obrigatório."], erro.Erros["email"]);
    }

    [Fact]
    public void OuNaoEncontrado_devolve_o_objeto_e_lanca_NaoEncontrado_para_nulo()
    {
        var existente = "achei";
        string? ausente = null;

        Assert.Same(existente, existente.OuNaoEncontrado());
        Assert.Throws<NaoEncontradoException>(() => ausente.OuNaoEncontrado());
    }
}

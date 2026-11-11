using RHSystem.Application.Importacoes;

namespace RHSystem.Application.Tests.Importacoes;

public class MotivoDaRecusaTests
{
    [Theory]
    [InlineData("Já existe um colaborador com este CPF.", "já existe um colaborador com este CPF")]
    [InlineData("Este e-mail aparece também na linha 2.", "este e-mail aparece também na linha 2")]
    [InlineData("O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar.", "o gestor do setor escolhido está inativo")]
    [InlineData("O cargo \"Analista\" não existe. Use um cargo da aba Valores válidos.", "o cargo \"Analista\" não existe")]
    [InlineData("A data \"31/02/2026\" não é válida. Use dd/mm/aaaa.", "a data \"31/02/2026\" não é válida")]
    [InlineData("Preencha esta coluna.", "preencha esta coluna")]
    [InlineData("Sem ponto final", "sem ponto final")]
    public void Fica_so_a_primeira_frase_sem_o_ponto_e_com_a_inicial_em_minuscula(string mensagem, string esperado) =>
        Assert.Equal(esperado, ConfirmarImportacaoCasoDeUso.Motivo(mensagem));

    [Theory]
    [InlineData("CPF inválido.", "CPF inválido")]
    [InlineData("RH não pode confirmar.", "RH não pode confirmar")]
    public void Sigla_no_comeco_continua_em_maiuscula(string mensagem, string esperado) =>
        Assert.Equal(esperado, ConfirmarImportacaoCasoDeUso.Motivo(mensagem));

    [Fact]
    public void O_ponto_dentro_das_aspas_nao_termina_a_frase()
    {
        Assert.Equal(
            "o cargo \"Sr. Analista. Pleno\" não existe",
            ConfirmarImportacaoCasoDeUso.Motivo("O cargo \"Sr. Analista. Pleno\" não existe. Use um cargo da aba Valores válidos."));
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("", "")]
    [InlineData("a.", "a")]
    public void Texto_curto_nao_quebra(string mensagem, string esperado) =>
        Assert.Equal(esperado, ConfirmarImportacaoCasoDeUso.Motivo(mensagem));
}

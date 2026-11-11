using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

/// <summary>A parte pura da validação da edição (sem banco): os campos que só a edição tem (e-mail pessoal, RG, órgão emissor e endereço).</summary>
public class ValidacaoDaEdicaoTests
{
    private static readonly FuncionarioEndereco EnderecoVazio = new(null, null, null, null, null, null, null);

    private static DadosPessoaisDaEdicao Dados(string? emailPessoal = null, string? rg = null, string? rgOrgaoEmissor = null, FuncionarioEndereco? endereco = null) =>
        new(emailPessoal, rg, rgOrgaoEmissor, endereco ?? EnderecoVazio);

    private static IEnumerable<string> Campos(DadosPessoaisDaEdicao dados) => ValidacaoDeCamposDaEdicao.Validar(3, dados).Select(p => p.Campo);

    [Fact]
    public void Tudo_vazio_passa_e_os_problemas_levam_a_linha_o_tipo_Campo_e_o_codigo_CAMPO_INVALIDO()
    {
        Assert.Empty(ValidacaoDeCamposDaEdicao.Validar(0, Dados()));

        var problemas = ValidacaoDeCamposDaEdicao.Validar(3, Dados(emailPessoal: "sem-arroba"));

        var problema = Assert.Single(problemas);
        Assert.Equal((3, TipoDeProblemaDeCadastro.Campo, "emailPessoal", ValidacaoDeCamposDoCadastro.CodigoDeCampoInvalido), (problema.Indice, problema.Tipo, problema.Campo, problema.Codigo));
    }

    [Fact]
    public void Coleta_todos_os_problemas_de_uma_vez_com_o_nome_do_campo_do_contrato()
    {
        var dados = Dados("sem-arroba", new string('9', 21), new string('S', 21), new FuncionarioEndereco("123", null, null, null, null, null, "XX"));

        Assert.Equal(["emailPessoal", "rg", "rgOrgaoEmissor", "endereco.cep", "endereco.uf"], Campos(dados));
    }

    [Theory]
    [InlineData(20, true)]
    [InlineData(21, false)]
    public void Rg_e_orgao_emissor_aceitam_ate_20_caracteres(int tamanho, bool passa)
    {
        var texto = new string('9', tamanho);

        Assert.Equal(passa, !Campos(Dados(rg: texto)).Contains("rg"));
        Assert.Equal(passa, !Campos(Dados(rgOrgaoEmissor: texto)).Contains("rgOrgaoEmissor"));
    }

    [Theory]
    [InlineData(138, true)] // 138 + "@exemplo.com" (12) = 150
    [InlineData(139, false)]
    public void Email_pessoal_aceita_ate_150_caracteres(int tamanhoDoUsuario, bool passa) =>
        Assert.Equal(passa, !Campos(Dados(emailPessoal: new string('a', tamanhoDoUsuario) + "@exemplo.com")).Contains("emailPessoal"));

    [Theory]
    [InlineData("01310100", true)]
    [InlineData("0131010", false)]
    [InlineData("013101000", false)]
    [InlineData("0131010a", false)]
    [InlineData("01310-100", false)]
    [InlineData("", false)]
    public void Cep_tem_8_digitos(string cep, bool passa) =>
        Assert.Equal(passa, !Campos(Dados(endereco: EnderecoVazio with { Cep = cep })).Contains("endereco.cep"));

    [Theory]
    [InlineData("SP", true)]
    [InlineData("AC", true)]
    [InlineData("TO", true)]
    [InlineData("DF", true)]
    [InlineData("sp", false)]
    [InlineData("XX", false)]
    [InlineData("S", false)]
    [InlineData("", false)]
    public void Uf_e_uma_das_27_siglas(string uf, bool passa) =>
        Assert.Equal(passa, !Campos(Dados(endereco: EnderecoVazio with { Uf = uf })).Contains("endereco.uf"));

    [Fact]
    public void As_27_siglas_de_UF_do_Brasil_sao_aceitas()
    {
        string[] siglas = ["AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO"];

        Assert.Equal(27, siglas.Length);
        Assert.All(siglas, sigla => Assert.DoesNotContain("endereco.uf", Campos(Dados(endereco: EnderecoVazio with { Uf = sigla }))));
    }

    [Theory]
    [InlineData("logradouro", 150)]
    [InlineData("numero", 10)]
    [InlineData("complemento", 100)]
    [InlineData("bairro", 100)]
    [InlineData("cidade", 100)]
    public void Campos_do_endereco_aceitam_o_tamanho_do_contrato_e_recusam_um_a_mais(string campo, int maximo)
    {
        FuncionarioEndereco Com(int tamanho)
        {
            var texto = new string('a', tamanho);
            return new FuncionarioEndereco(
                null,
                campo == "logradouro" ? texto : null,
                campo == "numero" ? texto : null,
                campo == "complemento" ? texto : null,
                campo == "bairro" ? texto : null,
                campo == "cidade" ? texto : null,
                null);
        }

        Assert.DoesNotContain($"endereco.{campo}", Campos(Dados(endereco: Com(maximo)))); // o contrato (e a coluna do banco) aceita até aqui
        Assert.Contains($"endereco.{campo}", Campos(Dados(endereco: Com(maximo + 1)))); // um a mais seria um 500 do banco (coluna menor), e o contrato manda 400
    }

}

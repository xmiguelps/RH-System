using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Tests.Colaboradores;

/// <summary>A parte pura da validação do cadastro (sem banco) e a tradução dos problemas nas exceções do contrato.</summary>
public class ValidacaoDoCadastroTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private static NovoFuncionario Valido() =>
        new("Bruno Lima", "52998224725", new DateOnly(1990, 5, 17), "11987654410", "bruno.lima@empresa.com.br", new DateOnly(2026, 2, 3), TipoDeContrato.Clt, 1, 1, Perfil.Funcionario, [1, 2]);

    [Fact]
    public void Validacao_coleta_todos_os_400_de_uma_vez_com_o_nome_do_campo_em_camelCase()
    {
        var invalido = new NovoFuncionario(" ", "123", new DateOnly(2030, 1, 1), "123", "sem-arroba", DateOnly.MinValue, default, 1, 1, (Perfil)99, [1, 1]);

        var problemas = ValidacaoDeCamposDoCadastro.Validar(4, invalido, Hoje);

        Assert.Equal(["nome", "cpf", "dataNascimento", "telefone", "emailCorporativo", "dataAdmissao", "tipoContrato", "perfil", "beneficioIds"], problemas.Select(p => p.Campo));
        Assert.All(problemas, p => Assert.Equal((4, TipoDeProblemaDeCadastro.Campo), (p.Indice, p.Tipo)));
        Assert.Equal("O nome é obrigatório.", problemas[0].Mensagem);
        Assert.Equal("CPF inválido.", problemas[1].Mensagem);
        Assert.Equal("A data de nascimento não pode ser no futuro.", problemas[2].Mensagem);
        Assert.Equal("Informe o telefone com DDD.", problemas[3].Mensagem);
        Assert.Equal("Informe o tipo de contrato.", problemas.Single(p => p.Campo == "tipoContrato").Mensagem); // omitido no JSON chega como o valor zero do enum
        Assert.Empty(ValidacaoDeCamposDoCadastro.Validar(0, Valido(), Hoje));
    }

    [Theory]
    [InlineData(150, true)]
    [InlineData(151, false)]
    public void Nome_de_150_caracteres_passa_e_de_151_falha(int tamanho, bool passa)
    {
        var problemas = ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { Nome = new string('a', tamanho) }, Hoje);

        Assert.Equal(passa, problemas.All(p => p.Campo != "nome"));
    }

    [Theory]
    [InlineData("1133447783", true)]
    [InlineData("11987654410", true)]
    [InlineData("113344778", false)]
    [InlineData("119876544101", false)]
    [InlineData("1198765441a", false)]
    [InlineData("", false)]
    public void Telefone_com_10_ou_11_digitos_passa(string telefone, bool passa) =>
        Assert.Equal(passa, ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { Telefone = telefone }, Hoje).All(p => p.Campo != "telefone"));

    [Fact]
    public void Nascimento_hoje_passa_e_amanha_falha_e_cpf_so_conta_se_tiver_os_digitos_verificadores()
    {
        Assert.DoesNotContain(ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { DataNascimento = Hoje }, Hoje), p => p.Campo == "dataNascimento");
        Assert.Contains(ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { DataNascimento = Hoje.AddDays(1) }, Hoje), p => p.Campo == "dataNascimento");
        Assert.Contains(ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { Cpf = "52998224726" }, Hoje), p => p.Campo == "cpf");
        Assert.Contains(ValidacaoDeCamposDoCadastro.Validar(0, Valido() with { Cpf = "529.982.247-25" }, Hoje), p => p.Campo == "cpf"); // quem chama normaliza antes
    }

    [Fact]
    public void Problemas_de_campo_viram_um_400_com_errors_por_campo_e_vencem_duplicidade_e_regra()
    {
        var problemas = new List<ProblemaDeCadastro>
        {
            new(0, TipoDeProblemaDeCadastro.Campo, "cpf", "CAMPO_INVALIDO", "CPF inválido."),
            new(0, TipoDeProblemaDeCadastro.Campo, "nome", "CAMPO_INVALIDO", "O nome é obrigatório."),
            new(0, TipoDeProblemaDeCadastro.Campo, "nome", "CAMPO_INVALIDO", "O nome é longo demais."),
            new(0, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Já existe um colaborador com este e-mail corporativo."),
            new(0, TipoDeProblemaDeCadastro.Regra, "setorId", "GESTOR_DO_SETOR_INATIVO", "O gestor do setor escolhido está inativo."),
        };

        var erro = Assert.IsType<ErroDeValidacaoException>(ProblemasDoCadastro.ParaExcecao(problemas));

        Assert.Equal(["cpf", "nome"], erro.Erros.Keys.Order());
        Assert.Equal(["O nome é obrigatório.", "O nome é longo demais."], erro.Erros["nome"]);
    }

    [Fact]
    public void Duplicidade_vira_409_com_cadastroExistente_e_o_CPF_vem_antes_do_email_e_regra_vira_422()
    {
        var existente = new CadastroExistente(12, "Bruno Lima", false);
        var doCpf = new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Este CPF pertence a um cadastro inativo (Bruno Lima).", existente);
        var doEmail = new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Já existe um colaborador com este e-mail corporativo.", existente with { Ativo = true });

        var conflito = Assert.IsType<ProblemaDeApiException>(ProblemasDoCadastro.ParaExcecao([doCpf, doEmail]));
        var regra = Assert.IsType<ErroDeNegocioException>(ProblemasDoCadastro.ParaExcecao([new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Regra, "setorId", "GESTOR_DO_SETOR_INATIVO", "O gestor do setor escolhido está inativo.")]));

        Assert.Equal((409, "Conflito.", "CPF_JA_CADASTRADO"), (conflito.Status, conflito.Titulo, conflito.Codigo));
        Assert.Equal(existente, conflito.Extensoes!["cadastroExistente"]);
        Assert.Equal(("GESTOR_DO_SETOR_INATIVO", "O gestor do setor escolhido está inativo."), (regra.Codigo, regra.Detalhe));
    }

    [Fact]
    public async Task Recusa_do_banco_por_duplicidade_vira_409_conferido_de_novo_e_por_regra_vira_422_com_regraDoBanco()
    {
        var existente = new CadastroExistente(7, "Quem Ganhou", true);
        var duplicidade = new LinhaRecusadaException(0, porDuplicidade: true, regraDoBanco: null);

        var conflito = Assert.IsType<ProblemaDeApiException>(await ProblemasDoCadastro.DaRecusaAsync(
            duplicidade,
            () => Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>([new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Já existe um colaborador com este CPF.", existente)])));
        var semAchar = await ProblemasDoCadastro.DaRecusaAsync(duplicidade, () => Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>([]));
        var gestorInativo = Assert.IsType<ProblemaDeApiException>(await ProblemasDoCadastro.DaRecusaAsync(
            new LinhaRecusadaException(0, false, ProblemasDoCadastro.TextoDaRegraDoGestorInativo),
            () => Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>([])));
        var outraRegra = Assert.IsType<ProblemaDeApiException>(await ProblemasDoCadastro.DaRecusaAsync(new LinhaRecusadaException(0, false, "CPF invalido."), () => Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>([])));

        Assert.Equal((409, "CPF_JA_CADASTRADO"), (conflito.Status, conflito.Codigo));
        Assert.IsType<ConflitoException>(semAchar); // ninguém achado: 409 genérico
        Assert.Equal((422, "GESTOR_DO_SETOR_INATIVO"), (gestorInativo.Status, gestorInativo.Codigo));
        Assert.Equal((422, (string?)null, (object?)"CPF invalido."), (outraRegra.Status, outraRegra.Codigo, outraRegra.Extensoes!["regraDoBanco"]));
    }
}

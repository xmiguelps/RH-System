using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Application.Comum;

namespace RHSystem.Application.Tests.Beneficios;

/// <summary>Os tamanhos e as datas do corpo dos pedidos de benefício e o motivo da recusa.</summary>
public class ValidacaoDoPedidoDeBeneficioTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private static readonly DadosDoDependente Helena = new("Helena Lima", "FILHA", new DateOnly(2019, 5, 21));

    private static readonly DadosDoTrajeto Trajeto = new("Rua das Acácias, 120", "Av. Paulista, 1000", "Metrô Linha 1");

    private static string Texto(int tamanho) => new('a', tamanho);

    private static IReadOnlyDictionary<string, string[]> Erros(Action acao) => Assert.Throws<ErroDeValidacaoException>(acao).Erros;

    [Fact]
    public void Um_pedido_bom_passa_e_hoje_e_um_inicio_valido()
    {
        ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, [Helena], Trajeto, Hoje);
        ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, null, null, Hoje);
        ValidacaoDoPedidoDeBeneficio.Dependente(Helena, Hoje);
        ValidacaoDoPedidoDeBeneficio.Trajeto(Trajeto);
    }

    [Fact]
    public void O_inicio_desejado_nao_pode_ser_anterior_a_hoje()
    {
        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(Hoje.AddDays(-1), [], null, Hoje));

        Assert.Equal(["A data de início não pode ser anterior a hoje."], erros["dataInicioDesejada"]);
    }

    [Fact]
    public void O_inicio_desejado_vai_ate_o_ano_2100()
    {
        ValidacaoDoPedidoDeBeneficio.Adesao(new DateOnly(2100, 12, 31), [], null, Hoje);
        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(new DateOnly(2101, 1, 1), [], null, Hoje));

        Assert.Equal(["Informe uma data de início até 2100."], erros["dataInicioDesejada"]);
    }

    [Fact]
    public void Ate_dez_dependentes_passam_e_onze_nao()
    {
        ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, Enumerable.Repeat(Helena, 10).ToList(), null, Hoje);

        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, Enumerable.Repeat(Helena, 11).ToList(), null, Hoje));

        Assert.Equal(["Informe no máximo 10 dependentes."], erros["dependentes"]);
    }

    [Fact]
    public void Os_erros_de_um_dependente_da_lista_levam_o_indice_e_os_de_um_dependente_so_levam_o_campo()
    {
        var ruim = new DadosDoDependente(" ", null, Hoje.AddDays(1));

        var daLista = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, [Helena, ruim], null, Hoje));
        var avulso = Erros(() => ValidacaoDoPedidoDeBeneficio.Dependente(ruim, Hoje));

        Assert.Equal(["dependentes[1].dataNascimento", "dependentes[1].nome", "dependentes[1].parentesco"], daLista.Keys.Order());
        Assert.Equal(["dataNascimento", "nome", "parentesco"], avulso.Keys.Order());
        Assert.Equal(["Informe o nome do dependente."], avulso["nome"]);
        Assert.Equal(["Informe o parentesco do dependente."], avulso["parentesco"]);
        Assert.Equal(["A data de nascimento não pode ser futura."], avulso["dataNascimento"]);
    }

    [Fact]
    public void Nome_ate_150_e_parentesco_ate_30_depois_do_trim()
    {
        ValidacaoDoPedidoDeBeneficio.Dependente(new DadosDoDependente(" " + Texto(150) + " ", Texto(30), Hoje), Hoje);

        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Dependente(new DadosDoDependente(Texto(151), Texto(31), Hoje), Hoje));

        Assert.Equal(["O nome pode ter até 150 caracteres."], erros["nome"]);
        Assert.Equal(["O parentesco pode ter até 30 caracteres."], erros["parentesco"]);
    }

    [Fact]
    public void Nascer_hoje_vale_e_antes_de_1900_nao()
    {
        ValidacaoDoPedidoDeBeneficio.Dependente(Helena with { DataNascimento = Hoje }, Hoje);
        ValidacaoDoPedidoDeBeneficio.Dependente(Helena with { DataNascimento = new DateOnly(1900, 1, 1) }, Hoje);

        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Dependente(Helena with { DataNascimento = new DateOnly(1899, 12, 31) }, Hoje));

        Assert.Equal(["Informe uma data de nascimento a partir de 1900."], erros["dataNascimento"]);
    }

    [Fact]
    public void O_trajeto_pede_os_tres_campos_com_os_tamanhos_do_contrato()
    {
        ValidacaoDoPedidoDeBeneficio.Trajeto(new DadosDoTrajeto(Texto(200), Texto(200), Texto(255)));

        var vazios = Erros(() => ValidacaoDoPedidoDeBeneficio.Trajeto(new DadosDoTrajeto("  ", null, "")));
        var longos = Erros(() => ValidacaoDoPedidoDeBeneficio.Trajeto(new DadosDoTrajeto(Texto(201), Texto(201), Texto(256))));

        Assert.Equal(["Informe a origem do trajeto."], vazios["origem"]);
        Assert.Equal(["Informe o destino do trajeto."], vazios["destino"]);
        Assert.Equal(["Informe as linhas ou os meios de transporte."], vazios["linhasMeios"]);
        Assert.Equal(["A origem pode ter até 200 caracteres."], longos["origem"]);
        Assert.Equal(["O destino pode ter até 200 caracteres."], longos["destino"]);
        Assert.Equal(["As linhas ou os meios podem ter até 255 caracteres."], longos["linhasMeios"]);
    }

    [Fact]
    public void O_trajeto_da_adesao_leva_o_prefixo_do_campo()
    {
        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(Hoje, [], new DadosDoTrajeto(null, "x", "y"), Hoje));

        Assert.Equal(["trajeto.origem"], erros.Keys);
    }

    [Fact]
    public void Todos_os_erros_de_uma_adesao_vem_juntos()
    {
        var erros = Erros(() => ValidacaoDoPedidoDeBeneficio.Adesao(Hoje.AddDays(-2), [new DadosDoDependente(null, "x", Hoje)], new DadosDoTrajeto("a", null, "c"), Hoje));

        Assert.Equal(["dataInicioDesejada", "dependentes[0].nome", "trajeto.destino"], erros.Keys.Order());
    }

    [Fact]
    public void O_motivo_sai_aparado_de_1_a_500_caracteres()
    {
        Assert.Equal("Falta a certidão", ValidacaoDoPedidoDeBeneficio.Motivo("  Falta a certidão \n"));
        Assert.Equal(500, ValidacaoDoPedidoDeBeneficio.Motivo(" " + Texto(500) + " ").Length);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \t ")]
    public void Motivo_vazio_ou_so_de_espacos_e_recusado(string? motivo)
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => ValidacaoDoPedidoDeBeneficio.Motivo(motivo));

        Assert.Equal(["Informe o motivo da recusa."], erro.Erros["motivo"]);
    }

    [Fact]
    public void Motivo_com_mais_de_500_caracteres_e_recusado()
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => ValidacaoDoPedidoDeBeneficio.Motivo(Texto(501)));

        Assert.Equal(["O motivo pode ter até 500 caracteres."], erro.Erros["motivo"]);
    }
}

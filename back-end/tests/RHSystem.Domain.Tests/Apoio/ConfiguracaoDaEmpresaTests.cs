using RHSystem.Domain.Empresa;

namespace RHSystem.Domain.Tests.Apoio;

public class ConfiguracaoDaEmpresaTests
{
    private static readonly TimeOnly Entrada = new(8, 0);
    private static readonly TimeOnly Saida = new(17, 30);

    private static ConfiguracaoDaEmpresa Valida() => ConfiguracaoDaEmpresa.Criar("Empresa Exemplo", "SP", "São Paulo", Entrada, Saida, 10, 5);

    [Fact]
    public void ConfiguracaoDaEmpresa_valida_UF_da_lista_de_27_e_recusa_sp_ZZ_tolerancia_121_e_dia_0_e_32_e_aceita_saida_menor_que_entrada()
    {
        var siglas = "AC AL AP AM BA CE DF ES GO MA MT MS MG PA PB PR PE PI RJ RN RS RO RR SC SP SE TO".Split(' ');
        Assert.Equal(27, siglas.Length);
        Assert.Equal(siglas.Order(), ConfiguracaoDaEmpresa.UfsValidas.Order());
        foreach (var sigla in siglas)
        {
            Assert.Equal(sigla, ConfiguracaoDaEmpresa.Criar("Empresa", sigla, "Cidade", Entrada, Saida, 0, 1).Uf);
        }

        foreach (var uf in new[] { "sp", "ZZ", "S", "SPP", string.Empty, " SP" })
        {
            Assert.ThrowsAny<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", uf, "Cidade", Entrada, Saida, 10, 5));
        }

        Assert.Equal(0, ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 0, 5).ToleranciaAtrasoMinutos);
        Assert.Equal(120, ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 120, 5).ToleranciaAtrasoMinutos);
        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 121, 5));
        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, -1, 5));
        Assert.Equal((1, 31), (ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 10, 1).DiaPagamento, ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 10, 31).DiaPagamento));
        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 10, 0));
        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", Entrada, Saida, 10, 32));

        // Turno que atravessa a meia-noite: a saída é "menor" que a entrada e vale.
        var noturna = ConfiguracaoDaEmpresa.Criar("Empresa", "SP", "Cidade", new TimeOnly(22, 0), new TimeOnly(6, 0), 10, 5);
        Assert.Equal((new TimeOnly(22, 0), new TimeOnly(6, 0)), (noturna.HorarioEntrada, noturna.HorarioSaida));
    }

    [Fact]
    public void ConfiguracaoDaEmpresa_normaliza_nome_e_municipio_com_Trim_e_recusa_vazio_ou_acima_do_limite()
    {
        var empresa = ConfiguracaoDaEmpresa.Criar("  Empresa Exemplo ", "SP", " São Paulo  ", Entrada, Saida, 10, 5);

        Assert.Equal((ConfiguracaoDaEmpresa.IdUnico, "Empresa Exemplo", "São Paulo"), (empresa.Id, empresa.NomeEmpresa, empresa.Municipio));
        Assert.Equal(new string('e', 150), ConfiguracaoDaEmpresa.Criar(new string('e', 150), "SP", "Cidade", Entrada, Saida, 10, 5).NomeEmpresa);
        Assert.Equal(new string('m', 100), ConfiguracaoDaEmpresa.Criar("Empresa", "SP", new string('m', 100), Entrada, Saida, 10, 5).Municipio);
        foreach (var texto in new[] { string.Empty, "   " })
        {
            Assert.ThrowsAny<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar(texto, "SP", "Cidade", Entrada, Saida, 10, 5));
            Assert.ThrowsAny<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", texto, Entrada, Saida, 10, 5));
        }

        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar(new string('e', 151), "SP", "Cidade", Entrada, Saida, 10, 5));
        Assert.Throws<ArgumentException>(() => ConfiguracaoDaEmpresa.Criar("Empresa", "SP", new string('m', 101), Entrada, Saida, 10, 5));
    }

    [Fact]
    public void ConfiguracaoDaEmpresa_Atualizar_aplica_as_mesmas_regras_e_o_que_foi_recusado_nao_troca_nada()
    {
        var empresa = Valida();

        empresa.Atualizar("Outra Empresa", "RJ", "Niterói", new TimeOnly(9, 0), new TimeOnly(18, 0), 15, 10);
        Assert.Equal(("Outra Empresa", "RJ", "Niterói", new TimeOnly(9, 0), new TimeOnly(18, 0), 15, 10), (empresa.NomeEmpresa, empresa.Uf, empresa.Municipio, empresa.HorarioEntrada, empresa.HorarioSaida, empresa.ToleranciaAtrasoMinutos, empresa.DiaPagamento));

        Assert.Throws<ArgumentException>(() => empresa.Atualizar("Terceira", "xx", "Cidade", Entrada, Saida, 10, 5));
        Assert.Throws<ArgumentException>(() => empresa.Atualizar("Terceira", "SP", "Cidade", Entrada, Saida, 999, 5));
        Assert.Throws<ArgumentException>(() => empresa.Atualizar("Terceira", "SP", "Cidade", Entrada, Saida, 10, 40));
        Assert.Equal(("Outra Empresa", "RJ", 15, 10), (empresa.NomeEmpresa, empresa.Uf, empresa.ToleranciaAtrasoMinutos, empresa.DiaPagamento));
    }

    [Fact]
    public void ConfiguracaoDaEmpresa_diz_o_nome_e_o_registro_afetado()
    {
        var empresa = Valida();

        Assert.Equal("Empresa Exemplo", empresa.NomeParaAuditoria);
        Assert.Equal("Configuração da empresa", empresa.RegistroAfetado);
    }
}

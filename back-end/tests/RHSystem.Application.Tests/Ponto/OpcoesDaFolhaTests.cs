using RHSystem.Application.Comum;
using RHSystem.Application.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>As escolhas do RH na exportação para a folha: as horas nos três formatos, o título de cada coluna e a leitura de <c>colunas</c>, <c>formatoHoras</c> e <c>separador</c>.</summary>
public class OpcoesDaFolhaTests
{
    [Theory]
    [InlineData(0, "0", "0,00", "00:00")]
    [InlineData(1, "1", "0,02", "00:01")]
    [InlineData(30, "30", "0,50", "00:30")]
    [InlineData(59, "59", "0,98", "00:59")]
    [InlineData(60, "60", "1,00", "01:00")]
    [InlineData(154, "154", "2,57", "02:34")]
    [InlineData(1041, "1041", "17,35", "17:21")]
    [InlineData(10560, "10560", "176,00", "176:00")]
    [InlineData(6000, "6000", "100,00", "100:00")]
    public void As_horas_saem_nos_tres_formatos_com_virgula_decimal_no_ponto_e_virgula(long minutos, string emMinutos, string decimais, string hhmm)
    {
        Assert.Equal(emMinutos, OpcoesDaFolha.Horas(minutos, FormatoDeHorasDaFolha.Minutos, SeparadorDaFolha.PontoEVirgula));
        Assert.Equal(decimais, OpcoesDaFolha.Horas(minutos, FormatoDeHorasDaFolha.HorasDecimais, SeparadorDaFolha.PontoEVirgula));
        Assert.Equal(hhmm, OpcoesDaFolha.Horas(minutos, FormatoDeHorasDaFolha.HhMm, SeparadorDaFolha.PontoEVirgula));
    }

    [Fact]
    public void Com_o_separador_virgula_a_marca_decimal_e_o_ponto_e_os_outros_formatos_nao_mudam()
    {
        Assert.Equal("17.35", OpcoesDaFolha.Horas(1041, FormatoDeHorasDaFolha.HorasDecimais, SeparadorDaFolha.Virgula));
        Assert.Equal("1041", OpcoesDaFolha.Horas(1041, FormatoDeHorasDaFolha.Minutos, SeparadorDaFolha.Virgula));
        Assert.Equal("17:21", OpcoesDaFolha.Horas(1041, FormatoDeHorasDaFolha.HhMm, SeparadorDaFolha.Virgula));
    }

    [Fact]
    public void O_titulo_leva_a_unidade_so_nas_quatro_colunas_de_horas()
    {
        var horas = new[] { ColunaDaFolha.HorasTrabalhadas, ColunaDaFolha.HorasPrevistas, ColunaDaFolha.BancoCreditoDoMes, ColunaDaFolha.BancoDebitoDoMes };

        Assert.All(OpcoesDaFolha.ColunasPadrao.Except(horas), c => Assert.Equal(OpcoesDaFolha.Codigo(c), OpcoesDaFolha.Titulo(c, FormatoDeHorasDaFolha.HhMm)));
        Assert.Equal(["horas_trabalhadas_min", "horas_previstas_min", "banco_credito_do_mes_min", "banco_debito_do_mes_min"], horas.Select(c => OpcoesDaFolha.Titulo(c, FormatoDeHorasDaFolha.Minutos)));
        Assert.Equal(["horas_trabalhadas_h", "horas_previstas_h", "banco_credito_do_mes_h", "banco_debito_do_mes_h"], horas.Select(c => OpcoesDaFolha.Titulo(c, FormatoDeHorasDaFolha.HorasDecimais)));
        Assert.Equal(["horas_trabalhadas_hhmm", "horas_previstas_hhmm", "banco_credito_do_mes_hhmm", "banco_debito_do_mes_hhmm"], horas.Select(c => OpcoesDaFolha.Titulo(c, FormatoDeHorasDaFolha.HhMm)));
    }

    [Fact]
    public void Os_titulos_padrao_sao_os_doze_do_arquivo_de_sempre_na_ordem_de_sempre()
    {
        Assert.Equal(
            ["matricula", "nome", "setor", "cargo", "dias_trabalhados", "horas_trabalhadas_min", "horas_previstas_min", "atrasos", "faltas", "faltas_compensadas", "banco_credito_do_mes_min", "banco_debito_do_mes_min"],
            OpcoesDaFolha.ColunasPadrao.Select(c => OpcoesDaFolha.Titulo(c, FormatoDeHorasDaFolha.Minutos)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Colunas_vazio_ou_ausente_valem_como_as_de_sempre(string? texto)
    {
        var erros = new ColetorDeErros();

        Assert.Equal(OpcoesDaFolha.ColunasPadrao, OpcoesDaFolha.LerColunas(erros, texto));
        erros.LancarSeHouver();
    }

    [Fact]
    public void As_colunas_saem_na_ordem_pedida_sem_diferenciar_caixa_nem_espacos()
    {
        var erros = new ColetorDeErros();

        var colunas = OpcoesDaFolha.LerColunas(erros, " Banco_Credito_Do_Mes ,nome,HORAS_TRABALHADAS");

        erros.LancarSeHouver();
        Assert.Equal([ColunaDaFolha.BancoCreditoDoMes, ColunaDaFolha.Nome, ColunaDaFolha.HorasTrabalhadas], colunas);
    }

    [Theory]
    [InlineData("nome,salario", "Coluna desconhecida: salario. Use: matricula, nome, setor, cargo, dias_trabalhados, horas_trabalhadas, horas_previstas, atrasos, faltas, faltas_compensadas, banco_credito_do_mes, banco_debito_do_mes.")]
    [InlineData("nome,NOME", "A coluna nome aparece mais de uma vez.")]
    [InlineData("nome,,setor", "Há uma coluna vazia na lista.")]
    [InlineData("nome,", "Há uma coluna vazia na lista.")]
    public void Coluna_desconhecida_repetida_ou_vazia_e_erro_do_campo_colunas_com_a_mensagem_certa(string texto, string mensagem)
    {
        var erros = new ColetorDeErros();

        _ = OpcoesDaFolha.LerColunas(erros, texto);

        var problema = Assert.Throws<ErroDeValidacaoException>(erros.LancarSeHouver);
        Assert.Equal(mensagem, Assert.Single(problema.Erros["colunas"]));
    }

    [Theory]
    [InlineData(null, FormatoDeHorasDaFolha.Minutos)]
    [InlineData("", FormatoDeHorasDaFolha.Minutos)]
    [InlineData("MINUTOS", FormatoDeHorasDaFolha.Minutos)]
    [InlineData("HORAS_DECIMAIS", FormatoDeHorasDaFolha.HorasDecimais)]
    [InlineData("HH_MM", FormatoDeHorasDaFolha.HhMm)]
    public void O_formato_vem_pelo_nome_do_contrato_e_vazio_e_minutos(string? texto, FormatoDeHorasDaFolha esperado)
    {
        var erros = new ColetorDeErros();

        Assert.Equal(esperado, OpcoesDaFolha.LerFormato(erros, texto));
        erros.LancarSeHouver();
    }

    [Theory]
    [InlineData(null, SeparadorDaFolha.PontoEVirgula)]
    [InlineData("", SeparadorDaFolha.PontoEVirgula)]
    [InlineData("PONTO_E_VIRGULA", SeparadorDaFolha.PontoEVirgula)]
    [InlineData("VIRGULA", SeparadorDaFolha.Virgula)]
    public void O_separador_vem_pelo_nome_do_contrato_e_vazio_e_ponto_e_virgula(string? texto, SeparadorDaFolha esperado)
    {
        var erros = new ColetorDeErros();

        Assert.Equal(esperado, OpcoesDaFolha.LerSeparador(erros, texto));
        erros.LancarSeHouver();
    }

    [Fact]
    public void Formato_e_separador_invalidos_dizem_os_valores_aceitos_e_o_nome_do_csharp_nao_vale()
    {
        var erros = new ColetorDeErros();

        _ = OpcoesDaFolha.LerFormato(erros, "HorasDecimais");
        _ = OpcoesDaFolha.LerSeparador(erros, "TAB");

        var problema = Assert.Throws<ErroDeValidacaoException>(erros.LancarSeHouver);
        Assert.Equal("Valor inválido. Use um destes: MINUTOS, HORAS_DECIMAIS, HH_MM.", Assert.Single(problema.Erros["formatoHoras"]));
        Assert.Equal("Valor inválido. Use um destes: PONTO_E_VIRGULA, VIRGULA.", Assert.Single(problema.Erros["separador"]));
    }

    [Fact]
    public void O_caractere_do_separador()
    {
        Assert.Equal(';', OpcoesDaFolha.Caractere(SeparadorDaFolha.PontoEVirgula));
        Assert.Equal(',', OpcoesDaFolha.Caractere(SeparadorDaFolha.Virgula));
    }
}

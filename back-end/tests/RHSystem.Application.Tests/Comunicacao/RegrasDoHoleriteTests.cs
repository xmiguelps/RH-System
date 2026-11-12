using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>
/// As regras puras do envio de holerite, sem banco: a leitura da competência e de "quando publicar" (todos os erros juntos, um por campo), a conferência da competência contra a admissão, o desligamento e o futuro (a mesma ordem e os mesmos textos do trigger de
/// <c>V002</c>) e o instante da publicação (agora, ou o começo do dia -03:00 de uma data futura).
/// </summary>
public class RegrasDoHoleriteTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 30, DateTimeKind.Utc);

    private static (T? Valor, IReadOnlyDictionary<string, string[]> Erros) Ler<T>(Func<ColetorDeErros, T?> leitura)
        where T : class
    {
        var coletor = new ColetorDeErros();
        var valor = leitura(coletor);
        if (!coletor.TemErros)
        {
            return (valor, new Dictionary<string, string[]>());
        }

        Assert.Null(valor); // com erro, nunca devolve o valor
        return (valor, Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros);
    }

    // ---------------------------------------------------------------- a competência

    [Theory]
    [InlineData("10", "2026", "MENSAL", 10, 2026, TipoDeHolerite.Mensal)]
    [InlineData("12", "2026", "DECIMO_TERCEIRO", 12, 2026, TipoDeHolerite.DecimoTerceiro)]
    [InlineData("1", "2000", "FERIAS", 1, 2000, TipoDeHolerite.Ferias)]
    [InlineData("12", "2100", "MENSAL", 12, 2100, TipoDeHolerite.Mensal)]
    public void A_competencia_valida_e_lida_com_o_tipo_pelo_nome_do_contrato(string mes, string ano, string tipo, int mesEsperado, int anoEsperado, TipoDeHolerite tipoEsperado)
    {
        var (competencia, erros) = Ler(e => LeitorDosCamposDoHolerite.LerCompetencia(e, mes, ano, tipo));

        Assert.Empty(erros);
        Assert.Equal(new CompetenciaDoHolerite(mesEsperado, anoEsperado, tipoEsperado), competencia);
    }

    [Theory]
    [InlineData(null, "2026", "MENSAL", "mes")]
    [InlineData("", "2026", "MENSAL", "mes")]
    [InlineData("0", "2026", "MENSAL", "mes")]
    [InlineData("13", "2026", "MENSAL", "mes")]
    [InlineData("dez", "2026", "MENSAL", "mes")]
    [InlineData("+9", "2026", "MENSAL", "mes")]
    [InlineData("10", "+2026", "MENSAL", "ano")]
    [InlineData("10", null, "MENSAL", "ano")]
    [InlineData("10", "1999", "MENSAL", "ano")]
    [InlineData("10", "2101", "MENSAL", "ano")]
    [InlineData("10", "26", "MENSAL", "ano")]
    [InlineData("10", "2026", null, "tipo")]
    [InlineData("10", "2026", "mensal", "tipo")]
    [InlineData("10", "2026", "DecimoTerceiro", "tipo")]
    [InlineData("10", "2026", "SALARIO", "tipo")]
    public void Cada_campo_invalido_da_o_erro_no_proprio_campo(string? mes, string? ano, string? tipo, string campo)
    {
        var (_, erros) = Ler(e => LeitorDosCamposDoHolerite.LerCompetencia(e, mes, ano, tipo));

        Assert.Equal([campo], erros.Keys);
    }

    [Fact]
    public void Todos_os_campos_invalidos_vem_juntos()
    {
        var (_, erros) = Ler(e => LeitorDosCamposDoHolerite.LerCompetencia(e, "0", "1900", "?"));

        Assert.Equal(["ano", "mes", "tipo"], erros.Keys.Order());
        Assert.Equal("Informe o mês, de 1 a 12.", Assert.Single(erros["mes"]));
        Assert.Equal("Informe o ano, de 2000 a 2100.", Assert.Single(erros["ano"]));
        Assert.Equal("Informe o tipo: MENSAL, DECIMO_TERCEIRO ou FERIAS.", Assert.Single(erros["tipo"]));
    }

    // ---------------------------------------------------------------- quando publicar

    [Fact]
    public void Sem_nada_publica_agora_e_a_data_de_hoje_tambem()
    {
        var (nada, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, null, null, Hoje));
        var (vazios, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "", " ", Hoje));
        var (hoje, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "2026-10-07", null, Hoje));

        Assert.Equal(new QuandoPublicarHolerite(null, false), nada);
        Assert.Equal(new QuandoPublicarHolerite(null, false), vazios);
        Assert.Equal(new QuandoPublicarHolerite(new DateOnly(2026, 10, 7), false), hoje);
    }

    [Fact]
    public void Data_futura_e_o_dia_do_pagamento_sao_lidos_e_false_vale_a_data()
    {
        var (futura, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "2026-11-06", null, Hoje));
        var (pagamento, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, null, "true", Hoje));
        var (pagamentoMaiusculo, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, null, "True", Hoje));
        var (dataComFalse, _) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "2026-11-06", "false", Hoje));

        Assert.Equal(new QuandoPublicarHolerite(new DateOnly(2026, 11, 6), false), futura);
        Assert.Equal(new QuandoPublicarHolerite(null, true), pagamento);
        Assert.Equal(new QuandoPublicarHolerite(null, true), pagamentoMaiusculo);
        Assert.Equal(new QuandoPublicarHolerite(new DateOnly(2026, 11, 6), false), dataComFalse);
    }

    [Fact]
    public void Dia_passado_formato_errado_e_valor_que_nao_e_booleano_dao_erro_no_campo()
    {
        var (_, passado) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "2026-10-06", null, Hoje));
        var (_, formato) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "07/10/2026", null, Hoje));
        var (_, booleano) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, null, "talvez", Hoje));

        Assert.Equal("A data de publicação não pode ser anterior a hoje.", Assert.Single(passado["publicarEm"]));
        Assert.Equal("Informe a data de publicação no formato AAAA-MM-DD.", Assert.Single(formato["publicarEm"]));
        Assert.Equal("Informe true ou false.", Assert.Single(booleano["noDiaDoPagamento"]));
    }

    [Fact]
    public void A_data_e_o_dia_do_pagamento_juntos_sao_400_no_campo_noDiaDoPagamento()
    {
        var (_, erros) = Ler(e => LeitorDosCamposDoHolerite.LerQuandoPublicar(e, "2026-11-06", "true", Hoje));

        Assert.Equal("Escolha a data de publicação ou o dia do pagamento, não os dois.", Assert.Single(erros["noDiaDoPagamento"]));
        Assert.Single(erros);
    }

    // ---------------------------------------------------------------- a competência contra a pessoa

    private static readonly DateOnly Admissao = new(2026, 1, 2);

    [Fact]
    public void Competencia_anterior_a_admissao_e_recusada_mas_o_mes_da_admissao_vale()
    {
        var antes = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(12, 2025, TipoDeHolerite.Mensal), Hoje, Admissao, null);
        var noMes = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(1, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, null);

        Assert.Equal(("HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", "A competencia do holerite e anterior a admissao."), (antes!.Mensagem.Codigo, antes.RegraDoBanco));
        Assert.Null(noMes);
    }

    [Fact]
    public void Competencia_posterior_ao_desligamento_e_recusada_mas_o_mes_do_desligamento_vale()
    {
        var desligamento = new DateOnly(2026, 3, 31);

        var depois = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(4, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, desligamento);
        var noMes = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(3, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, desligamento);

        Assert.Equal(("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", "A competencia do holerite e posterior ao desligamento."), (depois!.Mensagem.Codigo, depois.RegraDoBanco));
        Assert.Null(noMes);
    }

    [Fact]
    public void Competencia_futura_so_vale_em_ferias_e_o_mes_atual_nao_e_futuro()
    {
        var futuro = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(11, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, null);
        var decimoTerceiro = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(12, 2026, TipoDeHolerite.DecimoTerceiro), Hoje, Admissao, null);
        var ferias = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(11, 2026, TipoDeHolerite.Ferias), Hoje, Admissao, null);
        var mesAtual = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(10, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, null);

        Assert.Equal(("HOLERITE_COMPETENCIA_FUTURA", "Nao e possivel cadastrar holerite de competencia futura."), (futuro!.Mensagem.Codigo, futuro.RegraDoBanco));
        Assert.Equal("HOLERITE_COMPETENCIA_FUTURA", decimoTerceiro!.Mensagem.Codigo);
        Assert.Null(ferias);
        Assert.Null(mesAtual);
    }

    [Fact]
    public void A_ordem_das_recusas_e_a_do_trigger_admissao_depois_desligamento_depois_futuro()
    {
        // Competência de 2025-12: anterior à admissão E (com desligamento em 2025-06) posterior ao desligamento E, sendo MENSAL, nem é futura: vale a primeira.
        var primeira = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(12, 2025, TipoDeHolerite.Mensal), Hoje, Admissao, new DateOnly(2025, 6, 30));
        // Competência futura de FERIAS posterior a um desligamento: o desligamento vale.
        var segunda = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(12, 2026, TipoDeHolerite.Ferias), Hoje, Admissao, new DateOnly(2026, 6, 30));

        Assert.Equal("HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", primeira!.Mensagem.Codigo);
        Assert.Equal("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", segunda!.Mensagem.Codigo);
    }

    [Fact]
    public void Nos_limites_das_datas_a_competencia_vale()
    {
        var ultimoDiaIgualAAdmissao = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(10, 2026, TipoDeHolerite.Mensal), Hoje, new DateOnly(2026, 10, 31), null);
        var admissaoDepoisDoDia28 = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(10, 2026, TipoDeHolerite.Mensal), Hoje, new DateOnly(2026, 10, 29), null);
        var desligamentoNoPrimeiroDia = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(4, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, new DateOnly(2026, 4, 1));
        var hojeNoPrimeiroDoMes = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(10, 2026, TipoDeHolerite.Mensal), new DateOnly(2026, 10, 1), Admissao, null);

        Assert.Null(ultimoDiaIgualAAdmissao); // o último dia do mês é o da admissão: o mês da admissão vale
        Assert.Null(admissaoDepoisDoDia28); // outubro vai até 31, não até 28
        Assert.Null(desligamentoNoPrimeiroDia); // desligado no dia 1: o mês do desligamento vale
        Assert.Null(hojeNoPrimeiroDoMes); // hoje é o dia 1: o mês atual não é futuro
    }

    [Fact]
    public void Um_dia_depois_dos_limites_a_competencia_e_recusada()
    {
        var antesDaAdmissao = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(10, 2026, TipoDeHolerite.Mensal), Hoje, new DateOnly(2026, 11, 1), null);
        var depoisDoDesligamento = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(4, 2026, TipoDeHolerite.Mensal), Hoje, Admissao, new DateOnly(2026, 3, 31));
        var futura = RegraDaCompetenciaDoHolerite.Conferir(new CompetenciaDoHolerite(11, 2026, TipoDeHolerite.Mensal), new DateOnly(2026, 10, 31), Admissao, null);

        Assert.Equal("HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", antesDaAdmissao!.Mensagem.Codigo);
        Assert.Equal("HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", depoisDoDesligamento!.Mensagem.Codigo);
        Assert.Equal("HOLERITE_COMPETENCIA_FUTURA", futura!.Mensagem.Codigo);
    }

    // ---------------------------------------------------------------- o instante da publicação

    [Fact]
    public void Sem_data_ou_com_a_de_hoje_publica_agora_cortado_ao_segundo()
    {
        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 30, DateTimeKind.Utc), PublicacaoDoHolerite.Instante(null, false, Hoje, Agora.AddMilliseconds(600), null));
        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 30, DateTimeKind.Utc), PublicacaoDoHolerite.Instante(Hoje, false, Hoje, Agora, null));
    }

    [Fact]
    public void Data_futura_publica_no_comeco_do_dia_em_menos_3_horas()
    {
        Assert.Equal(new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), PublicacaoDoHolerite.Instante(new DateOnly(2026, 11, 6), false, Hoje, Agora, null)); // o exemplo do contrato
    }

    [Fact]
    public void No_dia_do_pagamento_vale_a_data_do_proximo_pagamento_e_se_e_hoje_publica_agora()
    {
        Assert.Equal(new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), PublicacaoDoHolerite.Instante(null, true, Hoje, Agora, new DateOnly(2026, 11, 6)));
        Assert.Equal(Agora, PublicacaoDoHolerite.Instante(null, true, Hoje, Agora, Hoje));
    }

    [Fact]
    public void No_dia_do_pagamento_sem_a_data_do_pagamento_e_erro_de_programacao()
    {
        Assert.Throws<ArgumentException>(() => PublicacaoDoHolerite.Instante(null, true, Hoje, Agora, null));
    }
}

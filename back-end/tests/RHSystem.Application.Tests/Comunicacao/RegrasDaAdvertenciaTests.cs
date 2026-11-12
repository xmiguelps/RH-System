using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>
/// As regras puras das advertências, sem banco: a conferência dos campos (todos os erros juntos, um por campo), o desfecho derivado da situação, quem aplicou e a situação que a decisão do RH produz, e as contas dos dois resumos (a regra única das não canceladas do ano).
/// </summary>
public class RegrasDaAdvertenciaTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private static (CamposDaAdvertencia? Campos, IReadOnlyDictionary<string, string[]> Erros) Registro(DateOnly data, string? motivo, string? descricao)
    {
        var coletor = new ColetorDeErros();
        var campos = ValidacaoDaAdvertencia.ValidarRegistro(coletor, data, Hoje, motivo, descricao);
        if (!coletor.TemErros)
        {
            return (campos, new Dictionary<string, string[]>());
        }

        Assert.Null(campos); // com erro, nunca devolve os campos
        return (campos, Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros);
    }

    // ---------------------------------------------------------------- o registro

    [Fact]
    public void Registro_valido_devolve_motivo_e_descricao_aparados()
    {
        var (campos, erros) = Registro(Hoje, "  Atraso  ", "  Chegou tarde.  ");

        Assert.Empty(erros);
        Assert.Equal(new CamposDaAdvertencia("Atraso", "Chegou tarde."), campos);
    }

    [Fact]
    public void O_dia_de_hoje_e_o_primeiro_do_ano_valem_e_o_de_amanha_e_o_dia_zero_do_calendario_nao()
    {
        Assert.Empty(Registro(Hoje, "m", "d").Erros);
        Assert.Empty(Registro(new DateOnly(2000, 1, 1), "m", "d").Erros);
        Assert.Equal(["A data do ocorrido não pode ser futura."], Registro(Hoje.AddDays(1), "m", "d").Erros["dataDoOcorrido"]);
        Assert.Equal(["Informe a data do ocorrido."], Registro(DateOnly.MinValue, "m", "d").Erros["dataDoOcorrido"]);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Motivo_e_descricao_vazios_sao_erro_de_campo(string? vazio)
    {
        var (_, erros) = Registro(Hoje, vazio, vazio);

        Assert.Equal(["descricao", "motivo"], erros.Keys.Order().ToArray());
        Assert.Equal(["Informe o motivo, com até 255 caracteres."], erros["motivo"]);
        Assert.Equal(["Informe a descrição, com até 5000 caracteres."], erros["descricao"]);
    }

    [Fact]
    public void O_motivo_vai_ate_255_e_a_descricao_ate_5000_caracteres_contados_depois_de_aparar()
    {
        Assert.Empty(Registro(Hoje, new string('m', 255), new string('d', 5000)).Erros);
        Assert.Empty(Registro(Hoje, "  " + new string('m', 255) + "  ", "  " + new string('d', 5000) + "  ").Erros);
        var (_, erros) = Registro(Hoje, new string('m', 256), new string('d', 5001));
        Assert.Equal(["descricao", "motivo"], erros.Keys.Order().ToArray());
    }

    [Fact]
    public void Os_tres_erros_do_registro_vem_juntos()
    {
        var (_, erros) = Registro(Hoje.AddDays(3), "", "");

        Assert.Equal(["dataDoOcorrido", "descricao", "motivo"], erros.Keys.Order().ToArray());
    }

    // ---------------------------------------------------------------- a contestação e a resposta

    private static (string? Texto, IReadOnlyDictionary<string, string[]> Erros) Contestacao(string? texto)
    {
        var coletor = new ColetorDeErros();
        var lido = ValidacaoDaAdvertencia.ValidarContestacao(coletor, texto);
        return (lido, coletor.TemErros ? Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros : new Dictionary<string, string[]>());
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("         ", false)]
    [InlineData("curto 9ch", false)]
    [InlineData("  curto 9ch  ", false)] // os espaços das pontas não contam
    [InlineData("dez letras", true)]
    [InlineData("  dez letras  ", true)]
    public void A_contestacao_tem_no_minimo_10_caracteres_aparados(string? texto, bool vale)
    {
        var (lido, erros) = Contestacao(texto);

        Assert.Equal(vale, lido is not null);
        Assert.Equal(vale ? 0 : 1, erros.Count);
        if (vale)
        {
            Assert.Equal(texto!.Trim(), lido);
        }
        else
        {
            Assert.Equal(["Explique o que aconteceu, com de 10 a 2000 caracteres."], erros["texto"]);
        }
    }

    [Fact]
    public void A_contestacao_vai_ate_2000_caracteres() =>
        Assert.Equal((true, false), (Contestacao(new string('t', 2000)).Texto is not null, Contestacao(new string('t', 2001)).Texto is not null));

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("   ", false)]
    [InlineData("x", true)]
    [InlineData("  x  ", true)]
    public void A_resposta_do_RH_tem_no_minimo_1_caractere_aparado(string? resposta, bool vale)
    {
        var coletor = new ColetorDeErros();

        var lida = ValidacaoDaAdvertencia.ValidarResposta(coletor, resposta);

        Assert.Equal(vale, lida is not null);
        Assert.Equal(!vale, coletor.TemErros);
        if (vale)
        {
            Assert.Equal(resposta!.Trim(), lida); // a resposta guardada é a aparada
        }
    }

    [Fact]
    public void A_resposta_do_RH_vai_ate_2000_caracteres_e_o_erro_e_no_campo_resposta()
    {
        var coletor = new ColetorDeErros();

        Assert.NotNull(ValidacaoDaAdvertencia.ValidarResposta(coletor, new string('r', 2000)));
        Assert.Null(ValidacaoDaAdvertencia.ValidarResposta(coletor, new string('r', 2001)));

        Assert.Equal(["Escreva a resposta, com até 2000 caracteres."], Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros["resposta"]);
    }

    // ---------------------------------------------------------------- o desfecho e quem aplicou

    [Theory]
    [InlineData(SituacaoDaAdvertencia.Pendente, null, null)]
    [InlineData(SituacaoDaAdvertencia.Pendente, "texto", null)]
    [InlineData(SituacaoDaAdvertencia.Contestada, "texto", null)]
    [InlineData(SituacaoDaAdvertencia.Aplicada, null, null)] // ciente, nunca contestou
    [InlineData(SituacaoDaAdvertencia.Aplicada, "texto", DesfechoDaContestacao.Mantida)]
    [InlineData(SituacaoDaAdvertencia.Cancelada, "texto", DesfechoDaContestacao.Acatada)]
    [InlineData(SituacaoDaAdvertencia.Cancelada, null, DesfechoDaContestacao.Acatada)]
    public void O_desfecho_e_derivado_da_situacao_e_do_texto_da_contestacao(SituacaoDaAdvertencia situacao, string? texto, DesfechoDaContestacao? esperado) =>
        Assert.Equal(esperado, RegrasDaAdvertencia.Desfecho(situacao, texto));

    [Fact]
    public void Quem_aplicou_e_o_gestor_com_o_nome_ou_o_RH_sem_nome()
    {
        Assert.Equal(new AdvertenciaAplicadaPor(QuemAplicouAAdvertencia.Gestor, "Carlos Ribeiro"), RegrasDaAdvertencia.AplicadaPor("Carlos Ribeiro"));
        Assert.Equal(new AdvertenciaAplicadaPor(QuemAplicouAAdvertencia.Rh, null), RegrasDaAdvertencia.AplicadaPor(null));
    }

    [Fact]
    public void Manter_volta_a_APLICADA_e_acatar_cancela()
    {
        Assert.Equal(SituacaoDaAdvertencia.Aplicada, RegrasDaAdvertencia.SituacaoDaDecisao(DecisaoDaResposta.Manter));
        Assert.Equal(SituacaoDaAdvertencia.Cancelada, RegrasDaAdvertencia.SituacaoDaDecisao(DecisaoDaResposta.Acatar));
    }

    [Fact]
    public void O_nome_do_tipo_e_verbal_ou_escrita()
    {
        Assert.Equal("verbal", RegrasDaAdvertencia.NomeDoTipo(TipoDeAdvertencia.Verbal));
        Assert.Equal("escrita", RegrasDaAdvertencia.NomeDoTipo(TipoDeAdvertencia.Escrita));
    }

    // ---------------------------------------------------------------- o resumo do RH

    private static AdvertenciaParaResumoRh Linha(SituacaoDaAdvertencia situacao, string? setor) => new(situacao, setor);

    [Fact]
    public void O_resumo_do_RH_conta_as_abas_com_as_canceladas_e_o_setor_so_com_as_nao_canceladas()
    {
        var resumo = ResumosDeAdvertencia.DoRh(
            2026,
            [
                Linha(SituacaoDaAdvertencia.Pendente, "Logística"),
                Linha(SituacaoDaAdvertencia.Pendente, "Logística"),
                Linha(SituacaoDaAdvertencia.Aplicada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Aplicada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Contestada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Cancelada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Cancelada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Cancelada, "Comercial"),
                Linha(SituacaoDaAdvertencia.Aplicada, null),
            ]);

        Assert.Equal(2026, resumo.Ano);
        Assert.Equal(new AdvertenciaContagensRh(9, 2, 3, 1, 3), resumo.Contagens);
        Assert.Equal(new AdvertenciaSetorComMaisRegistros("Comercial", 3), resumo.SetorComMaisRegistros); // Comercial tem 3 não canceladas (as 3 canceladas não contam) e Logística 2
    }

    [Fact]
    public void O_empate_de_setores_vai_para_o_primeiro_em_ordem_alfabetica_sem_olhar_maiusculas()
    {
        var resumo = ResumosDeAdvertencia.DoRh(2026, [Linha(SituacaoDaAdvertencia.Pendente, "bravo"), Linha(SituacaoDaAdvertencia.Pendente, "Alfa"), Linha(SituacaoDaAdvertencia.Pendente, "Zulu")]);

        Assert.Equal(new AdvertenciaSetorComMaisRegistros("Alfa", 1), resumo.SetorComMaisRegistros);
        Assert.Equal(new AdvertenciaSetorComMaisRegistros("alfa", 1), ResumosDeAdvertencia.DoRh(2026, [Linha(SituacaoDaAdvertencia.Pendente, "Bravo"), Linha(SituacaoDaAdvertencia.Pendente, "alfa")]).SetorComMaisRegistros); // minúscula antes de maiúscula de outra letra
        Assert.Equal(new AdvertenciaSetorComMaisRegistros("bravo", 2), ResumosDeAdvertencia.DoRh(2026, [Linha(SituacaoDaAdvertencia.Pendente, "bravo"), Linha(SituacaoDaAdvertencia.Aplicada, "bravo"), Linha(SituacaoDaAdvertencia.Pendente, "Alfa")]).SetorComMaisRegistros);
    }

    [Fact]
    public void Sem_nenhuma_nao_cancelada_com_setor_nao_ha_setor_com_mais_registros()
    {
        Assert.Null(ResumosDeAdvertencia.DoRh(2026, []).SetorComMaisRegistros);
        Assert.Null(ResumosDeAdvertencia.DoRh(2026, [Linha(SituacaoDaAdvertencia.Cancelada, "Comercial"), Linha(SituacaoDaAdvertencia.Pendente, null)]).SetorComMaisRegistros);
        Assert.Equal(new AdvertenciaContagensRh(0, 0, 0, 0, 0), ResumosDeAdvertencia.DoRh(2026, []).Contagens);
    }

    // ---------------------------------------------------------------- o resumo do gestor

    private static FuncionarioResumo Pessoa(int id, string nome) => new(id, nome, true, "Cargo", "Comercial");

    [Fact]
    public void O_resumo_do_gestor_usa_so_as_nao_canceladas_e_traz_toda_a_equipe_da_que_tem_mais_para_a_que_tem_menos_e_no_empate_por_nome()
    {
        var equipe = new[] { Pessoa(1, "Zélia"), Pessoa(2, "Ana"), Pessoa(3, "Bia"), Pessoa(4, "carlos") };
        var resumo = ResumosDeAdvertencia.DaEquipe(
            2026,
            equipe,
            [
                new AdvertenciaParaResumoDaEquipe(3, SituacaoDaAdvertencia.Pendente),
                new AdvertenciaParaResumoDaEquipe(3, SituacaoDaAdvertencia.Aplicada),
                new AdvertenciaParaResumoDaEquipe(1, SituacaoDaAdvertencia.Contestada),
                new AdvertenciaParaResumoDaEquipe(2, SituacaoDaAdvertencia.Pendente),
                new AdvertenciaParaResumoDaEquipe(4, SituacaoDaAdvertencia.Cancelada),
            ]);

        Assert.Equal((2026, 4, 3, 4), (resumo.Ano, resumo.Registradas, resumo.PessoasComAdvertencia, resumo.TotalDaEquipe));
        Assert.Equal([("Bia", 2), ("Ana", 1), ("Zélia", 1), ("carlos", 0)], resumo.PorPessoa.Select(p => (p.Funcionario.Nome, p.Quantidade)).ToArray());
        Assert.Equal(resumo.Registradas, resumo.PorPessoa.Sum(p => p.Quantidade));
        Assert.Equal(2, resumo.AguardandoCiencia.Quantidade);
        Assert.Equal(["Ana", "Bia"], resumo.AguardandoCiencia.Pessoas.Select(p => p.Nome).ToArray());
    }

    [Fact]
    public void O_resumo_de_uma_equipe_sem_advertencia_tem_todos_com_zero_por_nome()
    {
        var resumo = ResumosDeAdvertencia.DaEquipe(2026, [Pessoa(9, "Duda"), Pessoa(8, "Caio")], []);

        Assert.Equal((0, 0, 2), (resumo.Registradas, resumo.PessoasComAdvertencia, resumo.TotalDaEquipe));
        Assert.Equal([("Caio", 0), ("Duda", 0)], resumo.PorPessoa.Select(p => (p.Funcionario.Nome, p.Quantidade)).ToArray());
        Assert.Equal((0, 0), (resumo.AguardandoCiencia.Quantidade, resumo.AguardandoCiencia.Pessoas.Count));
        Assert.Equal((0, 0, 0), (ResumosDeAdvertencia.DaEquipe(2026, [], []).TotalDaEquipe, ResumosDeAdvertencia.DaEquipe(2026, [], []).PorPessoa.Count, ResumosDeAdvertencia.DaEquipe(2026, [], []).Registradas));
    }
}

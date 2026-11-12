using RHSystem.Application.Comum;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// As regras puras do formulário, sem banco: a situação (com o prazo vencido valendo como encerrado), os instantes (agendar às 00:00 -03:00, prazo até 23:59, encerramento nominal), a matriz de edição por situação e anonimato, a conferência de público x anonimato x tipo e o token do link.
/// </summary>
public class RegrasDoFormularioTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc); // 09:42 no Brasil

    // ---------------------------------------------------------------- a situação

    [Fact]
    public void As_quatro_situacoes_saem_das_colunas()
    {
        var publicacao = new DateTime(2026, 10, 1, 15, 0, 0, DateTimeKind.Utc);

        Assert.Equal(SituacaoDoFormulario.Rascunho, RegraDaSituacaoDoFormulario.De(false, null, null, null, Agora));
        Assert.Equal(SituacaoDoFormulario.Rascunho, RegraDaSituacaoDoFormulario.De(false, null, null, new DateOnly(2026, 11, 1), Agora)); // o prazo guardado no rascunho não o encerra
        Assert.Equal(SituacaoDoFormulario.Agendado, RegraDaSituacaoDoFormulario.De(false, publicacao.AddDays(30), null, null, Agora));
        Assert.Equal(SituacaoDoFormulario.Agendado, RegraDaSituacaoDoFormulario.De(false, publicacao, null, null, Agora)); // a tarefa de publicar ainda não rodou: continua agendado
        Assert.Equal(SituacaoDoFormulario.Publicado, RegraDaSituacaoDoFormulario.De(true, publicacao, null, null, Agora));
        Assert.Equal(SituacaoDoFormulario.Publicado, RegraDaSituacaoDoFormulario.De(true, publicacao, null, new DateOnly(2026, 10, 30), Agora));
        Assert.Equal(SituacaoDoFormulario.Encerrado, RegraDaSituacaoDoFormulario.De(false, publicacao, Agora, null, Agora));
        Assert.Equal(SituacaoDoFormulario.Encerrado, RegraDaSituacaoDoFormulario.De(false, null, Agora, null, Agora));
    }

    [Fact]
    public void O_prazo_vencido_vale_como_encerrado_mesmo_que_a_tarefa_de_encerrar_ainda_nao_tenha_rodado()
    {
        var publicacao = new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc);

        // prazo 06/10: na manhã de 07/10 já passou, e o banco ainda tem ATIVO ligado
        Assert.Equal(SituacaoDoFormulario.Encerrado, RegraDaSituacaoDoFormulario.De(true, publicacao, null, new DateOnly(2026, 10, 6), Agora));
        Assert.Equal(SituacaoDoFormulario.Publicado, RegraDaSituacaoDoFormulario.De(true, publicacao, null, new DateOnly(2026, 10, 7), Agora)); // o dia do prazo ainda vale
    }

    [Theory]
    [InlineData("2026-10-08T02:59:59Z", false)] // 23:59:59 do dia 07 no Brasil
    [InlineData("2026-10-08T03:00:00Z", true)] // 00:00 do dia 08 no Brasil
    [InlineData("2026-10-07T12:00:00Z", false)]
    public void O_prazo_vai_ate_23_59_do_horario_do_Brasil_e_nao_do_UTC(string agora, bool vencido) =>
        Assert.Equal(vencido, InstantesDoFormulario.PrazoVencido(new DateOnly(2026, 10, 7), DateTime.Parse(agora, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AdjustToUniversal)));

    [Fact]
    public void Sem_prazo_nunca_vence() => Assert.False(InstantesDoFormulario.PrazoVencido(null, new DateTime(2099, 1, 1, 0, 0, 0, DateTimeKind.Utc)));

    // ---------------------------------------------------------------- os instantes

    [Fact]
    public void Agendar_e_o_comeco_do_dia_em_menos_3_horas_e_o_encerramento_nominal_e_o_comeco_do_dia_seguinte_ao_prazo()
    {
        Assert.Equal(new DateTime(2026, 11, 6, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.InicioDoDiaAgendado(new DateOnly(2026, 11, 6)));
        Assert.Equal(new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.EncerramentoNominal(new DateOnly(2026, 9, 30))); // o exemplo do contrato
        Assert.Equal(new DateTime(2027, 1, 1, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.EncerramentoNominal(new DateOnly(2026, 12, 31)));
    }

    [Fact]
    public void O_dia_da_rodada_e_o_comeco_do_dia_local_da_publicacao_igual_para_qualquer_hora_do_dia()
    {
        Assert.Equal(new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.DiaDaRodada(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)));
        Assert.Equal(new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.DiaDaRodada(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc))); // 23:30 do dia 07 no Brasil
        Assert.Equal(new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc), InstantesDoFormulario.DiaDaRodada(new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc)));
    }

    [Theory]
    [InlineData(1, UnidadeDeRecorrencia.Dia, "2026-10-08")]
    [InlineData(10, UnidadeDeRecorrencia.Dia, "2026-10-17")]
    [InlineData(2, UnidadeDeRecorrencia.Semana, "2026-10-21")]
    [InlineData(1, UnidadeDeRecorrencia.Mes, "2026-11-07")]
    [InlineData(3, UnidadeDeRecorrencia.Mes, "2027-01-07")]
    public void A_proxima_reabertura_soma_o_intervalo_ao_dia_local(int intervalo, UnidadeDeRecorrencia unidade, string esperado) =>
        Assert.Equal(InstantesDoFormulario.InicioDoDiaAgendado(DateOnly.Parse(esperado, System.Globalization.CultureInfo.InvariantCulture)), InstantesDoFormulario.ProximaReabertura(Agora, intervalo, unidade));

    [Fact]
    public void A_reabertura_de_mes_nao_estoura_o_ultimo_dia_do_mes_curto() =>
        Assert.Equal(InstantesDoFormulario.InicioDoDiaAgendado(new DateOnly(2027, 2, 28)), InstantesDoFormulario.ProximaReabertura(new DateTime(2027, 1, 31, 15, 0, 0, DateTimeKind.Utc), 1, UnidadeDeRecorrencia.Mes));

    [Fact]
    public void A_proxima_reabertura_conta_do_dia_local_mesmo_quando_o_instante_ja_virou_o_dia_em_UTC() =>
        // 23:30 do dia 07 no Brasil já é 02:30Z do dia 08: o dia local é o 07, e mais um dia é o 08
        Assert.Equal(InstantesDoFormulario.InicioDoDiaAgendado(new DateOnly(2026, 10, 8)), InstantesDoFormulario.ProximaReabertura(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc), 1, UnidadeDeRecorrencia.Dia));

    [Fact]
    public void O_prazo_da_rodada_nova_avanca_o_mesmo_numero_de_dias_da_anterior()
    {
        // a rodada anterior abriu em 01/09 (manhã) com prazo em 15/09: 14 dias; a nova abre em 01/10
        var prazo = InstantesDoFormulario.NovoPrazo(new DateOnly(2026, 9, 15), new DateTime(2026, 9, 1, 15, 0, 0, DateTimeKind.Utc), new DateTime(2026, 10, 1, 3, 0, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 10, 15), prazo);
    }

    [Fact]
    public void O_prazo_da_rodada_nova_conta_os_dias_no_horario_do_Brasil_e_nao_no_UTC()
    {
        // a anterior abriu às 23:30 de 01/09 no Brasil (02:30Z de 02/09), prazo 15/09: 14 dias; a nova abre às 23:30 de 01/10 no Brasil (02:30Z de 02/10)
        var prazo = InstantesDoFormulario.NovoPrazo(new DateOnly(2026, 9, 15), new DateTime(2026, 9, 2, 2, 30, 0, DateTimeKind.Utc), new DateTime(2026, 10, 2, 2, 30, 0, DateTimeKind.Utc));

        Assert.Equal(new DateOnly(2026, 10, 15), prazo);
    }

    // ---------------------------------------------------------------- a matriz de edição

    [Fact]
    public void Encerrado_nao_muda_nada_nem_com_nome_nem_anonimo()
    {
        Assert.Equal(RecusaDeEdicao.FormularioEncerrado, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Encerrado, false, false, false, false, null, TipoDeFormulario.Pesquisa));
        Assert.Equal(RecusaDeEdicao.FormularioEncerrado, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Encerrado, true, true, false, false, null, TipoDeFormulario.Pesquisa));
    }

    [Theory]
    [InlineData(SituacaoDoFormulario.Rascunho)]
    [InlineData(SituacaoDoFormulario.Agendado)]
    public void Rascunho_e_agendado_mudam_tudo_inclusive_o_anonimato(SituacaoDoFormulario situacao)
    {
        Assert.Null(MatrizDeEdicao.Conferir(situacao, false, false, true, true, null, TipoDeFormulario.Avaliacao));
        Assert.Null(MatrizDeEdicao.Conferir(situacao, true, false, true, true, AlvoDoFormulario.Pessoas, TipoDeFormulario.Outro));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Anonimo_publicado_so_muda_titulo_e_descricao_com_ou_sem_resposta_e_o_resultado_e_o_mesmo(bool temResposta)
    {
        // o mesmo veredito com e sem resposta: nenhuma diferença observável
        Assert.Null(MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, true, temResposta, false, false, null, TipoDeFormulario.Pesquisa));
        Assert.Equal(RecusaDeEdicao.AnonimatoFixoAposPublicar, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, true, temResposta, true, false, null, TipoDeFormulario.Pesquisa));
        Assert.Equal(RecusaDeEdicao.AnonimatoFixoAposPublicar, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, true, temResposta, false, true, null, TipoDeFormulario.Pesquisa));
    }

    [Fact]
    public void Com_nome_publicado_o_anonimato_nunca_muda()
    {
        Assert.Equal(RecusaDeEdicao.AnonimatoFixoAposPublicar, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, false, true, false, null, TipoDeFormulario.Pesquisa));
        Assert.Equal(RecusaDeEdicao.AnonimatoFixoAposPublicar, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, true, true, false, null, TipoDeFormulario.Pesquisa));
    }

    [Fact]
    public void Com_nome_publicado_sem_resposta_o_resto_muda_mas_o_formulario_por_link_nao_sai_de_entrevista()
    {
        Assert.Null(MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, false, false, true, AlvoDoFormulario.Pessoas, TipoDeFormulario.Avaliacao));
        Assert.Null(MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, false, false, true, AlvoDoFormulario.Link, TipoDeFormulario.Entrevista));
        Assert.Equal(RecusaDeEdicao.LinkSoParaEntrevista, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, false, false, true, AlvoDoFormulario.Link, TipoDeFormulario.Pesquisa));
    }

    [Fact]
    public void Com_nome_publicado_com_resposta_so_titulo_e_descricao_mudam()
    {
        Assert.Null(MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, true, false, false, AlvoDoFormulario.Pessoas, TipoDeFormulario.Pesquisa));
        Assert.Equal(RecusaDeEdicao.FormularioJaRespondido, MatrizDeEdicao.Conferir(SituacaoDoFormulario.Publicado, false, true, false, true, AlvoDoFormulario.Pessoas, TipoDeFormulario.Avaliacao));
    }

    // ---------------------------------------------------------------- publico x anonimato x tipo

    [Fact]
    public void Anonimo_nao_vai_por_link_e_so_a_entrevista_vai_por_link_e_a_primeira_recusa_e_a_do_anonimato()
    {
        Assert.Null(RegrasDaPublicacao.Recusa(TipoDeFormulario.Entrevista, false, AlvoDoFormulario.Link));
        Assert.Null(RegrasDaPublicacao.Recusa(TipoDeFormulario.Pesquisa, true, AlvoDoFormulario.Pessoas));
        Assert.Null(RegrasDaPublicacao.Recusa(TipoDeFormulario.Pesquisa, false, AlvoDoFormulario.Setores));
        Assert.Equal(RecusaDePublicacao.AnonimoSemLink, RegrasDaPublicacao.Recusa(TipoDeFormulario.Entrevista, true, AlvoDoFormulario.Link));
        Assert.Equal(RecusaDePublicacao.AnonimoSemLink, RegrasDaPublicacao.Recusa(TipoDeFormulario.Pesquisa, true, AlvoDoFormulario.Link)); // anonimato antes do tipo
        Assert.Equal(RecusaDePublicacao.LinkSoParaEntrevista, RegrasDaPublicacao.Recusa(TipoDeFormulario.Admissao, false, AlvoDoFormulario.Link));
        Assert.Equal(RecusaDePublicacao.LinkSoParaEntrevista, RegrasDaPublicacao.Recusa(TipoDeFormulario.Outro, false, AlvoDoFormulario.Link));
    }

    // ---------------------------------------------------------------- o token do link

    [Fact]
    public void O_token_tem_43_caracteres_base64url_sem_mais_barra_nem_igual_e_nao_se_repete()
    {
        var tokens = Enumerable.Range(0, 200).Select(_ => TokenDoLink.Novo()).ToList();

        Assert.All(tokens, t => Assert.Matches("^[A-Za-z0-9_-]{43}$", t));
        Assert.Equal(200, tokens.Distinct().Count());
        Assert.All(tokens, t => Assert.True(TokenDoLink.Valido(t)));
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("curto", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaa", false)] // 21
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa", true)] // 22
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", true)] // 64
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa", false)] // 65
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa/aaaa", false)]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaa=", false)]
    [InlineData("2147483647", false)]
    [InlineData("../../etc/passwd/aaaaaaaa", false)]
    public void O_token_so_vale_no_formato_do_contrato(string? texto, bool valido) => Assert.Equal(valido, TokenDoLink.Valido(texto));
}

using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>
/// As contas puras das listas e dos downloads de holerite, sem banco: a situação pelo relógio (no instante exato a data já chegou), as contagens das abas, o nome do arquivo baixado e o texto do evento <c>Baixou</c>.
/// </summary>
public class RegrasDasListasDeHoleriteTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    // ---------------------------------------------------------------- a situação

    [Fact]
    public void Data_de_depois_de_agora_e_agendado_e_a_de_agora_ou_antes_e_publicado()
    {
        Assert.Equal((SituacaoDoHolerite.Agendado, Agora.AddSeconds(1)), RegrasDasListasDeHolerite.SituacaoDe(Agora.AddSeconds(1), Agora));
        Assert.Equal((SituacaoDoHolerite.Publicado, Agora), RegrasDasListasDeHolerite.SituacaoDe(Agora, Agora)); // no instante exato a data já chegou
        Assert.Equal((SituacaoDoHolerite.Publicado, Agora.AddSeconds(-1)), RegrasDasListasDeHolerite.SituacaoDe(Agora.AddSeconds(-1), Agora));
    }

    [Fact]
    public void Sem_data_de_publicacao_conta_como_publicado_agora()
    {
        Assert.Equal((SituacaoDoHolerite.Publicado, Agora), RegrasDasListasDeHolerite.SituacaoDe(null, Agora));
    }

    [Fact]
    public void O_item_do_RH_leva_cada_campo_da_linha_e_a_situacao_pelo_relogio()
    {
        var linha = new HoleriteDaListaDoRh(41, 7, TipoDeHolerite.Ferias, 3, 2027, 2, true, Agora.AddDays(2));

        var item = RegrasDasListasDeHolerite.ItemDoRh(linha, Agora);

        Assert.Equal(new HoleriteItemRh(41, 7, TipoDeHolerite.Ferias, 3, 2027, 2, true, SituacaoDoHolerite.Agendado, Agora.AddDays(2)), item);
        Assert.Equal(SituacaoDoHolerite.Publicado, RegrasDasListasDeHolerite.ItemDoRh(linha with { VersaoAtual = false, PublicacaoEmUtc = Agora.AddDays(-2) }, Agora).Situacao);
        Assert.False(RegrasDasListasDeHolerite.ItemDoRh(linha with { VersaoAtual = false }, Agora).VersaoAtual);
    }

    // ---------------------------------------------------------------- as contagens

    [Fact]
    public void As_contagens_somam_todos_e_separam_um_numero_por_tipo()
    {
        var contagens = RegrasDasListasDeHolerite.Contagens(
            [TipoDeHolerite.Mensal, TipoDeHolerite.Mensal, TipoDeHolerite.Mensal, TipoDeHolerite.Ferias, TipoDeHolerite.Ferias, TipoDeHolerite.DecimoTerceiro]);

        Assert.Equal(new HoleriteContagensEu(6, 3, 2, 1), contagens);
        Assert.Equal(new HoleriteContagensEu(0, 0, 0, 0), RegrasDasListasDeHolerite.Contagens([]));
        Assert.Equal(new HoleriteContagensEu(1, 0, 0, 1), RegrasDasListasDeHolerite.Contagens([TipoDeHolerite.DecimoTerceiro]));
    }

    // ---------------------------------------------------------------- o nome do arquivo e o evento

    [Theory]
    [InlineData(TipoDeHolerite.Mensal, 9, 2026, 1, false, "holerite-2026-09-mensal.pdf")]
    [InlineData(TipoDeHolerite.Mensal, 9, 2026, 2, true, "holerite-2026-09-mensal-v2.pdf")]
    [InlineData(TipoDeHolerite.DecimoTerceiro, 12, 2026, 1, false, "holerite-2026-12-decimo-terceiro.pdf")]
    [InlineData(TipoDeHolerite.Ferias, 1, 2027, 13, true, "holerite-2027-01-ferias-v13.pdf")]
    [InlineData(TipoDeHolerite.Ferias, 1, 2027, 13, false, "holerite-2027-01-ferias.pdf")]
    public void O_nome_do_arquivo_tem_o_ano_o_mes_com_dois_digitos_o_tipo_e_so_para_o_RH_a_versao(TipoDeHolerite tipo, int mes, int ano, int versao, bool comVersao, string esperado)
    {
        var holerite = new HoleriteParaDownload(5, "Ana Souza", mes, ano, tipo, versao, "holerites/x.pdf");

        Assert.Equal(esperado, RegrasDasListasDeHolerite.NomeDoArquivo(holerite, comVersao));
    }

    [Theory]
    [InlineData(TipoDeHolerite.Mensal, 9, 2026, 2, "Baixou o holerite mensal de setembro de 2026 (versão 2) de Ana Souza.")]
    [InlineData(TipoDeHolerite.DecimoTerceiro, 12, 2026, 1, "Baixou o holerite do 13º salário de dezembro de 2026 (versão 1) de Ana Souza.")]
    [InlineData(TipoDeHolerite.Ferias, 3, 2027, 1, "Baixou o holerite de férias de março de 2027 (versão 1) de Ana Souza.")]
    public void O_evento_diz_a_competencia_por_extenso_a_versao_e_o_nome_sem_o_arquivo(TipoDeHolerite tipo, int mes, int ano, int versao, string esperado)
    {
        var holerite = new HoleriteParaDownload(5, "Ana Souza", mes, ano, tipo, versao, "holerites/segredo.pdf");

        var descricao = RegrasDasListasDeHolerite.DescricaoDoDownload(holerite);

        Assert.Equal(esperado, descricao);
        Assert.DoesNotContain("segredo", descricao, StringComparison.Ordinal);
    }
}

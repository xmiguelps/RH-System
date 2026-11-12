using System.Globalization;
using RHSystem.Application.Comunicacao.Holerites;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>As contas puras das listas e dos downloads de holerite: a situação pelo relógio, as contagens das abas e o nome do arquivo baixado.</summary>
public static class RegrasDasListasDeHolerite
{
    /// <summary>
    /// Agendado é o que só fica visível depois de agora (a data chega no começo do dia agendado, e nesse instante já é publicado); publicado é o resto. Uma linha sem data de publicação (o trigger do banco sempre a preenche, então só uma escrita direta a deixaria
    /// assim) conta como publicada agora.
    /// </summary>
    public static (SituacaoDoHolerite Situacao, DateTime PublicacaoEmUtc) SituacaoDe(DateTime? publicacaoEmUtc, DateTime agoraUtc) =>
        publicacaoEmUtc is { } data ? (data > agoraUtc ? SituacaoDoHolerite.Agendado : SituacaoDoHolerite.Publicado, data) : (SituacaoDoHolerite.Publicado, agoraUtc);

    public static HoleriteItemRh ItemDoRh(HoleriteDaListaDoRh linha, DateTime agoraUtc)
    {
        var (situacao, publicacao) = SituacaoDe(linha.PublicacaoEmUtc, agoraUtc);
        return new HoleriteItemRh(linha.Id, linha.FuncionarioId, linha.Tipo, linha.Mes, linha.Ano, linha.Versao, linha.VersaoAtual, situacao, publicacao);
    }

    /// <summary>As abas da lista da pessoa: todos e um número por tipo, sobre os holerites visíveis do ano (cada um é uma competência e um tipo, na versão atual).</summary>
    public static HoleriteContagensEu Contagens(IReadOnlyList<TipoDeHolerite> tiposDoAno) =>
        new(
            tiposDoAno.Count,
            tiposDoAno.Count(t => t == TipoDeHolerite.Mensal),
            tiposDoAno.Count(t => t == TipoDeHolerite.Ferias),
            tiposDoAno.Count(t => t == TipoDeHolerite.DecimoTerceiro));

    /// <summary>O nome com que o PDF é baixado: <c>holerite-2026-09-mensal.pdf</c>, e o RH (que enxerga todas as versões) vê também a versão (<c>holerite-2026-09-mensal-v2.pdf</c>).</summary>
    public static string NomeDoArquivo(HoleriteParaDownload holerite, bool comVersao) =>
        string.Create(CultureInfo.InvariantCulture, $"holerite-{holerite.Ano}-{holerite.Mes:00}-{NomeNoArquivo(holerite.Tipo)}{(comVersao ? $"-v{holerite.Versao}" : string.Empty)}.pdf");

    private static string NomeNoArquivo(TipoDeHolerite tipo) =>
        tipo switch
        {
            TipoDeHolerite.Mensal => "mensal",
            TipoDeHolerite.DecimoTerceiro => "decimo-terceiro",
            _ => "ferias",
        };

    /// <summary>O texto do evento <c>Baixou</c>: <c>Baixou o holerite mensal de setembro de 2026 (versão 2) de Ana Souza.</c> Sem o nome do arquivo nem o caminho.</summary>
    public static string DescricaoDoDownload(HoleriteParaDownload holerite) =>
        string.Create(CultureInfo.InvariantCulture, $"Baixou o holerite {MensagensDaComunicacao.TextoDaCompetencia(holerite.Tipo, holerite.Mes, holerite.Ano)} (versão {holerite.Versao}) de {holerite.FuncionarioNome}.");
}

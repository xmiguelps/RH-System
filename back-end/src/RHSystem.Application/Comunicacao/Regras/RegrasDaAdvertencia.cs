using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>Os campos do registro depois de conferidos: motivo e descrição aparados.</summary>
public sealed record CamposDaAdvertencia(string Motivo, string Descricao);

/// <summary>
/// A conferência dos campos das advertências, <b>todos os erros juntos</b> (o 400 traz um por campo). O registro: o dia do ocorrido (hoje ou antes, pelo dia de hoje no Brasil; sem a data vem o dia zero do calendário, que também é recusado), o motivo (1 a 255
/// caracteres) e a descrição (1 a 5000). A contestação tem de 10 a 2000 caracteres e a resposta do RH, de 1 a 2000. As contas são por caractere, como o banco, e o texto é aparado.
/// </summary>
public static class ValidacaoDaAdvertencia
{
    public const int MotivoMaximo = 255;
    public const int DescricaoMaxima = 5000;
    public const int ContestacaoMinima = 10;
    public const int ContestacaoMaxima = 2000;
    public const int RespostaMaxima = 2000;

    public static CamposDaAdvertencia? ValidarRegistro(ColetorDeErros erros, DateOnly dataDoOcorrido, DateOnly hoje, string? motivo, string? descricao)
    {
        var valido = true;
        if (dataDoOcorrido == DateOnly.MinValue)
        {
            erros.Adicionar("dataDoOcorrido", "Informe a data do ocorrido.");
            valido = false;
        }
        else if (dataDoOcorrido > hoje)
        {
            erros.Adicionar("dataDoOcorrido", "A data do ocorrido não pode ser futura.");
            valido = false;
        }

        var motivoAparado = motivo?.Trim() ?? string.Empty;
        if (motivoAparado.Length == 0 || ContagemDeCaracteres.De(motivoAparado) > MotivoMaximo)
        {
            erros.Adicionar("motivo", $"Informe o motivo, com até {MotivoMaximo} caracteres.");
            valido = false;
        }

        var descricaoAparada = descricao?.Trim() ?? string.Empty;
        if (descricaoAparada.Length == 0 || ContagemDeCaracteres.De(descricaoAparada) > DescricaoMaxima)
        {
            erros.Adicionar("descricao", $"Informe a descrição, com até {DescricaoMaxima} caracteres.");
            valido = false;
        }

        return valido ? new CamposDaAdvertencia(motivoAparado, descricaoAparada) : null;
    }

    public static string? ValidarContestacao(ColetorDeErros erros, string? texto)
    {
        var aparado = texto?.Trim() ?? string.Empty;
        var tamanho = ContagemDeCaracteres.De(aparado);
        if (tamanho < ContestacaoMinima || tamanho > ContestacaoMaxima)
        {
            erros.Adicionar("texto", $"Explique o que aconteceu, com de {ContestacaoMinima} a {ContestacaoMaxima} caracteres.");
            return null;
        }

        return aparado;
    }

    public static string? ValidarResposta(ColetorDeErros erros, string? resposta)
    {
        var aparada = resposta?.Trim() ?? string.Empty;
        if (aparada.Length == 0 || ContagemDeCaracteres.De(aparada) > RespostaMaxima)
        {
            erros.Adicionar("resposta", $"Escreva a resposta, com até {RespostaMaxima} caracteres.");
            return null;
        }

        return aparada;
    }
}

/// <summary>As contas puras da advertência: quem aplicou, o desfecho (derivado, o banco não tem a coluna), a situação que a decisão do RH produz e as palavras do evento de auditoria.</summary>
public static class RegrasDaAdvertencia
{
    /// <summary>Sem gestor na linha é o RH (e o nome fica <c>null</c>); com gestor, o nome dele.</summary>
    public static AdvertenciaAplicadaPor AplicadaPor(string? gestorNome) =>
        gestorNome is null ? new AdvertenciaAplicadaPor(QuemAplicouAAdvertencia.Rh, null) : new AdvertenciaAplicadaPor(QuemAplicouAAdvertencia.Gestor, gestorNome);

    /// <summary>
    /// O que o RH decidiu sobre a contestação, deduzido: cancelada só existe por acatar (<c>ACATADA</c>); aplicada COM texto de contestação é a mantida (contestar só sai de pendente, então uma aplicada com contestação só pode ter voltado por manter); nos outros
    /// casos não houve decisão (<c>null</c>).
    /// </summary>
    public static DesfechoDaContestacao? Desfecho(SituacaoDaAdvertencia situacao, string? textoContestacao) =>
        situacao switch
        {
            SituacaoDaAdvertencia.Cancelada => DesfechoDaContestacao.Acatada,
            SituacaoDaAdvertencia.Aplicada when textoContestacao is not null => DesfechoDaContestacao.Mantida,
            _ => null,
        };

    /// <summary>Manter volta a advertência a <c>APLICADA</c>; acatar a <c>CANCELADA</c> (e cancelada não muda mais).</summary>
    public static SituacaoDaAdvertencia SituacaoDaDecisao(DecisaoDaResposta decisao) =>
        decisao == DecisaoDaResposta.Acatar ? SituacaoDaAdvertencia.Cancelada : SituacaoDaAdvertencia.Aplicada;

    public static string NomeDoTipo(TipoDeAdvertencia tipo) => tipo == TipoDeAdvertencia.Escrita ? "escrita" : "verbal";
}

using System.Globalization;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>A competência do holerite: o mês, o ano (2000 a 2100, como o contrato) e o tipo.</summary>
public sealed record CompetenciaDoHolerite(int Mes, int Ano, TipoDeHolerite Tipo);

/// <summary>"Quando publicar": uma data (<c>PublicarEm</c>) ou o dia do pagamento, nunca os dois; sem nenhum dos dois (ou com a data de hoje) publica agora.</summary>
public sealed record QuandoPublicarHolerite(DateOnly? PublicarEm, bool NoDiaDoPagamento);

/// <summary>Uma competência que a regra do banco recusaria: a mensagem do contrato e o texto ORIGINAL do trigger (que vai em <c>regraDoBanco</c>).</summary>
public sealed record RecusaDaCompetencia(MensagemDaComunicacao Mensagem, string RegraDoBanco);

/// <summary>
/// A leitura dos campos de texto do envio de holerite (<c>multipart/form-data</c> chega tudo como texto), <b>todos os erros juntos</b> (o 400 traz um por campo). O tipo é lido pelo nome do contrato (<c>MENSAL</c>, <c>DECIMO_TERCEIRO</c>,
/// <c>FERIAS</c>); <c>publicarEm</c> é <c>AAAA-MM-DD</c> de hoje em diante (o fuso é o de Brasília); <c>noDiaDoPagamento</c> é <c>true</c> ou <c>false</c>; os dois juntos (a data e o dia do pagamento) são 400.
/// </summary>
public static class LeitorDosCamposDoHolerite
{
    public const int PrimeiroAno = 2000;
    public const int UltimoAno = 2100;

    public static CompetenciaDoHolerite? LerCompetencia(ColetorDeErros erros, string? mes, string? ano, string? tipo)
    {
        var valido = true;
        if (!InteiroNoIntervalo(mes, 1, 12, out var mesLido))
        {
            erros.Adicionar("mes", "Informe o mês, de 1 a 12.");
            valido = false;
        }

        if (!InteiroNoIntervalo(ano, PrimeiroAno, UltimoAno, out var anoLido))
        {
            erros.Adicionar("ano", $"Informe o ano, de {PrimeiroAno} a {UltimoAno}.");
            valido = false;
        }

        var tipoLido = TipoDeHolerite.Mensal;
        var tipoValido = false;
        foreach (var candidato in Enum.GetValues<TipoDeHolerite>())
        {
            if (string.Equals(NomesDoContrato.Enum(candidato), tipo?.Trim(), StringComparison.Ordinal))
            {
                tipoLido = candidato;
                tipoValido = true;
            }
        }

        if (!tipoValido)
        {
            erros.Adicionar("tipo", "Informe o tipo: MENSAL, DECIMO_TERCEIRO ou FERIAS.");
            valido = false;
        }

        return valido ? new CompetenciaDoHolerite(mesLido, anoLido, tipoLido) : null;
    }

    public static QuandoPublicarHolerite? LerQuandoPublicar(ColetorDeErros erros, string? publicarEm, string? noDiaDoPagamento, DateOnly hoje)
    {
        var valido = true;
        DateOnly? data = null;
        if (!string.IsNullOrWhiteSpace(publicarEm))
        {
            if (!DateOnly.TryParseExact(publicarEm.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var lida))
            {
                erros.Adicionar("publicarEm", "Informe a data de publicação no formato AAAA-MM-DD.");
                valido = false;
            }
            else if (lida < hoje)
            {
                erros.Adicionar("publicarEm", "A data de publicação não pode ser anterior a hoje.");
                valido = false;
            }
            else
            {
                data = lida;
            }
        }

        var noDia = false;
        if (!string.IsNullOrWhiteSpace(noDiaDoPagamento))
        {
            if (string.Equals(noDiaDoPagamento.Trim(), "true", StringComparison.OrdinalIgnoreCase))
            {
                noDia = true;
            }
            else if (!string.Equals(noDiaDoPagamento.Trim(), "false", StringComparison.OrdinalIgnoreCase))
            {
                erros.Adicionar("noDiaDoPagamento", "Informe true ou false.");
                valido = false;
            }
        }

        if (valido && data is not null && noDia)
        {
            erros.Adicionar("noDiaDoPagamento", "Escolha a data de publicação ou o dia do pagamento, não os dois.");
            valido = false;
        }

        return valido ? new QuandoPublicarHolerite(data, noDia) : null;
    }

    private static bool InteiroNoIntervalo(string? texto, int minimo, int maximo, out int valor)
    {
        valor = 0;
        return int.TryParse(texto?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out valor) && valor >= minimo && valor <= maximo;
    }
}

/// <summary>
/// A conferência da competência contra a pessoa e o relógio, <b>na ordem e com os textos do trigger</b> <c>TRG_HOLERITES_BEFORE_INSERT</c>: anterior à admissão (o último dia do mês da competência vem antes da admissão), posterior ao desligamento (o
/// primeiro dia do mês vem depois dele) e futura (o primeiro dia do mês vem depois de hoje, e só o tipo <c>FERIAS</c> pode). A aplicação confere pelo <c>IRelogio</c> (o trigger usa o <c>CURDATE()</c> real) e devolve a mesma recusa que o banco daria.
/// </summary>
public static class RegraDaCompetenciaDoHolerite
{
    public static RecusaDaCompetencia? Conferir(CompetenciaDoHolerite competencia, DateOnly hoje, DateOnly admissao, DateOnly? desligamento)
    {
        var primeiroDia = new DateOnly(competencia.Ano, competencia.Mes, 1);
        var ultimoDia = new DateOnly(competencia.Ano, competencia.Mes, DateTime.DaysInMonth(competencia.Ano, competencia.Mes));

        if (ultimoDia < admissao)
        {
            return new RecusaDaCompetencia(MensagensDaComunicacao.HoleriteCompetenciaAnteriorAAdmissao, RegrasDoBancoDaComunicacao.HoleriteAnteriorAAdmissao);
        }

        if (desligamento is { } saida && primeiroDia > saida)
        {
            return new RecusaDaCompetencia(MensagensDaComunicacao.HoleriteCompetenciaPosteriorAoDesligamento, RegrasDoBancoDaComunicacao.HoleritePosteriorAoDesligamento);
        }

        if (competencia.Tipo != TipoDeHolerite.Ferias && primeiroDia > hoje)
        {
            return new RecusaDaCompetencia(MensagensDaComunicacao.HoleriteCompetenciaFutura, RegrasDoBancoDaComunicacao.HoleriteCompetenciaFutura);
        }

        return null;
    }
}

/// <summary>
/// O instante em que o holerite fica visível: <b>agora</b> (cortado ao segundo) se não há data, se é a de hoje ou a do pagamento de hoje; senão o <b>começo do dia</b> em Brasília (<c>00:00 -03:00</c>, por exemplo <c>2026-11-06T03:00:00Z</c>): a pessoa
/// só vê, e só recebe o e-mail, a partir daí.
/// </summary>
public static class PublicacaoDoHolerite
{
    /// <param name="dataDoPagamento">A data do próximo pagamento da empresa; obrigatória quando <paramref name="noDiaDoPagamento"/> (o chamador responde 422 antes se a empresa não tem dia de pagamento).</param>
    /// <exception cref="ArgumentException"><paramref name="noDiaDoPagamento"/> sem a data do pagamento.</exception>
    public static DateTime Instante(DateOnly? publicarEm, bool noDiaDoPagamento, DateOnly hoje, DateTime agoraUtc, DateOnly? dataDoPagamento)
    {
        var dia = noDiaDoPagamento ? dataDoPagamento ?? throw new ArgumentException("No dia do pagamento precisa da data do próximo pagamento.", nameof(dataDoPagamento)) : publicarEm;
        return dia is null || dia <= hoje ? InstanteAoSegundo.De(agoraUtc) : FusoBrasil.InicioDoDiaEmUtc(dia.Value);
    }
}

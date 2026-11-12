using System.Globalization;
using RHSystem.Application.Comum;

namespace RHSystem.Application.Ponto.Atestados;

/// <summary>Os campos do atestado já conferidos: dias inteiros (sem horas) ou algumas horas de UM dia.</summary>
public sealed record CamposDoAtestado(DateOnly DataInicio, DateOnly DataFim, TimeOnly? HoraInicio, TimeOnly? HoraFim);

/// <summary>
/// A leitura e a conferência dos campos de texto do envio, todos os erros juntos (o 400 traz um por campo): datas <c>AAAA-MM-DD</c>, fim não antes do início, horas <c>HH:mm</c> sempre as duas, só num dia, e a de fim depois da de início. Os campos do banco
/// (<c>CHK_ATESTADOS_PERIODO</c>, <c>CHK_ATESTADOS_HORAS_JUNTAS</c>, <c>CHK_ATESTADOS_HORAS_NO_MESMO_DIA</c>) são a rede de baixo; o 400 vem daqui, com o nome do campo.
/// </summary>
public static class LeitorDosCamposDoAtestado
{
    /// <summary>Os campos conferidos, ou <c>null</c> (com os erros em <paramref name="erros"/>) se algum não vale.</summary>
    public static CamposDoAtestado? Ler(ColetorDeErros erros, string? dataInicio, string? dataFim, string? horaInicio, string? horaFim)
    {
        var valido = true;
        var inicio = LerData(erros, dataInicio, "dataInicio", "a data de início");
        var fim = LerData(erros, dataFim, "dataFim", "a data de fim");
        var temHoraInicio = !string.IsNullOrWhiteSpace(horaInicio);
        var temHoraFim = !string.IsNullOrWhiteSpace(horaFim);
        var deInicio = LerHora(erros, horaInicio, "horaInicio");
        var deFim = LerHora(erros, horaFim, "horaFim");
        valido &= inicio is not null && fim is not null && (!temHoraInicio || deInicio is not null) && (!temHoraFim || deFim is not null);

        if (inicio is { } i && fim is { } f && f < i)
        {
            erros.Adicionar("dataFim", "A data de fim não pode ser antes da data de início.");
            valido = false;
        }

        if (temHoraInicio != temHoraFim)
        {
            // As horas vêm juntas: a que falta leva o erro.
            erros.Adicionar(temHoraInicio ? "horaFim" : "horaInicio", "Informe a hora de início e a hora de fim juntas.");
            valido = false;
        }
        else if (deInicio is { } hi && deFim is { } hf)
        {
            if (inicio is { } i2 && fim is { } f2 && i2 != f2)
            {
                erros.Adicionar("dataFim", "O atestado de algumas horas vale para um dia só: use a mesma data de início e de fim.");
                valido = false;
            }

            if (hf <= hi)
            {
                erros.Adicionar("horaFim", "A hora de fim precisa ser depois da hora de início.");
                valido = false;
            }
        }

        return valido ? new CamposDoAtestado(inicio!.Value, fim!.Value, deInicio, deFim) : null;
    }

    private static DateOnly? LerData(ColetorDeErros erros, string? texto, string campo, string nome)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            erros.Adicionar(campo, $"Informe {nome}.");
            return null;
        }

        if (!DateOnly.TryParseExact(texto.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
        {
            erros.Adicionar(campo, $"Use {nome} no formato AAAA-MM-DD.");
            return null;
        }

        return data;
    }

    private static TimeOnly? LerHora(ColetorDeErros erros, string? texto, string campo)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        if (!TimeOnly.TryParseExact(texto.Trim(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var hora))
        {
            erros.Adicionar(campo, "Use a hora no formato HH:mm.");
            return null;
        }

        return hora;
    }
}

/// <summary>O período como os textos da auditoria o escrevem: <c>01/10/2026 a 02/10/2026</c>, <c>06/10/2026</c> (um dia) ou <c>25/09/2026 das 13:00 às 17:48</c> (horas).</summary>
public static class PeriodoDoAtestado
{
    public static string Texto(DateOnly inicio, DateOnly fim, TimeOnly? horaInicio, TimeOnly? horaFim)
    {
        if (horaInicio is { } hi && horaFim is { } hf)
        {
            return string.Create(CultureInfo.InvariantCulture, $"{inicio:dd/MM/yyyy} das {hi:HH:mm} às {hf:HH:mm}");
        }

        return inicio == fim
            ? inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)
            : string.Create(CultureInfo.InvariantCulture, $"{inicio:dd/MM/yyyy} a {fim:dd/MM/yyyy}");
    }
}

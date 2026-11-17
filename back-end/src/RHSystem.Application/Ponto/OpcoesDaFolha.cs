using System.Globalization;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Ponto;

/// <summary>As colunas que a exportação para a folha pode trazer, <b>na ordem padrão</b> (a ordem de declaração é a do arquivo de sempre).</summary>
public enum ColunaDaFolha
{
    Matricula,
    Nome,
    Setor,
    Cargo,
    DiasTrabalhados,
    HorasTrabalhadas,
    HorasPrevistas,
    Atrasos,
    Faltas,
    FaltasCompensadas,
    BancoCreditoDoMes,
    BancoDebitoDoMes,
}

/// <summary>Como as quatro colunas de horas saem: minutos inteiros (o padrão), horas com duas casas ou <c>HH:MM</c>.</summary>
public enum FormatoDeHorasDaFolha
{
    Minutos,
    HorasDecimais,
    HhMm,
}

/// <summary>O separador das células do CSV.</summary>
public enum SeparadorDaFolha
{
    PontoEVirgula,
    Virgula,
}

/// <summary>
/// As escolhas do RH na exportação para a folha (decisão do dono, 2026-11-17): quais colunas e em que ordem (<c>colunas</c>, os códigos separados por vírgula, sem diferenciar caixa nem espaços), o formato das horas (<c>formatoHoras</c>) e o separador (<c>separador</c>).
/// Tudo é opcional e sem nada o arquivo é o de sempre. O cabeçalho das colunas de horas leva a unidade (<c>_min</c>, <c>_h</c> ou <c>_hhmm</c>). Horas decimais: minutos ÷ 60 com duas casas, arredondado para cima no meio, com vírgula como marca decimal quando o separador é
/// <c>;</c> e ponto quando é vírgula (para a marca não se confundir com o separador). <c>HH:MM</c>: no mínimo dois dígitos em cada parte, e as horas passam de 99 sem problema (176:00).
/// </summary>
public static class OpcoesDaFolha
{
    public static IReadOnlyList<ColunaDaFolha> ColunasPadrao { get; } = Enum.GetValues<ColunaDaFolha>();

    public static string Codigo(ColunaDaFolha coluna) => coluna switch
    {
        ColunaDaFolha.Matricula => "matricula",
        ColunaDaFolha.Nome => "nome",
        ColunaDaFolha.Setor => "setor",
        ColunaDaFolha.Cargo => "cargo",
        ColunaDaFolha.DiasTrabalhados => "dias_trabalhados",
        ColunaDaFolha.HorasTrabalhadas => "horas_trabalhadas",
        ColunaDaFolha.HorasPrevistas => "horas_previstas",
        ColunaDaFolha.Atrasos => "atrasos",
        ColunaDaFolha.Faltas => "faltas",
        ColunaDaFolha.FaltasCompensadas => "faltas_compensadas",
        ColunaDaFolha.BancoCreditoDoMes => "banco_credito_do_mes",
        _ => "banco_debito_do_mes",
    };

    public static char Caractere(SeparadorDaFolha separador) => separador == SeparadorDaFolha.PontoEVirgula ? ';' : ',';

    private static bool EhDeHoras(ColunaDaFolha coluna) => coluna is ColunaDaFolha.HorasTrabalhadas or ColunaDaFolha.HorasPrevistas or ColunaDaFolha.BancoCreditoDoMes or ColunaDaFolha.BancoDebitoDoMes;

    /// <summary>O título da coluna no cabeçalho: o código, mais a unidade nas colunas de horas.</summary>
    public static string Titulo(ColunaDaFolha coluna, FormatoDeHorasDaFolha formato)
    {
        if (!EhDeHoras(coluna))
        {
            return Codigo(coluna);
        }

        return Codigo(coluna) + formato switch
        {
            FormatoDeHorasDaFolha.Minutos => "_min",
            FormatoDeHorasDaFolha.HorasDecimais => "_h",
            _ => "_hhmm",
        };
    }

    /// <summary>A célula de uma coluna de horas: nunca negativa (o banco já vem em crédito e débito separados).</summary>
    public static string Horas(long minutos, FormatoDeHorasDaFolha formato, SeparadorDaFolha separador)
    {
        switch (formato)
        {
            case FormatoDeHorasDaFolha.Minutos:
                return minutos.ToString(CultureInfo.InvariantCulture);
            case FormatoDeHorasDaFolha.HorasDecimais:
                var horas = Math.Round(minutos / 60m, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
                return separador == SeparadorDaFolha.PontoEVirgula ? horas.Replace('.', ',') : horas;
            default:
                return string.Create(CultureInfo.InvariantCulture, $"{minutos / 60:00}:{minutos % 60:00}");
        }
    }

    /// <summary>
    /// Lê <c>colunas</c>: vazio é "as de sempre". Os códigos são separados por vírgula, sem diferenciar caixa nem espaços nas pontas; código desconhecido, repetido ou vazio é erro do campo <c>colunas</c> (só o primeiro problema).
    /// </summary>
    public static IReadOnlyList<ColunaDaFolha> LerColunas(ColetorDeErros erros, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return ColunasPadrao;
        }

        var lidas = new List<ColunaDaFolha>();
        foreach (var codigo in texto.Split(','))
        {
            var limpo = codigo.Trim();
            if (limpo.Length == 0)
            {
                erros.Adicionar("colunas", "Há uma coluna vazia na lista.");
                return ColunasPadrao;
            }

            var coluna = ColunasPadrao.Cast<ColunaDaFolha?>().FirstOrDefault(c => string.Equals(Codigo(c!.Value), limpo, StringComparison.OrdinalIgnoreCase));
            if (coluna is null)
            {
                erros.Adicionar("colunas", $"Coluna desconhecida: {limpo}. Use: {string.Join(", ", ColunasPadrao.Select(Codigo))}.");
                return ColunasPadrao;
            }

            if (lidas.Contains(coluna.Value))
            {
                erros.Adicionar("colunas", $"A coluna {Codigo(coluna.Value)} aparece mais de uma vez.");
                return ColunasPadrao;
            }

            lidas.Add(coluna.Value);
        }

        return lidas;
    }

    /// <summary>Lê <c>formatoHoras</c> (o nome do contrato, em maiúsculas); vazio é <see cref="FormatoDeHorasDaFolha.Minutos"/>.</summary>
    public static FormatoDeHorasDaFolha LerFormato(ColetorDeErros erros, string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return FormatoDeHorasDaFolha.Minutos;
        }

        var todos = Enum.GetValues<FormatoDeHorasDaFolha>();
        foreach (var formato in todos)
        {
            if (string.Equals(NomesDoContrato.Enum(formato), texto, StringComparison.Ordinal))
            {
                return formato;
            }
        }

        erros.Adicionar("formatoHoras", $"Valor inválido. Use um destes: {string.Join(", ", todos.Select(NomesDoContrato.Enum))}.");
        return FormatoDeHorasDaFolha.Minutos;
    }

    /// <summary>Lê <c>separador</c> (o nome do contrato, em maiúsculas); vazio é <see cref="SeparadorDaFolha.PontoEVirgula"/>.</summary>
    public static SeparadorDaFolha LerSeparador(ColetorDeErros erros, string? texto)
    {
        if (string.IsNullOrEmpty(texto))
        {
            return SeparadorDaFolha.PontoEVirgula;
        }

        var todos = Enum.GetValues<SeparadorDaFolha>();
        foreach (var separador in todos)
        {
            if (string.Equals(NomesDoContrato.Enum(separador), texto, StringComparison.Ordinal))
            {
                return separador;
            }
        }

        erros.Adicionar("separador", $"Valor inválido. Use um destes: {string.Join(", ", todos.Select(NomesDoContrato.Enum))}.");
        return SeparadorDaFolha.PontoEVirgula;
    }
}

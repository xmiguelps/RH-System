using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace RHSystem.Infrastructure.Persistencia;

/// <summary>
/// A decisão de fuso de cada coluna <c>DATETIME</c> mapeada. Dentro da aplicação todo instante é UTC (<c>Kind = Utc</c>); o banco guarda
/// de dois jeitos: as tabelas só da aplicação em UTC (<see cref="Utc"/>) e as tabelas do grupo em -03:00 (<see cref="Brasil"/>, sem horário
/// de verão: o Brasil não tem). Os dois recusam um <c>DateTime</c> que não seja UTC (Local ou Unspecified): é a ambiguidade que o conversor
/// existe para não deixar passar. Nenhum dos dois arredonda nem trunca a fração: quem trunca ao segundo uma coluna sem fração é a entidade.
/// </summary>
public static class ConversoresDeTempo
{
    private static readonly TimeSpan DiferencaDoBrasil = TimeSpan.FromHours(3);

    /// <summary>Tabelas só da aplicação (UTC no banco): grava exige <c>Kind = Utc</c> (senão lança); lê como <c>Kind = Utc</c>.</summary>
    public static ValueConverter<DateTime, DateTime> Utc { get; } = new(v => ParaOBancoEmUtc(v), v => DoBancoComoUtc(v));

    /// <summary>Tabelas do grupo (<c>DATETIME</c> em -03:00): grava UTC menos 3 horas; lê mais 3 horas como <c>Kind = Utc</c>.</summary>
    public static ValueConverter<DateTime, DateTime> Brasil { get; } = new(v => ParaOBancoNoBrasil(v), v => DoBancoNoBrasil(v));

    internal static DateTime ParaOBancoEmUtc(DateTime valor) =>
        DateTime.SpecifyKind(ExigirUtc(valor), DateTimeKind.Unspecified);

    internal static DateTime DoBancoComoUtc(DateTime valor) =>
        DateTime.SpecifyKind(valor, DateTimeKind.Utc);

    internal static DateTime ParaOBancoNoBrasil(DateTime valor) =>
        DateTime.SpecifyKind(ExigirUtc(valor) - DiferencaDoBrasil, DateTimeKind.Unspecified);

    internal static DateTime DoBancoNoBrasil(DateTime valor) =>
        DateTime.SpecifyKind(valor + DiferencaDoBrasil, DateTimeKind.Utc);

    private static DateTime ExigirUtc(DateTime valor) =>
        valor.Kind == DateTimeKind.Utc
            ? valor
            : throw new InvalidOperationException(
                $"Só se grava DateTime em UTC (Kind = Utc); recebi Kind = {valor.Kind}. Converta na fronteira (relógio ou entrada) antes de chegar ao banco.");
}

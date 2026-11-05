using RHSystem.Application.Colaboradores;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A versão padrão da fonte de fatos do dia, que diz a verdade do que existe hoje: ponto, atestado, feriado, férias e horário padrão são das fatias 3, 7 e 1c. Dia útil é
/// segunda a sexta; sem horário de entrada configurado ninguém está atrasado; ninguém tem fato. A fatia 3 registra a fonte dela (<c>AddScoped</c>, depois desta) para valer.
/// </summary>
public sealed class FonteDeFatosDoDiaSemRegistros : IFonteDeFatosDoDia
{
    public Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct) =>
        Task.FromResult(new CalendarioDoDia(dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday), LimiteDeEntrada: null));

    public Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct) =>
        Task.FromResult<IReadOnlyDictionary<int, FatosDaPessoaNoDia>>(new Dictionary<int, FatosDaPessoaNoDia>());
}

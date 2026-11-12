using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// Um dia do calendário da empresa: <see cref="EhDiaUtil"/> é segunda a sexta sem feriado que valha para a empresa. O <see cref="NomeDoFeriado"/> vem mesmo num fim de semana (o espelho mostra o nome quando a pessoa bate
/// ponto num feriado); com mais de um feriado na data vale o de tipo mais amplo (nacional, estadual, municipal, empresa).
/// </summary>
public sealed record DiaDoCalendario(bool EhDiaUtil, string? NomeDoFeriado);

/// <summary>Um feriado ativo que vale para a empresa.</summary>
public sealed record FeriadoQueVale(DateOnly Data, string Nome, TipoDeFeriado Tipo);

/// <summary>
/// O calendário que o ponto usa (espelho, situação de hoje, consolidação do banco de horas). Vale o feriado ATIVO que é nacional, da empresa, estadual da UF da empresa ou municipal do município da empresa; sem a empresa
/// configurada valem só os nacionais e os da empresa (o ponto não exige a configuração).
/// </summary>
public interface ICalendarioDoPonto
{
    Task<DiaDoCalendario> DiaAsync(DateOnly dia, CancellationToken ct);

    /// <summary>Os feriados que valem entre as duas datas (inclusive), por data, depois por tipo (nacional, estadual, municipal, empresa) e nome.</summary>
    Task<IReadOnlyList<FeriadoQueVale>> FeriadosQueValemAsync(DateOnly de, DateOnly ate, CancellationToken ct);
}

/// <summary>As férias de uma pessoa que cobrem um período (<c>APROVADA</c>, <c>EM_GOZO</c> ou <c>CONCLUIDA</c>); a fatia 7 é a dona da tabela, aqui só se lê.</summary>
public sealed record FeriasDoPeriodo(int FuncionarioId, DateOnly Inicio, DateOnly Fim);

public interface IFeriasDoPonto
{
    /// <summary>As férias que têm algum dia entre <paramref name="de"/> e <paramref name="ate"/> (inclusive), de todas as pessoas.</summary>
    Task<IReadOnlyList<FeriasDoPeriodo>> NoPeriodoAsync(DateOnly de, DateOnly ate, CancellationToken ct);
}

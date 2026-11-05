using System.Text.Json.Serialization;

namespace RHSystem.Application.Colaboradores;

/// <summary>A situação de uma pessoa hoje. No JSON: <c>SEM_EXPEDIENTE</c>, <c>FERIAS</c>, <c>ATESTADO</c>, <c>TRABALHANDO</c>, <c>SEM_REGISTRO</c>, <c>AINDA_NAO_COMECOU</c>.</summary>
public enum SituacaoHoje
{
    SemExpediente,
    Ferias,
    Atestado,
    Trabalhando,
    SemRegistro,
    AindaNaoComecou,
}

/// <summary>Quantas pessoas há em cada situação (a soma das seis é o total da equipe).</summary>
public sealed record PresencaContagens(int Trabalhando, int Ferias, int Atestado, int SemRegistro, int AindaNaoComecou, int SemExpediente)
{
    /// <summary>A soma das seis. Não é propriedade do JSON do contrato.</summary>
    [JsonIgnore]
    public int Total => Trabalhando + Ferias + Atestado + SemRegistro + AindaNaoComecou + SemExpediente;

    public static PresencaContagens De(IEnumerable<SituacaoHoje> situacoes)
    {
        var porSituacao = situacoes.GroupBy(s => s).ToDictionary(g => g.Key, g => g.Count());
        int Contar(SituacaoHoje s) => porSituacao.GetValueOrDefault(s);
        return new PresencaContagens(
            Contar(SituacaoHoje.Trabalhando),
            Contar(SituacaoHoje.Ferias),
            Contar(SituacaoHoje.Atestado),
            Contar(SituacaoHoje.SemRegistro),
            Contar(SituacaoHoje.AindaNaoComecou),
            Contar(SituacaoHoje.SemExpediente));
    }
}

/// <summary>O dia: se tem expediente e, para cada pessoa, a hora-limite de entrada (hora local; nula = a empresa não configurou horário).</summary>
public sealed record CalendarioDoDia(bool DiaComExpediente, TimeOnly? LimiteDeEntrada);

/// <summary>O que se sabe de uma pessoa no dia: já bateu entrada, está de férias, tem atestado de dia inteiro.</summary>
public sealed record FatosDaPessoaNoDia(bool JaRegistrouEntrada, bool EmFerias, bool ComAtestadoDeDiaInteiro);

/// <summary>A regra de <c>contrato/CONVENCOES.md</c> (situação de hoje).</summary>
public static class RegraDeSituacaoDeHoje
{
    /// <summary>
    /// Quem já registrou entrada trabalha. Sem entrada vale a primeira que se aplica: dia sem expediente, férias, atestado (de dia inteiro) e, depois do limite de
    /// entrada (<b>estritamente depois</b>), sem registro; senão, ainda não começou. Sem limite configurado ninguém está atrasado.
    /// </summary>
    public static SituacaoHoje Calcular(CalendarioDoDia calendario, FatosDaPessoaNoDia fatos, TimeOnly agoraLocal)
    {
        if (fatos.JaRegistrouEntrada)
        {
            return SituacaoHoje.Trabalhando;
        }

        if (!calendario.DiaComExpediente)
        {
            return SituacaoHoje.SemExpediente;
        }

        if (fatos.EmFerias)
        {
            return SituacaoHoje.Ferias;
        }

        if (fatos.ComAtestadoDeDiaInteiro)
        {
            return SituacaoHoje.Atestado;
        }

        return calendario.LimiteDeEntrada is { } limite && agoraLocal > limite ? SituacaoHoje.SemRegistro : SituacaoHoje.AindaNaoComecou;
    }
}

/// <summary>
/// Quem sabe os fatos do dia: ponto, atestado e feriado (fatia 3), férias (fatia 7) e o horário da empresa (fatia 1c). A fatia 3 substitui a versão padrão, que só conhece o
/// fim de semana.
/// </summary>
public interface IFonteDeFatosDoDia
{
    Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct);

    /// <summary>Quem não está no dicionário não tem nenhum fato.</summary>
    Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct);
}

public sealed record DiaDeHoje(DateOnly Dia, bool DiaUtil, IReadOnlyDictionary<int, SituacaoHoje> Situacoes);

public interface ISituacaoDeHoje
{
    /// <summary>"Hoje" é o dia em Brasília no instante do <c>IRelogio</c>; o limite de entrada compara com a hora de Brasília.</summary>
    Task<DiaDeHoje> ObterAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct);
}

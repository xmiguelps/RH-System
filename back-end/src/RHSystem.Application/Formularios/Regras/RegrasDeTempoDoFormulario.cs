using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>
/// A situação do formulário numa função <b>só</b>, usada em toda leitura (lista, detalhe, responder, página pública, resultado do anônimo): encerrado é a data de encerramento OU o prazo já vencido, mesmo que a tarefa de encerrar ainda não tenha rodado (até ela rodar o banco tem
/// <c>ATIVO</c> ligado e aceitaria a resposta; lida só das colunas, a situação abriria o resultado do anônimo tarde ou deixaria entrar resposta fora do prazo). Rascunho: inativo sem datas. Agendado: inativo com a data de publicação (mesmo vencida, enquanto a tarefa de publicar não rodou).
/// </summary>
public static class RegraDaSituacaoDoFormulario
{
    public static SituacaoDoFormulario De(bool ativo, DateTime? publicadoEmUtc, DateTime? encerradoEmUtc, DateOnly? prazo, DateTime agoraUtc)
    {
        if (encerradoEmUtc is not null)
        {
            return SituacaoDoFormulario.Encerrado;
        }

        if (ativo)
        {
            return InstantesDoFormulario.PrazoVencido(prazo, agoraUtc) ? SituacaoDoFormulario.Encerrado : SituacaoDoFormulario.Publicado;
        }

        return publicadoEmUtc is not null ? SituacaoDoFormulario.Agendado : SituacaoDoFormulario.Rascunho;
    }
}

/// <summary>
/// Os instantes do formulário. <c>prazo</c> e <c>agendarPara</c> são DIAS no horário do Brasil: agendar vale no começo do dia (00:00 -03:00, 03:00Z); o prazo vai até 23:59:59 do dia, e o encerramento NOMINAL (o que se grava ao encerrar pelo prazo) é o começo do dia seguinte (prazo 30/09 → 2026-10-01T03:00:00Z).
/// A "rodada" de um formulário é o dia local da publicação, e é a data grossa das respostas anônimas.
/// </summary>
public static class InstantesDoFormulario
{
    public static DateTime InicioDoDiaAgendado(DateOnly dia) => FusoBrasil.InicioDoDiaEmUtc(dia);

    public static DateTime EncerramentoNominal(DateOnly prazo) => FusoBrasil.InicioDoDiaEmUtc(prazo.AddDays(1));

    /// <summary>Depois do último dia do prazo, pelo dia de hoje no Brasil (às 21:00 locais o dia UTC já virou). Sem prazo, nunca vence.</summary>
    public static bool PrazoVencido(DateOnly? prazo, DateTime agoraUtc) => prazo is { } ultimoDia && FusoBrasil.Hoje(agoraUtc) > ultimoDia;

    /// <summary>O começo (00:00 -03:00) do dia local da publicação: igual para qualquer hora do dia.</summary>
    public static DateTime DiaDaRodada(DateTime publicadoEmUtc) => FusoBrasil.InicioDoDiaEmUtc(FusoBrasil.DiaLocalDe(publicadoEmUtc));

    /// <summary>A próxima reabertura do formulário recorrente: o dia local de <paramref name="baseUtc"/> mais o intervalo (o mês curto não estoura: 31/01 + 1 mês = 28/02), às 00:00 -03:00.</summary>
    public static DateTime ProximaReabertura(DateTime baseUtc, int intervalo, UnidadeDeRecorrencia unidade)
    {
        var dia = FusoBrasil.DiaLocalDe(baseUtc);
        var proximo = unidade switch
        {
            UnidadeDeRecorrencia.Dia => dia.AddDays(intervalo),
            UnidadeDeRecorrencia.Semana => dia.AddDays(7 * intervalo),
            _ => dia.AddMonths(intervalo),
        };
        return FusoBrasil.InicioDoDiaEmUtc(proximo);
    }

    /// <summary>A próxima reabertura, ou <c>null</c> se ela cairia depois do último dia em que ainda reabre (<paramref name="ate"/>, o dia no Brasil): a recorrência acaba ali.</summary>
    public static DateTime? ProximaReaberturaDentroDoLimite(DateTime baseUtc, int intervalo, UnidadeDeRecorrencia unidade, DateOnly? ate)
    {
        var proxima = ProximaReabertura(baseUtc, intervalo, unidade);
        return ate is { } limite && FusoBrasil.DiaLocalDe(proxima) > limite ? null : proxima;
    }

    /// <summary>O prazo da rodada nova: avança o MESMO número de dias que a rodada anterior tinha entre a publicação e o prazo (hipótese do contrato).</summary>
    public static DateOnly NovoPrazo(DateOnly prazoAnterior, DateTime publicadoAnteriorUtc, DateTime novaPublicacaoUtc)
    {
        var duracao = prazoAnterior.DayNumber - FusoBrasil.DiaLocalDe(publicadoAnteriorUtc).DayNumber;
        return FusoBrasil.DiaLocalDe(novaPublicacaoUtc).AddDays(duracao);
    }
}

using System.Globalization;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Uma batida pedida: nova (<paramref name="MarcacaoId"/> nulo) ou a correção de uma existente do mesmo dia.</summary>
public sealed record ItemParaAplicar(int? MarcacaoId, DateTime InstanteUtc, TipoDeMarcacao Tipo);

/// <summary>
/// "Correção pedida" em texto pronto (<c>resumoDaCorrecao</c>), com os horários em -03:00. Batida nova isolada: "Incluir a entrada às HH:mm" ou "Incluir a saída às HH:mm"; uma ENTRADA nova seguida de uma SAÍDA nova (em horário) é "Incluir o
/// período HH:mm - HH:mm"; batida existente: "Corrigir a entrada para HH:mm" ou "Corrigir a saída para HH:mm". Várias partes saem em ordem de horário, separadas por "; ".
/// </summary>
public static class ResumoDaCorrecao
{
    public static string Montar(IReadOnlyList<ItemParaAplicar> itens)
    {
        var partes = new List<(DateTime Quando, string Texto)>();
        foreach (var existente in itens.Where(i => i.MarcacaoId is not null))
        {
            partes.Add((existente.InstanteUtc, $"Corrigir a {Nome(existente.Tipo)} para {Hora(existente.InstanteUtc)}"));
        }

        var novas = itens.Where(i => i.MarcacaoId is null).OrderBy(i => i.InstanteUtc).ToList();
        for (var i = 0; i < novas.Count; i++)
        {
            if (novas[i].Tipo == TipoDeMarcacao.Entrada && i + 1 < novas.Count && novas[i + 1].Tipo == TipoDeMarcacao.Saida)
            {
                partes.Add((novas[i].InstanteUtc, $"Incluir o período {Hora(novas[i].InstanteUtc)} - {Hora(novas[i + 1].InstanteUtc)}"));
                i++;
            }
            else
            {
                partes.Add((novas[i].InstanteUtc, $"Incluir a {Nome(novas[i].Tipo)} às {Hora(novas[i].InstanteUtc)}"));
            }
        }

        return string.Join("; ", partes.OrderBy(p => p.Quando).Select(p => p.Texto));
    }

    private static string Hora(DateTime instanteUtc) => FusoBrasil.ParaLocal(instanteUtc).ToString("HH:mm", CultureInfo.InvariantCulture);

    private static string Nome(TipoDeMarcacao tipo) => tipo == TipoDeMarcacao.Entrada ? "entrada" : "saída";
}

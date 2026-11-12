namespace RHSystem.Application.Ponto.Regras;

/// <summary>Os períodos do dia como o contrato os mostra: cada batida com a posição no dia (a primeira é 1, por horário e depois por id) e o rótulo que a posição e o tipo dão.</summary>
public static class PeriodosDoDia
{
    public static IReadOnlyList<PontoPeriodo> De(IReadOnlyList<BatidaLida> batidas)
    {
        var marcacoes = batidas.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo)).ToList();
        var ordemPorId = marcacoes.OrderBy(m => m.InstanteUtc).ThenBy(m => m.Id).Select((m, i) => (m.Id, Ordem: i + 1)).ToDictionary(x => x.Id, x => x.Ordem);
        var porId = batidas.ToDictionary(b => b.Id);

        PontoMarcacao? Marcacao(MarcacaoDoDia? batida) =>
            batida is null
                ? null
                : new PontoMarcacao(batida.Id, ordemPorId[batida.Id], batida.Tipo, SequenciaDoDia.Rotulo(ordemPorId[batida.Id], batida.Tipo), batida.InstanteUtc, porId[batida.Id].Origem, porId[batida.Id].CorrigidaEmUtc);

        return [.. SequenciaDoDia.Periodos(marcacoes).Select(p => new PontoPeriodo(Marcacao(p.Entrada), Marcacao(p.Saida)))];
    }
}

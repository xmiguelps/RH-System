namespace RHSystem.Application.Colaboradores;

/// <summary>O que os indicadores do RH e do gestor têm em comum: o valor de um indicador é o PRIMEIRO que uma fonte registrada devolve; sem fonte (ou todas sem valor), <c>null</c>.</summary>
internal static class FontesDeIndicador
{
    public static async Task<int?> PrimeiroAsync(IEnumerable<IFonteDeIndicadorDaFicha> fontes, IndicadorDaFicha indicador, int funcionarioId, CancellationToken ct)
    {
        foreach (var fonte in fontes.Where(f => f.Indicador == indicador))
        {
            if (await fonte.ObterAsync(funcionarioId, ct) is { } valor)
            {
                return valor;
            }
        }

        return null;
    }
}

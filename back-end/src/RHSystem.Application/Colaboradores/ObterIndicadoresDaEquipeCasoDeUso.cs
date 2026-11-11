using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>obterIndicadoresDaEquipe</c>: os mesmos indicadores do RH (o primeiro valor das fontes registradas; <c>null</c> sem fonte), menos o próximo pagamento. Quem não é da equipe (ou não existe) é 404: a
/// pessoa é conferida pela mesma consulta da ficha, que já exclui o que está fora do escopo.
/// </summary>
public sealed class ObterIndicadoresDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe consulta, IEnumerable<IFonteDeIndicadorDaFicha> fontes)
{
    public async Task<FuncionarioIndicadoresGestor> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        _ = (await consulta.ObterFichaAsync(await escopo.ObterEquipeAsync(ct), funcionarioId, ct)).OuNaoEncontrado();
        var lista = fontes.ToList();
        return new FuncionarioIndicadoresGestor(
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.BancoDeHorasMinutos, funcionarioId, ct),
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.FeriasDisponiveisDias, funcionarioId, ct),
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.TreinamentosPendentes, funcionarioId, ct),
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.AdvertenciasQuantidade, funcionarioId, ct));
    }
}

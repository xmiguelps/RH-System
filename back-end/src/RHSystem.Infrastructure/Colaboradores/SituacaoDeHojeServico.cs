using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// Junta o relógio, o calendário e os fatos de cada pessoa na regra <see cref="RegraDeSituacaoDeHoje"/>. "Hoje" e a hora local são de Brasília (<see cref="FusoBrasil"/>):
/// às 02:30Z ainda é o dia anterior.
/// </summary>
public sealed class SituacaoDeHojeServico(IRelogio relogio, IFonteDeFatosDoDia fonte) : ISituacaoDeHoje
{
    private static readonly FatosDaPessoaNoDia SemFatos = new(false, false, false);

    public async Task<DiaDeHoje> ObterAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct)
    {
        var agoraUtc = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agoraUtc);
        var agoraLocal = TimeOnly.FromDateTime(FusoBrasil.ParaLocal(agoraUtc));

        var calendario = await fonte.ObterCalendarioAsync(hoje, ct);
        var fatos = await fonte.ObterPessoasAsync(funcionarioIds, hoje, ct);

        var situacoes = funcionarioIds
            .Distinct()
            .ToDictionary(id => id, id => RegraDeSituacaoDeHoje.Calcular(calendario, fatos.GetValueOrDefault(id, SemFatos), agoraLocal));
        return new DiaDeHoje(hoje, calendario.DiaComExpediente, situacoes);
    }
}

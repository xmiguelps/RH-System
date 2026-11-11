using System.Globalization;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// Monta o cartão "Seu ponto hoje" da pessoa: as batidas do dia do ponto (hoje, ou ontem quando a jornada atravessou a meia-noite e a SAÍDA de ontem ainda está por bater), a próxima batida, a jornada e o saldo do banco de horas.
/// "Hoje" é o dia em Brasília no instante do <see cref="IRelogio"/> (às 02:30Z ainda é o dia anterior); o relógio do navegador nunca decide nada.
/// </summary>
public sealed class MontadorDoCartaoDeHoje(IPontoRepositorio repositorio, ISaldoDoBancoDeHoras saldo, IConfiguracaoDaEmpresa empresa, IRelogio relogio)
{
    public async Task<PontoHoje> MontarAsync(int funcionarioId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var deHoje = await repositorio.BatidasDoDiaAsync(funcionarioId, hoje, ct);
        var deOntem = await repositorio.BatidasDoDiaAsync(funcionarioId, hoje.AddDays(-1), ct);

        var proxima = SequenciaDoDia.ProximaMarcacao(ComoMarcacoes(deHoje), ComoMarcacoes(deOntem), agora);
        var (diaDoPonto, batidas) = proxima.NoDiaAnterior ? (hoje.AddDays(-1), deOntem) : (hoje, deHoje);

        var configuracao = await empresa.ObterAsync(ct);
        var carga = await repositorio.CargaSemanalAsync(funcionarioId, ct);
        var banco = await saldo.ObterAsync(funcionarioId, ct);

        return new PontoHoje(
            diaDoPonto,
            agora,
            Jornada(carga, configuracao),
            Periodos(batidas),
            new PontoProximaMarcacao(proxima.Tipo, proxima.Rotulo, proxima.TextoDoBotao),
            new PontoBancoHoras(banco.SaldoMinutos, banco.AtualizadoAte));
    }

    /// <summary>A carga semanal do cargo em minutos e o dia = semanal ÷ 5 (44 h = 2640 e 528); o horário padrão "HH:mm" e a tolerância são da empresa. Cada grupo é <c>null</c> por inteiro quando falta a origem dele.</summary>
    internal static PontoJornada Jornada(decimal? cargaSemanalHoras, ConfiguracaoDaEmpresaLida? empresa)
    {
        int? semanal = cargaSemanalHoras is > 0 ? (int)Math.Round(cargaSemanalHoras.Value * 60m, 0, MidpointRounding.AwayFromZero) : null;
        int? diaria = semanal is null ? null : (int)Math.Round(semanal.Value / 5m, 0, MidpointRounding.AwayFromZero);
        return new PontoJornada(
            semanal,
            diaria,
            empresa?.HorarioEntrada.ToString("HH:mm", CultureInfo.InvariantCulture),
            empresa?.HorarioSaida.ToString("HH:mm", CultureInfo.InvariantCulture),
            empresa?.ToleranciaAtrasoMinutos);
    }

    internal static IReadOnlyList<PontoPeriodo> Periodos(IReadOnlyList<BatidaLida> batidas)
    {
        var marcacoes = ComoMarcacoes(batidas);
        var ordemPorId = marcacoes.OrderBy(m => m.InstanteUtc).ThenBy(m => m.Id).Select((m, i) => (m.Id, Ordem: i + 1)).ToDictionary(x => x.Id, x => x.Ordem);
        var porId = batidas.ToDictionary(b => b.Id);

        PontoMarcacao? Marcacao(MarcacaoDoDia? batida) =>
            batida is null ? null : new PontoMarcacao(batida.Id, ordemPorId[batida.Id], batida.Tipo, SequenciaDoDia.Rotulo(ordemPorId[batida.Id], batida.Tipo), batida.InstanteUtc, porId[batida.Id].Origem, porId[batida.Id].CorrigidaEmUtc);

        return [.. SequenciaDoDia.Periodos(marcacoes).Select(p => new PontoPeriodo(Marcacao(p.Entrada), Marcacao(p.Saida)))];
    }

    private static IReadOnlyList<MarcacaoDoDia> ComoMarcacoes(IReadOnlyList<BatidaLida> batidas) => [.. batidas.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo))];
}

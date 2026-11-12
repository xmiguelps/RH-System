using System.Globalization;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>
/// As contas dos resumos de advertência, sobre as linhas do ano (pela data do ocorrido). <b>Regra única de contagem</b>: advertência não cancelada (<c>PENDENTE</c>, <c>APLICADA</c> ou <c>CONTESTADA</c>) do ano; só as abas do RH (<c>contagens</c>) trazem as canceladas, como a lista faz.
/// Ordem por nome: sem diferenciar maiúsculas de minúsculas e, no empate, a ordem alfabética.
/// </summary>
public static class ResumosDeAdvertencia
{
    private static readonly StringComparer PorNome = StringComparer.Create(CultureInfo.InvariantCulture, ignoreCase: true);

    public static AdvertenciaResumoRh DoRh(int ano, IReadOnlyList<AdvertenciaParaResumoRh> doAno)
    {
        var contagens = new AdvertenciaContagensRh(
            doAno.Count,
            doAno.Count(a => a.Situacao == SituacaoDaAdvertencia.Pendente),
            doAno.Count(a => a.Situacao == SituacaoDaAdvertencia.Aplicada),
            doAno.Count(a => a.Situacao == SituacaoDaAdvertencia.Contestada),
            doAno.Count(a => a.Situacao == SituacaoDaAdvertencia.Cancelada));

        var setor = doAno
            .Where(a => a.Situacao != SituacaoDaAdvertencia.Cancelada && a.Setor is not null)
            .GroupBy(a => a.Setor!)
            .Select(g => new AdvertenciaSetorComMaisRegistros(g.Key, g.Count()))
            .OrderByDescending(s => s.Quantidade)
            .ThenBy(s => s.Setor, PorNome)
            .FirstOrDefault();
        return new AdvertenciaResumoRh(ano, contagens, setor);
    }

    public static AdvertenciaResumoGestor DaEquipe(int ano, IReadOnlyList<FuncionarioResumo> equipe, IReadOnlyList<AdvertenciaParaResumoDaEquipe> doAno)
    {
        var registradas = doAno.Where(a => a.Situacao != SituacaoDaAdvertencia.Cancelada).ToList();
        var pendentes = doAno.Where(a => a.Situacao == SituacaoDaAdvertencia.Pendente).ToList();
        var comPendencia = equipe.Where(p => pendentes.Any(a => a.FuncionarioId == p.Id)).OrderBy(p => p.Nome, PorNome).ThenBy(p => p.Id).ToList();
        var porPessoa = equipe
            .Select(p => new AdvertenciaPorPessoa(p, registradas.Count(a => a.FuncionarioId == p.Id)))
            .OrderByDescending(q => q.Quantidade)
            .ThenBy(q => q.Funcionario.Nome, PorNome)
            .ThenBy(q => q.Funcionario.Id)
            .ToList();
        return new AdvertenciaResumoGestor(
            ano,
            registradas.Count,
            new AdvertenciaAguardandoCiencia(pendentes.Count, comPendencia),
            registradas.Select(a => a.FuncionarioId).Distinct().Count(),
            equipe.Count,
            porPessoa);
    }
}

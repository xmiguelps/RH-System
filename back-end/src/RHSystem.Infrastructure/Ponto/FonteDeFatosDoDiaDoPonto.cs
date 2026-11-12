using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Os fatos do dia como o ponto os conhece (a fatia 3 substitui <c>FonteDeFatosDoDiaSemRegistros</c>): o dia é útil se não é fim de semana nem feriado que valha para a empresa; o limite de entrada é o horário padrão mais a
/// tolerância (sem a empresa configurada ninguém está atrasado); quem tem uma ENTRADA no dia trabalha, quem está em férias aprovadas ou tem atestado VALIDADO de dia inteiro cobrindo o dia fica de férias ou de atestado.
/// </summary>
public sealed class FonteDeFatosDoDiaDoPonto(AppDbContext db, ICalendarioDoPonto calendario, IFeriasDoPonto ferias, IConfiguracaoDaEmpresa empresa) : IFonteDeFatosDoDia
{
    public async Task<CalendarioDoDia> ObterCalendarioAsync(DateOnly dia, CancellationToken ct)
    {
        var doDia = await calendario.DiaAsync(dia, ct);
        var configuracao = await empresa.ObterAsync(ct);
        return new CalendarioDoDia(doDia.EhDiaUtil, LimiteDeEntrada(configuracao));
    }

    public async Task<IReadOnlyDictionary<int, FatosDaPessoaNoDia>> ObterPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly dia, CancellationToken ct)
    {
        var ids = funcionarioIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, FatosDaPessoaNoDia>();
        }

        var comEntrada = await (
            from m in db.Marcacoes.AsNoTracking()
            join p in db.PontosDosDias.AsNoTracking() on m.PontoId equals p.Id
            where m.Tipo == TipoDeMarcacao.Entrada && p.Dia == dia && ids.Contains(p.FuncionarioId)
            select p.FuncionarioId)
            .Distinct()
            .ToListAsync(ct);

        var comAtestado = await db.Atestados.AsNoTracking()
            .Where(a => a.Situacao == SituacaoDoAtestado.Validado && a.HoraInicio == null && a.DataInicio <= dia && a.DataFim >= dia && ids.Contains(a.FuncionarioId))
            .Select(a => a.FuncionarioId)
            .Distinct()
            .ToListAsync(ct);

        var deFerias = (await ferias.NoPeriodoAsync(dia, dia, ct)).Select(f => f.FuncionarioId).ToHashSet();

        var entraram = comEntrada.ToHashSet();
        var atestados = comAtestado.ToHashSet();
        return ids
            .Select(id => (Id: id, Fatos: new FatosDaPessoaNoDia(entraram.Contains(id), deFerias.Contains(id), atestados.Contains(id))))
            .Where(x => x.Fatos.JaRegistrouEntrada || x.Fatos.EmFerias || x.Fatos.ComAtestadoDeDiaInteiro)
            .ToDictionary(x => x.Id, x => x.Fatos);
    }

    /// <summary>O horário de entrada mais a tolerância; entrada às 23:50 com 20 minutos de tolerância não "dá a volta" na meia-noite (vale até o fim do dia).</summary>
    internal static TimeOnly? LimiteDeEntrada(ConfiguracaoDaEmpresaLida? configuracao)
    {
        if (configuracao is null)
        {
            return null;
        }

        var limite = configuracao.HorarioEntrada.ToTimeSpan() + TimeSpan.FromMinutes(configuracao.ToleranciaAtrasoMinutos);
        return limite >= TimeSpan.FromDays(1) ? TimeOnly.MaxValue : TimeOnly.FromTimeSpan(limite);
    }
}

using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Ponto;

/// <summary>O espelho de um mês já calculado: os dias do mês que a pessoa já viveu, a jornada e os cartões do topo.</summary>
public sealed record EspelhoDoMes(PessoaDoEspelho Pessoa, int Ano, int Mes, PontoJornada Jornada, PontoResumoMes Resumo, IReadOnlyList<DiaDoEspelho> Dias);

/// <summary>
/// Monta o espelho de ponto do mês de uma pessoa. Os dias vão do 1º do mês (ou da admissão, se for depois) até hoje (mês corrente) ou até o último dia (mês passado); mês futuro dá <c>dias: []</c>; nenhum dia antes da admissão nem depois do
/// desligamento. Sem <c>ano</c> e <c>mes</c> vale o mês atual (em -03:00); só um deles que falte pega o do mês atual. Cada dia é do <see cref="ConstrutorDoDiaDoEspelho"/>; aqui só se reúnem os fatos (batidas, feriados, férias, atestados, pedidos) em poucas consultas.
/// </summary>
public sealed class MontadorDoEspelho(IConsultaDoPonto consulta, ICalendarioDoPonto calendario, IFeriasDoPonto ferias, ISaldoDoBancoDeHoras saldo, IConfiguracaoDaEmpresa empresa, IRelogio relogio)
{
    public async Task<EspelhoDoMes> MontarAsync(PessoaDoEspelho pessoa, int? ano, int? mes, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var (anoDoMes, mesDoMes) = Periodo(ano, mes, hoje);
        var primeiro = new DateOnly(anoDoMes, mesDoMes, 1);
        var de = Maior(primeiro, pessoa.DataAdmissao);
        var ate = Menor(Menor(primeiro.AddMonths(1).AddDays(-1), hoje), pessoa.DataDesligamento ?? DateOnly.MaxValue);

        var configuracao = await empresa.ObterAsync(ct);
        var jornada = MontadorDoCartaoDeHoje.Jornada(pessoa.CargaSemanalHoras, configuracao);
        var banco = await saldo.ObterAsync(pessoa.Id, ct);
        var pendentes = await consulta.PedidosPendentesAsync([pessoa.Id], ct);

        var dias = new List<DiaDoEspelho>();
        if (de <= ate)
        {
            var batidas = (await consulta.BatidasAsync([pessoa.Id], de, ate, ct)).ToLookup(b => b.Dia, b => b.Batida);
            var feriados = (await calendario.FeriadosQueValemAsync(de, ate, ct)).GroupBy(f => f.Data).ToDictionary(g => g.Key, g => g.First().Nome);
            var deFerias = (await ferias.NoPeriodoAsync(de, ate, ct)).Where(f => f.FuncionarioId == pessoa.Id).ToList();
            var atestados = await consulta.AtestadosValidadosAsync([pessoa.Id], de, ate, ct);
            var pedidoPorDia = pendentes.GroupBy(p => p.Dia).ToDictionary(g => g.Key, g => g.First().PedidoId);

            for (var dia = de; dia <= ate; dia = dia.AddDays(1))
            {
                dias.Add(ConstrutorDoDiaDoEspelho.Construir(new DadosDoDiaDoEspelho(
                    dia,
                    hoje,
                    [.. batidas[dia]],
                    feriados.GetValueOrDefault(dia),
                    deFerias.Any(f => f.Inicio <= dia && dia <= f.Fim),
                    atestados.Any(a => a.HoraInicio is null && a.Inicio <= dia && dia <= a.Fim),
                    MinutosCobertosPorAtestadoDeHoras(atestados, dia),
                    pedidoPorDia.TryGetValue(dia, out var pedido) ? pedido : null,
                    jornada.CargaDiariaMinutos ?? 0,
                    configuracao?.HorarioEntrada,
                    configuracao?.ToleranciaAtrasoMinutos ?? 0)));
            }
        }

        var resumo = new PontoResumoMes(
            dias.Sum(d => d.HorasMinutos ?? 0),
            banco.SaldoMinutos,
            pendentes.Count,
            dias.Count(d => d.AtrasoMinutos is not null),
            dias.Count(d => d.Data < hoje && d.Situacao == SituacaoDoDiaNoPonto.SemRegistro));
        return new EspelhoDoMes(pessoa, anoDoMes, mesDoMes, jornada, resumo, dias);
    }

    /// <summary>Um dia só do espelho de uma pessoa (o que <c>corrigirMarcacoesDoDia</c> devolve): as mesmas regras do mês, com os fatos do dia lidos na hora.</summary>
    public async Task<DiaDoEspelho> MontarDiaAsync(PessoaDoEspelho pessoa, DateOnly dia, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var configuracao = await empresa.ObterAsync(ct);
        var jornada = MontadorDoCartaoDeHoje.Jornada(pessoa.CargaSemanalHoras, configuracao);
        var doCalendario = await calendario.DiaAsync(dia, ct);
        var batidas = (await consulta.BatidasAsync([pessoa.Id], dia, dia, ct)).Select(b => b.Batida).ToList();
        var deFerias = (await ferias.NoPeriodoAsync(dia, dia, ct)).Any(f => f.FuncionarioId == pessoa.Id);
        var atestados = await consulta.AtestadosValidadosAsync([pessoa.Id], dia, dia, ct);
        var pedido = (await consulta.PedidosPendentesAsync([pessoa.Id], ct)).Where(p => p.Dia == dia).Select(p => (int?)p.PedidoId).FirstOrDefault();

        return ConstrutorDoDiaDoEspelho.Construir(new DadosDoDiaDoEspelho(
            dia,
            hoje,
            batidas,
            doCalendario.NomeDoFeriado,
            deFerias,
            atestados.Any(a => a.HoraInicio is null && a.Inicio <= dia && dia <= a.Fim),
            MinutosCobertosPorAtestadoDeHoras(atestados, dia),
            pedido,
            jornada.CargaDiariaMinutos ?? 0,
            configuracao?.HorarioEntrada,
            configuracao?.ToleranciaAtrasoMinutos ?? 0));
    }

    /// <summary>Os minutos do dia cobertos por atestados VALIDADOS de algumas horas (fim menos início; o limite pela carga é do dia).</summary>
    internal static int MinutosCobertosPorAtestadoDeHoras(IReadOnlyList<AtestadoValidadoDoPonto> atestados, DateOnly dia) =>
        atestados.Where(a => a.HoraInicio is not null && a.HoraFim is not null && a.Inicio == dia).Sum(a => (int)(a.HoraFim!.Value.ToTimeSpan() - a.HoraInicio!.Value.ToTimeSpan()).TotalMinutes);

    /// <summary>O mês pedido (sem os dois, o mês atual); <c>ano</c> de 2000 a 2100 e <c>mes</c> de 1 a 12, senão 400.</summary>
    internal static (int Ano, int Mes) Periodo(int? ano, int? mes, DateOnly hoje)
    {
        var erros = new ColetorDeErros();
        if (ano is < 2000 or > 2100)
        {
            erros.Adicionar("ano", "O ano vai de 2000 a 2100.");
        }

        if (mes is < 1 or > 12)
        {
            erros.Adicionar("mes", "O mês vai de 1 a 12.");
        }

        erros.LancarSeHouver();
        return (ano ?? hoje.Year, mes ?? hoje.Month);
    }

    private static DateOnly Maior(DateOnly a, DateOnly b) => a >= b ? a : b;

    private static DateOnly Menor(DateOnly a, DateOnly b) => a <= b ? a : b;
}

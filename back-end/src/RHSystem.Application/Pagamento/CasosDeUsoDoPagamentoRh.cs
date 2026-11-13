using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Pagamento;

namespace RHSystem.Application.Pagamento;

/// <summary>O ano das rotas de pagamento: de 2020 a 2100 (o que o contrato aceita; o <c>YEAR</c> do banco aceita mais).</summary>
internal static class AnoDoPagamento
{
    public static bool Vale(int ano) => ano is >= ExcecaoDePagamento.AnoMinimo and <= ExcecaoDePagamento.AnoMaximo;

    public static void Conferir(int? ano)
    {
        if (ano is { } valor && !Vale(valor))
        {
            throw new ErroDeValidacaoException("ano", "Informe um ano de 2020 a 2100.");
        }
    }
}

/// <summary>
/// <c>GET /api/rh/pagamento/excecoes</c>: as exceções de pagamento em ordem de data, só do RH. Sem <c>ano</c>, as do mês atual em diante; com <c>ano</c> (2020 a 2100, senão 400), as daquele ano. Lista curta, sem paginação.
/// </summary>
public sealed class ListarExcecoesDePagamentoCasoDeUso(IExcecoesDePagamentoRepositorio repositorio, IRelogio relogio)
{
    public async Task<PagamentoExcecaoListaDto> ExecutarAsync(int? ano, CancellationToken ct)
    {
        AnoDoPagamento.Conferir(ano);
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var excecoes = await repositorio.ListarAsync(ano, ano is null ? (hoje.Year, hoje.Month) : null, ct);
        return new PagamentoExcecaoListaDto([.. excecoes.Select(e => new PagamentoExcecaoDto(e.Ano, e.Mes, e.Data))]);
    }
}

/// <summary>
/// <c>GET /api/rh/pagamento/calendario</c>: a data efetiva de pagamento dos 12 meses de um ano (padrão: o ano de hoje; 2020 a 2100, senão 400), com a mesma conta de <see cref="RegraDoPagamento.DoMes"/>: o dia da empresa, a exceção do mês e os feriados do calendário. Empresa sem dia de pagamento e mês sem
/// exceção: o mês não vem na lista. Os feriados vêm de 15 dias antes de 1º de janeiro (a antecipação pode voltar para o fim do mês anterior).
/// </summary>
public sealed class ObterCalendarioDePagamentoCasoDeUso(IConfiguracaoDaEmpresa empresa, IExcecoesDePagamentoRepositorio repositorio, ICalendarioDoPonto calendario, IRelogio relogio)
{
    public async Task<PagamentoCalendarioDto> ExecutarAsync(int? ano, CancellationToken ct)
    {
        AnoDoPagamento.Conferir(ano);
        var daqueleAno = ano ?? FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        var dia = (await empresa.ObterAsync(ct))?.DiaPagamento;
        var excecoes = (await repositorio.ListarAsync(daqueleAno, null, ct)).ToDictionary(e => e.Mes, e => e.Data);
        var feriados = (await calendario.FeriadosQueValemAsync(new DateOnly(daqueleAno, 1, 1).AddDays(-15), new DateOnly(daqueleAno, 12, 31), ct)).Select(f => f.Data).ToHashSet();

        var meses = new List<PagamentoDataDto>();
        for (var mes = 1; mes <= 12; mes++)
        {
            if (RegraDoPagamento.DoMes(daqueleAno, mes, dia, excecoes.TryGetValue(mes, out var data) ? data : null, feriados.Contains) is { } pagamento)
            {
                meses.Add(PagamentoDataDto.De(pagamento));
            }
        }

        return new PagamentoCalendarioDto(daqueleAno, meses);
    }
}

/// <summary>
/// <c>PUT /api/rh/pagamento/excecoes/{ano}/{mes}</c>: cadastra ou troca a data do mês (uma por mês e ano; só vale para aquele mês). 400 com o campo certo: <c>ano</c> fora de 2020 a 2100, <c>mes</c> fora de 1 a 12 e <c>dataPagamento</c> fora do mês (a regra do banco). Funciona sem a empresa ter <c>diaPagamento</c>; a data vale como
/// está, mesmo em fim de semana ou feriado.
/// </summary>
public sealed class DefinirExcecaoDePagamentoCasoDeUso(IExcecoesDePagamentoRepositorio repositorio)
{
    public async Task<PagamentoExcecaoDto> ExecutarAsync(int ano, int mes, PagamentoExcecaoComando corpo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        if (!AnoDoPagamento.Vale(ano))
        {
            erros.Adicionar("ano", "Informe um ano de 2020 a 2100.");
        }

        if (mes is < 1 or > 12)
        {
            erros.Adicionar("mes", "Informe um mês de 1 a 12.");
        }

        if (!erros.TemErros && (corpo.DataPagamento.Year != ano || corpo.DataPagamento.Month != mes))
        {
            erros.Adicionar("dataPagamento", $"A data do pagamento precisa cair em {mes:00}/{ano}.");
        }

        erros.LancarSeHouver();
        var gravada = await repositorio.DefinirAsync(ano, mes, corpo.DataPagamento, ct);
        return new PagamentoExcecaoDto(gravada.Ano, gravada.Mes, gravada.Data);
    }
}

/// <summary><c>DELETE /api/rh/pagamento/excecoes/{ano}/{mes}</c>: remove a exceção e o mês volta a seguir a regra. Exceção que não existe, e ano ou mês fora da faixa, é 404 (o contrato desta rota não tem 400).</summary>
public sealed class RemoverExcecaoDePagamentoCasoDeUso(IExcecoesDePagamentoRepositorio repositorio)
{
    public async Task ExecutarAsync(int ano, int mes, CancellationToken ct)
    {
        if (!AnoDoPagamento.Vale(ano) || mes is < 1 or > 12 || !await repositorio.RemoverAsync(ano, mes, ct))
        {
            throw new NaoEncontradoException();
        }
    }
}

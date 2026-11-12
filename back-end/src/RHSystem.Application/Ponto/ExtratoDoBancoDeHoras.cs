using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>Um lançamento do extrato como o contrato o mostra (<c>BancoHorasLancamento</c>): os minutos são sempre positivos e o sinal vem do tipo.</summary>
public sealed record PontoBancoDeHorasLancamento(int Id, DateOnly DiaReferencia, TipoDeLancamentoDoBanco Tipo, int QuantidadeMinutos, string Descricao);

/// <summary>O saldo e uma página do extrato (<c>BancoHorasExtrato</c>). <c>SaldoMinutos</c> é o de TODAS as linhas; <c>AtualizadoAte</c> é o último dia consolidado (global; <c>null</c> se a consolidação nunca rodou).</summary>
public sealed record PontoBancoDeHorasExtrato(IReadOnlyList<PontoBancoDeHorasLancamento> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, int SaldoMinutos, DateOnly? AtualizadoAte);

/// <summary>Os filtros do extrato: <c>de</c> e <c>ate</c> (inclusive, pelo dia de referência) e o <c>tipo</c>.</summary>
public sealed record FiltroDoExtrato(DateOnly? De, DateOnly? Ate, TipoDeLancamentoDoBanco? Tipo);

/// <summary>Uma linha do banco como o banco a guarda: <c>Horas</c> com 2 casas, que o caso de uso converte em minutos inteiros.</summary>
public sealed record LinhaDoExtrato(int Id, DateOnly DiaReferencia, TipoDeLancamentoDoBanco Tipo, decimal Horas, string? Observacao);

/// <summary>Uma página do extrato e o total de linhas dos filtros (para <c>totalPaginas</c>).</summary>
public sealed record PaginaDoExtrato(IReadOnlyList<LinhaDoExtrato> Itens, int Total);

/// <summary>A leitura do extrato de uma pessoa: do dia de referência mais recente para o mais antigo (ordem fixa; no mesmo dia, o id maior primeiro).</summary>
public interface IConsultaDoBancoDeHoras
{
    Task<PaginaDoExtrato> ListarAsync(int funcionarioId, FiltroDoExtrato filtro, ParametrosDeLista parametros, CancellationToken ct);
}

/// <summary>
/// <c>GET /api/eu/ponto/banco-de-horas</c> (qualquer perfil, só a própria pessoa) e <c>GET /api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas</c> (só o RH; pessoa que não existe é 404). O <c>saldoMinutos</c> soma os minutos DE CADA LINHA (convertida de horas com
/// arredondamento) de todos os lançamentos, nunca <c>FN_CALCULAR_SALDO_BANCO_HORAS</c> e nunca só os filtrados ou os da página. Todos os erros de filtro vêm juntos no 400 (<c>pagina</c>, <c>tamanhoPagina</c>, <c>tipo</c>, <c>ate</c>).
/// </summary>
public sealed class ObterExtratoDoBancoDeHorasCasoDeUso(IEscopoDoUsuario escopo, IConsultaDoPonto pessoas, IConsultaDoBancoDeHoras consulta, ISaldoDoBancoDeHoras saldo, IUltimoDiaConsolidado ultimoConsolidado)
{
    public Task<PontoBancoDeHorasExtrato> MeuAsync(int pagina, int tamanhoPagina, DateOnly? de, DateOnly? ate, string? tipo, CancellationToken ct) =>
        ExecutarAsync(escopo.FuncionarioId, pagina, tamanhoPagina, de, ate, tipo, ct);

    public async Task<PontoBancoDeHorasExtrato> DeFuncionarioAsync(int funcionarioId, int pagina, int tamanhoPagina, DateOnly? de, DateOnly? ate, string? tipo, CancellationToken ct)
    {
        _ = (await pessoas.PessoaAsync(funcionarioId, ct)).OuNaoEncontrado();
        return await ExecutarAsync(funcionarioId, pagina, tamanhoPagina, de, ate, tipo, ct);
    }

    private async Task<PontoBancoDeHorasExtrato> ExecutarAsync(int funcionarioId, int pagina, int tamanhoPagina, DateOnly? de, DateOnly? ate, string? tipo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? parametros = null;
        TipoDeLancamentoDoBanco? tipoLido = null;
        try
        {
            parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        }
        catch (ErroDeValidacaoException erro)
        {
            Juntar(erros, erro);
        }

        try
        {
            tipoLido = EnumsDeConsulta.Ler<TipoDeLancamentoDoBanco>(tipo, "tipo");
        }
        catch (ErroDeValidacaoException erro)
        {
            Juntar(erros, erro);
        }

        if (de is { } inicio && ate is { } fim && fim < inicio)
        {
            erros.Adicionar("ate", "A data final não pode ser antes da data inicial.");
        }

        erros.LancarSeHouver();

        var pagina1 = await consulta.ListarAsync(funcionarioId, new FiltroDoExtrato(de, ate, tipoLido), parametros!, ct);
        var saldos = await saldo.ObterSaldosAsync([funcionarioId], ct);
        var lista = RespostaPaginada<PontoBancoDeHorasLancamento>.De(
            [.. pagina1.Itens.Select(l => new PontoBancoDeHorasLancamento(l.Id, l.DiaReferencia, l.Tipo, LancamentoDoBancoDeHoras.HorasParaMinutos(l.Horas), l.Observacao ?? string.Empty))],
            pagina1.Total,
            parametros!);
        return new PontoBancoDeHorasExtrato(lista.Itens, lista.Pagina, lista.TamanhoPagina, lista.Total, lista.TotalPaginas, saldos[funcionarioId], await ultimoConsolidado.ObterAsync(ct));
    }

    private static void Juntar(ColetorDeErros destino, ErroDeValidacaoException erro)
    {
        foreach (var (campo, mensagens) in erro.Erros)
        {
            foreach (var mensagem in mensagens)
            {
                destino.Adicionar(campo, mensagem);
            }
        }
    }
}

using RHSystem.Application.Comum;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Ponto;

namespace RHSystem.Application.Ferias;

/// <summary>O que vale para o corpo de qualquer rota de férias que recebe datas: dentro de 2000 a 2100 (o resto da conta usa dias a mais e a menos), <c>dataFim</c> depois de <c>dataInicio</c> e o abono de 0 a 10.</summary>
public static class ValidacaoDeFerias
{
    public const int AnoMinimo = 2000;

    public const int AnoMaximo = 2100;

    public const int MaximoDoTexto = 500;

    /// <param name="campoDoTexto">O nome do campo de texto livre do corpo: <c>mensagem</c> no pedido da pessoa, <c>observacao</c> no registro do RH.</param>
    public static void Validar(DateOnly inicio, DateOnly fim, int diasAbono, string? texto, string campoDoTexto)
    {
        var erros = new ColetorDeErros();
        if (inicio.Year is < AnoMinimo or > AnoMaximo)
        {
            erros.Adicionar("dataInicio", "Informe uma data entre 2000 e 2100.");
        }

        if (fim.Year is < AnoMinimo or > AnoMaximo)
        {
            erros.Adicionar("dataFim", "Informe uma data entre 2000 e 2100.");
        }
        else if (fim < inicio)
        {
            erros.Adicionar("dataFim", "A data final não pode ser antes da inicial.");
        }

        if (diasAbono is < 0 or > 10)
        {
            erros.Adicionar("diasAbono", "Os dias de abono vão de 0 a 10.");
        }

        if (texto is not null && texto.Trim().Length > MaximoDoTexto)
        {
            erros.Adicionar(campoDoTexto, "O texto pode ter até 500 caracteres.");
        }

        erros.LancarSeHouver();
    }

    /// <summary>O texto livre como é gravado: sem espaços nas pontas e <c>null</c> quando fica vazio.</summary>
    public static string? TextoGravado(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}

/// <summary>
/// Monta e roda a conferência das regras de férias com o calendário de feriados (do início até 2 dias depois, o que a regra olha) e o saldo calculado das linhas da pessoa. É a MESMA conferência de pedir, conferir, aceitar a sugestão, sugerir e registrar; o que muda é o <see cref="ModoDaConferencia"/>.
/// <paramref name="ferias"/> vem SEM o pedido que está sendo conferido quando ele já existe.
/// </summary>
public sealed class ConferidorDeFerias(ICalendarioDoPonto calendario)
{
    public async Task<ConferenciaDeFerias> ConferirAsync(ComandoDeFerias comando, ModoDaConferencia modo, PessoaParaFerias pessoa, IReadOnlyList<FeriasLida> ferias, DateOnly hoje, DateOnly? inicioDoPeriodoFixo, CancellationToken ct)
    {
        var linhas = ferias.Select(f => f.ParaSaldo()).ToList();
        var saldo = SaldoDeFerias.De(pessoa.DataAdmissao, pessoa.DataDesligamento, hoje, linhas);
        var feriados = (await calendario.FeriadosQueValemAsync(comando.DataInicio, comando.DataInicio.AddDays(2), ct)).Select(f => f.Data).ToHashSet();
        return ConferenciaDeFerias.Conferir(comando, new ContextoDaConferencia(hoje, modo, saldo, linhas, feriados.Contains, inicioDoPeriodoFixo));
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// <c>REPETIR_FERIADO:ano=AAAA</c> ("Repetir todos os anos"): copia para o ano, com o mesmo nome, dia, mês, tipo, UF e município, os feriados ATIVOS marcados para repetir que existem em anos ANTERIORES (a origem é qualquer ano anterior, então um ano que a aplicação não rodou
/// não perde o feriado, e o 29/02 sai no próximo ano bissexto e não no dia 28). <b>Idempotente</b>: o que já existe naquela data com aquele nome (mesmo desativado pelo RH) não é recriado nem reativado; a conta é do banco, que não diferencia caixa nem acento. O ano vem do
/// <c>DiaReferencia</c> (1º de janeiro). A recuperação agenda o ano de hoje e o seguinte.
/// </summary>
public sealed class RepetirFeriadoTarefa(AppDbContext db, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "REPETIR_FERIADO";

    public string Tipo => TipoDaTarefa;

    public static string Chave(int ano) => ChaveDeIdempotencia.Montar(TipoDaTarefa, ("ano", ano));

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var ano = tarefa.DiaReferencia?.Year ?? throw new InvalidOperationException("A tarefa não traz o ano (DiaReferencia).");
        var inicioDoAno = new DateOnly(ano, 1, 1);
        var origens = await db.Feriados.AsNoTracking()
            .Where(f => f.RepeteTodoAno && f.Ativo && f.Data < inicioDoAno)
            .OrderBy(f => f.Data)
            .ToListAsync(ct);

        foreach (var origem in origens.DistinctBy(f => (f.Nome, f.Data.Month, f.Data.Day, f.Tipo, f.Uf, f.Municipio)))
        {
            if (origem.Data is { Month: 2, Day: 29 } && !DateTime.IsLeapYear(ano))
            {
                continue;
            }

            var data = new DateOnly(ano, origem.Data.Month, origem.Data.Day);
            var nome = origem.Nome;
            if (!await db.Feriados.AnyAsync(f => f.Data == data && f.Nome == nome, ct))
            {
                db.Feriados.Add(Feriado.Criar(nome, data, origem.Tipo, origem.Uf, origem.Municipio, repeteTodoAno: true));
                await db.SaveChangesAsync(ct); // um por vez: dois nomes que o banco vê como iguais não se chocam na mesma gravação
            }
        }
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var anoDeHoje = FusoBrasil.Hoje(agora).Year;
        foreach (var ano in new[] { anoDeHoje, anoDeHoje + 1 })
        {
            await agendador.AgendarAsync(new NovaTarefa(Tipo, Chave(ano), agora, DiaReferencia: new DateOnly(ano, 1, 1)), ct);
        }
    }
}

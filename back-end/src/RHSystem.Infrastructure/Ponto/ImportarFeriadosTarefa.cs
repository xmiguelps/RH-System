using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// <c>IMPORTAR_FERIADOS:ano=AAAA</c>: traz para o ano os feriados nacionais (calculados, com a Páscoa) e os estaduais da UF da empresa (tabela de data fixa). O ano vem do <c>DiaReferencia</c> (1º de janeiro). <b>Idempotente</b>: o que já existe não entra de novo, por data e tipo
/// (e por UF no estadual): a carga do <c>V001</c> de 2026 tem os nomes sem acento e não pode ficar duplicada; um feriado nacional que o RH desativou também não volta. Quando a mesma data e o mesmo nome já existem com outro tipo (um municipal com o nome do nacional), a
/// chave única do banco manda e o feriado fica como está. Sem a empresa configurada entram só os nacionais; UF sem feriado fixo na tabela não ganha estadual. A recuperação (na subida e a cada hora) agenda o ano de hoje e o seguinte.
/// </summary>
public sealed class ImportarFeriadosTarefa(AppDbContext db, IConfiguracaoDaEmpresa empresa, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "IMPORTAR_FERIADOS";

    public string Tipo => TipoDaTarefa;

    public static string Chave(int ano) => ChaveDeIdempotencia.Montar(TipoDaTarefa, ("ano", ano));

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var ano = tarefa.DiaReferencia?.Year ?? throw new InvalidOperationException("A tarefa não traz o ano (DiaReferencia).");
        var configuracao = await empresa.ObterAsync(ct);

        var candidatos = FeriadosNacionais.Do(ano).Select(f => (f.Nome, f.Data, Tipo: TipoDeFeriado.Nacional, Uf: (string?)null))
            .Concat(TabelaDeFeriadosEstaduais.Da(configuracao?.Uf, ano).Select(f => (f.Nome, f.Data, Tipo: TipoDeFeriado.Estadual, Uf: (string?)configuracao!.Uf.Trim().ToUpperInvariant())));
        foreach (var (nome, data, tipo, uf) in candidatos)
        {
            if (!await db.Feriados.AnyAsync(f => f.Data == data && (f.Nome == nome || (f.Tipo == tipo && f.Uf == uf)), ct))
            {
                db.Feriados.Add(Feriado.Criar(nome, data, tipo, uf, null, repeteTodoAno: false));
            }
        }

        await db.SaveChangesAsync(ct);
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

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Emails;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// <c>PUBLICAR_AVISO:aviso={id}:em={instante}</c>: o que acontece na hora da publicação (agora ou na data agendada). <b>Relê o aviso</b> e só age se ele ainda existe, está no ar e foi publicado exatamente naquele instante (editado,
/// excluído, encerrado ou reagendado nesse meio tempo, a tarefa antiga não faz nada). Completa a linha de leitura de quem entrou depois da criação do aviso e enfileira o e-mail <c>AVISO_PUBLICADO:aviso={id}:func={id}</c> de cada pessoa
/// ativa, admitida e com e-mail corporativo (quem não tem e-mail é pulado). <b>Idempotente</b> (a linha de leitura é <c>INSERT IGNORE</c> e a chave do e-mail repetida é sucesso). A recuperação agenda o que ficou para trás: o aviso
/// agendado e o publicado nos últimos 7 dias sem tarefa (a fila e as tarefas concluídas somem em 90 e 30 dias, então reenfileirar tudo reenviaria).
/// </summary>
public sealed class PublicarAvisoTarefa(AppDbContext db, IFilaDeEmails fila, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDoEmail = "AVISO_PUBLICADO";
    private const int TamanhoDoLote = 100;
    private static readonly TimeSpan JanelaDeRecuperacao = TimeSpan.FromDays(7);

    public string Tipo => AgendamentoDoAviso.Tipo;

    public static string ChaveDoEmail(int avisoId, int funcionarioId) => ChaveDeIdempotencia.Montar(TipoDoEmail, ("aviso", avisoId), ("func", funcionarioId));

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var avisoId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o aviso (ReferenciaId).");
        var instante = AgendamentoDoAviso.InstanteDaChave(tarefa.ChaveDeIdempotencia) ?? throw new InvalidOperationException("A chave da tarefa não traz o instante da publicação.");
        var aviso = await db.Avisos.AsNoTracking().FirstOrDefaultAsync(a => a.Id == avisoId, ct);
        var agora = relogio.AgoraUtc;
        if (aviso is null || aviso.PublicacaoEmUtc != instante || RegraDaSituacaoDoAviso.De(aviso.Enviado, aviso.PublicacaoEmUtc, aviso.ExpiracaoEmUtc, agora) != SituacaoDoAviso.Ativo)
        {
            return;
        }

        // AUDITORIA-OK: completar as linhas de leitura é efeito do sistema sobre dado de controle (quem ainda não viu o aviso), sem fato de negócio a auditar; o INSERT IGNORE torna a tarefa idempotente.
        await db.Database.ExecuteSqlInterpolatedAsync(
            $@"INSERT IGNORE INTO AVISOS_VISUALIZACOES (ID_AVISO, ID_FUNCIONARIO, VISUALIZADO)
               SELECT {avisoId}, f.ID_FUNCIONARIO, FALSE FROM FUNCIONARIOS f
                WHERE f.ATIVO = TRUE
                  AND NOT EXISTS (SELECT 1 FROM AVISOS_VISUALIZACOES v WHERE v.ID_AVISO = {avisoId} AND v.ID_FUNCIONARIO = f.ID_FUNCIONARIO)",
            ct);

        await EnfileirarOsEmailsAsync(aviso, FusoBrasil.Hoje(agora), ct);
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var desde = relogio.AgoraUtc - JanelaDeRecuperacao;
        var avisos = await db.Avisos.AsNoTracking().Where(a => a.Enviado && a.PublicacaoEmUtc != null && a.PublicacaoEmUtc >= desde).Select(a => new { a.Id, a.PublicacaoEmUtc }).ToListAsync(ct);
        foreach (var aviso in avisos)
        {
            var publicacao = aviso.PublicacaoEmUtc!.Value;
            await agendador.AgendarAsync(new NovaTarefa(Tipo, AgendamentoDoAviso.Chave(aviso.Id, publicacao), publicacao, ReferenciaId: aviso.Id), ct);
        }
    }

    private async Task EnfileirarOsEmailsAsync(Aviso aviso, DateOnly hoje, CancellationToken ct)
    {
        var previa = PreviaDoAviso.Montar(aviso.Tipo ?? TipoDeAviso.Simples, aviso.Conteudo);
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(f => f.Ativo && f.EmailCorporativo != null && f.DataAdmissao <= hoje)
            .OrderBy(f => f.Id)
            .Select(f => new { f.Id, f.Nome, f.EmailCorporativo })
            .ToListAsync(ct);

        foreach (var lote in pessoas.Chunk(TamanhoDoLote))
        {
            await fila.EnfileirarLoteAsync(
                [.. lote.Select(p => new EmailParaEnfileirar(TipoDoEmail, ChaveDoEmail(aviso.Id, p.Id), p.EmailCorporativo!, new { nome = p.Nome, titulo = aviso.Titulo, previa }, FuncionarioId: p.Id, ReferenciaId: aviso.Id))],
                ct);
        }
    }
}

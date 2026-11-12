using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// <c>PUBLICAR_HOLERITE:holerite={id}</c>: o e-mail de um holerite na hora em que ele fica visível (agora, ou no começo do dia agendado). <b>Relê o holerite</b> e só manda se ele existe, já está visível e é a <b>maior versão já publicada</b> da competência (uma
/// versão mais nova já publicada torna este e-mail inútil), e só para pessoa ativa com e-mail corporativo. A mensagem não leva valor nenhum do holerite (nem salário, nem o arquivo): só diz que ele está disponível. <b>Idempotente</b> pela chave do e-mail
/// (<c>HOLERITE_PUBLICADO:holerite={id}</c>). A recuperação agenda o que ficou sem tarefa: os agendados e os publicados nos últimos 7 dias.
/// </summary>
public sealed class PublicarHoleriteTarefa(AppDbContext db, IFilaDeEmails fila, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDoEmail = "HOLERITE_PUBLICADO";
    private static readonly TimeSpan JanelaDeRecuperacao = TimeSpan.FromDays(7);

    public string Tipo => AgendamentoDoHolerite.Tipo;

    public static string ChaveDoEmail(int holeriteId) => ChaveDeIdempotencia.Montar(TipoDoEmail, ("holerite", holeriteId));

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var holeriteId = tarefa.ReferenciaId ?? throw new InvalidOperationException("A tarefa não traz o holerite (ReferenciaId).");
        var agora = relogio.AgoraUtc;
        var holerite = await db.Holerites.AsNoTracking().FirstOrDefaultAsync(h => h.Id == holeriteId, ct);
        if (holerite is null || holerite.PublicacaoEmUtc is not { } publicacao || publicacao > agora)
        {
            return;
        }

        var haMaisNovaPublicada = await db.Holerites.AsNoTracking().AnyAsync(
            o => o.FuncionarioId == holerite.FuncionarioId && o.Ano == holerite.Ano && o.Mes == holerite.Mes && o.Tipo == holerite.Tipo && o.Versao > holerite.Versao && o.PublicacaoEmUtc <= agora,
            ct);
        var pessoa = await db.Funcionarios.AsNoTracking().Where(f => f.Id == holerite.FuncionarioId && f.Ativo && f.EmailCorporativo != null).Select(f => new { f.Id, f.Nome, f.EmailCorporativo }).FirstOrDefaultAsync(ct);
        if (haMaisNovaPublicada || pessoa is null)
        {
            return;
        }

        await fila.EnfileirarAsync(
            new EmailParaEnfileirar(TipoDoEmail, ChaveDoEmail(holerite.Id), pessoa.EmailCorporativo!, new { nome = pessoa.Nome, competencia = MensagensDaComunicacao.TextoDaCompetencia(holerite.Tipo, holerite.Mes, holerite.Ano), versao = holerite.Versao }, FuncionarioId: pessoa.Id, ReferenciaId: holerite.Id),
            ct);
    }

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var desde = relogio.AgoraUtc - JanelaDeRecuperacao;
        var holerites = await db.Holerites.AsNoTracking().Where(h => h.PublicacaoEmUtc != null && h.PublicacaoEmUtc >= desde).Select(h => new { h.Id, h.PublicacaoEmUtc }).ToListAsync(ct);
        foreach (var holerite in holerites)
        {
            await agendador.AgendarAsync(new NovaTarefa(Tipo, AgendamentoDoHolerite.Chave(holerite.Id), holerite.PublicacaoEmUtc!.Value, ReferenciaId: holerite.Id), ct);
        }
    }
}

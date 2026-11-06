using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Agendamento;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Acesso;

/// <summary>
/// <c>LIMPAR_TOKENS_DE_SENHA</c>: apaga de <c>USUARIOS_TOKENS_SENHA</c> o que foi criado há mais de <c>Retencao:TokensDeSenhaDias</c> (30) e já não serve: usado, invalidado ou vencido. <b>Nunca</b> apaga um token
/// em aberto ainda válido (a pessoa pode estar com o e-mail aberto) nem o ÚLTIMO <c>CONVITE</c> de cada pessoa (decisão 12: é o que diz que ela já foi convidada, mesmo depois de usado ou vencido); o convite
/// antigo de quem tem um mais novo sai. O hash do token não é dado pessoal, mas a linha não tem motivo para ficar para sempre. A recuperação agenda uma por dia (<c>LIMPAR_TOKENS_DE_SENHA:dia=AAAA-MM-DD</c>).
/// </summary>
public sealed class LimparTokensDeSenhaTarefa(AppDbContext db, IAgendador agendador, IRelogio relogio, OpcoesDeRetencao retencao) : ITarefaAgendada
{
    public const string TipoDaTarefa = "LIMPAR_TOKENS_DE_SENHA";

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var corte = agora.AddDays(-retencao.TokensDeSenhaDias);

        // O último convite de cada pessoa (o mais recente; no empate, o de maior id) fica. Calculado aqui, em memória: o MySQL não deixa o DELETE consultar a própria tabela.
        var convites = await db.UsuariosTokensSenha.AsNoTracking()
            .Where(t => t.Tipo == TipoDeTokenDeSenha.Convite)
            .Select(t => new { t.Id, t.UsuarioId, t.CriadoEm })
            .ToListAsync(ct);
        var ultimosConvites = convites
            .GroupBy(c => c.UsuarioId)
            .Select(g => g.OrderByDescending(c => c.CriadoEm).ThenByDescending(c => c.Id).First().Id)
            .ToList();

        var apagaveis = db.UsuariosTokensSenha
            .Where(t => t.CriadoEm < corte
                && !(t.UsadoEm == null && t.InvalidadoEm == null && t.ExpiraEm > agora)
                && !ultimosConvites.Contains(t.Id));

        // AUDITORIA-OK: tabela técnica, sem registro por decisão (limpeza de retenção: o hash do token não tem motivo para ficar)
        await apagaveis.ExecuteDeleteAsync(ct);
    }
}

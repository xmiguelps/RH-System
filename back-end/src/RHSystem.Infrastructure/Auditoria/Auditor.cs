using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Auditoria;

/// <summary>
/// O <see cref="IAuditor"/>. <see cref="Registrar"/> e <see cref="RegistrarComo"/> usam o <see cref="AppDbContext"/> da requisição: o evento é só junto ao
/// contexto e sai no próximo <c>SalvarAsync</c>, na mesma conexão e transação do fato. Os três <c>RegistrarEGravar*</c> usam um contexto NOVO da
/// <see cref="IDbContextFactory{TContext}"/> e recusam transação aberta: abrir segunda conexão enquanto a transação do chamador segura trava na linha da pessoa
/// faria a segunda esperar o limite do InnoDB (50 s) e a requisição virar 500. Todo instante vem do <see cref="IRelogio"/>.
/// </summary>
public sealed class Auditor(
    AppDbContext db,
    IDbContextFactory<AppDbContext> fabrica,
    IAtorAtual ator,
    IRelogio relogio,
    ContextoDeAuditoria contexto) : IAuditor
{
    public void Registrar(EventoDeAuditoriaNovo evento) =>
        db.EventosDeAuditoria.Add(ConstrutorDeEvento.Criar(relogio.AgoraUtc, DadosDoAtor.De(ator), evento));

    public void RegistrarComo(AtorDoEvento informado, EventoDeAuditoriaNovo evento) =>
        db.EventosDeAuditoria.Add(ConstrutorDeEvento.Criar(relogio.AgoraUtc, DadosDoAtor.De(informado, ator.Ip), evento));

    public async Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct)
    {
        ExigirForaDeTransacao();
        await GravarAsync(ConstrutorDeEvento.Criar(relogio.AgoraUtc, DadosDoAtor.De(ator), evento), ct);
    }

    public async Task RegistrarEGravarComoAsync(AtorDoEvento informado, EventoDeAuditoriaNovo evento, CancellationToken ct)
    {
        ExigirForaDeTransacao();
        await GravarAsync(ConstrutorDeEvento.Criar(relogio.AgoraUtc, DadosDoAtor.De(informado, ator.Ip), evento), ct);
    }

    public async Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento informado, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct)
    {
        ExigirForaDeTransacao();
        var agora = relogio.AgoraUtc;
        var desde = agora - janela;

        await using (var consulta = await fabrica.CreateDbContextAsync(ct))
        {
            var jaTem = await consulta.EventosDeAuditoria.AnyAsync(
                e => e.FuncionarioAtorId == informado.FuncionarioId && e.Area == evento.Area && e.Acao == evento.Acao && e.Descricao == evento.Descricao && e.Quando > desde,
                ct);
            if (jaTem)
            {
                return false;
            }
        }

        await GravarAsync(ConstrutorDeEvento.Criar(agora, DadosDoAtor.De(informado, ator.Ip), evento), ct);
        return true;
    }

    public IDisposable SuspenderRegistroAutomatico() => contexto.Suspender();

    private void ExigirForaDeTransacao()
    {
        if (db.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Dentro de transação use Registrar ou RegistrarComo.");
        }
    }

    private async Task GravarAsync(EventoDeAuditoria evento, CancellationToken ct)
    {
        await using var proprio = await fabrica.CreateDbContextAsync(ct);
        proprio.EventosDeAuditoria.Add(evento);
        await proprio.SaveChangesAsync(ct);
    }
}

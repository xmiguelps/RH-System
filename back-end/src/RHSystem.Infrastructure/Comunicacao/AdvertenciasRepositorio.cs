using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// A gravação das advertências. O que o trigger recusa vira o erro do contrato aqui mesmo (<see cref="TradutorDeRecusaDaComunicacao"/>): pessoa inativa (<c>FUNCIONARIO_INATIVO</c>) e gestor que adverte a si mesmo. Cada mudança de situação é um UPDATE condicional na situação de
/// partida: duas abas (ou ciência e contestação ao mesmo tempo) têm um efeito só, e o que o banco diz que mudou é a verdade. A data e o texto da contestação vêm da aplicação (o <c>IRelogio</c>, cortado ao segundo).
/// </summary>
public sealed class AdvertenciasRepositorio(AppDbContext db) : IAdvertenciasRepositorio
{
    public async Task<int> AdicionarAsync(Advertencia advertencia, CancellationToken ct)
    {
        db.Advertencias.Add(advertencia);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro)
        {
            var problema = TradutorDeRecusaDaComunicacao.Traduzir(erro, new ContextoDaComunicacao(OperacaoDaComunicacao.RegistrarAdvertencia));
            if (problema is null)
            {
                throw;
            }

            db.ChangeTracker.Clear();
            throw problema;
        }

        return advertencia.Id;
    }

    public async Task<bool> ConfirmarCienciaAsync(int advertenciaId, int funcionarioId, DateTime agoraUtc, CancellationToken ct) =>
        // AUDITORIA-OK: o evento ADVERTENCIAS/ALTEROU é gravado pelo caso de uso na mesma transação
        await db.Advertencias
            .Where(a => a.Id == advertenciaId && a.FuncionarioId == funcionarioId && a.Situacao == SituacaoDaAdvertencia.Pendente)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Situacao, SituacaoDaAdvertencia.Aplicada).SetProperty(a => a.CienciaEmUtc, InstanteAoSegundo.De(agoraUtc)), ct) == 1;

    public async Task<bool> ContestarAsync(int advertenciaId, int funcionarioId, string texto, DateTime agoraUtc, CancellationToken ct) =>
        // AUDITORIA-OK: o evento ADVERTENCIAS/ALTEROU é gravado pelo caso de uso na mesma transação
        await db.Advertencias
            .Where(a => a.Id == advertenciaId && a.FuncionarioId == funcionarioId && a.Situacao == SituacaoDaAdvertencia.Pendente)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.Situacao, SituacaoDaAdvertencia.Contestada).SetProperty(a => a.TextoContestacao, texto).SetProperty(a => a.ContestacaoEmUtc, InstanteAoSegundo.De(agoraUtc)),
                ct) == 1;

    public async Task<bool> ResponderAsync(int advertenciaId, DecisaoDaResposta decisao, CancellationToken ct)
    {
        var nova = RegrasDaAdvertencia.SituacaoDaDecisao(decisao);

        // AUDITORIA-OK: o evento ADVERTENCIAS/ALTEROU (com a resposta no VALOR_DEPOIS) é gravado pelo caso de uso na mesma transação
        return await db.Advertencias
            .Where(a => a.Id == advertenciaId && a.Situacao == SituacaoDaAdvertencia.Contestada)
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.Situacao, nova), ct) == 1;
    }
}

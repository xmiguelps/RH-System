using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto.Atestados;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// As gravações do atestado. A decisão é um <c>UPDATE</c> condicional em <c>STATUS_ATESTADO = 'ENVIADO'</c>: de duas decisões ao mesmo tempo só uma altera a linha (a outra vê zero linhas e perde com o 409). O <c>CHK_ATESTADOS_DECISAO</c> do banco exige o RH e o instante junto da decisão
/// e o <c>CHK_ATESTADOS_RECUSA_COM_MOTIVO</c> o motivo na recusa.
/// </summary>
public sealed class AtestadosRepositorio(AppDbContext db) : IAtestadosRepositorio
{
    public async Task<int> AdicionarAsync(Atestado atestado, CancellationToken ct)
    {
        db.Atestados.Add(atestado);
        await db.SaveChangesAsync(ct);
        return atestado.Id;
    }

    public async Task<bool> DecidirAsync(int atestadoId, SituacaoDoAtestado decisao, int funcionarioRhId, DateTime agoraUtc, string? motivo, CancellationToken ct)
    {
        var instante = MarcacaoDePonto.AoSegundo(agoraUtc);

        // AUDITORIA-OK: o evento ATESTADOS/APROVOU ou RECUSOU, com a pessoa do RH como ator, é registrado pelo caso de uso na mesma transação (o atestado não é uma entidade auditada por interceptor).
        var alteradas = await db.Atestados
            .Where(a => a.Id == atestadoId && a.Situacao == SituacaoDoAtestado.Enviado)
            .ExecuteUpdateAsync(
                s => s.SetProperty(a => a.Situacao, decisao)
                    .SetProperty(a => a.FuncionarioRhId, funcionarioRhId)
                    .SetProperty(a => a.DecididoEmUtc, instante)
                    .SetProperty(a => a.MotivoRecusa, decisao == SituacaoDoAtestado.Recusado ? motivo : null),
                ct);
        return alteradas == 1;
    }
}

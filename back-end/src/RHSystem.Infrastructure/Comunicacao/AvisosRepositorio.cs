using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comunicacao.Avisos;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// A gravação dos avisos. Publicar, encerrar, excluir e editar são <c>UPDATE</c>/<c>DELETE</c> condicionais ao estado que o caso de uso leu (o banco decide a corrida entre dois cliques e entre dois RHs); como o <c>ExecuteUpdate</c> não passa pelo
/// interceptor do EF, cada chamada leva o <c>AUDITORIA-OK</c> e o caso de uso grava o evento à mão, na mesma transação.
/// </summary>
public sealed class AvisosRepositorio(AppDbContext db) : IAvisosRepositorio
{
    public async Task<int> AdicionarAsync(Aviso aviso, CancellationToken ct)
    {
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync(ct);

        if (aviso.PublicacaoEmUtc is null)
        {
            // AUDITORIA-OK: a criação é auditada à mão pelo caso de uso; o trigger de INSERT pôs o NOW() do servidor na data nula (inclusive no rascunho) e este UPDATE a devolve para NULL, que é o que o contrato mostra.
            await db.Avisos.Where(a => a.Id == aviso.Id).ExecuteUpdateAsync(s => s.SetProperty(a => a.PublicacaoEmUtc, (DateTime?)null), ct);
        }

        return aviso.Id;
    }

    public async Task<bool> AtualizarAsync(int avisoId, bool enviadoLido, string titulo, string conteudo, bool fixado, DateTime? publicacaoEmUtc, DateTime? expiracaoEmUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: a edição é auditada à mão pelo caso de uso (evento Alterou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
        var linhas = await db.Avisos.Where(a => a.Id == avisoId && a.Enviado == enviadoLido).ExecuteUpdateAsync(
            s => s.SetProperty(a => a.Titulo, titulo)
                .SetProperty(a => a.Conteudo, conteudo)
                .SetProperty(a => a.Fixado, fixado)
                .SetProperty(a => a.PublicacaoEmUtc, publicacaoEmUtc)
                .SetProperty(a => a.ExpiracaoEmUtc, expiracaoEmUtc),
            ct);
        return linhas == 1;
    }

    public async Task<bool> PublicarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: a publicação é auditada à mão pelo caso de uso (evento Publicou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
        var linhas = await db.Avisos.Where(a => a.Id == avisoId && !a.Enviado).ExecuteUpdateAsync(s => s.SetProperty(a => a.Enviado, true).SetProperty(a => a.PublicacaoEmUtc, publicacaoEmUtc), ct);
        return linhas == 1;
    }

    public async Task<bool> EncerrarAsync(int avisoId, DateTime agoraUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: o encerramento é auditado à mão pelo caso de uso (evento Alterou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
        var linhas = await db.Avisos
            .Where(a => a.Id == avisoId && a.Enviado && (a.PublicacaoEmUtc == null || a.PublicacaoEmUtc <= agoraUtc) && (a.ExpiracaoEmUtc == null || a.ExpiracaoEmUtc > agoraUtc))
            .ExecuteUpdateAsync(s => s.SetProperty(a => a.ExpiracaoEmUtc, agoraUtc), ct);
        return linhas == 1;
    }

    public async Task<bool> ExcluirAsync(int avisoId, DateTime agoraUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: a exclusão é auditada à mão pelo caso de uso (evento Excluiu, na mesma transação), e o ExecuteDelete não passa pelo interceptor do EF. As leituras saem primeiro (a chave estrangeira não tem ON DELETE) e só se o aviso ainda não está publicado.
        await db.LeiturasDeAviso.Where(l => l.AvisoId == avisoId && db.Avisos.Any(a => a.Id == avisoId && (!a.Enviado || a.PublicacaoEmUtc > agoraUtc))).ExecuteDeleteAsync(ct);

        // AUDITORIA-OK: a exclusão é auditada à mão pelo caso de uso (evento Excluiu, na mesma transação), e o ExecuteDelete não passa pelo interceptor do EF.
        var linhas = await db.Avisos.Where(a => a.Id == avisoId && (!a.Enviado || a.PublicacaoEmUtc > agoraUtc)).ExecuteDeleteAsync(ct);
        return linhas == 1;
    }
}

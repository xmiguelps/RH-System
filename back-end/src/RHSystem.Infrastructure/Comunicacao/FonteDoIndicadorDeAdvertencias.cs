using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>
/// O indicador "Advertências" da ficha (<c>advertenciasQuantidade</c>): as advertências NÃO canceladas do ano corrente (no horário do Brasil) da pessoa. É a regra única de contagem das advertências (a mesma do resumo do gestor); a lista da ficha traz todas, de todos os anos e
/// inclusive as canceladas, então o total dela pode ser maior que este número.
/// </summary>
public sealed class FonteDoIndicadorDeAdvertencias(AppDbContext db, IRelogio relogio) : IFonteDeIndicadorDaFicha
{
    public IndicadorDaFicha Indicador => IndicadorDaFicha.AdvertenciasQuantidade;

    public async Task<int?> ObterAsync(int funcionarioId, CancellationToken ct)
    {
        var ano = FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        var primeiro = new DateOnly(ano, 1, 1);
        var ultimo = new DateOnly(ano, 12, 31);
        return await db.Advertencias.AsNoTracking().CountAsync(
            a => a.FuncionarioId == funcionarioId && a.Data >= primeiro && a.Data <= ultimo && (a.Situacao == SituacaoDaAdvertencia.Pendente || a.Situacao == SituacaoDaAdvertencia.Aplicada || a.Situacao == SituacaoDaAdvertencia.Contestada),
            ct);
    }
}

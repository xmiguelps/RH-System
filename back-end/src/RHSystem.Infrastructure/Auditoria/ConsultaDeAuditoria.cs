using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Auditoria;

/// <summary>
/// A leitura do log. Ordem fixa: <c>Quando</c> e depois <c>Id</c>, os dois decrescentes (o <c>Id</c> desempata eventos do mesmo instante, senão a
/// paginação repetiria ou perderia linha). <c>desligado</c> do ator vem da ficha DE HOJE (<c>Funcionario.Ativo</c>), enquanto o perfil é o gravado com o evento.
/// </summary>
public sealed class ConsultaDeAuditoria(AppDbContext db) : IConsultaDeAuditoria
{
    private IQueryable<EventoDeAuditoria> Filtrar(FiltroDeAuditoria filtro)
    {
        var consulta = db.EventosDeAuditoria.AsNoTracking().Where(e => e.Quando >= filtro.DeUtc && e.Quando < filtro.AteUtcExclusivo);

        if (filtro.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            consulta = consulta.Where(e => EF.Functions.Like(e.AtorNome, like) || EF.Functions.Like(e.Descricao, like));
        }

        if (filtro.Area is { } area)
        {
            consulta = consulta.Where(e => e.Area == area);
        }

        if (filtro.Acao is { } acao)
        {
            consulta = consulta.Where(e => e.Acao == acao);
        }

        if (filtro.ApenasAlteracoes)
        {
            // Fora: login, acesso negado, download e a marcação de ponto. Entra o resto (criar, alterar, excluir, inativar, pedir ajuste, aprovar...).
            consulta = consulta.Where(e =>
                e.Acao != AuditoriaAcao.Entrou
                && e.Acao != AuditoriaAcao.AcessoNegado
                && e.Acao != AuditoriaAcao.Baixou
                && !(e.Area == AuditoriaArea.Ponto && e.Acao == AuditoriaAcao.Registrou));
        }

        return consulta.OrderByDescending(e => e.Quando).ThenByDescending(e => e.Id);
    }

    public Task<RespostaPaginada<AuditoriaEvento>> ListarAsync(FiltroDeAuditoria filtro, ParametrosDeLista paginacao, CancellationToken ct) =>
        Filtrar(filtro).ToRespostaPaginadaAsync(
            paginacao,
            e => new AuditoriaEvento(
                e.Id,
                e.Quando,
                new AuditoriaAtor(
                    e.FuncionarioAtorId,
                    e.AtorNome,
                    e.AtorTipo,
                    e.AtorPerfil,
                    e.FuncionarioAtorId != null && db.Funcionarios.Any(f => f.Id == e.FuncionarioAtorId && !f.Ativo)),
                e.Area,
                e.Acao,
                e.Descricao),
            ct);

    public async Task<AuditoriaEventoDetalhe?> ObterAsync(long id, CancellationToken ct)
    {
        var evento = await db.EventosDeAuditoria.AsNoTracking().Include(e => e.Mudancas).FirstOrDefaultAsync(e => e.Id == id, ct);
        if (evento is null)
        {
            return null;
        }

        var desligado = evento.FuncionarioAtorId is { } funcionarioId && await db.Funcionarios.AnyAsync(f => f.Id == funcionarioId && !f.Ativo, ct);
        return new AuditoriaEventoDetalhe(
            evento.Id,
            evento.Quando,
            new AuditoriaAtor(evento.FuncionarioAtorId, evento.AtorNome, evento.AtorTipo, evento.AtorPerfil, desligado),
            evento.Area,
            evento.Acao,
            evento.Descricao,
            evento.RegistroAfetado,
            new AuditoriaOrigem(evento.Canal, evento.Ip),
            [.. evento.Mudancas.OrderBy(m => m.Ordem).Select(m => new AuditoriaMudanca(m.Campo, m.ValorAntes, m.ValorDepois, m.ValorOculto))]);
    }

    public async Task<IReadOnlyList<LinhaDeExportacaoDeAuditoria>> ListarParaExportacaoAsync(FiltroDeAuditoria filtro, CancellationToken ct) =>
        await Filtrar(filtro)
            .Select(e => new LinhaDeExportacaoDeAuditoria(e.Quando, e.AtorNome, e.AtorTipo, e.AtorPerfil, e.Area, e.Acao, e.Descricao, e.RegistroAfetado, e.Canal, e.Ip))
            .ToListAsync(ct);
}

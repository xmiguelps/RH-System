using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Ponto.Atestados;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// As leituras dos atestados. O escopo sai na PRIMEIRA consulta (<see cref="EscopoDeAtestados"/>): a pessoa vê os dela, o gestor os da equipe dele (sem o próprio gestor; nenhum se não tem equipe) e o RH todos. Mais recente primeiro: o instante de envio e, no empate, o id maior. A
/// <c>busca</c> procura no nome da pessoa.
/// </summary>
public sealed class ConsultaDeAtestados(AppDbContext db) : IConsultaDeAtestados
{
    public async Task<AtestadoLido?> ObterAsync(int atestadoId, EscopoDeAtestados escopo, CancellationToken ct) =>
        await Projetar(NoEscopo(escopo).Where(a => a.Id == atestadoId)).SingleOrDefaultAsync(ct);

    public async Task<PaginaDeAtestados> ListarAsync(EscopoDeAtestados escopo, FiltroDeAtestados filtro, ParametrosDeLista parametros, CancellationToken ct)
    {
        var atestados = NoEscopo(escopo);
        if (filtro.Situacao is { } situacao)
        {
            atestados = atestados.Where(a => a.Situacao == situacao);
        }

        if (filtro.FuncionarioId is { } funcionarioId)
        {
            atestados = atestados.Where(a => a.FuncionarioId == funcionarioId);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            atestados = atestados.Where(a => db.Funcionarios.Any(f => f.Id == a.FuncionarioId && EF.Functions.Like(f.Nome, like)));
        }

        var total = await atestados.CountAsync(ct);
        var itens = await Projetar(atestados.OrderByDescending(a => a.EnviadoEmUtc).ThenByDescending(a => a.Id).Skip(parametros.Pular).Take(parametros.TamanhoPagina)).ToListAsync(ct);
        return new PaginaDeAtestados(itens, total);
    }

    private IQueryable<Atestado> NoEscopo(EscopoDeAtestados escopo)
    {
        var atestados = db.Atestados.AsNoTracking();
        switch (escopo.Papel)
        {
            case PapelNaConsulta.PropriaPessoa:
                var minhaId = escopo.FuncionarioId;
                return atestados.Where(a => a.FuncionarioId == minhaId);
            case PapelNaConsulta.Gestor:
                if (escopo.Equipe is not { } equipe)
                {
                    return atestados.Where(_ => false);
                }

                return atestados.Where(a => db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(a.FuncionarioId));
            default:
                return atestados;
        }
    }

    /// <summary>A projeção ordenada: a página sai na ordem pedida; <c>decididoPorNome</c> é o nome de quem do RH decidiu.</summary>
    private IQueryable<AtestadoLido> Projetar(IQueryable<Atestado> atestados) =>
        from a in atestados
        join f in db.Funcionarios on a.FuncionarioId equals f.Id
        join rh in db.Funcionarios on a.FuncionarioRhId equals rh.Id into rhs
        from rh in rhs.DefaultIfEmpty()
        select new AtestadoLido(
            a.Id,
            new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null),
            a.DataInicio,
            a.DataFim,
            a.HoraInicio,
            a.HoraFim,
            a.Situacao,
            a.NomeArquivoOriginal,
            a.CaminhoArquivo,
            a.EnviadoEmUtc,
            a.DecididoEmUtc,
            rh != null ? rh.Nome : null,
            a.MotivoRecusa);
}

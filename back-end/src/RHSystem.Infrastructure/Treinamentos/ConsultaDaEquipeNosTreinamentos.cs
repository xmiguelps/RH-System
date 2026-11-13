using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Treinamentos.Equipe;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A leitura dos treinamentos para a pendência, a ficha e a equipe do gestor. Só conta linhas válidas (<c>INSCRITO</c>, <c>EM_ANDAMENTO</c>, <c>CONCLUIDO</c>). A pendência é a definição única: pessoa ATIVA, treinamento OBRIGATÓRIO em andamento (publicado, já liberado, não encerrado) e linha <c>INSCRITO</c> ou <c>EM_ANDAMENTO</c>; opcional nunca é pendência.
/// </summary>
public sealed class ConsultaDaEquipeNosTreinamentos(AppDbContext db) : IConsultaDaEquipeNosTreinamentos
{
    public async Task<int> PendenciasAsync(int funcionarioId, DateOnly hoje, CancellationToken ct)
    {
        var eu = funcionarioId;
        if (!await db.Funcionarios.AsNoTracking().AnyAsync(f => f.Id == eu && f.Ativo, ct))
        {
            return 0;
        }

        var obrigatoriosEmAndamento = await db.Treinamentos.AsNoTracking()
            .Where(t => t.Obrigatorio && t.Publicado && t.EncerradoEmUtc == null && (t.ComecaEm == null || t.ComecaEm <= hoje))
            .Select(t => t.Id)
            .ToListAsync(ct);
        return await db.TreinamentosFuncionarios.AsNoTracking()
            .CountAsync(l => l.FuncionarioId == eu && obrigatoriosEmAndamento.Contains(l.TreinamentoId) && (l.Situacao == SituacaoNoTreinamento.Inscrito || l.Situacao == SituacaoNoTreinamento.EmAndamento), ct);
    }

    public async Task<IReadOnlyList<TreinamentoDaEquipeLido>> TreinamentosDaEquipeAsync(IReadOnlyList<int> equipe, int setorId, CancellationToken ct)
    {
        var publicados = await db.Treinamentos.AsNoTracking().Where(t => t.Publicado).Select(t => new { t.Id, t.Titulo, t.Obrigatorio, t.TipoTreinamento, t.ComecaEm, t.EncerradoEmUtc, t.Publico }).ToListAsync(ct);
        if (publicados.Count == 0)
        {
            return [];
        }

        var ids = publicados.Select(t => t.Id).ToList();
        var linhas = await db.TreinamentosFuncionarios.AsNoTracking()
            .Where(l => ids.Contains(l.TreinamentoId) && equipe.Contains(l.FuncionarioId) && (l.Situacao == SituacaoNoTreinamento.Inscrito || l.Situacao == SituacaoNoTreinamento.EmAndamento || l.Situacao == SituacaoNoTreinamento.Concluido))
            .Select(l => new { l.TreinamentoId, l.Situacao })
            .ToListAsync(ct);
        var publico = await db.TreinamentosPublico.AsNoTracking().Where(p => ids.Contains(p.TreinamentoId)).Select(p => new { p.TreinamentoId, p.SetorId, p.FuncionarioId }).ToListAsync(ct);

        var resultado = new List<TreinamentoDaEquipeLido>();
        foreach (var t in publicados)
        {
            var comLinha = linhas.Where(l => l.TreinamentoId == t.Id).ToList();
            var doPublico = publico.Where(p => p.TreinamentoId == t.Id).ToList();
            var noPublico = t.Publico switch
            {
                PublicoDoTreinamento.Todos => equipe.Count,
                PublicoDoTreinamento.Setores => doPublico.Any(p => p.SetorId == setorId) ? equipe.Count : 0,
                PublicoDoTreinamento.Pessoas => equipe.Count(pessoa => doPublico.Any(p => p.FuncionarioId == pessoa)),
                _ => 0,
            };
            if (noPublico == 0 && comLinha.Count == 0)
            {
                continue; // não alcança ninguém da equipe
            }

            resultado.Add(new TreinamentoDaEquipeLido(t.Id, t.Titulo, t.Obrigatorio, t.TipoTreinamento == Treinamento.TipoOnboarding, t.ComecaEm, t.EncerradoEmUtc, t.Obrigatorio ? comLinha.Count : noPublico, comLinha.Count(l => l.Situacao == SituacaoNoTreinamento.Concluido), comLinha.Count));
        }

        return resultado;
    }

    public async Task<IReadOnlyList<PessoaDaEquipeNoTreinamento>> PessoasDaEquipeAsync(int treinamentoId, IReadOnlyList<int> equipe, CancellationToken ct)
    {
        var id = treinamentoId;
        var linhas = await (
                from l in db.TreinamentosFuncionarios.AsNoTracking()
                where l.TreinamentoId == id && equipe.Contains(l.FuncionarioId) && (l.Situacao == SituacaoNoTreinamento.Inscrito || l.Situacao == SituacaoNoTreinamento.EmAndamento || l.Situacao == SituacaoNoTreinamento.Concluido)
                join f in db.Funcionarios.AsNoTracking() on l.FuncionarioId equals f.Id
                orderby f.Nome, l.Id
                select new { l.FuncionarioId, f.Nome, f.Ativo, Cargo = f.Cargo != null ? f.Cargo.Nome : null, Setor = f.Setor != null ? f.Setor.Nome : null, l.Situacao, l.ConcluidoEm })
            .ToListAsync(ct);
        if (linhas.Count == 0)
        {
            return [];
        }

        var modulos = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == id).OrderBy(m => m.Ordem).Select(m => new { m.Id, m.Ordem, m.DuracaoMinutos }).ToListAsync(ct);
        var idsDosModulos = modulos.Select(m => m.Id).ToList();
        var pessoas = linhas.Select(l => l.FuncionarioId).ToList();
        var progresso = (await db.TreinamentosModulosProgresso.AsNoTracking().Where(p => pessoas.Contains(p.FuncionarioId) && idsDosModulos.Contains(p.ModuloId)).Select(p => new { p.FuncionarioId, p.ModuloId, p.ConcluidoEmUtc }).ToListAsync(ct))
            .GroupBy(p => p.FuncionarioId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return
        [
            .. linhas.Select(l =>
            {
                var seus = progresso.GetValueOrDefault(l.FuncionarioId) ?? [];
                var feitos = seus.Select(p => p.ModuloId).ToHashSet();
                var paraProgresso = modulos.Select(m => new ModuloParaProgresso(m.Ordem, m.DuracaoMinutos, feitos.Contains(m.Id))).ToList();
                DateTime? concluidoEm = l.Situacao != SituacaoNoTreinamento.Concluido ? null : seus.Count > 0 ? seus.Max(p => p.ConcluidoEmUtc) : l.ConcluidoEm is { } dia ? FusoBrasil.InicioDoDiaEmUtc(dia) : null;
                return new PessoaDaEquipeNoTreinamento(l.FuncionarioId, l.Nome, l.Cargo, l.Setor, l.Ativo, l.Situacao!.Value, concluidoEm, paraProgresso);
            }),
        ];
    }
}

/// <summary>O indicador <c>treinamentosPendentes</c> da ficha do colaborador: o MESMO número do <c>resumo.pendentes</c> das fichas de treinamento (só pessoa ativa).</summary>
public sealed class FonteDoIndicadorDeTreinamentos(IConsultaDaEquipeNosTreinamentos consulta, IRelogio relogio) : IFonteDeIndicadorDaFicha
{
    public IndicadorDaFicha Indicador => IndicadorDaFicha.TreinamentosPendentes;

    public async Task<int?> ObterAsync(int funcionarioId, CancellationToken ct) => await consulta.PendenciasAsync(funcionarioId, FusoBrasil.Hoje(relogio.AgoraUtc), ct);
}

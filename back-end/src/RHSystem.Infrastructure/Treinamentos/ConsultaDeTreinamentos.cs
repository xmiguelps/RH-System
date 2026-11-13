using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A leitura dos treinamentos para o RH. A situação sai de <see cref="RegraDaSituacaoDoTreinamento"/> com o dia de hoje em -03:00 (não há coluna); o caminho do arquivo do módulo NUNCA sai (só o nome original). "Pessoas que o público alcança" são sempre ativas com a admissão já ocorrida.
/// </summary>
public sealed class ConsultaDeTreinamentos(AppDbContext db) : IConsultaDeTreinamentos
{
    public async Task<TreinamentoLido?> ObterAsync(int treinamentoId, CancellationToken ct)
    {
        var t = await db.Treinamentos.AsNoTracking().Where(x => x.Id == treinamentoId).SingleOrDefaultAsync(ct);
        if (t is null)
        {
            return null;
        }

        var setores = await db.TreinamentosPublico.AsNoTracking().Where(p => p.TreinamentoId == treinamentoId && p.SetorId != null).Select(p => p.SetorId!.Value).OrderBy(i => i).ToListAsync(ct);
        var pessoas = await db.TreinamentosPublico.AsNoTracking().Where(p => p.TreinamentoId == treinamentoId && p.FuncionarioId != null).Select(p => p.FuncionarioId!.Value).OrderBy(i => i).ToListAsync(ct);
        var modulos = await db.TreinamentosModulos.AsNoTracking().CountAsync(m => m.TreinamentoId == treinamentoId, ct);
        return new TreinamentoLido(t.Id, t.Titulo, t.Descricao, t.Onboarding, t.ComecaEm, t.Obrigatorio, t.Publicado, t.Publico, t.EmiteCertificado, t.IncluiAdmitidos, t.EncerradoEmUtc, setores, pessoas, modulos);
    }

    public async Task<TreinamentoRh?> ObterDetalheAsync(int treinamentoId, DateOnly hoje, CancellationToken ct)
    {
        var lido = await ObterAsync(treinamentoId, ct);
        if (lido is null)
        {
            return null;
        }

        var setores = await db.Setores.AsNoTracking().Where(s => lido.SetorIds.Contains(s.Id)).OrderBy(s => s.Nome).ThenBy(s => s.Id).Select(s => new TreinamentoSetorResumo(s.Id, s.Nome)).ToListAsync(ct);
        var pessoas = await db.Funcionarios.AsNoTracking().Where(f => lido.FuncionarioIds.Contains(f.Id)).OrderBy(f => f.Nome).ThenBy(f => f.Id)
            .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
            .ToListAsync(ct);
        var modulos = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamentoId).OrderBy(m => m.Ordem)
            .Select(m => new TreinamentoModuloRh(m.Id, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos, m.UrlExterna, m.ConteudoTexto, m.NomeArquivoOriginal))
            .ToListAsync(ct);
        return new TreinamentoRh(lido.Id, lido.Titulo, lido.EncerradoEmUtc, lido.Obrigatorio, lido.Onboarding, lido.EmiteCertificado, lido.Publicado, lido.Situacao(hoje), setores, pessoas, lido.IncluiAdmitidos, modulos, lido.Descricao, lido.ComecaEm, lido.Publico);
    }

    public async Task<int> ContarPublicoAsync(PublicoDoTreinamento publico, IReadOnlyList<int> setorIds, IReadOnlyList<int> funcionarioIds, DateOnly hoje, CancellationToken ct)
    {
        var alcancaveis = db.Funcionarios.AsNoTracking().Where(f => f.Ativo && f.DataAdmissao <= hoje);
        return publico switch
        {
            PublicoDoTreinamento.Todos => await alcancaveis.CountAsync(ct),
            PublicoDoTreinamento.Setores => await alcancaveis.CountAsync(f => f.SetorId != null && setorIds.Contains(f.SetorId.Value), ct),
            _ => await alcancaveis.CountAsync(f => funcionarioIds.Contains(f.Id), ct),
        };
    }

    public async Task<PendentesDoTreinamento> ContarPendentesAsync(int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;
        var ativas = db.TreinamentosFuncionarios.AsNoTracking().Where(l => l.TreinamentoId == id && db.Funcionarios.Any(f => f.Id == l.FuncionarioId && f.Ativo));
        return new PendentesDoTreinamento(
            await ativas.CountAsync(l => l.Situacao == SituacaoNoTreinamento.EmAndamento, ct),
            await ativas.CountAsync(l => l.Situacao == SituacaoNoTreinamento.Inscrito, ct));
    }

    public async Task<int> ContarCertificadosEmitidosAsync(int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;
        var emite = await db.Treinamentos.AsNoTracking().Where(t => t.Id == id).Select(t => t.EmiteCertificado).SingleOrDefaultAsync(ct);
        return emite ? await db.TreinamentosFuncionarios.AsNoTracking().CountAsync(l => l.TreinamentoId == id && l.Situacao == SituacaoNoTreinamento.Concluido, ct) : 0;
    }

    public async Task<IReadOnlyList<int>> SetoresInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var existem = await db.Setores.AsNoTracking().Where(s => ids.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct);
        return [.. ids.Except(existem)];
    }

    public async Task<IReadOnlyList<int>> FuncionariosInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct)
    {
        var existem = await db.Funcionarios.AsNoTracking().Where(f => ids.Contains(f.Id)).Select(f => f.Id).ToListAsync(ct);
        return [.. ids.Except(existem)];
    }
}

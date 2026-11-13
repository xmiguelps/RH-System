using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A leitura dos treinamentos da pessoa logada. Só olha a PRÓPRIA linha, o PRÓPRIO progresso e o PRÓPRIO certificado (nunca os de outra pessoa). Os candidatos são os publicados já liberados; a visibilidade é a de <see cref="VisibilidadeParaAPessoa"/> (linha <c>INSCRITO</c>, <c>EM_ANDAMENTO</c> ou <c>CONCLUIDO</c>, ou opcional do público dela sem linha; encerrado só com linha <c>EM_ANDAMENTO</c> ou <c>CONCLUIDO</c>);
/// linha <c>REPROVADO</c>, <c>CANCELADO</c> ou sem situação (dado antigo) nunca aparece. O número de treinamentos publicados é pequeno, então o filtro de visibilidade roda em memória com as mesmas regras puras que os testes de regra cobrem.
/// </summary>
public sealed class ConsultaDeMeusTreinamentos(AppDbContext db) : IConsultaDeMeusTreinamentos
{
    public async Task<IReadOnlyList<MeuTreinamentoLido>> ListarAsync(int funcionarioId, DateOnly hoje, string? busca, bool? obrigatorio, CancellationToken ct) =>
        await CarregarAsync(funcionarioId, hoje, null, busca, obrigatorio, ct);

    public async Task<MeuTreinamentoLido?> ObterAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct) =>
        (await CarregarAsync(funcionarioId, hoje, treinamentoId, null, null, ct)).SingleOrDefault();

    public async Task<DateTime?> InstanteDaConclusaoDoModuloAsync(int funcionarioId, int moduloId, CancellationToken ct) =>
        await db.TreinamentosModulosProgresso.AsNoTracking().Where(p => p.FuncionarioId == funcionarioId && p.ModuloId == moduloId).Select(p => (DateTime?)p.ConcluidoEmUtc).SingleOrDefaultAsync(ct);

    private async Task<IReadOnlyList<MeuTreinamentoLido>> CarregarAsync(int eu, DateOnly hoje, int? treinamentoId, string? busca, bool? obrigatorio, CancellationToken ct)
    {
        var candidatos = db.Treinamentos.AsNoTracking().Where(t => t.Publicado && (t.ComecaEm == null || t.ComecaEm <= hoje));
        if (treinamentoId is { } unico)
        {
            candidatos = candidatos.Where(t => t.Id == unico);
        }

        if (obrigatorio is { } obrig)
        {
            candidatos = candidatos.Where(t => t.Obrigatorio == obrig);
        }

        if (busca is not null)
        {
            var like = BuscaLike.Contem(busca);
            candidatos = candidatos.Where(t => EF.Functions.Like(t.Titulo, like) || (t.Descricao != null && EF.Functions.Like(t.Descricao, like)));
        }

        var treinamentos = await candidatos.Select(t => new { t.Id, t.Titulo, t.Descricao, t.Obrigatorio, t.TipoTreinamento, t.ComecaEm, t.EncerradoEmUtc, t.Publico, t.EmiteCertificado }).ToListAsync(ct);
        if (treinamentos.Count == 0)
        {
            return [];
        }

        var ids = treinamentos.Select(t => t.Id).ToList();
        var linhas = (await db.TreinamentosFuncionarios.AsNoTracking().Where(l => l.FuncionarioId == eu && ids.Contains(l.TreinamentoId)).Select(l => new { l.TreinamentoId, l.Situacao, l.IniciadoEm }).ToListAsync(ct))
            .ToDictionary(l => l.TreinamentoId);
        var semLinha = treinamentos.Where(t => !t.Obrigatorio && !linhas.ContainsKey(t.Id)).Select(t => t.Id).ToList();
        var publico = semLinha.Count == 0
            ? []
            : await db.TreinamentosPublico.AsNoTracking().Where(p => semLinha.Contains(p.TreinamentoId)).Select(p => new { p.TreinamentoId, p.SetorId, p.FuncionarioId }).ToListAsync(ct);
        var meuSetor = semLinha.Count == 0 ? null : await db.Funcionarios.AsNoTracking().Where(f => f.Id == eu).Select(f => f.SetorId).SingleOrDefaultAsync(ct);

        var visiveis = new List<(int Id, SituacaoNoTreinamento? Linha, DateOnly? IniciadoEm)>();
        foreach (var t in treinamentos)
        {
            var temLinha = linhas.TryGetValue(t.Id, out var linha);
            if (temLinha && linha!.Situacao is not (SituacaoNoTreinamento.Inscrito or SituacaoNoTreinamento.EmAndamento or SituacaoNoTreinamento.Concluido))
            {
                continue; // REPROVADO, CANCELADO ou sem situação: nunca aparece
            }

            var opcionalDoPublico = !t.Obrigatorio && !temLinha && PublicoDaPessoa.Alcanca(t.Publico, publico.Where(p => p.TreinamentoId == t.Id).Select(p => (p.SetorId, p.FuncionarioId)), eu, meuSetor);
            if (VisibilidadeParaAPessoa.Visivel(true, t.ComecaEm, t.EncerradoEmUtc, hoje, linha?.Situacao, opcionalDoPublico))
            {
                visiveis.Add((t.Id, linha?.Situacao, linha?.IniciadoEm));
            }
        }

        if (visiveis.Count == 0)
        {
            return [];
        }

        var idsVisiveis = visiveis.Select(v => v.Id).ToList();
        var modulos = await db.TreinamentosModulos.AsNoTracking().Where(m => idsVisiveis.Contains(m.TreinamentoId)).OrderBy(m => m.Ordem).Select(m => new { m.Id, m.TreinamentoId, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos }).ToListAsync(ct);
        var idsDosModulos = modulos.Select(m => m.Id).ToList();
        var progresso = (await db.TreinamentosModulosProgresso.AsNoTracking().Where(p => p.FuncionarioId == eu && idsDosModulos.Contains(p.ModuloId)).Select(p => new { p.ModuloId, p.ConcluidoEmUtc }).ToListAsync(ct))
            .ToDictionary(p => p.ModuloId, p => (DateTime?)p.ConcluidoEmUtc);
        var idsConcluidos = visiveis.Where(v => v.Linha == SituacaoNoTreinamento.Concluido).Select(v => v.Id).ToList();
        var certificados = idsConcluidos.Count == 0
            ? []
            : (await db.TreinamentosCertificados.AsNoTracking().Where(c => c.FuncionarioId == eu && idsConcluidos.Contains(c.TreinamentoId)).Select(c => new { c.TreinamentoId, c.Id, c.EmitidoEm, c.CodigoValidacao }).ToListAsync(ct))
                .ToDictionary(c => c.TreinamentoId, c => new CertificadoLido(c.Id, c.EmitidoEm, c.CodigoValidacao));

        var porId = treinamentos.ToDictionary(t => t.Id);
        return
        [
            .. visiveis.Select(v =>
            {
                var t = porId[v.Id];
                var seus = modulos.Where(m => m.TreinamentoId == v.Id).Select(m => new MeuModuloLido(m.Id, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos, progresso.GetValueOrDefault(m.Id))).ToList();
                return new MeuTreinamentoLido(t.Id, t.Titulo, t.Descricao, t.Obrigatorio, t.TipoTreinamento == Treinamento.TipoOnboarding, t.ComecaEm, t.EncerradoEmUtc, t.EmiteCertificado, v.Linha, v.IniciadoEm, seus, certificados.GetValueOrDefault(v.Id));
            }),
        ];
    }
}

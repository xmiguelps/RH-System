using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Painel;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Ferias;
using RHSystem.Domain.Formularios;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ferias;
using RHSystem.Infrastructure.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Painel;

/// <summary>
/// As filas de pendências do RH e as pessoas da presença. Cada fila usa o MESMO critério da tela de origem (o do contador do menu do ponto, o predicado de situação da lista de férias do RH, o atestado <c>ENVIADO</c>, a situação calculada do formulário e a fila de benefícios) e é UMA consulta mais UMA de pessoas em lote.
/// </summary>
public sealed class ConsultaDoPainelDoRh(AppDbContext db) : IConsultaDoPainelDoRh
{
    private static readonly DateTime SemData = DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc);

    public async Task<IReadOnlyList<PendenciaLida>> AjustesDePontoNaEtapaDoRhAsync(CancellationToken ct)
    {
        var pedidos = await (from p in db.PedidosDeAjuste.AsNoTracking()
                             join d in db.PontosDosDias.AsNoTracking() on p.PontoId equals d.Id
                             where p.Status == StatusDoPedidoDeAjuste.Pendente && (p.GestorId == null || p.ResultadoDoGestor != null)
                             select new { p.Id, p.FuncionarioId, d.Dia, p.SolicitadoEmUtc, p.EtapaDoGestorEmUtc })
            .ToListAsync(ct);
        var pessoas = await PessoasAsync(pedidos.Select(p => p.FuncionarioId), ct);
        return [.. pedidos.Select(p => new PendenciaLida(PainelPendenciaTipo.AjusteDePonto, p.Id, pessoas[p.FuncionarioId], p.EtapaDoGestorEmUtc ?? p.SolicitadoEmUtc ?? SemData, TextosDasPendencias.DoAjuste(p.Dia)))];
    }

    public async Task<IReadOnlyList<PendenciaLida>> FeriasNaEtapaDoRhAsync(CancellationToken ct)
    {
        var pedidos = await db.PedidosDeFerias.AsNoTracking()
            .Where(ConsultaDeFerias.NaSituacao(SituacaoDasFerias.ComORh))
            .Select(p => new { p.Id, p.FuncionarioId, p.DataInicio, p.DataFim, p.SolicitadaEmUtc, p.ResultadoDoGestor, p.EtapaDoGestorEmUtc, p.RespostaDaSugestao, p.RespostaDaSugestaoEmUtc })
            .ToListAsync(ct);
        var pessoas = await PessoasAsync(pedidos.Select(p => p.FuncionarioId), ct);
        return [.. pedidos.Select(p => new PendenciaLida(PainelPendenciaTipo.Ferias, p.Id, pessoas[p.FuncionarioId], DesdeDasFerias(p.ResultadoDoGestor, p.EtapaDoGestorEmUtc, p.RespostaDaSugestao, p.RespostaDaSugestaoEmUtc, p.SolicitadaEmUtc), TextosDasPendencias.DasFerias(p.DataInicio, p.DataFim)))];
    }

    /// <summary>Desde quando as férias esperam o RH: a aprovação do gestor, quando há; a resposta da pessoa à sugestão que ela aceitou; senão a solicitação (o gestor saiu, ou o pedido não tem etapa do gestor).</summary>
    private static DateTime DesdeDasFerias(ResultadoDoGestorNasFerias? resultado, DateTime? etapaDoGestorEmUtc, RespostaDaSugestao? resposta, DateTime? respostaEmUtc, DateTime? solicitadaEmUtc) =>
        resultado == ResultadoDoGestorNasFerias.Aprovou && etapaDoGestorEmUtc is { } aprovada ? aprovada
        : resposta == RespostaDaSugestao.Aceitou && respostaEmUtc is { } respondida ? respondida
        : solicitadaEmUtc ?? SemData;

    public async Task<IReadOnlyList<PendenciaLida>> AtestadosEnviadosAsync(CancellationToken ct)
    {
        var atestados = await db.Atestados.AsNoTracking()
            .Where(a => a.Situacao == SituacaoDoAtestado.Enviado)
            .Select(a => new { a.Id, a.FuncionarioId, a.DataInicio, a.DataFim, a.EnviadoEmUtc })
            .ToListAsync(ct);
        var pessoas = await PessoasAsync(atestados.Select(a => a.FuncionarioId), ct);
        return [.. atestados.Select(a => new PendenciaLida(PainelPendenciaTipo.Atestado, a.Id, pessoas[a.FuncionarioId], a.EnviadoEmUtc, TextosDasPendencias.DoAtestado(a.DataInicio, a.DataFim)))];
    }

    public async Task<IReadOnlyList<PendenciaLida>> AdmissoesComFormularioPendenteAsync(DateOnly hoje, CancellationToken ct)
    {
        var ate = hoje.AddDays(7);
        var publicados = db.Formularios.AsNoTracking().NaSituacaoCalculada(SituacaoDoFormulario.Publicado, hoje).Where(f => f.Tipo == TipoDeFormulario.Admissao && f.Alvo == AlvoDoFormulario.Pessoas);
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(f => f.Ativo && f.DataAdmissao >= hoje && f.DataAdmissao <= ate
                && publicados.Any(form => db.FormulariosDestinatarios.Any(d => d.FormularioId == form.Id && d.FuncionarioId == f.Id)
                    && !db.FormulariosRespostas.Any(r => r.FormularioId == form.Id && r.FuncionarioId == f.Id)))
            .Select(f => new { f.Id, f.Nome, f.Ativo, Cargo = f.Cargo != null ? f.Cargo.Nome : null, Setor = f.Setor != null ? f.Setor.Nome : null, f.DataAdmissao })
            .ToListAsync(ct);
        return [.. pessoas.Select(p => new PendenciaLida(PainelPendenciaTipo.AdmissaoComDocumentosPendentes, p.Id, new FuncionarioResumo(p.Id, p.Nome, p.Ativo, p.Cargo, p.Setor), FusoBrasil.InicioDoDiaEmUtc(p.DataAdmissao), TextosDasPendencias.DaAdmissao(p.DataAdmissao)))];
    }

    public async Task<IReadOnlyList<PendenciaLida>> BeneficiosPendentesAsync(CancellationToken ct)
    {
        var adesoes = await (from l in db.BeneficiosDosFuncionarios.AsNoTracking()
                             join f in db.Funcionarios.AsNoTracking() on l.FuncionarioId equals f.Id
                             join b in db.Beneficios.AsNoTracking() on l.BeneficioId equals b.Id
                             where f.Ativo
                                && (l.Status == StatusDoPedidoDeBeneficio.Pendente
                                    || (l.Status == StatusDoPedidoDeBeneficio.Aprovado
                                        && (db.Dependentes.Any(d => d.AdesaoId == l.Id && d.Status == StatusDoPedidoDeBeneficio.Pendente)
                                            || db.TrajetosDeValeTransporte.Any(t => t.AdesaoId == l.Id && t.Status == StatusDoPedidoDeBeneficio.Pendente))))
                             select new { l.Id, l.FuncionarioId, l.Status, l.PedidoEmUtc, Beneficio = b.Nome })
            .ToListAsync(ct);
        if (adesoes.Count == 0)
        {
            return [];
        }

        var ids = adesoes.Select(a => a.Id).ToList();
        var dependentes = await db.Dependentes.AsNoTracking().Where(d => ids.Contains(d.AdesaoId) && d.Status == StatusDoPedidoDeBeneficio.Pendente).Select(d => new { d.AdesaoId, d.PedidoEmUtc }).ToListAsync(ct);
        var trajetos = await db.TrajetosDeValeTransporte.AsNoTracking().Where(t => ids.Contains(t.AdesaoId) && t.Status == StatusDoPedidoDeBeneficio.Pendente).Select(t => new { t.AdesaoId, t.PedidoEmUtc }).ToListAsync(ct);
        var pessoas = await PessoasAsync(adesoes.Select(a => a.FuncionarioId), ct);

        var itens = new List<PendenciaLida>();
        foreach (var adesao in adesoes)
        {
            // o pedido mais antigo da adesão: ela mesma (se pendente), um dependente ou um trajeto; no empate vale a adesão, depois o dependente
            var candidatos = new List<(TipoDoPedidoDeBeneficio Tipo, DateTime Em)>();
            if (adesao.Status == StatusDoPedidoDeBeneficio.Pendente)
            {
                candidatos.Add((TipoDoPedidoDeBeneficio.Adesao, adesao.PedidoEmUtc ?? SemData));
            }

            candidatos.AddRange(dependentes.Where(d => d.AdesaoId == adesao.Id).Select(d => (TipoDoPedidoDeBeneficio.Dependente, d.PedidoEmUtc)));
            candidatos.AddRange(trajetos.Where(t => t.AdesaoId == adesao.Id).Select(t => (TipoDoPedidoDeBeneficio.Trajeto, t.PedidoEmUtc)));
            var maisAntigo = candidatos.OrderBy(c => c.Em).ThenBy(c => c.Tipo).First();
            itens.Add(new PendenciaLida(PainelPendenciaTipo.Beneficios, adesao.Id, pessoas[adesao.FuncionarioId], maisAntigo.Em, TextosDasPendencias.DoBeneficio(maisAntigo.Tipo, adesao.Beneficio)));
        }

        return itens;
    }

    public async Task<IReadOnlyList<int>> IdsDasPessoasAtivasEAdmitidasAsync(DateOnly hoje, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking().Where(f => f.Ativo && f.DataAdmissao <= hoje).Select(f => f.Id).ToListAsync(ct);

    public async Task<IReadOnlyList<FuncionarioResumo>> ResumosAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct) =>
        funcionarioIds.Count == 0
            ? []
            : await db.Funcionarios.AsNoTracking()
                .Where(f => funcionarioIds.Contains(f.Id))
                .Select(f => new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null))
                .ToListAsync(ct);

    private async Task<IReadOnlyDictionary<int, FuncionarioResumo>> PessoasAsync(IEnumerable<int> ids, CancellationToken ct) =>
        (await ResumosAsync([.. ids.Distinct()], ct)).ToDictionary(p => p.Id);
}

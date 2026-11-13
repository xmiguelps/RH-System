using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Treinamentos.Acompanhar;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// "Quem fez": as linhas do treinamento com a pessoa (inativas continuam; <c>REPROVADO</c> e <c>CANCELADO</c> nunca aparecem). Os filtros de setor e busca valem para a lista e para as contagens; a aba só para a lista. O instante da conclusão é o do ÚLTIMO módulo concluído; sem progresso (conclusão anterior aos módulos), o dia de <c>DATA_CONCLUSAO</c> às 00:00 em -03:00. Na ordem por início e por conclusão o nulo fica por último nos dois sentidos
/// (a conclusão ordena pelo DIA gravado na linha); o empate é sempre pela linha.
/// </summary>
public sealed class ConsultaDeParticipantes(AppDbContext db) : IConsultaDeParticipantes
{
    /// <summary>A linha lida. É uma classe com inicializador de membros (e não um <c>record</c> posicional) para o EF saber a qual propriedade cada valor projetado corresponde quando se filtra e ordena DEPOIS do <c>select</c>.</summary>
    private sealed class Linha
    {
        public int LinhaId { get; init; }

        public int FuncionarioId { get; init; }

        public SituacaoNoTreinamento? Situacao { get; init; }

        public DateOnly? IniciadoEm { get; init; }

        public DateOnly? ConcluidoEm { get; init; }

        public string Nome { get; init; } = string.Empty;

        public bool Ativo { get; init; }

        public int? SetorId { get; init; }

        public string? CargoNome { get; init; }

        public string? SetorNome { get; init; }
    }

    public async Task<(IReadOnlyList<TreinamentoParticipante> Itens, int Total, TreinamentoParticipantesContagens Contagens)> ListarAsync(int treinamentoId, bool emiteCertificado, FiltroDeParticipantes filtro, ParametrosDeLista parametros, bool paginar, CancellationToken ct)
    {
        var id = treinamentoId;
        var todas = from l in db.TreinamentosFuncionarios.AsNoTracking()
                    where l.TreinamentoId == id && (l.Situacao == SituacaoNoTreinamento.Inscrito || l.Situacao == SituacaoNoTreinamento.EmAndamento || l.Situacao == SituacaoNoTreinamento.Concluido)
                    join f in db.Funcionarios.AsNoTracking() on l.FuncionarioId equals f.Id
                    select new Linha { LinhaId = l.Id, FuncionarioId = f.Id, Situacao = l.Situacao, IniciadoEm = l.IniciadoEm, ConcluidoEm = l.ConcluidoEm, Nome = f.Nome, Ativo = f.Ativo, SetorId = f.SetorId, CargoNome = f.Cargo != null ? f.Cargo.Nome : null, SetorNome = f.Setor != null ? f.Setor.Nome : null };

        if (filtro.SetorId is { } setor)
        {
            todas = todas.Where(x => x.SetorId == setor);
        }

        if (parametros.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            todas = todas.Where(x => EF.Functions.Like(x.Nome, like));
        }

        var contagens = new TreinamentoParticipantesContagens(
            await todas.CountAsync(ct),
            await todas.CountAsync(x => x.Situacao == SituacaoNoTreinamento.Concluido, ct),
            await todas.CountAsync(x => x.Situacao == SituacaoNoTreinamento.EmAndamento, ct),
            await todas.CountAsync(x => x.Situacao == SituacaoNoTreinamento.Inscrito, ct));

        var daAba = filtro.Situacao is { } aba ? todas.Where(x => x.Situacao == aba) : todas;
        var total = await daAba.CountAsync(ct);
        var ordenada = Ordenar(daAba, parametros.OrdenarPor, parametros.Direcao == DirecaoDeOrdenacao.Desc);
        var pagina = await (paginar ? ordenada.Skip(parametros.Pular).Take(parametros.TamanhoPagina) : ordenada).ToListAsync(ct);

        var pessoas = pagina.Select(x => x.FuncionarioId).ToList();
        var progresso = (await db.TreinamentosModulosProgresso.AsNoTracking()
                .Where(p => pessoas.Contains(p.FuncionarioId) && db.TreinamentosModulos.Any(m => m.Id == p.ModuloId && m.TreinamentoId == id))
                .Select(p => new { p.FuncionarioId, p.ConcluidoEmUtc })
                .ToListAsync(ct))
            .GroupBy(p => p.FuncionarioId)
            .ToDictionary(g => g.Key, g => (Quantos: g.Count(), Ultimo: g.Max(p => p.ConcluidoEmUtc)));

        IReadOnlyList<TreinamentoParticipante> itens =
        [
            .. pagina.Select(x =>
            {
                var andamento = progresso.GetValueOrDefault(x.FuncionarioId);
                var concluido = x.Situacao == SituacaoNoTreinamento.Concluido;
                DateTime? concluidoEm = !concluido ? null : andamento.Quantos > 0 ? andamento.Ultimo : x.ConcluidoEm is { } dia ? FusoBrasil.InicioDoDiaEmUtc(dia) : null;
                return new TreinamentoParticipante(new FuncionarioResumo(x.FuncionarioId, x.Nome, x.Ativo, x.CargoNome, x.SetorNome), x.Situacao!.Value, x.IniciadoEm, concluidoEm, andamento.Quantos, concluido && emiteCertificado);
            }),
        ];
        return (itens, total, contagens);
    }

    private static IOrderedQueryable<Linha> Ordenar(IQueryable<Linha> consulta, string? ordenarPor, bool decrescente)
    {
        var ordenada = (ordenarPor ?? "NOME") switch
        {
            "SETOR" => decrescente ? consulta.OrderBy(x => x.SetorNome == null).ThenByDescending(x => x.SetorNome) : consulta.OrderBy(x => x.SetorNome == null).ThenBy(x => x.SetorNome),
            "SITUACAO" => decrescente
                ? consulta.OrderByDescending(x => x.Situacao == SituacaoNoTreinamento.Inscrito ? 0 : x.Situacao == SituacaoNoTreinamento.EmAndamento ? 1 : 2)
                : consulta.OrderBy(x => x.Situacao == SituacaoNoTreinamento.Inscrito ? 0 : x.Situacao == SituacaoNoTreinamento.EmAndamento ? 1 : 2),
            "INICIO" => decrescente ? consulta.OrderBy(x => x.IniciadoEm == null).ThenByDescending(x => x.IniciadoEm) : consulta.OrderBy(x => x.IniciadoEm == null).ThenBy(x => x.IniciadoEm),
            "CONCLUSAO" => decrescente ? consulta.OrderBy(x => x.ConcluidoEm == null).ThenByDescending(x => x.ConcluidoEm) : consulta.OrderBy(x => x.ConcluidoEm == null).ThenBy(x => x.ConcluidoEm),
            _ => decrescente ? consulta.OrderByDescending(x => x.Nome) : consulta.OrderBy(x => x.Nome),
        };
        return ordenada.ThenBy(x => x.LinhaId);
    }
}

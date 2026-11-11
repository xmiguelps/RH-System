using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>As leituras da exportação para a folha: quem esteve ativo no período (da admissão ao desligamento, inativo inclusive) e o banco de horas do período por pessoa (cada linha convertida de horas para minutos e depois somada).</summary>
public sealed class ConsultaDaFolha(AppDbContext db) : IConsultaDaFolha
{
    public async Task<IReadOnlyList<PessoaDaFolha>> PessoasDoPeriodoAsync(DateOnly primeiroDia, DateOnly ultimoDia, CancellationToken ct)
    {
        var linhas = await db.Funcionarios.AsNoTracking()
            .Where(f => f.DataAdmissao <= ultimoDia && (f.DataDesligamento == null || f.DataDesligamento >= primeiroDia))
            .OrderBy(f => f.Nome)
            .ThenBy(f => f.Id)
            .Select(f => new
            {
                f.Matricula,
                f.Id,
                f.Nome,
                Cargo = f.Cargo != null ? f.Cargo.Nome : null,
                Setor = f.Setor != null ? f.Setor.Nome : null,
                f.Ativo,
                f.DataAdmissao,
                f.DataDesligamento,
                Carga = f.Cargo != null ? f.Cargo.CargaSemanalHoras : (decimal?)null,
            })
            .ToListAsync(ct);
        return [.. linhas.Select(l => new PessoaDaFolha(l.Matricula, new PessoaDoEspelho(l.Id, l.Nome, l.Cargo, l.Setor, l.Ativo, l.DataAdmissao, l.DataDesligamento, l.Carga)))];
    }

    public async Task<IReadOnlyDictionary<int, BancoDoMesDaPessoa>> BancoDoPeriodoAsync(DateOnly primeiroDia, DateOnly ultimoDia, CancellationToken ct)
    {
        var linhas = await db.LancamentosDoBancoDeHoras.AsNoTracking()
            .Where(l => l.DiaDeReferencia >= primeiroDia && l.DiaDeReferencia <= ultimoDia && l.Tipo != null && l.Quantidade != null)
            .Select(l => new { l.FuncionarioId, l.Tipo, l.Quantidade })
            .ToListAsync(ct);
        return linhas.GroupBy(l => l.FuncionarioId).ToDictionary(
            g => g.Key,
            g => new BancoDoMesDaPessoa(
                g.Where(l => l.Tipo == TipoDeLancamentoDoBanco.Credito).Sum(l => LancamentoDoBancoDeHoras.HorasParaMinutos(l.Quantidade!.Value)),
                g.Where(l => l.Tipo == TipoDeLancamentoDoBanco.Debito).Sum(l => LancamentoDoBancoDeHoras.HorasParaMinutos(l.Quantidade!.Value))));
    }
}

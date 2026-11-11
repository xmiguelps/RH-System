using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O ponto no banco. A trava da pessoa é a de <see cref="OperacoesComTrava"/> (<c>FOR UPDATE</c> na linha de <c>FUNCIONARIOS</c>): TODO escritor do ponto de uma pessoa a pega primeiro, então criar o dia e gravar a batida não correm entre si e a
/// chave <c>UQ_PONTOS_FUNCIONARIO_DATA</c> nunca é disputada. A chave da batida (<c>UQ_PONTOS_MARCACOES_PONTO_DATAHORA</c>) ainda vira o 409 do contrato se algo escapar da conferência.
/// </summary>
public sealed class PontoRepositorio(AppDbContext db) : IPontoRepositorio
{
    public async Task<PessoaDoPonto?> TravarPessoaAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct);
        return pessoa is null ? null : new PessoaDoPonto(pessoa.Ativo, pessoa.DataAdmissao, pessoa.DataDesligamento);
    }

    public async Task<decimal?> CargaSemanalAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => f.Id == funcionarioId)
            .Select(f => f.Cargo != null ? f.Cargo.CargaSemanalHoras : (decimal?)null)
            .FirstOrDefaultAsync(ct);

    public async Task<IReadOnlyList<BatidaLida>> BatidasDoDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct) =>
        await (
            from m in db.Marcacoes.AsNoTracking()
            join p in db.PontosDosDias.AsNoTracking() on m.PontoId equals p.Id
            where p.FuncionarioId == funcionarioId && p.Dia == dia
            orderby m.InstanteUtc, m.Id
            select new BatidaLida(m.Id, m.InstanteUtc, m.Tipo, m.Origem, m.CorrigidaEmUtc))
            .ToListAsync(ct);

    public async Task<int> GravarBatidaAsync(int funcionarioId, DateOnly dia, DateTime instanteUtc, TipoDeMarcacao tipo, CancellationToken ct)
    {
        try
        {
            var ponto = await db.PontosDosDias.FirstOrDefaultAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia, ct);
            if (ponto is null)
            {
                ponto = PontoDoDia.Novo(funcionarioId, dia);
                db.PontosDosDias.Add(ponto);
                await db.SaveChangesAsync(ct);
            }

            var batida = MarcacaoDePonto.Nova(ponto.Id, instanteUtc, tipo);
            db.Marcacoes.Add(batida);
            await db.SaveChangesAsync(ct);
            return batida.Id;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao) is { } problema)
        {
            throw problema;
        }
    }
}

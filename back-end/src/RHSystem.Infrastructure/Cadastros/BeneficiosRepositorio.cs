using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Beneficios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Cadastros;

/// <summary>
/// O catálogo de benefícios no banco. <c>quantidadePessoas</c> conta as linhas de <c>BeneficioDoFuncionario</c> com <c>Ativo = true</c> (pedido pendente ou inativo não conta). Excluir é um <c>Remove</c>
/// rastreado, para o interceptor gravar <c>Excluiu</c>; o vínculo que nasce entre a conferência e a exclusão faz o banco recusar (<c>1451</c>).
/// </summary>
public sealed class BeneficiosRepositorio(AppDbContext db) : IBeneficiosRepositorio
{
    private static readonly MapaDeOrdenacao<Beneficio> Ordem = new MapaDeOrdenacao<Beneficio>("ID", DirecaoDeOrdenacao.Asc, b => b.Id).Por("ID", b => b.Id);

    public async Task<RespostaPaginada<BeneficioCatalogoItem>> ListarAsync(ParametrosDeLista p, CancellationToken ct)
    {
        var consulta = db.Beneficios.AsNoTracking();
        if (p.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            consulta = consulta.Where(b => EF.Functions.Like(b.Nome, like) || (b.Descricao != null && EF.Functions.Like(b.Descricao, like)));
        }

        return await Ordem.Aplicar(consulta, p).ToRespostaPaginadaAsync(
            p,
            b => new BeneficioCatalogoItem(
                b.Id,
                b.Nome,
                b.Descricao,
                b.Opcional,
                b.AceitaDependentes,
                b.AceitaTrajeto,
                db.BeneficiosDosFuncionarios.Count(v => v.BeneficioId == b.Id && v.Ativo)),
            ct);
    }

    public async Task<Beneficio?> ObterAsync(int beneficioId, CancellationToken ct) => await db.Beneficios.FirstOrDefaultAsync(b => b.Id == beneficioId, ct);

    public async Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct) =>
        await db.Beneficios.AnyAsync(b => b.Nome == nome && (ignorarId == null || b.Id != ignorarId), ct);

    public async Task<int> QuantidadeDePessoasAsync(int beneficioId, CancellationToken ct) =>
        await db.BeneficiosDosFuncionarios.CountAsync(v => v.BeneficioId == beneficioId && v.Ativo, ct);

    public async Task<bool> TemVinculoAsync(int beneficioId, CancellationToken ct) => await db.BeneficiosDosFuncionarios.AnyAsync(v => v.BeneficioId == beneficioId, ct);

    public async Task AdicionarAsync(Beneficio beneficio, CancellationToken ct)
    {
        db.Beneficios.Add(beneficio);
        await SalvarAsync(ct);
    }

    public async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveRepetida(erro))
        {
            // Mesma corrida do cargo: a chave única do banco decide, e a mensagem do MySQL (que traz o valor) nunca sai.
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }
    }

    public async Task ExcluirAsync(Beneficio beneficio, CancellationToken ct)
    {
        db.Beneficios.Remove(beneficio);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveEstrangeiraEmUso(erro))
        {
            throw MensagensDoApoio.CadastroEmUso.ComoConflito();
        }
    }
}

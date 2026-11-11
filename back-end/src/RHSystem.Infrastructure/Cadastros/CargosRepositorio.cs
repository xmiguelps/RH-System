using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Cadastros;

/// <summary>
/// Os cargos no banco. A lista conta as pessoas ativas de cada cargo numa subconsulta (o texto do contrato: "ativas", sem olhar a data de admissão). O nome único é o da chave do banco
/// (<c>utf8mb4_unicode_ci</c>: sem diferenciar maiúsculas nem acentos), e a comparação da verificação usa a mesma regra porque compara com a coluna.
/// </summary>
public sealed class CargosRepositorio(AppDbContext db, ILogger<CargosRepositorio> log) : ICargosRepositorio
{
    private static readonly MapaDeOrdenacao<Cargo> Ordem = new MapaDeOrdenacao<Cargo>("NOME", DirecaoDeOrdenacao.Asc, c => c.Id).Por("NOME", c => c.Nome);

    private sealed record Linha(int Id, string Nome, string? Descricao, decimal? CargaSemanalHoras, int QuantidadePessoas);

    public async Task<RespostaPaginada<CargoItem>> ListarAsync(ParametrosDeLista p, CancellationToken ct)
    {
        var consulta = db.Cargos.AsNoTracking();
        if (p.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            consulta = consulta.Where(c => EF.Functions.Like(c.Nome, like) || (c.Descricao != null && EF.Functions.Like(c.Descricao, like)));
        }

        var pagina = await Ordem.Aplicar(consulta, p).ToRespostaPaginadaAsync(
            p,
            c => new Linha(c.Id, c.Nome, c.Descricao, c.CargaSemanalHoras, db.Funcionarios.Count(f => f.CargoId == c.Id && f.Ativo)),
            ct);

        // O banco aceita cargo sem carga (NULL); o contrato não. Só existiria por SQL direto: sai 0 e fica o aviso.
        foreach (var linha in pagina.Itens.Where(l => l.CargaSemanalHoras is null))
        {
            log.LogWarning("O cargo {CargoId} está sem carga horária no banco; a lista devolve 0.", linha.Id);
        }

        return RespostaPaginada<CargoItem>.De(
            [.. pagina.Itens.Select(l => new CargoItem(l.Id, l.Nome, l.Descricao, l.CargaSemanalHoras ?? 0, l.QuantidadePessoas))],
            pagina.Total,
            p);
    }

    public async Task<Cargo?> ObterAsync(int cargoId, CancellationToken ct) => await db.Cargos.FirstOrDefaultAsync(c => c.Id == cargoId, ct);

    public async Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct) =>
        await db.Cargos.AnyAsync(c => c.Nome == nome && (ignorarId == null || c.Id != ignorarId), ct);

    public async Task<int> QuantidadeDePessoasAsync(int cargoId, CancellationToken ct) => await db.Funcionarios.CountAsync(f => f.CargoId == cargoId && f.Ativo, ct);

    public async Task AdicionarAsync(Cargo cargo, CancellationToken ct)
    {
        db.Cargos.Add(cargo);
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
            // A verificação de nome passou e outra chamada gravou o mesmo nome antes (a corrida): a chave única do banco decide. A mensagem do MySQL traz o valor e nunca é mostrada nem registrada.
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }
    }
}

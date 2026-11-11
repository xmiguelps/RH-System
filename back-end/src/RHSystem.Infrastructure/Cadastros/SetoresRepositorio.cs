using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Cadastros;

/// <summary>
/// Os setores no banco. A lista conta as pessoas ativas com a admissão já começada de cada setor numa subconsulta (inclusive o gestor, se ele for do setor) e mostra o gestor mesmo inativo. O nome único é o da chave
/// do banco (<c>utf8mb4_unicode_ci</c>: sem diferenciar maiúsculas nem acentos). Duas gravações separadas na troca de gestor: a linha de <c>GESTORES</c> com os níveis, e o setor (onde só o nome pode repetir).
/// </summary>
public sealed class SetoresRepositorio(AppDbContext db) : ISetoresRepositorio
{
    private static readonly MapaDeOrdenacao<Setor> Ordem = new MapaDeOrdenacao<Setor>("NOME", DirecaoDeOrdenacao.Asc, s => s.Id).Por("NOME", s => s.Nome);

    private sealed record Linha(int Id, string Nome, string? Descricao, int? GestorId, int? GestorFuncionarioId, string? GestorNome, int QuantidadePessoas);

    public async Task<RespostaPaginada<SetorItem>> ListarAsync(ParametrosDeLista p, DateOnly hoje, CancellationToken ct)
    {
        var consulta = db.Setores.AsNoTracking();
        if (p.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            consulta = consulta.Where(s => EF.Functions.Like(s.Nome, like) || (s.Descricao != null && EF.Functions.Like(s.Descricao, like)));
        }

        var pagina = await Ordem.Aplicar(consulta, p).ToRespostaPaginadaAsync(p, Projecao(hoje), ct);
        return RespostaPaginada<SetorItem>.De([.. pagina.Itens.Select(Item)], pagina.Total, p);
    }

    public async Task<SetorItem?> ObterItemAsync(int setorId, DateOnly hoje, CancellationToken ct)
    {
        var linha = await db.Setores.AsNoTracking().Where(s => s.Id == setorId).Select(Projecao(hoje)).FirstOrDefaultAsync(ct);
        return linha is null ? null : Item(linha);
    }

    public async Task<Setor?> ObterAsync(int setorId, CancellationToken ct) =>
        await db.Setores
            .Include(s => s.Gestor).ThenInclude(g => g!.Funcionario).ThenInclude(f => f.NivelDeAcesso)
            .FirstOrDefaultAsync(s => s.Id == setorId, ct);

    public async Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct) =>
        await db.Setores.AnyAsync(s => s.Nome == nome && (ignorarId == null || s.Id != ignorarId), ct);

    public async Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.Include(f => f.NivelDeAcesso).FirstOrDefaultAsync(f => f.Id == funcionarioId, ct);

    public async Task<string?> NomeDoSetorQueGerenciaAsync(int funcionarioId, int? ignorarSetorId, CancellationToken ct) =>
        await db.Setores.AsNoTracking()
            .Where(s => s.Gestor != null && s.Gestor.FuncionarioId == funcionarioId && (ignorarSetorId == null || s.Id != ignorarSetorId))
            .OrderBy(s => s.Id)
            .Select(s => s.Nome)
            .FirstOrDefaultAsync(ct);

    public async Task<Gestor?> ObterGestorDaPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Gestores.Include(g => g.Funcionario).FirstOrDefaultAsync(g => g.FuncionarioId == funcionarioId, ct);

    public async Task<NivelDeAcesso> ObterNivelAsync(Perfil perfil, CancellationToken ct)
    {
        var nome = NomesDeNivel.DoPerfil(perfil);
        return await db.NiveisDeAcesso.FirstAsync(n => n.Nome == nome, ct);
    }

    public void AdicionarGestor(Gestor gestor) => db.Gestores.Add(gestor);

    public void AdicionarSetor(Setor setor) => db.Setores.Add(setor);

    public async Task SalvarGestorEOsNiveisAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    public async Task DefinirGestorDoColaboradorAsync(int funcionarioId, int gestorId, CancellationToken ct) =>
        // AUDITORIA-OK: o gestor do colaborador é derivado do setor; o evento é o da troca de gestor do setor (fatia 1c)
        await db.Funcionarios
            .Where(f => f.Id == funcionarioId && f.Ativo)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.GestorId, (int?)gestorId), ct);

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

    private Expression<Func<Setor, Linha>> Projecao(DateOnly hoje) =>
        s => new Linha(
            s.Id,
            s.Nome,
            s.Descricao,
            s.GestorId,
            s.Gestor != null ? s.Gestor.FuncionarioId : (int?)null,
            s.Gestor != null ? s.Gestor.Funcionario.Nome : null,
            db.Funcionarios.Count(f => f.SetorId == s.Id && f.Ativo && f.DataAdmissao <= hoje));

    private static SetorItem Item(Linha l) =>
        new(l.Id, l.Nome, l.Descricao, l is { GestorId: { } gestorId, GestorFuncionarioId: { } funcionarioId, GestorNome: { } nome } ? new GestorReferencia(gestorId, funcionarioId, nome) : null, l.QuantidadePessoas);
}

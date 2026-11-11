using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestores;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Gestores;

/// <summary>
/// Os gestores no banco. Quem é gestor: linha em <c>GESTORES</c>, pessoa ativa e (gerencia um setor ou tem o nível <c>RH</c>). A equipe é contada pela definição única da 1a (<c>DaEquipe</c>), uma consulta por gestor: são poucos
/// gestores, então a lista não agrupa. O banco permite um gestor em mais de um setor (<c>SETORES.ID_GESTOR</c> não é único); vale o de menor id, com aviso no log, como o escopo do gestor logado.
/// </summary>
public sealed class GestoresRepositorio(AppDbContext db, ILogger<GestoresRepositorio> log) : IGestoresRepositorio
{
    private static readonly string NivelRh = NomesDeNivel.DoPerfil(Perfil.RH);

    public async Task<IReadOnlyList<GestorItem>> ListarAsync(DateOnly hoje, CancellationToken ct) => await LerAsync(null, hoje, ct);

    public async Task<GestorItem?> ObterAsync(int gestorId, DateOnly hoje, CancellationToken ct) => (await LerAsync(gestorId, hoje, ct)).SingleOrDefault();

    public async Task<Gestor?> ObterParaAusenciaAsync(int gestorId, CancellationToken ct) =>
        await db.Gestores.Include(g => g.Funcionario).FirstOrDefaultAsync(g => g.Id == gestorId, ct);

    public async Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct) =>
        await db.Funcionarios.Include(f => f.NivelDeAcesso).FirstOrDefaultAsync(f => f.Id == funcionarioId, ct);

    public async Task<bool> JaECadastradoAsync(int funcionarioId, CancellationToken ct) => await db.Gestores.AnyAsync(g => g.FuncionarioId == funcionarioId, ct);

    public async Task<Gestor> AdicionarAsync(Funcionario pessoa, CancellationToken ct)
    {
        var gestor = Gestor.Criar(pessoa);
        db.Gestores.Add(gestor);
        await SalvarAsync(ct);
        return gestor;
    }

    public async Task SalvarAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveRepetida(erro))
        {
            // A verificação passou e outra chamada cadastrou a mesma pessoa antes (a corrida): a chave única do banco decide. A mensagem do MySQL traz o valor e nunca é mostrada nem registrada.
            throw MensagensDoApoio.GestorJaCadastrado.ComoConflito();
        }
    }

    private async Task<List<GestorItem>> LerAsync(int? gestorId, DateOnly hoje, CancellationToken ct)
    {
        var consulta = db.Gestores.AsNoTracking()
            .Where(g => g.Funcionario.Ativo && (db.Setores.Any(s => s.GestorId == g.Id) || g.Funcionario.NivelDeAcesso!.Nome == NivelRh));
        if (gestorId is { } id)
        {
            consulta = consulta.Where(g => g.Id == id);
        }

        var gestores = await consulta
            .Include(g => g.Funcionario).ThenInclude(f => f.Cargo)
            .Include(g => g.Funcionario).ThenInclude(f => f.Setor)
            .OrderBy(g => g.Id)
            .ToListAsync(ct);

        var ids = gestores.Select(g => g.Id).ToList();
        var setoresGerenciados = await db.Setores.AsNoTracking()
            .Where(s => s.GestorId != null && ids.Contains(s.GestorId.Value))
            .OrderBy(s => s.Id)
            .Select(s => new { GestorId = s.GestorId!.Value, s.Id, s.Nome })
            .ToListAsync(ct);

        var itens = new List<GestorItem>(gestores.Count);
        foreach (var gestor in gestores)
        {
            var geridos = setoresGerenciados.Where(s => s.GestorId == gestor.Id).ToList();
            if (geridos.Count > 1)
            {
                log.LogWarning("O gestor {GestorId} responde por {Quantidade} setores; vale o de menor id.", gestor.Id, geridos.Count);
            }

            var setor = geridos.FirstOrDefault();
            var quantidadeEquipe = setor is null
                ? 0
                : await db.Funcionarios.DaEquipe(new EquipeDoGestor(gestor.Id, setor.Id, setor.Nome, gestor.FuncionarioId, hoje)).CountAsync(ct);
            var pessoa = gestor.Funcionario;
            itens.Add(new GestorItem(
                gestor.Id,
                new FuncionarioResumo(pessoa.Id, pessoa.Nome, pessoa.Ativo, pessoa.Cargo?.Nome, pessoa.Setor?.Nome),
                setor is null ? null : new SetorReferencia(setor.Id, setor.Nome),
                SoDecidePedidosDeGestores: setor is null,
                quantidadeEquipe,
                gestor.Ausente,
                gestor.AusenteAte,
                gestor.AusenteEm(hoje)));
        }

        return itens;
    }
}

using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Gestao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Beneficios;

/// <summary>
/// As gravações dos pedidos de benefício. O pedido nasce na transação em que a pessoa já foi travada (a mesma trava de férias e do ponto): é o que serializa dois pedidos da mesma pessoa e o pedido e a edição do RH. A recusa do banco (a chave única da adesão, o trigger de pessoa inativa) sobe já
/// traduzida (<see cref="TradutorDeRecusaDaGestao"/>). Estas entidades não são auditadas pelo interceptor: os eventos são escritos à mão pelos casos de uso e nunca levam dado de dependente nem de trajeto.
/// </summary>
public sealed class AdesoesRepositorio(AppDbContext db) : IAdesoesRepositorio
{
    public async Task<PessoaParaBeneficio?> TravarPessoaAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct);
        return pessoa is null ? null : new PessoaParaBeneficio(pessoa.Id, pessoa.Nome, pessoa.Ativo);
    }

    public async Task<ItemDoCatalogo?> ItemDoCatalogoAsync(int beneficioId, CancellationToken ct) =>
        await db.Beneficios.AsNoTracking()
            .Where(b => b.Id == beneficioId)
            .Select(b => new ItemDoCatalogo(b.Id, b.Nome, b.Descricao, b.Opcional, b.AceitaDependentes, b.AceitaTrajeto))
            .FirstOrDefaultAsync(ct);

    public async Task<AdesaoDaPessoa?> AdesaoDaPessoaAsync(int funcionarioId, int beneficioId, CancellationToken ct) =>
        await db.BeneficiosDosFuncionarios.AsNoTracking()
            .Where(l => l.FuncionarioId == funcionarioId && l.BeneficioId == beneficioId)
            .Select(l => new AdesaoDaPessoa(l.Id, l.BeneficioId, l.Status, l.Ativo, l.DataInicioDesejada, l.AutorizaDescontoNaFolha, l.MotivoDaRecusa, l.RespondidoEmUtc, l.PedidoEmUtc))
            .FirstOrDefaultAsync(ct);

    public async Task<AdesaoParaFilho?> AdesaoDaPessoaPorIdAsync(int adesaoId, int funcionarioId, CancellationToken ct) =>
        await (from l in db.BeneficiosDosFuncionarios.AsNoTracking()
               join b in db.Beneficios.AsNoTracking() on l.BeneficioId equals b.Id
               where l.Id == adesaoId && l.FuncionarioId == funcionarioId
               select new AdesaoParaFilho(l.Id, l.FuncionarioId, l.BeneficioId, b.Nome, b.AceitaDependentes, b.AceitaTrajeto, l.Status, l.Ativo))
            .FirstOrDefaultAsync(ct);

    public async Task<int> CriarAdesaoAsync(NovaAdesao dados, CancellationToken ct)
    {
        var linha = BeneficioDoFuncionario.Pedir(dados.FuncionarioId, dados.BeneficioId, dados.DataInicioDesejada, dados.AutorizaDescontoEmFolha, dados.AgoraUtc);
        db.BeneficiosDosFuncionarios.Add(linha);
        await SalvarTraduzindoAsync(ct);
        await GravarOsFilhosAsync(linha.Id, dados, ct);
        return linha.Id;
    }

    public async Task ReabrirAdesaoAsync(int adesaoId, NovaAdesao dados, CancellationToken ct)
    {
        var linha = await db.BeneficiosDosFuncionarios.FirstAsync(l => l.Id == adesaoId, ct);
        linha.Reabrir(dados.DataInicioDesejada, dados.AutorizaDescontoEmFolha, dados.AgoraUtc);
        await SalvarTraduzindoAsync(ct);

        // AUDITORIA-OK: o cancelamento dos filhos pendentes não tem evento próprio: o evento da reabertura da adesão, escrito à mão pelo caso de uso na mesma transação, já diz que o pedido recomeçou; o ExecuteUpdate não passa pelo interceptor do EF. Sem resposta (RH e data ficam nulos juntos, CHECK).
        await db.Dependentes.Where(d => d.AdesaoId == adesaoId && d.Status == StatusDoPedidoDeBeneficio.Pendente).ExecuteUpdateAsync(s => s.SetProperty(d => d.Status, StatusDoPedidoDeBeneficio.Cancelado), ct);

        // AUDITORIA-OK: idem para o trajeto pendente anterior: o evento da reabertura é o registro; o ExecuteUpdate não passa pelo interceptor do EF.
        await db.TrajetosDeValeTransporte.Where(t => t.AdesaoId == adesaoId && t.Status == StatusDoPedidoDeBeneficio.Pendente).ExecuteUpdateAsync(s => s.SetProperty(t => t.Status, StatusDoPedidoDeBeneficio.Cancelado), ct);

        await GravarOsFilhosAsync(adesaoId, dados, ct);
    }

    public async Task<int> CriarDependenteAsync(int adesaoId, DadosDoDependente dependente, DateTime agoraUtc, CancellationToken ct)
    {
        var novo = Dependente.Pedir(adesaoId, dependente.Nome, dependente.Parentesco, dependente.DataNascimento, agoraUtc);
        db.Dependentes.Add(novo);
        await db.SaveChangesAsync(ct);
        return novo.Id;
    }

    public async Task<int> CriarTrajetoAsync(int adesaoId, DadosDoTrajeto trajeto, DateTime agoraUtc, CancellationToken ct)
    {
        var novo = TrajetoDeValeTransporte.Pedir(adesaoId, trajeto.Origem, trajeto.Destino, trajeto.LinhasMeios, agoraUtc);
        db.TrajetosDeValeTransporte.Add(novo);
        await db.SaveChangesAsync(ct);
        return novo.Id;
    }

    private async Task GravarOsFilhosAsync(int adesaoId, NovaAdesao dados, CancellationToken ct)
    {
        foreach (var dependente in dados.Dependentes)
        {
            db.Dependentes.Add(Dependente.Pedir(adesaoId, dependente.Nome, dependente.Parentesco, dependente.DataNascimento, dados.AgoraUtc));
        }

        if (dados.Trajeto is { } trajeto)
        {
            db.TrajetosDeValeTransporte.Add(TrajetoDeValeTransporte.Pedir(adesaoId, trajeto.Origem, trajeto.Destino, trajeto.LinhasMeios, dados.AgoraUtc));
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>A chave única da adesão e o trigger de pessoa inativa viram a resposta do contrato; o que o contexto acumulou se descarta.</summary>
    private async Task SalvarTraduzindoAsync(CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }
}

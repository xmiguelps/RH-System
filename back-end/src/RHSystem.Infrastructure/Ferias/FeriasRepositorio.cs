using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Colaboradores;
using RHSystem.Infrastructure.Gestao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// As gravações de férias. O pedido nasce na transação em que a pessoa já foi travada (a mesma trava do ponto): é o que serializa dois pedidos, o pedido e o registro do RH, o aceite da sugestão e o pedido, e o saldo nunca estoura. A recusa de trigger do banco sobe já traduzida
/// (<see cref="TradutorDeRecusaDaGestao"/>); as entidades de férias não são auditadas pelo interceptor, os eventos são escritos à mão pelos casos de uso.
/// </summary>
public sealed class FeriasRepositorio(AppDbContext db) : IFeriasRepositorio
{
    public async Task<PessoaParaFerias?> TravarPessoaAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await OperacoesComTrava.TravarFuncionarioAsync(db, funcionarioId, ct);
        return pessoa is null ? null : new PessoaParaFerias(pessoa.Id, pessoa.Nome, pessoa.Ativo, pessoa.DataAdmissao, pessoa.DataDesligamento);
    }

    public async Task<int> CriarAsync(PedidoDeFerias pedido, CancellationToken ct)
    {
        db.PedidosDeFerias.Add(pedido);
        try
        {
            await db.SaveChangesAsync(ct);
            return pedido.Id;
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDaGestao.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }
}

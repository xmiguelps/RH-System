using System.Data;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// O que as operações que mudam o vínculo de uma pessoa (desligar, cancelar, efetivar, readmitir) têm em comum: a transação <c>READ COMMITTED</c> e as travas <c>FOR UPDATE</c>, SEMPRE na mesma ordem: a linha de
/// <c>FUNCIONARIOS</c> e depois a de <c>DESLIGAMENTOS_AGENDADOS</c>. Travar a pessoa primeiro é o que serializa o clique duplo e a corrida entre as operações. <c>READ COMMITTED</c> porque, no padrão, o
/// <c>FOR UPDATE</c> de uma linha que ainda não existe segura bloqueio de intervalo e duas pessoas diferentes dariam deadlock.
/// </summary>
internal static class OperacoesComTrava
{
    /// <summary>
    /// A transação da operação, <c>READ COMMITTED</c>. Se algo lança, o que o contexto acumulou (a pessoa já "desligada" ou "readmitida" em memória, por exemplo) é descartado: a recuperação chama a efetivação
    /// várias vezes no mesmo escopo, e uma pessoa que falhou não pode ser gravada junto da próxima.
    /// </summary>
    public static async Task<T> EmTransacaoAsync<T>(AppDbContext db, IUnidadeDeTrabalho unidade, Func<Task<T>> acao, CancellationToken ct)
    {
        try
        {
            return await unidade.ExecutarEmTransacaoAsync(acao, IsolationLevel.ReadCommitted, ct);
        }
        catch
        {
            db.ChangeTracker.Clear();
            throw;
        }
    }

    public static async Task<Funcionario?> TravarFuncionarioAsync(AppDbContext db, int funcionarioId, CancellationToken ct)
    {
        var jaRastreado = db.ChangeTracker.Entries<Funcionario>().Any(e => e.Entity.Id == funcionarioId);
        var pessoa = (await db.Funcionarios.FromSql($"SELECT * FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId} FOR UPDATE").ToListAsync(ct)).FirstOrDefault();
        if (pessoa is not null && jaRastreado)
        {
            await db.Entry(pessoa).ReloadAsync(ct); // o EF devolveria os valores de antes da trava
        }

        return pessoa;
    }

    /// <summary>O desligamento vigente (<c>AGENDADO</c> ou <c>EFETIVADO</c>) da pessoa, travado; o banco garante um só.</summary>
    public static async Task<DesligamentoAgendado?> TravarVigenteAsync(AppDbContext db, int funcionarioId, CancellationToken ct)
    {
        var jaRastreado = db.ChangeTracker.Entries<DesligamentoAgendado>().Any(e => e.Entity.FuncionarioId == funcionarioId);
        var linha = (await db.DesligamentosAgendados
            .FromSql($"SELECT * FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {funcionarioId} AND VIGENTE = 1 ORDER BY ID_DESLIGAMENTO_AGENDADO FOR UPDATE")
            .ToListAsync(ct)).FirstOrDefault();
        if (linha is not null && jaRastreado)
        {
            await db.Entry(linha).ReloadAsync(ct);
        }

        return linha;
    }

    public static async Task<DesligamentoAgendado?> TravarPorIdAsync(AppDbContext db, int desligamentoId, CancellationToken ct)
    {
        var jaRastreado = db.ChangeTracker.Entries<DesligamentoAgendado>().Any(e => e.Entity.Id == desligamentoId);
        var linha = (await db.DesligamentosAgendados
            .FromSql($"SELECT * FROM DESLIGAMENTOS_AGENDADOS WHERE ID_DESLIGAMENTO_AGENDADO = {desligamentoId} FOR UPDATE")
            .ToListAsync(ct)).FirstOrDefault();
        if (linha is not null && jaRastreado)
        {
            await db.Entry(linha).ReloadAsync(ct);
        }

        return linha;
    }
}

using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Ferias.Regras;

/// <summary>Uma pessoa ativa do setor (o gestor do setor também é pessoa do setor e trabalha) e quando foi admitida.</summary>
public sealed record MembroDoSetor(int Id, FuncionarioResumo Resumo, DateOnly DataAdmissao);

/// <summary>Férias já aprovadas (<c>APROVADA</c>, <c>EM_GOZO</c> ou <c>CONCLUIDA</c>) de uma pessoa do setor.</summary>
public sealed record FeriasAprovadaDoSetor(int FuncionarioId, DateOnly Inicio, DateOnly Fim);

/// <summary>Férias já aprovadas de outra pessoa do setor que coincidem com as datas pedidas (<c>FeriasConflito</c> do contrato).</summary>
public sealed record FeriasConflito(FuncionarioResumo Funcionario, DateOnly DataInicio, DateOnly DataFim, int DiasEmComum);

public sealed record ContextoDoPedidoDaEquipe(IReadOnlyList<FeriasConflito> Conflitos, int? EquipeDisponivel);

/// <summary>
/// O que o gestor e o RH veem ao lado do pedido: os conflitos (só enquanto o pedido está em aberto; vazio quando já terminou) e quantas pessoas do setor ficam trabalhando no período (ativas, já admitidas até o último dia, sem quem pede e sem quem tem férias aprovadas no período).
/// Sem setor, o pedido não tem contexto de equipe (<c>equipeDisponivel</c> nulo).
/// </summary>
public static class ConflitosDeFerias
{
    public static ContextoDoPedidoDaEquipe Calcular(int funcionarioId, DateOnly inicio, DateOnly fim, bool pedidoEmAberto, int? setorId, IReadOnlyList<MembroDoSetor> membros, IReadOnlyList<FeriasAprovadaDoSetor> aprovadas)
    {
        if (setorId is null)
        {
            return new ContextoDoPedidoDaEquipe([], null);
        }

        var porId = membros.ToDictionary(m => m.Id);
        var coincidem = aprovadas.Where(a => a.FuncionarioId != funcionarioId && porId.ContainsKey(a.FuncionarioId) && a.Inicio <= fim && a.Fim >= inicio).ToList();
        var conflitos = pedidoEmAberto
            ? coincidem.OrderBy(a => a.Inicio).ThenBy(a => a.FuncionarioId)
                .Select(a => new FeriasConflito(porId[a.FuncionarioId].Resumo, a.Inicio, a.Fim, DiasEmComum(inicio, fim, a.Inicio, a.Fim)))
                .ToList()
            : [];

        var comFerias = coincidem.Select(a => a.FuncionarioId).ToHashSet();
        var trabalhando = membros.Count(m => m.Id != funcionarioId && m.DataAdmissao <= fim && !comFerias.Contains(m.Id));
        return new ContextoDoPedidoDaEquipe(conflitos, trabalhando);
    }

    private static int DiasEmComum(DateOnly inicioA, DateOnly fimA, DateOnly inicioB, DateOnly fimB)
    {
        var de = inicioA > inicioB ? inicioA : inicioB;
        var ate = fimA < fimB ? fimA : fimB;
        return ate.DayNumber - de.DayNumber + 1;
    }
}

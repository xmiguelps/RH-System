using RHSystem.Application.Escopo;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia;

public static class EscopoDeEquipeExtensions
{
    /// <summary>
    /// A definição única da equipe do gestor (contrato, fatia 1): as pessoas ativas, com a admissão já começada (<c>DataAdmissao &lt;= e.Hoje</c>, o dia
    /// em -03:00), do setor <c>e.SetorId</c>, sem o próprio gestor. Para tabelas com <c>ID_FUNCIONARIO</c> (ponto, férias, advertências...), filtre por
    /// <c>db.Funcionarios.DaEquipe(e).Select(f =&gt; f.Id).Contains(x.FuncionarioId)</c>.
    /// </summary>
    public static IQueryable<Funcionario> DaEquipe(this IQueryable<Funcionario> consulta, EquipeDoGestor equipe)
    {
        var hoje = equipe.Hoje;
        var setorId = equipe.SetorId;
        var gestorFuncionarioId = equipe.GestorFuncionarioId;
        return consulta.Where(f => f.Ativo && f.DataAdmissao <= hoje && f.SetorId == setorId && f.Id != gestorFuncionarioId);
    }
}

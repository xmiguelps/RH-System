namespace RHSystem.Domain.Beneficios;

/// <summary>O estado do pedido de um benefício (<c>BENEFICIOS_FUNCIONARIOS.STATUS_PEDIDO</c>: <c>PENDENTE</c>, <c>APROVADO</c>, <c>RECUSADO</c>, <c>CANCELADO</c>).</summary>
public enum StatusDoPedidoDeBeneficio
{
    Pendente,
    Aprovado,
    Recusado,
    Cancelado,
}

/// <summary>
/// Uma linha de <c>BENEFICIOS_FUNCIONARIOS</c>: o benefício que a pessoa tem. A fatia 1b só cria linhas <c>APROVADO</c> (o RH concede no cadastro e na
/// edição) e só desativa ou reativa linhas <c>APROVADO</c>; o pedido do colaborador (colunas de resposta e de recusa) é da fatia 7.
/// </summary>
public class BeneficioDoFuncionario
{
    private BeneficioDoFuncionario()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public int BeneficioId { get; private set; }

    public Beneficio? Beneficio { get; private set; }

    public bool Ativo { get; private set; }

    public StatusDoPedidoDeBeneficio Status { get; private set; }

    /// <summary>Uma linha nova: ativa e aprovada.</summary>
    public static BeneficioDoFuncionario Conceder(int funcionarioId, int beneficioId) =>
        new()
        {
            FuncionarioId = funcionarioId,
            BeneficioId = beneficioId,
            Ativo = true,
            Status = StatusDoPedidoDeBeneficio.Aprovado,
        };

    /// <summary>
    /// O RH concede um benefício que já tem linha para a pessoa (pendente, recusado, cancelado ou aprovado e desativado): a chave única
    /// <c>(ID_FUNCIONARIO, ID_BENEFICIO)</c> impede uma segunda linha, então a mesma é atualizada para ativa e aprovada.
    /// </summary>
    public void Reconceder()
    {
        Status = StatusDoPedidoDeBeneficio.Aprovado;
        Ativo = true;
    }

    public void Desativar() => Ativo = false;

    /// <summary>O <c>CHECK</c> do banco só aceita ativo com <c>APROVADO</c>: reativar outra coisa seria recusado na gravação, então nem tenta.</summary>
    public void Reativar()
    {
        if (Status != StatusDoPedidoDeBeneficio.Aprovado)
        {
            throw new InvalidOperationException("Só um benefício aprovado pode ser reativado.");
        }

        Ativo = true;
    }
}

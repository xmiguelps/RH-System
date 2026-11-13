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
/// Uma linha de <c>BENEFICIOS_FUNCIONARIOS</c>: o benefício que a pessoa tem ou pediu. A fatia 1b cria linhas <c>APROVADO</c> (o RH concede no cadastro e na edição); a fatia 7 acrescenta o PEDIDO da pessoa (<see cref="Pedir"/>, <see cref="Reabrir"/>) e a resposta do RH. As colunas do pedido
/// são nulas na concessão direta. Os instantes são UTC na aplicação e -03:00 no banco, sempre pelo relógio da aplicação (o <c>DEFAULT</c> do banco não vale). Esta entidade não é <c>[Auditado]</c>: os eventos de benefício são escritos à mão e nunca levam dependente nem trajeto.
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

    /// <summary>Quando a pessoa pediu (<c>DATA_PEDIDO</c>); <c>null</c> na concessão direta do RH, que não é pedido.</summary>
    public DateTime? PedidoEmUtc { get; private set; }

    public DateOnly? DataInicioDesejada { get; private set; }

    /// <summary>A autorização de desconto em folha da pessoa (<c>AUTORIZA_DESCONTO_FOLHA</c>); o padrão <c>FALSE</c> do banco não é declarado.</summary>
    public bool AutorizaDescontoNaFolha { get; private set; }

    /// <summary>Quem do RH respondeu e quando (as duas colunas andam juntas, <c>CHECK</c>).</summary>
    public int? FuncionarioRhId { get; private set; }

    public DateTime? RespondidoEmUtc { get; private set; }

    public string? MotivoDaRecusa { get; private set; }

    /// <summary>O pedido da pessoa: nasce <c>PENDENTE</c> e INATIVO (o <c>CHECK</c> só aceita ativo com <c>APROVADO</c>).</summary>
    public static BeneficioDoFuncionario Pedir(int funcionarioId, int beneficioId, DateOnly? dataInicioDesejada, bool autorizaDescontoNaFolha, DateTime agoraUtc) =>
        new()
        {
            FuncionarioId = funcionarioId,
            BeneficioId = beneficioId,
            Ativo = false,
            Status = StatusDoPedidoDeBeneficio.Pendente,
            PedidoEmUtc = AoSegundo(agoraUtc),
            DataInicioDesejada = dataInicioDesejada,
            AutorizaDescontoNaFolha = autorizaDescontoNaFolha,
        };

    /// <summary>
    /// A mesma linha volta a ser um pedido <c>PENDENTE</c> (a chave única <c>(ID_FUNCIONARIO, ID_BENEFICIO)</c> impede uma segunda): recusado, cancelado ou aprovado que o RH tirou. Limpa a resposta e a data dela JUNTAS e o motivo (os <c>CHECK</c> exigem as duas colunas de resposta juntas).
    /// </summary>
    public void Reabrir(DateOnly? dataInicioDesejada, bool autorizaDescontoNaFolha, DateTime agoraUtc)
    {
        Status = StatusDoPedidoDeBeneficio.Pendente;
        Ativo = false;
        PedidoEmUtc = AoSegundo(agoraUtc);
        DataInicioDesejada = dataInicioDesejada;
        AutorizaDescontoNaFolha = autorizaDescontoNaFolha;
        FuncionarioRhId = null;
        RespondidoEmUtc = null;
        MotivoDaRecusa = null;
    }

    internal static DateTime AoSegundo(DateTime instanteUtc)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante precisa ter Kind = Utc.", nameof(instanteUtc));
        }

        return new DateTime(instanteUtc.Ticks - (instanteUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }

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

namespace RHSystem.Domain.Beneficios;

/// <summary>
/// Um dependente pedido numa adesão (<c>DEPENDENTES</c>): nome, parentesco (texto livre de 1 a 30) e nascimento (dado de menor de idade). Nasce <c>PENDENTE</c>; o RH o aprova junto da adesão ou à parte, e a recusa leva o motivo. O <c>DEFAULT CURRENT_TIMESTAMP</c> do banco em <c>DATA_PEDIDO</c> não vale:
/// a data vem do relógio da aplicação, em -03:00. Os dados do dependente NUNCA vão para o log de auditoria nem para rota de gestor.
/// </summary>
public class Dependente
{
    private Dependente()
    {
    }

    public int Id { get; private set; }

    /// <summary>A adesão a que pertence (<c>ID_BENEFICIO_FUNCIONARIOS</c>).</summary>
    public int AdesaoId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Parentesco { get; private set; } = string.Empty;

    public DateOnly DataDeNascimento { get; private set; }

    public StatusDoPedidoDeBeneficio Status { get; private set; }

    public DateTime PedidoEmUtc { get; private set; }

    public int? FuncionarioRhId { get; private set; }

    public DateTime? RespondidoEmUtc { get; private set; }

    public string? MotivoDaRecusa { get; private set; }

    public static Dependente Pedir(int adesaoId, string nome, string parentesco, DateOnly dataDeNascimento, DateTime agoraUtc) =>
        new()
        {
            AdesaoId = adesaoId,
            Nome = nome,
            Parentesco = parentesco,
            DataDeNascimento = dataDeNascimento,
            Status = StatusDoPedidoDeBeneficio.Pendente,
            PedidoEmUtc = BeneficioDoFuncionario.AoSegundo(agoraUtc),
        };
}

/// <summary>
/// Um trajeto do vale-transporte pedido numa adesão (<c>TRAJETOS_VALE_TRANSPORTE</c>): origem (o endereço de casa, dado pessoal), destino e linhas e meios. O pedido novo não apaga o trajeto em vigor (o <c>APROVADO</c> mais recente): ele continua até o RH aprovar o novo. Origem e destino NUNCA vão para o log de
/// auditoria nem para rota de gestor.
/// </summary>
public class TrajetoDeValeTransporte
{
    private TrajetoDeValeTransporte()
    {
    }

    public int Id { get; private set; }

    public int AdesaoId { get; private set; }

    public string Origem { get; private set; } = string.Empty;

    public string Destino { get; private set; } = string.Empty;

    public string LinhasEMeios { get; private set; } = string.Empty;

    public StatusDoPedidoDeBeneficio Status { get; private set; }

    public DateTime PedidoEmUtc { get; private set; }

    public int? FuncionarioRhId { get; private set; }

    public DateTime? RespondidoEmUtc { get; private set; }

    public string? MotivoDaRecusa { get; private set; }

    public static TrajetoDeValeTransporte Pedir(int adesaoId, string origem, string destino, string linhasEMeios, DateTime agoraUtc) =>
        new()
        {
            AdesaoId = adesaoId,
            Origem = origem,
            Destino = destino,
            LinhasEMeios = linhasEMeios,
            Status = StatusDoPedidoDeBeneficio.Pendente,
            PedidoEmUtc = BeneficioDoFuncionario.AoSegundo(agoraUtc),
        };
}

using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes;

// Os formatos do contrato (fatia 7, benefícios). Dependente e trajeto são dado pessoal: só a própria pessoa e o RH os veem, nunca o gestor, e nenhum dos dois entra no log de auditoria.

/// <summary>O item do catálogo como as telas o mostram.</summary>
public sealed record BeneficioDoCatalogoDto(int Id, string Nome, string? Descricao);

public sealed record DependenteDto(
    int Id,
    string Nome,
    string Parentesco,
    DateOnly DataNascimento,
    StatusDoPedidoDeBeneficio Situacao,
    DateTime PedidoEmUtc,
    DateTime? RespondidoEmUtc,
    string? MotivoRecusa);

public sealed record TrajetoDto(
    int Id,
    string Origem,
    string Destino,
    string LinhasMeios,
    StatusDoPedidoDeBeneficio Situacao,
    DateTime PedidoEmUtc,
    DateTime? RespondidoEmUtc,
    string? MotivoRecusa);

/// <summary>Um cartão de "Seus benefícios": os dependentes são só os <c>APROVADO</c> e o trajeto é o em vigor.</summary>
public sealed record CartaoDeBeneficioDto(
    int AdesaoId,
    BeneficioDoCatalogoDto Beneficio,
    bool AceitaDependentes,
    bool AceitaTrajeto,
    DateOnly? InicioEm,
    bool AutorizaDescontoEmFolha,
    IReadOnlyList<DependenteDto> Dependentes,
    TrajetoDto? TrajetoAtual);

public sealed record BeneficioDisponivelDto(BeneficioDoCatalogoDto Beneficio, bool AceitaDependentes, bool AceitaTrajeto, UltimoPedidoDoBeneficio? UltimoPedido);

public sealed record BeneficiosEuResposta(
    IReadOnlyList<CartaoDeBeneficioDto> Beneficios,
    IReadOnlyList<BeneficioDisponivelDto> Disponiveis,
    IReadOnlyList<ResumoDoPedido> Pedidos,
    IReadOnlyList<DadosDoDependente> DependentesCadastrados);

/// <summary>O corpo de <c>pedirAdesaoABeneficio</c>. <c>dependentes</c> e <c>trajeto</c> são opcionais (padrão vazio e nulo).</summary>
public sealed record AdesaoPedirComando
{
    public required int BeneficioId { get; init; }

    public required DateOnly DataInicioDesejada { get; init; }

    public required bool AutorizaDescontoEmFolha { get; init; }

    public IReadOnlyList<DadosDoDependente> Dependentes { get; init; } = [];

    public DadosDoTrajeto? Trajeto { get; init; }
}

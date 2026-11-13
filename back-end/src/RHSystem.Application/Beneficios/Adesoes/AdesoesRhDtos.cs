using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes;

/// <summary>
/// Uma adesão para o RH analisar (<c>BeneficioAdesaoRh</c>), com os dependentes e os trajetos dela de QUALQUER situação. Dado pessoal (dependentes e endereço de casa): só o RH e a própria pessoa veem. <c>PedidoEmUtc</c>, <c>RespostaPrevistaAte</c> e <c>DataInicioDesejada</c> são nulos na adesão que o
/// RH deu direto na ficha (não houve pedido).
/// </summary>
public sealed record AdesaoParaRhDto(
    int AdesaoId,
    FuncionarioResumo Funcionario,
    BeneficioDoCatalogoDto Beneficio,
    StatusDoPedidoDeBeneficio Situacao,
    DateTime? PedidoEmUtc,
    DateOnly? RespostaPrevistaAte,
    DateOnly? DataInicioDesejada,
    bool AutorizaDescontoEmFolha,
    FuncionarioResumo? RespondidoPor,
    DateTime? RespondidoEmUtc,
    string? MotivoRecusa,
    IReadOnlyList<DependenteDto> Dependentes,
    IReadOnlyList<TrajetoDto> Trajetos);

/// <summary>O corpo das três recusas (adesão, dependente e trajeto): o motivo, de 1 a 500 caracteres depois de aparado; a pessoa o vê.</summary>
public sealed record AdesaoRecusarComando
{
    public required string Motivo { get; init; }
}

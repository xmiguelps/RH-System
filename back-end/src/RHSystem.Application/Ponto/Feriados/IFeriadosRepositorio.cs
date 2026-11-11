using RHSystem.Application.Empresa;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Feriados;

/// <summary>O que os casos de uso dos feriados pedem ao banco. A chave <c>(DATA, NOME)</c> repetida vira o 409 <c>FERIADO_DUPLICADO</c> aqui, na gravação.</summary>
public interface IFeriadosRepositorio
{
    /// <summary>Os feriados do recorte que valem para a empresa (sem ela, só os nacionais e os da empresa), por data.</summary>
    Task<IReadOnlyList<Feriado>> ListarAsync(FiltroDeFeriados filtro, ConfiguracaoDaEmpresaLida? empresa, CancellationToken ct);

    /// <summary>O feriado RASTREADO (a edição o muda e o <see cref="SalvarEdicaoAsync"/> grava), ou <c>null</c>.</summary>
    Task<Feriado?> ObterParaEditarAsync(int feriadoId, CancellationToken ct);

    Task<int> AdicionarAsync(Feriado feriado, CancellationToken ct);

    Task SalvarEdicaoAsync(CancellationToken ct);
}

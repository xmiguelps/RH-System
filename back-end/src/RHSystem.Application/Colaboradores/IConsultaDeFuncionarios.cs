using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

public interface IConsultaDeFuncionarios
{
    /// <summary>
    /// A lista do RH. <paramref name="p"/> chega validado, com <c>OrdenarPor</c> já no nome do contrato (<c>NOME</c> ou <c>DATA_ADMISSAO</c>, nulo = <c>NOME</c>). A busca procura no nome; se o termo só tem
    /// dígitos e <c>.-/</c> e espaço, procura também no CPF (só os dígitos contam) e na matrícula (contém). Os totais são da empresa inteira. <c>SituacaoHoje</c> só vem para quem é <c>ATIVO</c>.
    /// </summary>
    Task<FuncionarioListaRespostaRh> ListarAsync(ParametrosDeLista p, FuncionarioSituacao situacao, int? setorId, TipoDeContrato? tipoContrato, CancellationToken ct);

    /// <summary>A ficha de qualquer colaborador, ativo ou não; <c>null</c> se não existe.</summary>
    Task<FuncionarioFichaRh?> ObterFichaRhAsync(int funcionarioId, CancellationToken ct);
}

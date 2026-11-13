using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Domain.Ferias;

namespace RHSystem.Application.Ferias;

/// <summary>A pessoa como as férias a enxergam: se está ativa, a admissão e o desligamento (os períodos aquisitivos contam da admissão).</summary>
public sealed record PessoaParaFerias(int Id, string Nome, bool Ativo, DateOnly DataAdmissao, DateOnly? DataDesligamento);

/// <summary>
/// Um pedido de férias como o banco o guarda, com as pessoas já identificadas (a do pedido, a do gestor da etapa e a do RH que decidiu). <see cref="GestorId"/> é <c>GESTORES.ID_GESTOR</c> e <see cref="Gestor"/> a pessoa dessa linha; <see cref="Observacao"/> vem CRUA (o sufixo do trigger de inativação
/// sai no montador).
/// </summary>
public sealed record FeriasLida(
    int Id,
    FuncionarioResumo Funcionario,
    DateOnly PeriodoInicio,
    DateOnly PeriodoFim,
    DateOnly DataInicio,
    DateOnly DataFim,
    bool AbonoPecuniario,
    int DiasAbono,
    bool Adiantar13Salario,
    StatusDasFerias Status,
    DateTime? SolicitadaEmUtc,
    string? Observacao,
    int? GestorId,
    FuncionarioResumo? Gestor,
    ResultadoDoGestorNasFerias? ResultadoDoGestor,
    DateTime? EtapaDoGestorEmUtc,
    bool EtapaDoGestorAutomatica,
    FuncionarioResumo? FuncionarioRh,
    DateTime? ConfirmadaPeloRhEmUtc,
    DateOnly? SugestaoInicio,
    DateOnly? SugestaoFim,
    RespostaDaSugestao? Resposta,
    DateTime? RespostaEmUtc,
    string? MotivoRecusa)
{
    public LinhaDoPedidoDeFerias ComoLinha() =>
        new(Status, GestorId, Gestor?.Id, ResultadoDoGestor, EtapaDoGestorEmUtc, EtapaDoGestorAutomatica, FuncionarioRh?.Id, ConfirmadaPeloRhEmUtc, SugestaoInicio, SugestaoFim, Resposta, RespostaEmUtc, MotivoRecusa);

    public FeriasParaSaldo ParaSaldo() => new(PeriodoInicio, PeriodoFim, DataInicio, DataFim, DiasAbono, Status, ResultadoDoGestor, Resposta);
}

/// <summary>As leituras de férias. Só lê: as gravações são do <see cref="IFeriasRepositorio"/>.</summary>
public interface IConsultaDeFerias
{
    Task<PessoaParaFerias?> PessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Todas as linhas de férias da pessoa (qualquer status), sem ordem garantida.</summary>
    Task<IReadOnlyList<FeriasLida>> DaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Um pedido da pessoa; o de outra pessoa (ou que não existe) volta <c>null</c>.</summary>
    Task<FeriasLida?> ObterDaPessoaAsync(int feriasId, int funcionarioId, CancellationToken ct);

    /// <summary>A pessoa da linha de <c>GESTORES</c> (<c>ID_GESTOR</c>), como resumo.</summary>
    Task<FuncionarioResumo?> ResumoDoGestorAsync(int gestorId, CancellationToken ct);
}

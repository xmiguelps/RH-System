using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
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

    /// <summary>Os dias de férias disponíveis (o MESMO saldo de <see cref="SaldoDeFerias"/>) de várias pessoas de uma vez, em número CONSTANTE de consultas (o painel da equipe). Quem não existe fica de fora.</summary>
    Task<IReadOnlyDictionary<int, int>> DiasDisponiveisDasPessoasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly hoje, CancellationToken ct);

    /// <summary>Todas as linhas de férias da pessoa (qualquer status), sem ordem garantida.</summary>
    Task<IReadOnlyList<FeriasLida>> DaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Um pedido da pessoa; o de outra pessoa (ou que não existe) volta <c>null</c>.</summary>
    Task<FeriasLida?> ObterDaPessoaAsync(int feriasId, int funcionarioId, CancellationToken ct);

    /// <summary>A pessoa da linha de <c>GESTORES</c> (<c>ID_GESTOR</c>), como resumo.</summary>
    Task<FuncionarioResumo?> ResumoDoGestorAsync(int gestorId, CancellationToken ct);

    /// <summary>
    /// A página dos pedidos da EQUIPE do gestor (os que têm o <c>ID_GESTOR</c> dele e são de pessoas da equipe de hoje), na situação pedida, do mais antigo ao mais novo. O pedido do próprio gestor, o de outro gestor e o de outro setor não entram.
    /// </summary>
    Task<(IReadOnlyList<FeriasLida> Itens, int Total)> ListarDaEquipeAsync(EquipeDoGestor equipe, SituacaoDasFerias situacao, ParametrosDeLista parametros, CancellationToken ct);

    /// <summary>Um pedido da equipe do gestor; fora do escopo (ou que não existe) volta <c>null</c>: o mesmo 404.</summary>
    Task<FeriasLida?> ObterDaEquipeAsync(int feriasId, EquipeDoGestor equipe, CancellationToken ct);

    /// <summary>Os conflitos e a equipe disponível de cada pedido (por <c>id</c>), em poucas consultas para a lista inteira.</summary>
    Task<IReadOnlyDictionary<int, ContextoDoPedidoDaEquipe>> ContextoDaEquipeAsync(IReadOnlyList<FeriasLida> pedidos, CancellationToken ct);

    /// <summary>A página dos pedidos de TODA a empresa (só o RH), na situação pedida, do mais antigo ao mais novo; <paramref name="setorId"/> filtra pelo setor de quem pede e a busca do <paramref name="parametros"/> procura no nome dela.</summary>
    Task<(IReadOnlyList<FeriasLida> Itens, int Total)> ListarParaRhAsync(SituacaoDasFerias situacao, int? setorId, ParametrosDeLista parametros, CancellationToken ct);

    /// <summary>Um pedido de qualquer pessoa (só o RH); que não existe volta <c>null</c>.</summary>
    Task<FeriasLida?> ObterAsync(int feriasId, CancellationToken ct);

    /// <summary>A linha de <c>GESTORES</c> da pessoa (<c>null</c> se ela não está lá): o banco a exige na decisão final de um pedido sem etapa do gestor.</summary>
    Task<int?> GestorIdDaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Os pedidos das pessoas da equipe de hoje do gestor (por pessoa, não pelo <c>ID_GESTOR</c>) que encostam na janela, menos os rejeitados e cancelados.</summary>
    Task<IReadOnlyList<FeriasLida>> DaEquipeNaJanelaAsync(EquipeDoGestor equipe, DateOnly de, DateOnly ate, CancellationToken ct);

    /// <summary>A pessoa como resumo (nome, cargo, setor); <c>null</c> se não existe.</summary>
    Task<FuncionarioResumo?> ResumoAsync(int funcionarioId, CancellationToken ct);

    /// <summary>A pessoa é da equipe de hoje do gestor (ativa, do setor dele, sem ele e sem outros gestores)?</summary>
    Task<bool> EhDaEquipeAsync(int funcionarioId, EquipeDoGestor equipe, CancellationToken ct);

    /// <summary>O e-mail corporativo da pessoa (<c>null</c> quando não tem: nada entra na fila).</summary>
    Task<string?> EmailCorporativoAsync(int funcionarioId, CancellationToken ct);
}

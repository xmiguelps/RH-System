using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>O treinamento como o banco o guarda, com as listas do público e a quantidade de módulos (o que a edição precisa saber para decidir).</summary>
public sealed record TreinamentoLido(
    int Id,
    string Titulo,
    string? Descricao,
    bool Onboarding,
    DateOnly? ComecaEm,
    bool Obrigatorio,
    bool Publicado,
    PublicoDoTreinamento? Publico,
    bool EmiteCertificado,
    bool IncluiAdmitidos,
    DateTime? EncerradoEmUtc,
    IReadOnlyList<int> SetorIds,
    IReadOnlyList<int> FuncionarioIds,
    int TotalDeModulos)
{
    public SituacaoDoTreinamento Situacao(DateOnly hoje) => RegraDaSituacaoDoTreinamento.De(Publicado, ComecaEm, EncerradoEmUtc, hoje);

    public CamposDoTreinamento Campos => new(Titulo, Descricao, ComecaEm, Obrigatorio, Onboarding, EmiteCertificado, Publico, SetorIds, FuncionarioIds, IncluiAdmitidos);
}

/// <summary>A leitura dos treinamentos para o RH.</summary>
public interface IConsultaDeTreinamentos
{
    Task<TreinamentoLido?> ObterAsync(int treinamentoId, CancellationToken ct);

    /// <summary>O treinamento completo (<c>TreinamentoRh</c>): setores com nome, pessoas com cargo e setor, módulos em ordem. <c>null</c> se não existe.</summary>
    Task<TreinamentoRh?> ObterDetalheAsync(int treinamentoId, DateOnly hoje, CancellationToken ct);

    /// <summary>Quantas pessoas o público alcança: só <b>ativas com a admissão já ocorrida</b> (<paramref name="hoje"/> inclusive); <c>TODOS</c> ignora as listas, <c>SETORES</c> usa os setores e <c>PESSOAS</c> as pessoas (número que não existe não conta).</summary>
    Task<int> ContarPublicoAsync(PublicoDoTreinamento publico, IReadOnlyList<int> setorIds, IReadOnlyList<int> funcionarioIds, DateOnly hoje, CancellationToken ct);

    /// <summary>Dos números dados, os que NÃO são setor.</summary>
    Task<IReadOnlyList<int>> SetoresInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>Dos números dados, os que NÃO são pessoa.</summary>
    Task<IReadOnlyList<int>> FuncionariosInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct);
}

/// <summary>A escrita dos treinamentos. Tudo roda na transação do chamador.</summary>
public interface ITreinamentosRepositorio
{
    /// <summary>Insere o rascunho (e as linhas do público) e devolve o número.</summary>
    Task<int> CriarAsync(CamposDoTreinamento campos, CancellationToken ct);

    /// <summary>
    /// Grava os campos por <c>UPDATE</c> condicional ao estado lido (publicação e encerramento iguais; a data de início igual quando é ela que muda): <c>false</c> se outra aba mudou o treinamento no meio. Rascunho: todos os campos e o público trocado (apaga as linhas antigas antes de inserir as novas).
    /// Publicado: só título, descrição, a caixa de admitidos e a data de início.
    /// </summary>
    Task<bool> AtualizarAsync(TreinamentoLido lido, CamposDoTreinamento novos, CancellationToken ct);
}

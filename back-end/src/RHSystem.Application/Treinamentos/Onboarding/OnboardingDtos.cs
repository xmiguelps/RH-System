namespace RHSystem.Application.Treinamentos.Onboarding;

/// <summary>Um treinamento de onboarding em vigor (<c>TreinamentoOnboardingItem</c>): publicado, marcado como onboarding e NÃO encerrado (o agendado entra).</summary>
public sealed record TreinamentoOnboardingItem(int Id, string Titulo, DateOnly? ComecaEm);

/// <summary><c>TreinamentoOnboardingLista</c>: sem paginação; vazia desliga a caixa do cadastro.</summary>
public sealed record TreinamentoOnboardingLista(IReadOnlyList<TreinamentoOnboardingItem> Itens);

/// <summary>Um treinamento em que a pessoa foi incluída (<c>TreinamentoOnboardingInclusaoItem</c>); <c>JaEstavaIncluida</c> quando ela já tinha linha (a chamada é repetível).</summary>
public sealed record TreinamentoOnboardingInclusaoItem(int TreinamentoId, string Titulo, bool JaEstavaIncluida);

/// <summary><c>TreinamentoOnboardingInclusaoResposta</c>.</summary>
public sealed record TreinamentoOnboardingInclusaoResposta(int FuncionarioId, IReadOnlyList<TreinamentoOnboardingInclusaoItem> Treinamentos);

/// <summary>A leitura e a escrita do onboarding dos treinamentos. A escrita roda na transação do chamador.</summary>
public interface IOnboardingDosTreinamentos
{
    /// <summary>Os treinamentos de onboarding em vigor (publicado, <c>TIPO_TREINAMENTO = 'ONBOARDING'</c>, não encerrado), pelo <c>comecaEm</c> (sem data por último) e id.</summary>
    Task<IReadOnlyList<TreinamentoOnboardingItem>> EmVigorAsync(CancellationToken ct);

    /// <summary><c>null</c> se a pessoa não existe; senão se está ativa.</summary>
    Task<bool?> PessoaAtivaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>
    /// Cria a linha <c>INSCRITO</c> da pessoa em cada treinamento dado em que ela ainda não tem linha (qualquer que seja o público; datas vazias) e diz, por treinamento, se ela já estava. A recusa do trigger (pessoa inativa) sobe traduzida em 422.
    /// </summary>
    Task<IReadOnlyList<TreinamentoOnboardingInclusaoItem>> IncluirAsync(int funcionarioId, IReadOnlyList<TreinamentoOnboardingItem> treinamentos, CancellationToken ct);
}

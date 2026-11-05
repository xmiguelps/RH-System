using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A versão padrão de "incluir no onboarding": enquanto não há treinamento (fatia 6) não existe treinamento de onboarding em vigor, e o contrato diz o que acontece com a caixa marcada: 422
/// <c>SEM_TREINAMENTO_DE_ONBOARDING</c>, sem gravar nada. A fatia 6 registra a implementação dela DEPOIS desta (<c>AddScoped</c>, o último registro vale).
/// </summary>
public sealed class OnboardingIndisponivel : IInclusaoNoOnboarding
{
    public Task ConferirAsync(CancellationToken ct) =>
        throw new ErroDeNegocioException("SEM_TREINAMENTO_DE_ONBOARDING", "Não há treinamento de onboarding publicado. Desmarque a caixa ou publique o treinamento.");

    public Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct) =>
        throw new InvalidOperationException("Sem treinamento de onboarding: ConferirAsync já recusou, IncluirAsync nunca devia ser chamado.");
}

/// <summary>
/// A versão padrão de "enviar o formulário de admissão": enquanto não há formulários (fatia 5) nenhum existe, e o contrato diz que formulário que não existe é 400 em
/// <c>formularioAdmissaoId</c>. A fatia 5 registra a implementação dela DEPOIS desta.
/// </summary>
public sealed class FormularioDeAdmissaoIndisponivel : IEnvioDoFormularioDeAdmissao
{
    public Task ConferirAsync(int formularioId, CancellationToken ct) =>
        throw new ErroDeValidacaoException("formularioAdmissaoId", "O formulário de admissão escolhido não existe ou não está publicado.");

    public Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct) =>
        throw new InvalidOperationException("Sem formulários: ConferirAsync já recusou, AdicionarDestinatariosAsync nunca devia ser chamado.");
}

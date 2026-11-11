using RHSystem.Application.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// O <see cref="ICadastroDeFuncionarios"/> de verdade com UMA falha forçada: <see cref="CadastrarAsync"/> deixa o cadastro real inserir TODAS as pessoas (dentro da transação do caso de uso) e DEPOIS lança
/// <see cref="LinhaRecusadaException"/> na linha <c>indice</c>, como um trigger que recusa a 500ª linha (ou, com <c>porDuplicidade</c>, como a chave única que alguém ocupou no meio). O teste prova que a transação desfaz tudo
/// (<see cref="ApiFactory.ComFalhaNoCadastroDaLinha"/>). <c>revalidacao</c>, se vier, é o que a validação responde DEPOIS da falha (o caso de uso a relê para dizer qual dado se repetiu).
/// </summary>
public sealed class CadastroDeFuncionariosComFalha(ICadastroDeFuncionarios real, int indice, string? regraDoBanco, bool porDuplicidade = false, IReadOnlyList<ProblemaDeCadastro>? revalidacao = null) : ICadastroDeFuncionarios
{
    public Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(IReadOnlyList<NovoFuncionario> novos, CancellationToken ct) => revalidacao is null ? real.ValidarAsync(novos, ct) : Task.FromResult(revalidacao);

    public Task<IReadOnlyList<ProblemaDeCadastro>> ValidarEdicaoAsync(int funcionarioId, NovoFuncionario dados, CancellationToken ct) => real.ValidarEdicaoAsync(funcionarioId, dados, ct);

    public async Task<ResultadoDoCadastro> CadastrarAsync(IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro opcoes, CancellationToken ct)
    {
        await real.CadastrarAsync(novos, opcoes, ct);
        throw new LinhaRecusadaException(indice, porDuplicidade, regraDoBanco);
    }
}

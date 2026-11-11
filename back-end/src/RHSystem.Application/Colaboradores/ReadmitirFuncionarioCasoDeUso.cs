using RHSystem.Application.Comum;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>readmitirFuncionario</c> (tela 03, botão "Readmitir"): chama a readmissão (que abre a própria transação) e devolve a ficha atualizada. Se o banco recusar a gravação depois de a validação passar (um
/// gestor de setor inativado no meio), a transação desfaz tudo e a resposta é a 422 com <c>regraDoBanco</c>, pela mesma tradução do cadastro (<see cref="ProblemasDoCadastro"/>).
/// </summary>
public sealed class ReadmitirFuncionarioCasoDeUso(IReadmissaoDeFuncionarios readmissao, IConsultaDeFuncionarios consulta)
{
    public async Task<FuncionarioFichaRh> ExecutarAsync(int funcionarioId, FuncionarioReadmitirComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var dados = new DadosDaReadmissao(comando.DataAdmissao, comando.CargoId, comando.SetorId, comando.TipoContrato, comando.IncluirNoOnboarding, comando.FormularioAdmissaoId, comando.EnviarConvite);

        try
        {
            await readmissao.ReadmitirAsync(funcionarioId, dados, ct);
        }
        catch (LinhaRecusadaException recusada)
        {
            throw await ProblemasDoCadastro.DaRecusaAsync(recusada, () => Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>([]));
        }

        return await consulta.ObterFichaRhAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}

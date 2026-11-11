using RHSystem.Application.Comum;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>inativarFuncionario</c> (tela 03, botão "Inativar"): registra o desligamento a partir do último dia de trabalho (<see cref="IDesligamentoDeFuncionarios.AgendarAsync"/>, que abre a própria transação) e devolve a ficha
/// atualizada, com <c>desligamento</c> <c>AGENDADO</c> (a pessoa continua ativa até o dia passar) ou <c>EFETIVADO</c> (o último dia já tinha passado). 400 em <c>ultimoDiaDeTrabalho</c>, 404 e 409
/// <c>DESLIGAMENTO_JA_REGISTRADO</c> saem do serviço. A palavra "desativar" que a tela pede é só da interface: a API não a recebe.
/// </summary>
public sealed class InativarFuncionarioCasoDeUso(IDesligamentoDeFuncionarios desligamentos, IConsultaDeFuncionarios consulta)
{
    public async Task<FuncionarioFichaRh> ExecutarAsync(int funcionarioId, FuncionarioInativarComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        await desligamentos.AgendarAsync(funcionarioId, comando.UltimoDiaDeTrabalho, ct);
        return await consulta.ObterFichaRhAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}

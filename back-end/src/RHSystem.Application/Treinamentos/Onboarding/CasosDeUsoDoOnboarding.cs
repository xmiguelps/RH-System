using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Treinamentos.Onboarding;

/// <summary><c>GET /api/rh/treinamentos/onboarding</c>: os treinamentos de onboarding em vigor para a caixa "Incluir no treinamento" do cadastro (publicado, marcado como onboarding e não encerrado; o agendado entra). Sem paginação; lista vazia desliga a caixa.</summary>
public sealed class ListarTreinamentosDeOnboardingCasoDeUso(IOnboardingDosTreinamentos onboarding)
{
    public async Task<TreinamentoOnboardingLista> ExecutarAsync(CancellationToken ct) => new(await onboarding.EmVigorAsync(ct));
}

/// <summary>
/// <c>POST /api/rh/funcionarios/{funcionarioId}/treinamentos/onboarding</c>: inclui a pessoa em CADA treinamento de onboarding em vigor, qualquer que seja o público (cria a linha <c>INSCRITO</c>; quem já tem linha não muda e vem com <c>jaEstavaIncluida</c>: repetir é inofensivo). Ordem: 404 (a pessoa não existe), 422 <c>FUNCIONARIO_INATIVO</c>, 422
/// <c>SEM_TREINAMENTO_DE_ONBOARDING</c> (nenhum em vigor; nada é gravado). Evento <c>ALTEROU</c> só quando alguma linha nova foi criada, sem o nome da pessoa.
/// </summary>
public sealed class IncluirFuncionarioNoOnboardingCasoDeUso(IOnboardingDosTreinamentos onboarding, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<TreinamentoOnboardingInclusaoResposta> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        var ativa = await onboarding.PessoaAtivaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        if (!ativa)
        {
            throw MensagensDosTreinamentos.FuncionarioInativo();
        }

        var itens = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var emVigor = await onboarding.EmVigorAsync(ct);
                if (emVigor.Count == 0)
                {
                    throw MensagensDosTreinamentos.SemTreinamentoDeOnboarding.ComoProblema();
                }

                var incluidos = await onboarding.IncluirAsync(funcionarioId, emVigor, ct);
                var novos = incluidos.Count(i => !i.JaEstavaIncluida);
                if (novos > 0)
                {
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Incluiu uma pessoa em {novos} {(novos == 1 ? "treinamento" : "treinamentos")} de onboarding.", "Onboarding dos treinamentos", "FUNCIONARIOS", funcionarioId));
                    await unidade.SalvarAsync(ct);
                }

                return incluidos;
            },
            ct);
        return new TreinamentoOnboardingInclusaoResposta(funcionarioId, itens);
    }
}

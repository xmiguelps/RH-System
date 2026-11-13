using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Onboarding;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// O onboarding dos treinamentos: quais estão em vigor e a inclusão da pessoa. Em vigor = publicado, <c>TIPO_TREINAMENTO = 'ONBOARDING'</c> e NÃO encerrado (o agendado entra: o treinamento aparece para a pessoa quando <c>comecaEm</c> chegar). A pessoa entra em TODOS, qualquer que seja o público. As entidades não são auditadas pelo interceptor; o evento é do caso de uso.
/// </summary>
public sealed class OnboardingDosTreinamentos(AppDbContext db) : IOnboardingDosTreinamentos
{
    public async Task<IReadOnlyList<TreinamentoOnboardingItem>> EmVigorAsync(CancellationToken ct) =>
        await db.Treinamentos.AsNoTracking()
            .Where(t => t.Publicado && t.TipoTreinamento == Treinamento.TipoOnboarding && t.EncerradoEmUtc == null)
            .OrderBy(t => t.ComecaEm == null).ThenBy(t => t.ComecaEm).ThenBy(t => t.Id)
            .Select(t => new TreinamentoOnboardingItem(t.Id, t.Titulo, t.ComecaEm))
            .ToListAsync(ct);

    public async Task<bool?> PessoaAtivaAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await db.Funcionarios.AsNoTracking().Where(f => f.Id == funcionarioId).Select(f => new { f.Ativo }).SingleOrDefaultAsync(ct);
        return pessoa?.Ativo;
    }

    public async Task<IReadOnlyList<TreinamentoOnboardingInclusaoItem>> IncluirAsync(int funcionarioId, IReadOnlyList<TreinamentoOnboardingItem> treinamentos, CancellationToken ct)
    {
        var ids = treinamentos.Select(t => t.Id).ToList();
        var jaTem = (await db.TreinamentosFuncionarios.AsNoTracking().Where(l => l.FuncionarioId == funcionarioId && ids.Contains(l.TreinamentoId)).Select(l => l.TreinamentoId).ToListAsync(ct)).ToHashSet();
        db.TreinamentosFuncionarios.AddRange(treinamentos.Where(t => !jaTem.Contains(t.Id)).Select(t => TreinamentoFuncionario.Criar(t.Id, funcionarioId, SituacaoNoTreinamento.Inscrito)));
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (TradutorDeRecusaDosTreinamentos.Traduzir(erro) is { } recusa)
        {
            db.ChangeTracker.Clear();
            throw recusa;
        }

        return [.. treinamentos.Select(t => new TreinamentoOnboardingInclusaoItem(t.Id, t.Titulo, jaTem.Contains(t.Id)))];
    }
}

/// <summary>
/// A caixa "Incluir no treinamento" do cadastro, da readmissão e da importação (<see cref="IInclusaoNoOnboarding"/>): substitui o <c>OnboardingIndisponivel</c> da fatia 1 (registrada DEPOIS dele, o último registro vale). <c>ConferirAsync</c> recusa com o texto longo do cadastro se nenhum treinamento está em vigor; <c>IncluirAsync</c> roda DENTRO da transação do chamador, lança a mesma recusa se
/// o treinamento foi encerrado entre a conferência e a inclusão (o cadastro desfaz: nunca "0 em silêncio") e devolve QUANTAS PESSOAS entraram em pelo menos um treinamento (3 pessoas e 2 treinamentos = 3; quem já tinha linha em todos não conta).
/// </summary>
public sealed class InclusaoNoOnboarding(IOnboardingDosTreinamentos onboarding) : IInclusaoNoOnboarding
{
    private const string Codigo = "SEM_TREINAMENTO_DE_ONBOARDING";

    private const string Texto = "Não há treinamento de onboarding publicado. Desmarque a caixa ou publique o treinamento.";

    public async Task ConferirAsync(CancellationToken ct)
    {
        if ((await onboarding.EmVigorAsync(ct)).Count == 0)
        {
            throw new ErroDeNegocioException(Codigo, Texto);
        }
    }

    public async Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct)
    {
        var emVigor = await onboarding.EmVigorAsync(ct);
        if (emVigor.Count == 0)
        {
            throw new ErroDeNegocioException(Codigo, Texto);
        }

        var entraram = 0;
        foreach (var pessoa in funcionarioIds.Distinct())
        {
            if ((await onboarding.IncluirAsync(pessoa, emVigor, ct)).Any(i => !i.JaEstavaIncluida))
            {
                entraram++;
            }
        }

        return entraram;
    }
}

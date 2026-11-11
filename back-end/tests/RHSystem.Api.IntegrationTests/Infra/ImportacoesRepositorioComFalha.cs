using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Importacoes;
using RHSystem.Domain.Importacoes;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// O <see cref="IImportacoesRepositorio"/> de verdade com UMA falha forçada: <see cref="AdicionarAsync"/> grava a linha (dentro da transação do caso de uso) e DEPOIS lança, como um banco que cai no meio. O teste prova que a
/// transação desfaz a linha e o evento e que o caso de uso apaga o blob (<see cref="ApiFactory.ComFalhaNoBancoAoSalvarImportacao"/>).
/// </summary>
public sealed class ImportacoesRepositorioComFalha(IImportacoesRepositorio real) : IImportacoesRepositorio
{
    public const string TextoDaFalha = "falha-simulada-do-banco-ao-salvar-importacao-4343";

    public async Task AdicionarAsync(Importacao importacao, CancellationToken ct)
    {
        await real.AdicionarAsync(importacao, ct);
        throw new DbUpdateException(TextoDaFalha);
    }

    public Task<Importacao?> ObterAsync(int id, CancellationToken ct) => real.ObterAsync(id, ct);

    public Task<bool> ReivindicarAsync(int id, DateTime agoraUtc, CancellationToken ct) => real.ReivindicarAsync(id, agoraUtc, ct);

    public Task RegistrarResultadoAsync(int id, int convites, int onboarding, int formulario, CancellationToken ct) => real.RegistrarResultadoAsync(id, convites, onboarding, formulario, ct);

    public Task<IReadOnlyList<FuncionarioResumo>> ResumirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct) => real.ResumirAsync(funcionarioIds, ct);

    public Task<IReadOnlyList<ImportacaoVencida>> ListarVencidasAsync(DateTime agoraUtc, int maximo, CancellationToken ct) => real.ListarVencidasAsync(agoraUtc, maximo, ct);

    public Task LimparVencidaAsync(int id, bool tambemOCaminho, CancellationToken ct) => real.LimparVencidaAsync(id, tambemOCaminho, ct);
}

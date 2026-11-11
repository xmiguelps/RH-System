using Testcontainers.Azurite;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Um Azurite (o emulador do Azure Blob Storage) em contêiner, com a MESMA imagem do <c>docker-compose.yml</c> (versão fixada, nunca <c>latest</c>). Só <c>ArmazenamentoBlobTests</c> o usa: o resto dos testes da API troca o
/// armazenamento pelo <see cref="ArmazenamentoEmMemoria"/>. Cada teste cria o contêiner de blobs dele (nome único) e não enxerga o dos outros.
/// </summary>
public sealed class AzuriteFixture : IAsyncLifetime
{
    // --skipApiVersionCheck: o SDK fala a versão de API 2026-10-06 e o Azurite 3.37.0 (o mais novo em 2026-11-10) ainda a recusa (o mesmo flag do docker-compose.yml).
    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.37.0").WithCommand("--skipApiVersionCheck").Build();

    /// <summary>A connection string do contêiner (conta <c>devstoreaccount1</c>, com a chave pública conhecida do emulador).</summary>
    public string Conexao => _container.GetConnectionString();

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}

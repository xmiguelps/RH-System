namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Uma API (e um banco com os scripts, 15 s) para os testes que só LEEM dela, como o documento OpenAPI. Os testes que mudam dados
/// (login, renovação) criam a deles.
/// </summary>
public static class ApiCompartilhada
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static ApiFactory? _api;

    public static async Task<ApiFactory> ObterAsync(MySqlFixture mysql)
    {
        await Trava.WaitAsync();
        try
        {
            return _api ??= await ApiFactory.CriarAsync(mysql);
        }
        finally
        {
            Trava.Release();
        }
    }
}

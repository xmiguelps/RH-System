namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// Um banco com os scripts (35 a 40 s para criar) por CLASSE de teste, e não por teste: lição medida na fatia 1a (12 testes com 12 bancos = 7 min 44 s). Cada classe
/// pede o seu pela <c>chave</c>; os testes da classe usam dados próprios (e-mails e nomes com sufixo) e nunca mudam o que outro teste lê.
/// </summary>
public static class BancoCompartilhado
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static readonly Dictionary<string, object> Bancos = [];

    /// <summary>A conexão do banco da classe; <paramref name="preparar"/> roda uma única vez, logo depois dos scripts, e o que devolve vai junto.</summary>
    public static async Task<(string Conexao, T Preparado)> ObterAsync<T>(MySqlFixture mysql, string chave, Func<string, Task<T>> preparar)
    {
        await Trava.WaitAsync();
        try
        {
            if (!Bancos.TryGetValue(chave, out var guardado))
            {
                var conexao = await mysql.NovoBancoComScriptsAsync();
                guardado = (conexao, await preparar(conexao));
                Bancos[chave] = guardado;
            }

            return ((string, T))guardado;
        }
        finally
        {
            Trava.Release();
        }
    }

    public static async Task<string> ObterAsync(MySqlFixture mysql, string chave) =>
        (await ObterAsync(mysql, chave, _ => Task.FromResult(0))).Conexao;
}

using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Escopo;

/// <summary>Uma API com o elenco semeado, para todos os testes de escopo (um banco só). Os testes que precisam mexer em gente criam a deles, em setor próprio.</summary>
internal static class ApiComElenco
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static (ApiFactory Api, ElencoDeTeste Elenco)? _pronta;

    public static async Task<(ApiFactory Api, ElencoDeTeste Elenco)> ObterAsync(MySqlFixture mysql)
    {
        await Trava.WaitAsync();
        try
        {
            if (_pronta is null)
            {
                var api = await ApiFactory.CriarAsync(mysql);
                _pronta = (api, await api.SemearElencoAsync());
            }

            return _pronta.Value;
        }
        finally
        {
            Trava.Release();
        }
    }

    public static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    public static async Task<int> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return Convert.ToInt32(await comando.ExecuteScalarAsync(), System.Globalization.CultureInfo.InvariantCulture);
    }
}

/// <summary>Um <see cref="IAtorAtual"/> escolhido pelo teste (para exercitar o escopo sem passar pela API).</summary>
internal sealed class AtorDeTeste : IAtorAtual
{
    public TipoDeAtorAtual Tipo { get; init; }

    public int? UsuarioId { get; init; }

    public int? FuncionarioId { get; init; }

    public Perfil? Perfil { get; init; }

    public string Nome { get; init; } = "Teste";

    public string? Ip => null;

    public CanalDeOrigem Canal => CanalDeOrigem.Portal;

    public static AtorDeTeste Pessoa(PessoaDeTeste pessoa) =>
        new() { Tipo = TipoDeAtorAtual.Pessoa, UsuarioId = pessoa.UsuarioId, FuncionarioId = pessoa.FuncionarioId, Perfil = pessoa.Perfil, Nome = pessoa.Nome };

    public static AtorDeTeste Anonimo() => new() { Tipo = TipoDeAtorAtual.Anonimo, Nome = "Anônimo" };

    public static AtorDeTeste Sistema() => new() { Tipo = TipoDeAtorAtual.Sistema, Nome = "Sistema" };
}

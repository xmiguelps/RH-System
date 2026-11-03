using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using MySqlConnector;

namespace RHSystem.Infrastructure.Esquema;

/// <summary>
/// Aplica os scripts SQL pendentes, em ordem, e registra cada um em <c>SCHEMA_VERSOES</c> com o hash (SHA-256 do texto sem BOM e com
/// fins de linha normalizados). Recusa: script já aplicado que foi editado; versão pendente menor que a maior aplicada; versão
/// aplicada cujo arquivo sumiu; banco que já tem tabelas mas está sem histórico. Uma trava (<c>GET_LOCK</c>) por banco garante que dois
/// executores simultâneos apliquem cada script uma única vez.
/// </summary>
public sealed class ExecutorDeScripts
{
    private const int SegundosDeEsperaDaTrava = 60;
    private const int TamanhoMaximoDoNomeDaTrava = 64;

    private readonly string _conexao;
    private readonly string _banco;
    private readonly ILogger<ExecutorDeScripts> _log;

    public ExecutorDeScripts(string connectionString, ILogger<ExecutorDeScripts> log)
    {
        var construtor = new MySqlConnectionStringBuilder(connectionString)
        {
            AllowUserVariables = true,
            DefaultCommandTimeout = 600,
        };

        if (string.IsNullOrWhiteSpace(construtor.Database))
        {
            throw new ArgumentException("A connection string precisa dizer o banco (Database=...).", nameof(connectionString));
        }

        _banco = construtor.Database;
        _conexao = construtor.ConnectionString;
        _log = log;
    }

    /// <summary>SHA-256, em hexadecimal minúsculo, do texto sem BOM e com CRLF (e CR solto) normalizados para LF.</summary>
    public static string CalcularHash(string conteudo)
    {
        var normalizado = conteudo.TrimStart('﻿').Replace("\r\n", "\n").Replace('\r', '\n');
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(normalizado))).ToLowerInvariant();
    }

    public async Task AplicarPendentesAsync(IReadOnlyList<ScriptSql> scripts, CancellationToken ct)
    {
        var ordenados = scripts.OrderBy(s => s.Versao).ToList();
        var repetida = ordenados.GroupBy(s => s.Versao).FirstOrDefault(g => g.Count() > 1);
        if (repetida is not null)
        {
            throw new ArgumentException($"A versão {repetida.Key} aparece em mais de um script.", nameof(scripts));
        }

        // Uma conexão só para a trava; cada script usa a sua própria conexão (estado de sessão, como SET FOREIGN_KEY_CHECKS, não vaza).
        await using var conexaoDaTrava = new MySqlConnection(_conexao);
        await conexaoDaTrava.OpenAsync(ct);
        var nomeDaTrava = NomeDaTrava();
        await AdquirirTravaAsync(conexaoDaTrava, nomeDaTrava, ct);

        try
        {
            await GarantirHistoricoAsync(conexaoDaTrava, ct);
            var aplicados = await LerAplicadosAsync(conexaoDaTrava, ct);
            var pendentes = ConferirEListarPendentes(ordenados, aplicados);

            foreach (var script in pendentes)
            {
                await AplicarAsync(script, ct);
            }
        }
        finally
        {
            await LiberarTravaAsync(conexaoDaTrava, nomeDaTrava);
        }
    }

    private string NomeDaTrava()
    {
        var nome = $"rhsystem_esquema:{_banco}";
        if (nome.Length <= TamanhoMaximoDoNomeDaTrava)
        {
            return nome;
        }

        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(_banco)))[..16].ToLowerInvariant();
        return $"rhsystem_esquema:{hash}";
    }

    private static async Task AdquirirTravaAsync(MySqlConnection conexao, string nomeDaTrava, CancellationToken ct)
    {
        await using var comando = new MySqlCommand("SELECT GET_LOCK(@nome, @espera)", conexao);
        comando.Parameters.AddWithValue("@nome", nomeDaTrava);
        comando.Parameters.AddWithValue("@espera", SegundosDeEsperaDaTrava);
        var resultado = await comando.ExecuteScalarAsync(ct);

        // 1 = pegou; 0 = esgotou a espera; NULL = erro no servidor. Só o 1 segue.
        if (resultado is null or DBNull)
        {
            throw new InvalidOperationException("O servidor não conseguiu criar a trava do executor de scripts.");
        }

        if (Convert.ToInt64(resultado, System.Globalization.CultureInfo.InvariantCulture) != 1)
        {
            throw new InvalidOperationException(
                $"Outro executor está aplicando scripts neste banco há mais de {SegundosDeEsperaDaTrava} segundos; esta tentativa desistiu de esperar.");
        }
    }

    private async Task LiberarTravaAsync(MySqlConnection conexao, string nomeDaTrava)
    {
        try
        {
            await using var comando = new MySqlCommand("SELECT RELEASE_LOCK(@nome)", conexao);
            comando.Parameters.AddWithValue("@nome", nomeDaTrava);
            await comando.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (MySqlException erro)
        {
            // Fechar a conexão libera a trava de qualquer forma; só registramos.
            _log.LogWarning(erro, "Não foi possível liberar a trava {Trava} explicitamente.", nomeDaTrava);
        }
    }

    private static async Task GarantirHistoricoAsync(MySqlConnection conexao, CancellationToken ct)
    {
        var tabelasDoBanco = await EscalarAsync(
            conexao,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE' AND LOWER(TABLE_NAME) <> 'schema_versoes'",
            ct);
        var temHistorico = await EscalarAsync(
            conexao,
            "SELECT COUNT(*) FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND LOWER(TABLE_NAME) = 'schema_versoes'",
            ct) > 0;
        var linhasDeHistorico = temHistorico ? await EscalarAsync(conexao, "SELECT COUNT(*) FROM SCHEMA_VERSOES", ct) : 0;

        if (tabelasDoBanco > 0 && linhasDeHistorico == 0)
        {
            throw new BancoSemHistoricoDeScriptsException((int)tabelasDoBanco);
        }

        await using var criar = new MySqlCommand(
            "CREATE TABLE IF NOT EXISTS SCHEMA_VERSOES (VERSAO INT PRIMARY KEY, NOME VARCHAR(200) NOT NULL, HASH_SHA256 CHAR(64) NOT NULL, APLICADO_EM DATETIME(6) NOT NULL) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci",
            conexao);
        await criar.ExecuteNonQueryAsync(ct);
    }

    private static async Task<long> EscalarAsync(MySqlConnection conexao, string sql, CancellationToken ct)
    {
        await using var comando = new MySqlCommand(sql, conexao);
        return Convert.ToInt64(await comando.ExecuteScalarAsync(ct), System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task<Dictionary<int, string>> LerAplicadosAsync(MySqlConnection conexao, CancellationToken ct)
    {
        var aplicados = new Dictionary<int, string>();
        await using var comando = new MySqlCommand("SELECT VERSAO, HASH_SHA256 FROM SCHEMA_VERSOES ORDER BY VERSAO", conexao);
        await using var leitor = await comando.ExecuteReaderAsync(ct);
        while (await leitor.ReadAsync(ct))
        {
            aplicados[leitor.GetInt32(0)] = leitor.GetString(1);
        }

        return aplicados;
    }

    private static List<ScriptSql> ConferirEListarPendentes(List<ScriptSql> scripts, Dictionary<int, string> aplicados)
    {
        foreach (var (versao, hashAplicado) in aplicados.OrderBy(a => a.Key))
        {
            var script = scripts.Find(s => s.Versao == versao) ?? throw new ScriptAplicadoSumiuException(versao);
            if (!string.Equals(CalcularHash(script.Conteudo), hashAplicado, StringComparison.OrdinalIgnoreCase))
            {
                throw new ScriptJaAplicadoFoiAlteradoException(versao);
            }
        }

        var pendentes = scripts.Where(s => !aplicados.ContainsKey(s.Versao)).ToList();
        if (aplicados.Count > 0)
        {
            var maiorAplicada = aplicados.Keys.Max();
            var fora = pendentes.Find(s => s.Versao < maiorAplicada);
            if (fora is not null)
            {
                throw new VersaoForaDeOrdemException(fora.Versao, maiorAplicada);
            }
        }

        return pendentes;
    }

    private async Task AplicarAsync(ScriptSql script, CancellationToken ct)
    {
        var comandos = DivisorDeScript.Dividir(script.Conteudo);
        _log.LogInformation("Aplicando o script V{Versao} ({Nome}), {Comandos} comando(s).", script.Versao.ToString("D3"), script.Nome, comandos.Count);

        await using var conexao = new MySqlConnection(_conexao);
        await conexao.OpenAsync(ct);

        for (var i = 0; i < comandos.Count; i++)
        {
            try
            {
                await using var comando = new MySqlCommand(comandos[i], conexao);
                await comando.ExecuteNonQueryAsync(ct);
            }
            catch (MySqlException erro)
            {
                throw new FalhaNoScriptException(script.Versao, i + 1, erro.Message, erro);
            }
        }

        // Só depois do script inteiro: um script que falhou no meio não fica registrado.
        await using var registrar = new MySqlCommand(
            "INSERT INTO SCHEMA_VERSOES (VERSAO, NOME, HASH_SHA256, APLICADO_EM) VALUES (@versao, @nome, @hash, UTC_TIMESTAMP(6))",
            conexao);
        registrar.Parameters.AddWithValue("@versao", script.Versao);
        registrar.Parameters.AddWithValue("@nome", script.Nome);
        registrar.Parameters.AddWithValue("@hash", CalcularHash(script.Conteudo));
        await registrar.ExecuteNonQueryAsync(ct);
    }
}

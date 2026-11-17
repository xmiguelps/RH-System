using System.Globalization;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// Apoio dos testes da parte D do <c>V007</c> (advertência, treinamento, formulário e desligamento): o dia de hoje pelo relógio do BANCO (as procedures usam <c>CURDATE()</c>), a leitura de uma linha como texto e a chamada de duas procedures ao mesmo tempo, em duas sessões.
/// </summary>
internal static class ApoioDaParteD
{
    /// <summary>O que uma chamada concorrente deu: <c>null</c> se passou, ou a mensagem do servidor se foi recusada (com o SQLSTATE).</summary>
    public sealed record Desfecho(string? Mensagem, string? SqlState, IReadOnlyDictionary<string, object?>? Saidas)
    {
        public bool Passou => Mensagem is null;
    }

    /// <summary>Hoje no banco (<c>CURDATE()</c>, já em -03:00) mais <paramref name="dias"/> dias.</summary>
    public static async Task<DateOnly> DiaAsync(string conexao, int dias = 0) =>
        DateOnly.ParseExact(await TextoAsync(conexao, $"SELECT DATE_FORMAT(CURDATE() + INTERVAL {dias.ToString(CultureInfo.InvariantCulture)} DAY, '%Y-%m-%d')") ?? string.Empty, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public static string Iso(DateOnly dia) => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    /// <summary>O valor da primeira coluna da primeira linha, como texto (<c>null</c> do banco, ou nenhuma linha, vira <c>null</c>).</summary>
    public static async Task<string?> TextoAsync(string conexao, string sql)
    {
        var valor = await Sql.EscalarAsync(conexao, sql);
        return valor is null ? null : Convert.ToString(valor, CultureInfo.InvariantCulture);
    }

    public static async Task<int> IdAsync(string conexao, string sql) => (int)await Sql.ContarAsync(conexao, sql);

    /// <summary>Chama a procedure e devolve o desfecho em vez de lançar: serve para as corridas, em que não se sabe qual das chamadas perde.</summary>
    public static async Task<Desfecho> TentarAsync(string conexao, string procedure, params object?[] argumentos)
    {
        try
        {
            return new Desfecho(null, null, await ChamarAsync(conexao, procedure, argumentos));
        }
        catch (MySqlException erro)
        {
            return new Desfecho(erro.Message, erro.SqlState, null);
        }
    }

    /// <summary>Dispara as chamadas juntas, cada uma na sua sessão, e espera todas.</summary>
    public static async Task<Desfecho[]> AoMesmoTempoAsync(params Func<Task<Desfecho>>[] chamadas)
    {
        using var largada = new SemaphoreSlim(0);
        var tarefas = chamadas.Select(chamada => Task.Run(async () =>
        {
            await largada.WaitAsync();
            return await chamada();
        })).ToArray();
        largada.Release(chamadas.Length);
        return await Task.WhenAll(tarefas);
    }

    /// <summary>
    /// Duas (ou mais) chamadas ao mesmo tempo DE VERDADE: uma terceira sessão segura a linha que a procedure trava primeiro (<paramref name="sqlDaTrava"/>, um <c>SELECT ... FOR UPDATE</c>), as chamadas são soltas e o teste confere que TODAS estão esperando (nenhuma terminou) antes de liberar. Uma procedure que
    /// não trava essa linha termina sem esperar e derruba o teste aqui, sempre, e não só quando o acaso sobrepõe as chamadas. <paramref name="enquantoEsperam"/> roda na sessão da trava, com as chamadas já esperando (o que a API faria com a trava na mão).
    /// </summary>
    public static async Task<Desfecho[]> EsperandoATravaAsync(string conexao, string sqlDaTrava, Func<Task<Desfecho>>[] chamadas, string? enquantoEsperam = null)
    {
        Task<Desfecho>[] tarefas;
        await using (var trava = new MySqlConnection(conexao))
        {
            await trava.OpenAsync();
            await using var transacao = await trava.BeginTransactionAsync();
            await using (var comando = new MySqlCommand(sqlDaTrava, trava, transacao))
            {
                Assert.NotNull(await comando.ExecuteScalarAsync()); // a linha a travar existe
            }

            tarefas = [.. chamadas.Select(chamada => Task.Run(chamada))];
            await Task.Delay(TimeSpan.FromSeconds(1.5));
            Assert.All(tarefas, t => Assert.False(t.IsCompleted, "A chamada deveria estar esperando a trava: a procedure nao travou a linha que devia travar primeiro."));
            if (enquantoEsperam is not null)
            {
                await using var escrita = new MySqlCommand(enquantoEsperam, trava, transacao);
                _ = await escrita.ExecuteNonQueryAsync();
            }

            await transacao.CommitAsync();
        }

        return await Task.WhenAll(tarefas);
    }

    /// <summary>A consolidação do banco de horas de um dia já concluída (a tarefa diária da API), como a efetivação do desligamento a procura.</summary>
    public static async Task ConsolidarBancoDeHorasAsync(string conexao, DateOnly dia) =>
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TAREFAS_AGENDADAS (TIPO, CHAVE_IDEMPOTENCIA, DIA_REFERENCIA, SITUACAO, CONCLUIDA_EM) VALUES ('CONSOLIDAR_BANCO_HORAS', 'CONSOLIDAR_BANCO_HORAS:dia={Iso(dia)}', '{Iso(dia)}', 'CONCLUIDA', UTC_TIMESTAMP(6))");

    /// <summary>O setor com o gestor dele: a pessoa <paramref name="chefe"/> vira gestora (linha em <c>GESTORES</c>) e o setor passa a apontar para ela. Devolve o <c>ID_GESTOR</c>.</summary>
    public static async Task<int> GestorDoSetorAsync(string conexao, int chefe, int setor)
    {
        var gestor = await GestorAsync(conexao, chefe);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {setor}");
        return gestor;
    }
}

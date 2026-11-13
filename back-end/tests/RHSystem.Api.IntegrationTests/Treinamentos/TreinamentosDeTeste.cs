using System.Globalization;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// O que os testes dos treinamentos têm em comum: nomes únicos, o treinamento semeado direto no banco em cada situação (datas LOCAIS, -03:00, com "hoje" em 2026-10-07), os módulos de cada tipo e leituras de texto e contagem.
/// </summary>
internal static class TreinamentosDeTeste
{
    private static int _sequencia;

    public static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    public static string Valor(string? v) => v is null ? "NULL" : $"'{v.Replace("'", "''", StringComparison.Ordinal)}'";

    public static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    public static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    public static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    public static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int treinamentoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "TREINAMENTOS", treinamentoId);

    /// <summary>Um treinamento direto no banco, na situação pedida: <c>RASCUNHO</c>, <c>AGENDADO</c> (começa em 20/10), <c>EM_ANDAMENTO</c> (começou em 01/10) ou <c>ENCERRADO</c> (começou em 01/09 e foi encerrado em 01/10).</summary>
    public static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, string situacao = "RASCUNHO", bool obrigatorio = false, string? publico = "TODOS", bool emite = true, bool onboarding = false, bool admitidos = true, string? descricao = null)
    {
        var (publicado, comeca, encerramento) = situacao switch
        {
            "AGENDADO" => (1, "'2026-10-20'", "NULL"),
            "EM_ANDAMENTO" => (1, "'2026-10-01'", "NULL"),
            "ENCERRADO" => (1, "'2026-09-01'", "'2026-10-01 00:00:00'"),
            _ => (0, "NULL", "NULL"),
        };
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO TREINAMENTOS (TITULO, DESCRICAO, TIPO_TREINAMENTO, DATA_INICIO, OBRIGATORIO, PUBLICADO, PUBLICO, EMITE_CERTIFICADO, INCLUI_ADMITIDOS, ENCERRADO_EM) VALUES ({Valor(titulo)}, {Valor(descricao)}, {(onboarding ? "'ONBOARDING'" : "NULL")}, {comeca}, {(obrigatorio ? 1 : 0)}, {publicado}, {Valor(publico)}, {(emite ? 1 : 0)}, {(admitidos ? 1 : 0)}, {encerramento})");
        return (int)await ContarAsync(api, $"SELECT ID_TREINAMENTO FROM TREINAMENTOS WHERE TITULO = {Valor(titulo)}");
    }

    /// <summary>Um módulo direto no banco (a ordem é a pedida). <c>VIDEO</c> e <c>LINK</c> levam o endereço, <c>TEXTO</c> o texto e <c>PDF</c> o caminho e o nome do arquivo.</summary>
    public static async Task<int> ModuloAsync(ApiDeAcesso api, int treinamento, int ordem, string tipo = "TEXTO", string? titulo = null, short? duracao = 10, string? caminho = null, string? nomeArquivo = null)
    {
        var nome = titulo ?? $"Módulo {ordem}";
        var (url, texto, arquivo, original) = tipo switch
        {
            "VIDEO" => ("'https://videos.exemplo.com.br/aula'", "NULL", "NULL", "NULL"),
            "LINK" => ("'https://artigo.exemplo.com.br'", "NULL", "NULL", "NULL"),
            "PDF" => ("NULL", "NULL", Valor(caminho ?? $"treinamentos/modulo-{ordem}.pdf"), Valor(nomeArquivo ?? "Apostila.pdf")),
            _ => ("NULL", "'Texto do módulo.'", "NULL", "NULL"),
        };
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, DURACAO_MINUTOS, URL_EXTERNA, CONTEUDO_TEXTO, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL) VALUES ({treinamento}, {ordem}, {Valor(nome)}, '{tipo}', {(duracao is null ? "NULL" : duracao.Value.ToString(CultureInfo.InvariantCulture))}, {url}, {texto}, {arquivo}, {original})");
        return (int)await ContarAsync(api, $"SELECT ID_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {treinamento} AND ORDEM = {ordem}");
    }
}

using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace RHSystem.Application.Comum;

/// <summary>
/// A chave que impede a mesma mensagem (ou a mesma tarefa) de entrar duas vezes: <c>TIPO:nome=valor:nome=valor</c>. Quem enfileira ou agenda monta a
/// chave AQUI, e o erro de formato aparece no teste, não no <c>CHECK</c> ou na chave única do banco.
/// </summary>
public static partial class ChaveDeIdempotencia
{
    /// <summary>O tamanho de <c>TAREFAS_AGENDADAS.TIPO</c>, o maior das duas tabelas (<c>FILA_EMAILS.TIPO</c> é mais estreita, 3 a 40, e a fila confere isso à parte).</summary>
    public const int TamanhoMaximoDoTipo = 50;

    /// <summary>A coluna <c>CHAVE_IDEMPOTENCIA</c> é <c>varchar(190)</c> ASCII.</summary>
    public const int TamanhoMaximoDaChave = 190;

    /// <summary>
    /// <c>Montar("AVISO_PUBLICADO", ("aviso", 15), ("func", 21))</c> é <c>"AVISO_PUBLICADO:aviso=15:func=21"</c>. Os valores entram em texto invariante.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// O tipo não é <c>MAIÚSCULAS_COM_SUBLINHADO</c> de 3 a 50 caracteres; não há nenhuma parte (sem ela, todas as mensagens do tipo seriam a mesma); ou a
    /// chave sai do ASCII ou passa de 190 caracteres.
    /// </exception>
    public static string Montar(string tipo, params (string Nome, object? Valor)[] partes)
    {
        if (tipo is null || !FormatoDoTipo().IsMatch(tipo))
        {
            throw new ArgumentException($"O tipo é um nome em maiúsculas com sublinhado, de 3 a {TamanhoMaximoDoTipo} caracteres (por exemplo AVISO_PUBLICADO).", nameof(tipo));
        }

        if (partes is null || partes.Length == 0)
        {
            throw new ArgumentException("A chave precisa de pelo menos uma parte (nome e valor): sem ela, todas as mensagens do tipo teriam a mesma chave.", nameof(partes));
        }

        var chave = new StringBuilder(tipo);
        foreach (var (nome, valor) in partes)
        {
            chave.Append(':').Append(nome).Append('=').Append(Texto(valor));
        }

        var texto = chave.ToString();
        if (texto.Length > TamanhoMaximoDaChave || texto.Any(c => c > 127))
        {
            throw new ArgumentException($"A chave é ASCII e tem até {TamanhoMaximoDaChave} caracteres (esta tem {texto.Length}).", nameof(partes));
        }

        return texto;
    }

    /// <summary>Datas em ISO (<c>2026-10-07</c>, <c>2026-10-07T12:42:00Z</c>), porque o formato padrão de <c>DateOnly</c> e <c>DateTime</c> depende da cultura; o resto em texto invariante.</summary>
    private static string Texto(object? valor) => valor switch
    {
        null => string.Empty,
        DateOnly dia => dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        DateTime instante => instante.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture) + (instante.Kind == DateTimeKind.Utc ? "Z" : string.Empty),
        _ => Convert.ToString(valor, CultureInfo.InvariantCulture) ?? string.Empty,
    };

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{2,49}\z")]
    private static partial Regex FormatoDoTipo();
}

using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.Configuracao;

/// <summary>
/// O JSON do contrato (<c>contrato/CONVENCOES.md</c>, seção 3): propriedades em <c>camelCase</c>; enum como texto em
/// <c>MAIÚSCULAS_COM_SUBLINHADO</c>, menos o <see cref="Perfil"/>, que o contrato fixa como <c>Funcionario</c>, <c>Gestor</c> e <c>RH</c>; instante só em
/// UTC e terminando em <c>Z</c>; <c>DateOnly</c> como <c>yyyy-MM-dd</c> (o padrão do <c>System.Text.Json</c>); <c>TimeOnly</c> como <c>HH:mm</c>;
/// número só como número.
/// </summary>
public static class JsonDoContrato
{
    public static void Configurar(JsonSerializerOptions opcoes)
    {
        opcoes.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
        opcoes.NumberHandling = JsonNumberHandling.Strict;

        // O conversor do Perfil vem primeiro: vale o primeiro da lista que sabe converter o tipo. Número no lugar do nome do enum é recusado.
        opcoes.Converters.Add(new JsonStringEnumConverter<Perfil>(namingPolicy: null, allowIntegerValues: false));
        opcoes.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseUpper, allowIntegerValues: false));
        opcoes.Converters.Add(new InstanteEmUtcConversor());
        opcoes.Converters.Add(new HoraDoDiaConversor());
    }
}

/// <summary>
/// Instante: na saída só <c>Kind = Utc</c> (sai com <c>Z</c>); qualquer outro <c>Kind</c> lança, para um <c>datetime</c> que o provedor devolveu sem
/// <c>Kind</c> quebrar o teste em vez de sair sem <c>Z</c> e o front o ler no fuso dele. Na entrada exige <c>Z</c> ou um deslocamento (<c>-03:00</c>) e
/// converte para UTC (o <c>System.Text.Json</c> devolveria <c>Kind = Local</c>).
/// </summary>
internal sealed class InstanteEmUtcConversor : JsonConverter<DateTime>
{
    private static readonly string[] Formatos = ["yyyy-MM-dd'T'HH:mm:ssK", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFK"];

    public override DateTime Read(ref Utf8JsonReader reader, Type tipo, JsonSerializerOptions opcoes)
    {
        var texto = reader.GetString();
        if (texto is null
            || !DateTime.TryParseExact(texto, Formatos, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var instante)
            || instante.Kind == DateTimeKind.Unspecified)
        {
            throw new JsonException("O instante precisa estar no formato 2026-10-07T12:42:00Z, terminando em Z ou com o deslocamento (por exemplo -03:00).");
        }

        return instante.ToUniversalTime();
    }

    public override void Write(Utf8JsonWriter writer, DateTime valor, JsonSerializerOptions opcoes)
    {
        if (valor.Kind != DateTimeKind.Utc)
        {
            throw new InvalidOperationException($"Um instante só sai no JSON com Kind = Utc (veio {valor.Kind}): sem isso ele sairia sem o Z.");
        }

        writer.WriteStringValue(valor);
    }
}

/// <summary>Horário do dia: texto <c>HH:mm</c>, sem fuso e sem segundos (o contrato não tem segundos).</summary>
internal sealed class HoraDoDiaConversor : JsonConverter<TimeOnly>
{
    private const string Formato = "HH:mm";

    public override TimeOnly Read(ref Utf8JsonReader reader, Type tipo, JsonSerializerOptions opcoes)
    {
        if (!TimeOnly.TryParseExact(reader.GetString(), Formato, CultureInfo.InvariantCulture, DateTimeStyles.None, out var horario))
        {
            throw new JsonException("O horário precisa estar no formato HH:mm (por exemplo 08:30).");
        }

        return horario;
    }

    public override void Write(Utf8JsonWriter writer, TimeOnly valor, JsonSerializerOptions opcoes)
    {
        if (valor != new TimeOnly(valor.Hour, valor.Minute))
        {
            throw new InvalidOperationException($"Um horário do dia sai no JSON como HH:mm, e este tem segundos ({valor:HH:mm:ss}).");
        }

        writer.WriteStringValue(valor.ToString(Formato, CultureInfo.InvariantCulture));
    }
}

using System.Text.Json;
using System.Text.Json.Serialization;

namespace RHSystem.Application.Comum;

/// <summary>
/// Um inteiro OPCIONAL no corpo que o contrato declara <c>type: integer</c> sem <c>null</c> (<c>ordem</c> e o <c>id</c> da alternativa): ausente vale "sem valor", mas <c>null</c> escrito no corpo é recusado (400), como o contrato diz. Sem isto o C# <c>int?</c> aceitaria o
/// <c>null</c> e o documento da API divergiria do contrato. A Api tira o <c>null</c> do esquema da propriedade que usa este conversor.
/// </summary>
public sealed class InteiroOpcionalSemNullConversor : JsonConverter<int?>
{
    public override bool HandleNull => true;

    public override int? Read(ref Utf8JsonReader reader, Type tipo, JsonSerializerOptions opcoes)
    {
        if (reader.TokenType == JsonTokenType.Null)
        {
            throw new JsonException("Este campo não aceita null: tire-o do corpo ou informe um número inteiro.");
        }

        if (reader.TokenType != JsonTokenType.Number || !reader.TryGetInt32(out var numero))
        {
            throw new JsonException("Este campo precisa de um número inteiro.");
        }

        return numero;
    }

    public override void Write(Utf8JsonWriter writer, int? valor, JsonSerializerOptions opcoes)
    {
        if (valor is { } numero)
        {
            writer.WriteNumberValue(numero);
        }
        else
        {
            writer.WriteNullValue();
        }
    }
}

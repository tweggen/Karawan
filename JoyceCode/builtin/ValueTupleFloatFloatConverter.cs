using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace builtin;

/// <summary>
/// Custom JSON converter for ValueTuple&lt;float, float&gt;.
/// Deserializes from JSON arrays [min, max] into (float min, float max) tuples.
/// </summary>
public class ValueTupleFloatFloatConverter : JsonConverter<(float, float)>
{
    public override (float, float) Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.StartArray)
        {
            reader.Read(); // Move to first element
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException($"Expected number in array, got {reader.TokenType}");
            float min = reader.GetSingle();

            reader.Read(); // Move to second element
            if (reader.TokenType != JsonTokenType.Number)
                throw new JsonException($"Expected number in array, got {reader.TokenType}");
            float max = reader.GetSingle();

            reader.Read(); // Move past end of array
            if (reader.TokenType != JsonTokenType.EndArray)
                throw new JsonException($"Expected end of array, got {reader.TokenType}");

            return (min, max);
        }

        throw new JsonException($"Expected JSON array, got {reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, (float, float) value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.Item1);
        writer.WriteNumberValue(value.Item2);
        writer.WriteEndArray();
    }
}

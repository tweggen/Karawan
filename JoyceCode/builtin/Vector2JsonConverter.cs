using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;

namespace builtin;

public class Vector2JsonConverter : JsonConverter<Vector2>
{
    public override Vector2 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            ErrorThrow<InvalidCastException>($"Expected beginning of array, got {reader.TokenType}.");
        }

        reader.Read();
        float[] m = new float[2];
        int i = 0;
        while (i<2)
        {
            if (reader.TokenType != JsonTokenType.Number)
            {
                ErrorThrow<InvalidCastException>($"Expected number in matrix.");
            }

            m[i] = reader.GetSingle();
            i++;
            reader.Read();
        }

        if (reader.TokenType != JsonTokenType.EndArray)
        {
            ErrorThrow<InvalidCastException>($"Expected number in matrix.");
        }

        return new Vector2(
            m[0], m[1]
        );
    }

    public override void Write(Utf8JsonWriter writer, Vector2 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteEndArray();
    }
}
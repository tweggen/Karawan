using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;

namespace builtin;

public class QuaternionJsonConverter : JsonConverter<Quaternion>
{
    public override Quaternion Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            ErrorThrow<InvalidCastException>($"Expected beginning of array, got {reader.TokenType}.");
        }

        reader.Read();
        float[] m = new float[4];
        int i = 0;
        while (i<4)
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

        return new Quaternion(
            m[0], m[1], m[2], m[3]
        );
    }

    public override void Write(Utf8JsonWriter writer, Quaternion value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.X);
        writer.WriteNumberValue(value.Y);
        writer.WriteNumberValue(value.Z);
        writer.WriteNumberValue(value.W);
        writer.WriteEndArray();
    }
}

using System;
using System.Numerics;
using System.Text.Json;
using System.Text.Json.Serialization;
using static engine.Logger;

namespace builtin;

public class Matrix4x4JsonConverter : JsonConverter<Matrix4x4>
{
    public override Matrix4x4 Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
        {
            ErrorThrow<InvalidCastException>($"Expected beginning of array, got {reader.TokenType}.");
        }

        reader.Read();
        float[] m = new float[16];
        int i = 0;
        while (i<16)
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

        return new Matrix4x4(
            m[0], m[1], m[2], m[3],
            m[4], m[5], m[6], m[7],
            m[8], m[9], m[10], m[11],
            m[12], m[13], m[14], m[15]
            );
    }

    public override void Write(Utf8JsonWriter writer, Matrix4x4 value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(value.M11);
        writer.WriteNumberValue(value.M12);
        writer.WriteNumberValue(value.M13);
        writer.WriteNumberValue(value.M14);
        writer.WriteNumberValue(value.M21);
        writer.WriteNumberValue(value.M22);
        writer.WriteNumberValue(value.M23);
        writer.WriteNumberValue(value.M24);
        writer.WriteNumberValue(value.M31);
        writer.WriteNumberValue(value.M32);
        writer.WriteNumberValue(value.M33);
        writer.WriteNumberValue(value.M34);
        writer.WriteNumberValue(value.M41);
        writer.WriteNumberValue(value.M42);
        writer.WriteNumberValue(value.M43);
        writer.WriteNumberValue(value.M44);
        writer.WriteEndArray();
    }
}

using System;
using System.Collections.Generic;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using engine.joyce;
using engine.joyce.components;
using engine.world;
using static engine.Logger;

namespace engine.streets;

/**
 * Our streetPoint converter loads street points from the underlying cluster.
 */
public class StreetPointConverter : JsonConverter<StreetPoint>
{
    public override StreetPoint Read(
        ref Utf8JsonReader reader,
        System.Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        int id = default;
        int clusterId = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var clusterDesc = I.Get<ClusterList>().GetCluster(clusterId);
                var streetPoint = clusterDesc.StrokeStore().GetStreetPoint(id);
                return streetPoint;
            }

            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                string propertyName = reader.GetString();
                reader.Read();

                /*
                 * Note that we do not read all the properties but instead read the
                 * street point from the store.
                 */
                switch (propertyName)
                {
                    case nameof(StreetPoint.Id):
                        id = reader.GetInt32();
                        break;
                    case nameof(StreetPoint.ClusterId):
                        clusterId = reader.GetInt32();
                        break;
                }
            }
        }

        throw new JsonException();
    }


    public override void Write(
        Utf8JsonWriter writer,
        StreetPoint sp,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(StreetPoint.Id), sp.Id);
        writer.WriteNumber(nameof(StreetPoint.ClusterId), sp.ClusterId);
        writer.WriteString(nameof(StreetPoint.Creator), sp.Creator);
        writer.WritePropertyName(nameof(StreetPoint.Pos));
        writer.WriteRawValue(JsonSerializer.Serialize<Vector2>(sp.Pos, options));
        writer.WriteEndObject();
    }
}



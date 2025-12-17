using System.Text.Json;
using System.Text.Json.Serialization;

namespace engine.world;


public class ClusterDescConverter : JsonConverter<ClusterDesc>
{
    public override ClusterDesc Read(
        ref Utf8JsonReader reader,
        System.Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject)
        {
            throw new JsonException();
        }

        int clusterId = default;

        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.EndObject)
            {
                var clusterDesc = I.Get<ClusterList>().GetCluster(clusterId);
                return clusterDesc;
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
                    case nameof(ClusterDesc.Id):
                        clusterId = reader.GetInt32();
                        break;
                }
            }
        }

        throw new JsonException();
    }


    public override void Write(
        Utf8JsonWriter writer,
        ClusterDesc clusterDesc,
        JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteNumber(nameof(ClusterDesc.Id), clusterDesc.Id);
        writer.WriteEndObject();
    }
}
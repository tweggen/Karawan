using System.Text.Json;
using System.Text.Json.Serialization;

namespace engine.joyce.components;


public class FromModelConverter : JsonConverter<FromModel>
{
    public required builtin.entitySaver.Context Context;
    
    public override FromModel Read(
        ref Utf8JsonReader reader,
        System.Type typeToConvert,
        JsonSerializerOptions options)
    {
        var mcpObject = JsonSerializer.Deserialize(ref reader, typeof(ModelCacheParams), options);
        var mcp = mcpObject as ModelCacheParams;
        var model = I.Get<ModelCache>().InstantiatePlaceholder(Context.Entity, mcp);
        
        return new FromModel() { ModelCacheParams = mcp, Model = model };
    }
        

    public override void Write(
        Utf8JsonWriter writer,
        FromModel fm,
        JsonSerializerOptions options) =>
        writer.WriteRawValue(JsonSerializer.Serialize<ModelCacheParams>(fm.ModelCacheParams, options));
}


/**
 * This component defines how that entity shall be constructed.
 * Components of this type shall be added to entities if they
 * require to be reconstructed after loading.
 */
[engine.IsPersistable]
public struct FromModel
{
    public ModelCacheParams ModelCacheParams;

    public Model? Model;
}
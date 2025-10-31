using System.IO;
using engine.joyce;
using MessagePack;
using MessagePack.Resolvers;

namespace builtin.baking;

public class ModelAnimationCollectionWriter
{
    public static void Write(Stream stream, ModelAnimationCollection modelAnimationCollection)
    {
        MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            ;
        MessagePackSerializer.Serialize(stream, modelAnimationCollection, options);
    }  
}
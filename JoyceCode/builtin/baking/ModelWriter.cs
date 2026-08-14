using System.IO;
using engine.joyce;
using MessagePack;

namespace builtin.baking;

/**
 * Writes a baked mo-{hash} model. Mirrors ModelAnimationCollectionWriter,
 * including the compression, so both bake artifacts share one format decision.
 */
public class ModelWriter
{
    public static void Write(Stream stream, Model model)
    {
        MessagePackSerializerOptions options = MessagePackSerializerOptions.Standard
            .WithCompression(MessagePackCompression.Lz4BlockArray)
            ;
        MessagePackSerializer.Serialize(stream, model, options);
    }
}

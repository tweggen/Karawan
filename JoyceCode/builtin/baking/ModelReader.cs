using System.IO;
using engine.joyce;
using MessagePack;

namespace builtin.baking;

/**
 * Reads a baked mo-{hash} model. Mirrors ModelAnimationCollectionReader.
 *
 * Note what this does NOT do: it does not call Model.Polish. Polish needs the
 * model base bone, which is a property of the load request rather than of the
 * file, so the caller supplies it - exactly as the fbx path does at the end of
 * FbxModel.Load.
 */
public class ModelReader
{
    public static Model? Read(Stream stream)
    {
        var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        return MessagePackSerializer.Deserialize<Model>(stream, options);
    }
}

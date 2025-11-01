using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using engine.joyce;
using MessagePack;
using MessagePack.Resolvers;

namespace builtin.baking;

public class ModelAnimationCollectionReader
{
    public static string ModelAnimationCollectionFileName(string urlModel, string? urlAnimations)
    {
        string strModelAnims;
        if (urlAnimations != null)
        {
            strModelAnims = $"{urlModel};{urlAnimations}";
        }
        else
        {
            strModelAnims = $"{urlModel}";
        }
        
        string strHash = 
            Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(strModelAnims)));
        return  $"ac-{strHash}";;
    }

    public static ModelAnimationCollection? Read(Stream stream)
    {
        var animcoll = MessagePackSerializer.Deserialize<ModelAnimationCollection>(stream);
        return animcoll;
    }
}
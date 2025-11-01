using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using engine.joyce;
using static engine.Logger;
using MessagePack;
using MessagePack.Resolvers;

namespace builtin.baking;

public class ModelAnimationCollectionReader
{
    private static SHA256 _sha256 = SHA256.Create();
    public static string ModelAnimationCollectionFileName(string urlModel, string? urlAnimations)
    {
        string strModelAnims;
        if (!String.IsNullOrWhiteSpace(urlAnimations))
        {
            strModelAnims = $"{urlModel};{urlAnimations}";
        }
        else
        {
            strModelAnims = $"{urlModel}";
        }
        
        string strHash = 
            Convert.ToBase64String(_sha256.ComputeHash(Encoding.UTF8.GetBytes(strModelAnims)))
                .Replace('+', '-')
                .Replace('/', '_')
                .Replace('=', '~');
                ;
        Trace($"Returning hash {strHash} for {strModelAnims}");
        return  $"ac-{strHash}";;
    }

    public static ModelAnimationCollection? Read(Stream stream)
    {
        var options = MessagePackSerializerOptions.Standard.WithCompression(MessagePackCompression.Lz4BlockArray);
        var animcoll = MessagePackSerializer.Deserialize<ModelAnimationCollection>(stream, options);
        return animcoll;
    }
}
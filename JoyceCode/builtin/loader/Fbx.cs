using System;
using System.Collections.Generic;
using System.IO;
using engine;
using static engine.Logger;

namespace builtin.loader;

public class Fbx
{
    static public void LoadModelInstanceSync(string url,
        ModelProperties modelProperties,
        out engine.joyce.InstanceDesc instanceDesc, out engine.ModelInfo modelInfo)
    {
        instanceDesc = new(
            new List<engine.joyce.Mesh>(), 
            new List<int>(), 
            new List<engine.joyce.Material>(),
            400f);

        modelInfo = new();

        var fbxImporter = new FbxSharp.FbxImporter();

        var fileStream = Assets.Open(url);
        FbxSharp.FbxScene? fbxScene = null;
        try
        {
            using (StreamReader input = new StreamReader(fileStream))
            {
                fbxScene = new FbxSharp.Converter().ConvertScene(
                    new FbxSharp.Parser(
                        new FbxSharp.Tokenizer((TextReader)input))
                        {  AutoExpandArray = true }.ReadFile());
            }
        }
        catch (Exception e)
        {
            Error($"Unable to load fbx from url {url}: {e}.");
        }
        
        if (fbxScene != null)
        {
            Trace("Successfully loaded model.");
        }
    }

    public static void Unit()
    {
        LoadModelInstanceSync("u.fbx", 
            new ModelProperties()
            , out var _, out var _);
    }
}
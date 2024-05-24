using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using builtin.loader.fbx;
using engine;
using engine.joyce;
using Silk.NET.Assimp;
using static engine.Logger;

namespace builtin.loader;

public class Fbx
{
    static private Assimp _assimp;
    
    static public void LoadModelInstanceSync(string url,
        ModelProperties modelProperties,
        out Model model)
    {
        engine.joyce.InstanceDesc instanceDesc = new(
            new List<engine.joyce.Mesh>(),
            new List<int>(),
            new List<engine.joyce.Material>(),
            400f);

        using (var fbxModel = new FbxModel())
        {
            model = new Model(instanceDesc);
            fbxModel.Load
        }
    }
    
#if false
    static public void LoadModelInstanceSync(string url,
        ModelProperties modelProperties,
        out Model model)
    {
        engine.joyce.InstanceDesc instanceDesc = new(
            new List<engine.joyce.Mesh>(), 
            new List<int>(), 
            new List<engine.joyce.Material>(),
            400f);

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

        model = new Model(instanceDesc);
    }
#endif
    
    static public Task<Model> LoadModelInstance(string url, ModelProperties modelProperties)
    {
        return Task.Run(() => 
        {
            LoadModelInstanceSync(url, modelProperties, out var model);
            return model;
        });
    }

        
    public static void Unit()
    {
        LoadModelInstanceSync("u.fbx", new ModelProperties(), out var _);
    }
    
}
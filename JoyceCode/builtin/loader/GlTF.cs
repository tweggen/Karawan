using System;
using System.Collections.Generic;
using glTFLoader;
using glTFLoader.Schema;
using static engine.Logger;

namespace builtin.loader;

public class GlTF
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

        Gltf? model = null;
        using (var fileStream = engine.Assets.Open(url))
        {
            try
            {
                model = Interface.LoadModel(fileStream);
            }
            catch (Exception e)
            {
                Error($"Unable to load gltf file: Exception: {e}");
            }
        }

        if (model != null)
        {
            Trace("Successfully loaded model.");
        }
    }
    
    
    public static void Unit()
    {
        LoadModelInstanceSync("u.glb", 
            new ModelProperties()
            , out var _, out var _);
    }
}
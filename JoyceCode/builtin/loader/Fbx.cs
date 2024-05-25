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

        using (var fbxModel = new fbx.FbxModel())
        {
            model = new Model(instanceDesc);
            fbxModel.Load(url);
        }
    }
    
   
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
        LoadModelInstanceSync("U5.fbx", new ModelProperties(), out var _);
        LoadModelInstanceSync("Spring Boy.fbx", new ModelProperties(), out var _);
    }
    
}
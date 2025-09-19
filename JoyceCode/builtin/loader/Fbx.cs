using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
            new List<engine.joyce.ModelNode>(),
            400f);

        float scale = 1f;
        List<string>? additionalUrls = null;
        AxisInterpreter? axisInterpreter = null;
        AxisInterpreter? animAxisInterpreter = null;
        
        if (modelProperties.Properties.ContainsKey("AdditionalUrls"))
        {
            additionalUrls = modelProperties.Properties["AdditionalUrls"].Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        if (modelProperties.Properties.ContainsKey("Scale"))
        {
            scale = float.Parse(modelProperties.Properties["Scale"]);
        }

        if (modelProperties.Properties.ContainsKey("Axis"))
        {
            axisInterpreter = AxisInterpreter.CreateFromString(modelProperties.Properties["Axis"]);
        }
        
        if (modelProperties.Properties.ContainsKey("AnimAxis"))
        {
            animAxisInterpreter = AxisInterpreter.CreateFromString(modelProperties.Properties["AnimAxis"]);
        }
        
        using (var fbxModel = new fbx.FbxModel())
        {
            fbxModel.Load(url, additionalUrls, scale,
                axisInterpreter, 
                animAxisInterpreter,
                out model);
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
        //LoadModelInstanceSync("U5.fbx", new ModelProperties(), out var _);
        //LoadModelInstanceSync("Spring Boy.fbx", new ModelProperties(), out var _);
    }
}
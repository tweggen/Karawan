using System;
using System.IO;
using System.Text;
using engine;
using static engine.Logger;

namespace engine.Resource;


public class ShaderSource : IDisposable
{
    private string _shaderCode;
    public string ShaderCode
    {
        get => _shaderCode;
    }


    public void Dispose()
    {
    }
    

    public ShaderSource(string uri)
    {
        var stream = engine.Assets.Open(uri);
        using var sr = new StreamReader(stream, Encoding.UTF8);
        string strShader = sr.ReadToEnd();

        string api = engine.GlobalSettings.Get("platform.threeD.API");
        string version = engine.GlobalSettings.Get("platform.threeD.API.version");
        string esString = "";
        
        bool haveSSBO = false;
        bool haveUniform = false;
        bool haveUBO = false;
        
        if (api == "OpenGL")
        {
            if (String.Compare(version, "430") < 0)
            {
                haveSSBO = false;
                haveUBO = true;
            }
            else
            {
                haveSSBO = true;
                haveUBO = false;
            }
        }
        else if (api == "OpenGLES")
        {
            esString = " es";
            haveUBO = true;
            
        }
        else
        {
            ErrorThrow($"Invalid graphics API setup in global config \"platform.threeD.API\": \"{api}\".",
                m => new InvalidOperationException(m));
            return;
        }
        _shaderCode = $"#version {version}{esString}\n\n"+
                      $"#define USE_ANIM_SSBO {(haveSSBO?"1":"0")}\n"+
                      $"#define USE_ANIM_UNIFORM {(haveUniform?"1":"0")}\n"+
                      $"#define USE_ANIM_UBO {(haveUBO?"1":"0")}\n"+
                      "\n"+
                      strShader;
    }
}

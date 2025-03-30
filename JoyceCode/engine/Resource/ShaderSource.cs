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
        if (api == "OpenGL")
        {
            _shaderCode = "#version 430\n"+
                          "#define USE_ANIM_SSBO 1\n"+
                          "#define USE_ANIM_UBO 0\n"+
                          "\n"+
                          strShader;
        }
        else if (api == "OpenGLES")
        {
            _shaderCode = "#version 310 es\n\n"+
                          "#define USE_ANIM_SSBO 0\n"+
                          "#define USE_ANIM_UBO 1\n"+
                          "\n"+
                          strShader;
        }
        else
        {
            ErrorThrow($"Invalid graphics API setup in global config \"platform.threeD.API\": \"{api}\".",
                m => new InvalidOperationException(m));
            return;
        }
    }
}

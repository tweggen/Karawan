using System;
using System.Data;
using Silk.NET.OpenGL;
using static Splash.Silk.GLCheck;
using static engine.Logger;

namespace Splash.Silk;

public class SkSingleShaderEntry : ASingleShaderEntry
{
    public uint Handle = 0xffffffff;
    private ShaderType _shaderType;
    private GL _gl;

    private uint _loadShaderSource(ShaderType type, string source)
    {
        //To load a single shader we need to:
        //1) Load the shader from a file.
        //2) Create the handle.
        //3) Upload the source to opengl.
        //4) Compile the shader.
        //5) Check for errors.
        uint handle = _gl.CreateShader(type);
        CheckError(_gl,$"glCreateShader {type}.");
        _gl.ShaderSource(handle, source);
        CheckError(_gl,$"glShaderSource {handle}.");
        _gl.CompileShader(handle);
        CheckError(_gl,$"glCompileShader {handle}.");
        string infoLog = _gl.GetShaderInfoLog(handle);
        CheckError(_gl,$"glGetShaderInfoLog {handle}.");
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            ErrorThrow($"Error compiling shader of type {type}, failed with error {infoLog}", (m)=>new InvalidExpressionException(m));
        }

        return handle;
    }
    
    
    public override void Dispose()
    {
        if (Handle != 0xffffffff)
        {
            _gl.DeleteShader(Handle);
            Handle = 0xffffffff;
        }
    }

    
    public override bool IsUploaded()
    {
        return Handle != 0xffffffff;
    }


    public void Upload()
    {
        if (Handle != 0xffffffff)
        {
            ErrorThrow($"Shader already uploaded.", m => new InvalidOperationException(m));
            return;
        }

        Handle = _loadShaderSource(_shaderType, SplashAnyShader.Source);
    }
    

    public SkSingleShaderEntry(GL gl, SplashAnyShader splashAnyShader, ShaderType shaderType) : base(splashAnyShader)
    {
        _gl = gl;
        _shaderType = shaderType;
    }
}

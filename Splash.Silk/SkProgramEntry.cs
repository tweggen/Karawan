using System.Numerics;
using engine;
using Silk.NET.OpenGL;
using static engine.Logger;
using static Splash.Silk.GLCheck;

namespace Splash.Silk;

public class SkProgramEntry : IDisposable
{
    public SkSingleShaderEntry SkVertexShader;
    public SkSingleShaderEntry SkFragmentShader;
    private bool _traceShader = false;

    private GL _gl;
    public uint Handle = 0xffffffff;

    public SortedDictionary<string, ShaderLocs> ShaderUseCases = new();

    private const bool _checkErrors = true;
    
    public void SetUniform(int location, int value)
    {
        if (-1 == location) return;
        _gl.Uniform1(location, value);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error setting uniform int {location}: {err}");
            }
        }
    }

    public void SetUniform(int location, uint value)
    {
        if (-1 == location) return;
        _gl.Uniform1(location, value);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error setting uniform uint {location}: {err}");
            }
        }
    }

    public void SetUniform(int location, float value)
    {
        if (-1 == location) return;
        _gl.Uniform1(location, value);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error setting uniform float {location}: {err}");
            }
        }
    }
    
    //Uniforms are properties that applies to the entire geometry
    public void SetUniform(string name, int value)
    {
        //Setting a uniform on a shader using a name.
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1) //If GetUniformLocation returns -1 the uniform is not found.
        {
            if (_traceShader) Error($"{name} uniform not found on shader.");
            return;
        }
        _gl.Uniform1(location, value);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error setting uniform {name}: {err}");
            }
        }
    }

    public void SetUniform(string name, float value)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (_traceShader) Error($"{name} uniform not found on shader.");
            return;
        }
        _gl.Uniform1(location, value);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error setting uniform {name}: {err}");
            }
        }
    }

    public int GetUniform(string name)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (_traceShader) Error($"{name} uniform not found on shader.");
            return -1;
        }

        return (int) location;
    }


    public int GetAttrib(string name)
    {
        int location = _gl.GetAttribLocation(Handle, name);
        if (location == -1)
        {
            if (_traceShader) Error($"{name} attribute not found on shader.");
            return 0;
        }

        return (int) location;
    }
    
    public void SetUniform(string name, in Vector4 v)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (_traceShader) Error($"{name} uniform not found on shader.");
            return;
        }
        _gl.Uniform4(location, v.X, v.Y, v.Z, v.W);
    }
    

    public void SetUniform(int location, in Vector4 v)
    {
        if (-1 == location) return;
        _gl.Uniform4(location, v.X, v.Y, v.Z, v.W);
    }


    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        //A new overload has been created for setting a uniform so we can use the transform in our shader.
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        _gl.UniformMatrix4(location, 1, false, (float*) &value);
    }

    
    public unsafe void SetUniform(int location, Matrix4x4 value)
    {
        if (-1 == location) return;
        _gl.UniformMatrix4(location, 1, false, (float*) &value);
    }

    
    public void SetUniform(int location, Vector3 value)
    {
        if (-1 == location) return;
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }

    
    public void SetUniform(string name, Vector3 value)
    {
        int location = _gl.GetUniformLocation(Handle, name);
        if (location == -1)
        {
            if (_traceShader) Error($"Unable to find uniform {name}");
            return;
        }
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }


    public void Use()
    {
        _gl.UseProgram(Handle);
        if (_checkErrors)
        {
            var err = _gl.GetError();
            if (err != GLEnum.NoError)
            {
                if (_traceShader) Error($"Error using program {Handle}: {err}.");
            }
        }
    }
    
    
    public void Upload()
    {
        if (Handle != 0xffffffff)
        {
            ErrorThrow($"Shader already uploaded.", m => new InvalidOperationException(m));
            return;
        }

        if (!SkVertexShader.IsUploaded()) SkVertexShader.Upload();
        if (!SkFragmentShader.IsUploaded()) SkFragmentShader.Upload();

        Handle = _gl.CreateProgram();
        CheckError(_gl, $"CreateProgram");
        
        /*
         * Attach the individual shaders.
         */
        _gl.AttachShader(Handle, SkVertexShader.Handle);
        CheckError(_gl, $"glAttachShader Vertex {SkVertexShader.Handle} to Program {Handle} .");
        _gl.AttachShader(Handle, SkFragmentShader.Handle);
        CheckError(_gl, $"glAttachShader Fragment {SkFragmentShader.Handle} to Program {Handle} .");
        _gl.LinkProgram(Handle); 
        CheckError(_gl, $"glLinkProgram {Handle}.");
        //Check for linking errors.
        _gl.GetProgram(Handle, GLEnum.LinkStatus, out var status);
        CheckError(_gl, $"glGetProgram LinkStatus {Handle}.");
        if (status == 0)
        {
            throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(Handle)}");
        }
        //Detach and delete the shaders
        _gl.DetachShader(Handle, SkVertexShader.Handle);
        CheckError(_gl, $"glDetachShader Vertex {SkVertexShader.Handle} from Program {Handle} .");
        _gl.DetachShader(Handle, SkFragmentShader.Handle);
        CheckError(_gl, $"glDetachShader Fragment {SkFragmentShader.Handle} from Program {Handle} .");
    }
    
    
    public bool IsUploaded()
    {
        return Handle != 0xffffffff;
    }

    public void Dispose()
    {
        if (Handle != 0xffffffff)
        {
            _gl.DeleteProgram(Handle);
            CheckError(_gl, $"glDeleteProgram {Handle}.");
            Handle = 0xffffffff;
        }
        SkVertexShader.Dispose();
        SkFragmentShader.Dispose();
    }
    
    
    public SkProgramEntry(GL gl, SkSingleShaderEntry skVertexShader, SkSingleShaderEntry skFragmentShader)
    {
        _gl = gl;
        SkVertexShader = skVertexShader;
        SkFragmentShader = skFragmentShader;
    }
}

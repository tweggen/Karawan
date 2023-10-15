#if false
using Silk.NET.OpenGL;
using static engine.Logger;
using System.Numerics;
using System;
using System.Data;

namespace Splash.Silk;


/**
 * Represent both fragment and vertex shader.
 */
public class SkShader : IDisposable
{
    //Our handle and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private GL _gl;
    public uint Handle
    {
        get => _handle;
    }
    
    
    public int CheckError(string what)
    {
        while (true)
        {
            var error = _gl.GetError();
            if (error != GLEnum.NoError)
            {
                Error($"Found OpenGL {what} error {error}");
            }
            else
            {
                // Console.WriteLine($"OK: {what}");
                return 0;
            }
        }
    }


    public SkShader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        //Load the individual shaders.
        uint vertex = LoadShaderSource(ShaderType.VertexShader, vertexSource);
        uint fragment = LoadShaderSource(ShaderType.FragmentShader, fragmentSource);
        //Create the shader program.
        _handle = _gl.CreateProgram();
        CheckError("glCreateProgram.");
        //Attach the individual shaders.
        _gl.AttachShader(_handle, vertex);
        CheckError($"glAttachShader {_handle} vertex .");
        _gl.AttachShader(_handle, fragment);
        CheckError($"glAttachShader {_handle} fragment .");
        _gl.LinkProgram(_handle); 
        CheckError($"glLinkProgram {_handle}.");
        //Check for linking errors.
        _gl.GetProgram(_handle, GLEnum.LinkStatus, out var status);
        CheckError($"glGetProgram LinkStatus {_handle}.");
        if (status == 0)
        {
            throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_handle)}");
        }

        _debuggingHook();
        //Detach and delete the shaders
        _gl.DetachShader(_handle, vertex);
        CheckError($"glDetachShader vertex {_handle}.");
        _gl.DetachShader(_handle, fragment);
        CheckError($"glDetachShader fragment {_handle}.");
        _gl.DeleteShader(vertex);
        CheckError($"glDeleteShader vertex.");
        _gl.DeleteShader(fragment);
        CheckError($"glDeleteShader fragment.");
    }

    public void _debuggingHook()
    {
        string[] uniforms = new string[]
        {
            "mvp", "texture0", "texture2",
            "col4Diffuse", "col4Emissive", "col4EmissiveFactors",
            "col4Ambient", "v3AbsPosView"
        };
        foreach (var name in uniforms)
        {
            int index = _gl.GetUniformLocation(_handle, name);
            Console.WriteLine( $"Uniform {name} has index {index}");
        }

        string[] attribs = new string[]
        {
            "vertexPosition", "vertexTexCoord", "vertexTexCoord2",
            "vertexNormal", "vertexColor", "instanceTransform",
            "fragPosition", "fragTexCoord", "fragTexCoord2",
            "fragColor", "fragNormal"
        };
        foreach (var name in attribs)
        {
            int index = _gl.GetAttribLocation(_handle, name);
            Console.WriteLine($"Attrib {name} has index {index}");
        }

    }

    public void Use()
    {
        
        //Using the program
        _gl.UseProgram(_handle);
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            Error($"Error using shader {_handle}: {err}");
        }
    }

    public void SetUniform(int location, int value)
    {
        _gl.Uniform1(location, value);
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            Error($"Error setting uniform {location}: {err}");
        }
    }

    public void SetUniform(int location, float value)
    {
        _gl.Uniform1(location, value);
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            Error($"Error setting uniform {location}: {err}");
        }
    }
    
    //Uniforms are properties that applies to the entire geometry
    public void SetUniform(string name, int value)
    {
        //Setting a uniform on a shader using a name.
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1) //If GetUniformLocation returns -1 the uniform is not found.
        {
            ErrorThrow($"{name} uniform not found on shader.", (m) => new InvalidOperationException(m));
        }
        _gl.Uniform1(location, value);
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            Error($"Error setting uniform {name}: {err}");
        }
    }

    public void SetUniform(string name, float value)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            ErrorThrow($"{name} uniform not found on shader.", (m) => new InvalidOperationException(m));
        }
        _gl.Uniform1(location, value);
        var err = _gl.GetError();
        if (err != GLEnum.NoError)
        {
            Error($"Error setting uniform {name}: {err}");
        }
    }

    public uint GetUniform(string name)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            ErrorThrow($"{name} uniform not found on shader.", (m) => new InvalidOperationException(m));
        }

        return (uint) location;
    }


    public uint GetAttrib(string name)
    {
        int location = _gl.GetAttribLocation(_handle, name);
        if (location == -1)
        {
            ErrorThrow($"{name} attribute not found on shader.", (m) => new InvalidOperationException(m));
        }

        return (uint) location;
    }
    
    public void SetUniform(string name, in Vector4 v)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            ErrorThrow($"{name} uniform not found on shader.", (m) => new InvalidOperationException(m));
        }
        _gl.Uniform4(location, v.X, v.Y, v.Z, v.W);
    }
    

    public void SetUniform(int location, in Vector4 v)
    {
        _gl.Uniform4(location, v.X, v.Y, v.Z, v.W);
    }


    public unsafe void SetUniform(string name, Matrix4x4 value)
    {
        //A new overload has been created for setting a uniform so we can use the transform in our shader.
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        _gl.UniformMatrix4(location, 1, false, (float*) &value);
    }

    
    public void SetUniform(int location, Vector3 value)
    {
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }
    
    public void SetUniform(string name, Vector3 value)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            throw new Exception($"{name} uniform not found on shader.");
        }
        _gl.Uniform3(location, value.X, value.Y, value.Z);
    }
    
    public void Dispose()
    {
        //Remember to delete the program when we are done.
        _gl.DeleteProgram(_handle);
        CheckError($"glDeleteProgram {_handle}.");
    }


}
#endif
﻿using Silk.NET.OpenGL;
using static engine.Logger;
using System.Numerics;
using System;
using System.Data;

namespace Splash.Silk;

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

    public SkShader(GL gl, string vertexSource, string fragmentSource)
    {
        _gl = gl;

        //Load the individual shaders.
        uint vertex = LoadShaderSource(ShaderType.VertexShader, vertexSource);
        uint fragment = LoadShaderSource(ShaderType.FragmentShader, fragmentSource);
        //Create the shader program.
        _handle = _gl.CreateProgram();
        //Attach the individual shaders.
        _gl.AttachShader(_handle, vertex);
        _gl.AttachShader(_handle, fragment);
        _gl.LinkProgram(_handle); 
        //Check for linking errors.
        _gl.GetProgram(_handle, GLEnum.LinkStatus, out var status);
        if (status == 0)
        {
            throw new Exception($"Program failed to link with error: {_gl.GetProgramInfoLog(_handle)}");
        }

        _debuggingHook();
        //Detach and delete the shaders
        _gl.DetachShader(_handle, vertex);
        _gl.DetachShader(_handle, fragment);
        _gl.DeleteShader(vertex);
        _gl.DeleteShader(fragment);
    }

    public void _debuggingHook()
    {
        string[] uniforms = new string[]
        {
            "mvp", "texture0", "texture2", "colDiffuse", "ambient", "viewPos"
        };
        foreach (var name in uniforms)
        {
            int index = _gl.GetUniformLocation(_handle, name);
            Console.WriteLine( $"Uniform {name} has index {index}");
        }

        string[] attribs = new string[]
        {
            "vertexPosition", "vertexTexCoord", "vertexTexCoord2",
            "vertexNormal", "vertexColor", "instanceTransform"
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
    }

    public void SetUniform(int location, int value)
    {
        _gl.Uniform1(location, value);
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
    }

    public void SetUniform(string name, float value)
    {
        int location = _gl.GetUniformLocation(_handle, name);
        if (location == -1)
        {
            ErrorThrow($"{name} uniform not found on shader.", (m) => new InvalidOperationException(m));
        }
        _gl.Uniform1(location, value);
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
    }

    private uint LoadShaderSource(ShaderType type, string source)
    {
        //To load a single shader we need to:
        //1) Load the shader from a file.
        //2) Create the handle.
        //3) Upload the source to opengl.
        //4) Compile the shader.
        //5) Check for errors.
        uint handle = _gl.CreateShader(type);
        _gl.ShaderSource(handle, source);
        _gl.CompileShader(handle);
        string infoLog = _gl.GetShaderInfoLog(handle);
        if (!string.IsNullOrWhiteSpace(infoLog))
        {
            ErrorThrow($"Error compiling shader of type {type}, failed with error {infoLog}", (m)=>new InvalidExpressionException(m));
        }

        return handle;
    }
}
﻿using Silk.NET.OpenGL;
using System;
using System.Diagnostics;
using Newtonsoft.Json.Serialization;
using static engine.Logger;

namespace Splash.Silk;

/**
 * Shamelessly copied from the tutorial
 */
public class BufferObject<TDataType> : IDisposable
    where TDataType : unmanaged
{
    //Our handle, buffertype and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private BufferTargetARB _bufferType;
    private GL _gl;
    private const bool _checkErrors = false;  

    public unsafe BufferObject(GL gl, Span<TDataType> data, BufferTargetARB bufferType)
    {
        //Setting the gl instance and storing our buffer type.
        _gl = gl;
        _bufferType = bufferType;

        //Getting the handle, and then uploading the data to said handle.
        _handle = _gl.GenBuffer();
        _gl.BindBuffer(_bufferType, _handle);
        fixed (void* d = data)
        {
            _gl.BufferData(bufferType, (nuint)(data.Length * sizeof(TDataType)), d, BufferUsageARB.StaticDraw);
        }

        if (_checkErrors && _gl.GetError() != GLEnum.NoError)
        {
            Trace("Error uploading mesh.");
        }
    }

    
    public void BindBuffer()
    {
        //Binding the buffer object, with the correct buffer type.
        _gl.BindBuffer(_bufferType, _handle);
    }

    
    public void Dispose()
    {
        //Remember to delete our buffer.
        _gl.DeleteBuffer(_handle);
    }
}

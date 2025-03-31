using Silk.NET.OpenGL;
using static engine.Logger;
using static Splash.Silk.GLCheck;

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

    public unsafe BufferObject(GL gl, Span<TDataType> data, BufferTargetARB bufferType)
    {
        //Setting the gl instance and storing our buffer type.
        _gl = gl;
        _bufferType = bufferType;

        //Getting the handle, and then uploading the data to said handle.
        _handle = _gl.GenBuffer();
        _gl.BindBuffer(_bufferType, _handle);
        CheckError(_gl,$"BindBuffer type {_bufferType}.");
        fixed (void* d = data)
        {
            _gl.BufferData(bufferType, (nuint)(data.Length * sizeof(TDataType)), d, BufferUsageARB.DynamicDraw);
            CheckError(_gl,$"BufferData type {_bufferType}.");
        }
    }


    public void BindBufferBase(uint slot)
    {
        _gl.BindBufferBase(_bufferType, slot, _handle);
        CheckError(_gl,$"BindBufferBase.");
    }
    
    
    public void BindBuffer()
    {
        //Binding the buffer object, with the correct buffer type.
        _gl.BindBuffer(_bufferType, _handle);
        CheckError(_gl,$"BindBuffer.");
    }

    
    public void Dispose()
    {
        //Remember to delete our buffer.
        _gl.DeleteBuffer(_handle);
    }
}

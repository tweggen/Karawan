using Silk.NET.OpenGL;
using System;
using System.Numerics;

namespace Splash.Silk;

//The vertex array object abstraction.
public class VertexArrayObject : IDisposable
{
    //Our handle and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private GL _gl;

    private BufferObject<ushort> _bIndices = null;
    private BufferObject<float> _bVertices = null;
    private BufferObject<float> _bNormals = null;
    private BufferObject<float> _bUVs = null;

    public uint Handle
    {
        get => _handle;
    }

    public unsafe VertexArrayObject(GL gl, SkMeshEntry skMeshEntry)
    {
        /*
         * We need the GL instance.
         */
        _gl = gl;
        
        /*
         * Now create buffer objects for all properties of the mesh entry. 
         */
        _bIndices = new BufferObject<ushort>(_gl, skMeshEntry.Indices, BufferTargetARB.ElementArrayBuffer);
        _bVertices = new BufferObject<float>(_gl, skMeshEntry.Vertices, BufferTargetARB.ArrayBuffer);
        _bNormals = new BufferObject<float>(_gl, skMeshEntry.Normals, BufferTargetARB.ArrayBuffer);
        _bUVs = new BufferObject<float>(_gl, skMeshEntry.UVs, BufferTargetARB.ArrayBuffer);

        //Setting out handle and binding the VBO and EBO to this VAO.
        _handle = _gl.GenVertexArray();
        BindVertexArray();
        _bVertices.BindBuffer();
        _gl.EnableVertexAttribArray(0);
        _gl.VertexAttribPointer(
            0,
            3,
            VertexAttribPointerType.Float,
            false,
            0,
            (void*) 0);
        _bUVs.BindBuffer();
        _gl.EnableVertexAttribArray(1);
        _gl.VertexAttribPointer(
            1,
            2,
            VertexAttribPointerType.Float,
            false,
            0,
            (void*) 0);
        _gl.EnableVertexAttribArray(2);
        _gl.VertexAttribPointer(
            2,
            2,
            VertexAttribPointerType.Float,
            false,
            0,
            (void*) 0);
        _bNormals.BindBuffer();
        _gl.EnableVertexAttribArray(3);
        _gl.VertexAttribPointer(
            3,
            3,
            VertexAttribPointerType.UnsignedShort,
            false,
            0,
            (void*) 0);
        //_bIndices.BindBuffer();
        _gl.VertexAttrib4(4, new Vector4(1f, 1f, 1f, 1f));
        _gl.DisableVertexAttribArray(4);
    }
    
    
    public void BindVertexArray()
    {
        //Binding the vertex array.
        _gl.BindVertexArray(_handle);
        _bIndices.BindBuffer();
    }

    public void Dispose()
    {
        //Remember to dispose this object so the data GPU side is cleared.
        //We dont delete the VBO and EBO here, as you can have one VBO stored under multiple VAO's.
        _gl.DeleteVertexArray(_handle);
        _bVertices.Dispose();
        _bVertices = null;
        _bUVs.Dispose();
        _bUVs = null;
        _bNormals.Dispose();
        _bNormals = null;
        _bIndices.Dispose();
        _bIndices = null;
    }
}

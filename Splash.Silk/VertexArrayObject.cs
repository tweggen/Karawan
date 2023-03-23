using Silk.NET.OpenGL;
using System;

namespace Splash.Silk;

//The vertex array object abstraction.
public class VertexArrayObject : IDisposable
{
    //Our handle and the GL instance this class will use, these are private because they have no reason to be public.
    //Most of the time you would want to abstract items to make things like this invisible.
    private uint _handle;
    private GL _gl;

    public unsafe VertexArrayObject(GL gl, SkMeshEntry skMeshEntry)
    {
        /*
         * We need the GL instance.
         */
        _gl = gl;
        
        /*
         * Now create buffer objects for all properties of the mesh entry. 
         */
        var bIndices = new BufferObject<uint>(_gl, skMeshEntry.Indices, BufferTargetARB.ElementArrayBuffer);
        var bVertices = new BufferObject<float>(_gl, skMeshEntry.Vertices, BufferTargetARB.ArrayBuffer);
        var bNormals = new BufferObject<float>(_gl, skMeshEntry.Normals, BufferTargetARB.ArrayBuffer);
        var bUVs = new BufferObject<float>(_gl, skMeshEntry.UVs, BufferTargetARB.ArrayBuffer);

        //Setting out handle and binding the VBO and EBO to this VAO.
        _handle = _gl.GenVertexArray();
        BindVertexArray();
        bVertices.BindBuffer();
        _gl.VertexAttribPointer(
            0,
            3,
            VertexAttribPointerType.Float,
            false,
            0 /* 3 * (uint) sizeof(float) */,
            (void*) 0);
        bNormals.BindBuffer();
        _gl.VertexAttribPointer(
            0,
            3,
            VertexAttribPointerType.Float,
            false,
            0 /* 3 * (uint) sizeof(float) */,
            (void*) 0);
        bUVs.BindBuffer();
        _gl.VertexAttribPointer(
            0,
            2,
            VertexAttribPointerType.Float,
            false,
            0 /* 2 * (uint) sizeof(float) */,
            (void*) 0);
        bIndices.BindBuffer();
        
    }
    
    
    public void BindVertexArray()
    {
        //Binding the vertex array.
        _gl.BindVertexArray(_handle);
    }

    public void Dispose()
    {
        //Remember to dispose this object so the data GPU side is cleared.
        //We dont delete the VBO and EBO here, as you can have one VBO stored under multiple VAO's.
        _gl.DeleteVertexArray(_handle);
    }
}

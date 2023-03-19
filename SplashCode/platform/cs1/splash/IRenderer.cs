using System;
using System.Numerics;

namespace Karawan.platform.cs1.splash;

public interface IRenderer
{
    public void DrawMeshInstanced(
        in AMeshEntry aMeshEntry,
        in AMaterialEntry aMaterialEntry,
        in Span<Matrix4x4> spanMatrices,
        in int nMatrices);
}
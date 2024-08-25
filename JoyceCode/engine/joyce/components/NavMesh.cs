using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce.components;

/**
 * This lists the Meshes for this navmesh component.
 */
public struct NavMesh
{
    public Matrix4x4 ToWorld;
    public IEnumerable<Mesh> Meshes;
}
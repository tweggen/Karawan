using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce.components;

/**
 * This navmesh component can be attached to any other component.
 * Different NavMesh components can be merged into one common navmesh.
 *
 * Usually, navmeshes would be computed while loading a fragment.
 */
public struct NavMesh
{
    public Matrix4x4 ToWorld;
    public IEnumerable<Mesh> Meshes;
}

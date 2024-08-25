using System.Collections.Generic;

namespace engine.joyce.components;

/**
 * This lists the Meshes for this navmesh component.
 */
public struct NavMesh
{
    public IEnumerable<Mesh> Meshes;
}
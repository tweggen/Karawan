using System.Collections.Generic;
using engine.joyce;

namespace engine;

/**
 * Represents one part of a loaded model.
 * Usually maps to a entity.
 *
 * Trees of model nodes map to entities related
 * by the hierarchy API.
 */
public class ModelNode
{
    /**
     * If non-null, contains a list of children of this node.
     */
    public IList<ModelNode>? Children;

    /**
     * If non-null, contains a instance desc with meshes and
     * materials associated with this node.
     */
    public InstanceDesc? InstanceDesc;

    /**
     * If non-null, contains a transformation relative to the parent.
     */
    public engine.joyce.components.Transform3ToParent Transform;
}


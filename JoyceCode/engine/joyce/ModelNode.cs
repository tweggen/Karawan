using System.Collections.Generic;

namespace engine.joyce;

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
     * The root of this model.
     */
    public Model Model;
    
    /**
     * A possible node name.
     */
    public string Name;

    /**
     * A node index unique within the parent model.
     */
    public int Index;
    
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
     * If non-null describes the skin.
     */
    public Skin? Skin;
    
    /**
     * If non-null, contains a transformation relative to the parent.
     */
    public engine.joyce.components.Transform3ToParent Transform;
}


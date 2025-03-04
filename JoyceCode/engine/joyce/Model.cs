using System.Collections.Generic;
using System.Numerics;

namespace engine.joyce;

public class Joint
{
    /**
     * The node this joint controls
     */
    public IList<ModelNode> ControlledNodes;
    public Matrix4x4 InverseBindMatrix;
}


public class Skin
{
    public string Name;
    public IList<Joint> Joints;
}


/**
 * Represent a loaded or generated model.
 *
 * This contains
 * - general information about the model.
 * - a hierarchy of InstanceDescs.
 */
public class Model
{
    public string Name;

    public ModelNode RootNode;
    private int _nextNodeIndex = 1;
    private int _nextAnimIndex = 1;
    public Skeleton? Skeleton = null;
    public SortedDictionary<string, ModelAnimation> MapAnimations;
    public SortedDictionary<string, ModelNode> MapNodes = new();

    /**
     * Convenience method to create a model from a single InstanceDesc
     */
    public Model(InstanceDesc instanceDesc)
    {
        ModelNode mnRoot = new()
        {
            Model = this,
            InstanceDesc = instanceDesc,
            Transform = new(true, 0xffff, Matrix4x4.Identity)
        };
        RootNode = mnRoot;
    }


    public ModelNode CreateNode()
    {
        return new()
        {
            Model = this,
            Index = _nextNodeIndex++
        };
    }


    public ModelAnimation CreateAnimation()
    {
        return new ModelAnimation()
        {
            Index = _nextAnimIndex++
        };
    }


    /**
     * Fill my model structure and my root instance desc with the
     * contents from the other model.
     */
    public void FillPlaceholderFrom(Model other)
    {
        /*
         * We will use their rootnode and their name, however use our InstanceDesc
         * as we already gave out our instanceDesc to clients.
         */
        RootNode = other.RootNode;
        Name = other.Name;
    }


    public Skeleton FindSkeleton()
    {
        if (null == Skeleton)
        {
            Skeleton = new();
        }

        return Skeleton;
    }
    

    public Model()
    {
    }
}

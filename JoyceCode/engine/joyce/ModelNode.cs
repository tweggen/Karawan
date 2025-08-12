using System.Collections.Generic;
using System.Numerics;
using BepuPhysics.Collidables;

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
    public required Model Model;

    /**
     * The model tree we belong to
     */
    public required ModelNodeTree ModelNodeTree;
    
    /**
     * The parent model node
     */
    public required ModelNode? Parent;
    
    /**
     * A possible node name.
     */
    public string Name;

    /*
     * A node index unique within the parent model.
     */
    // public int Index;

    /**
     * What kind of entity relevant data does this one carry below in its children?
     */
    public uint EntityData = 0;
    
    /**
     * If non-null, contains a list of children of this node.
     */
    public List<ModelNode>? Children;

    public void AddChild(ModelNode mnChild)
    {
        if (null == Children)
        {
            Children = new List<ModelNode>();
        }
        Children.Add(mnChild);
        mnChild.Parent = this;
    }


    public void SetInstanceDesc(InstanceDesc id)
    {
        InstanceDesc = id;
        Transform = new(true, 0xffff, Matrix4x4.Identity);
    }


    public void SetModel(Model model, ModelNodeTree? modelNodeTree = null)
    {
        Model = model;
        if (modelNodeTree != null)
        {
            ModelNodeTree = modelNodeTree;
        }
        else
        {
            ModelNodeTree = Model.ModelNodeTree;
        }
            
    }
    
    
    /**
     * If non-null, contains a instance desc with meshes and
     * materials associated with this node.
     */
    public InstanceDesc? InstanceDesc;

    /**
     * If non-null, contains a transformation relative to the parent.
     */
    public engine.joyce.components.Transform3ToParent Transform;
    
    
    private string _dumpNodeLevel(int level)
    {
        string s = "";
        string t = new(' ', level * 4);
        {
            s += "{\n";
            s += $"{t}\"name\": \"{Name}\"";
            if (ModelNodeTree != null && ModelNodeTree.MapNodes != null)
            {
                if (ModelNodeTree.MapNodes.ContainsKey(Name))
                {
                    s += " (added)";
                }
                else
                {
                    s += " (standalone)";
                }
            }
            s += ",\n";
            if (!Transform.Matrix.IsIdentity)
            {
                s += $"{t}\"transform\": {Transform.Matrix.ToString()}";
            }
            s += $"{t}}},\n";
            if (Children != null)
            {
                s += $"{t}\"children\": ";
                if (Children != null)
                {
                    foreach (var mnChild in Children)
                    {
                        s += $"{mnChild._dumpNodeLevel(level + 1)}";
                    }
                }
                else
                {
                    s += "null";
                }
            }

            s += $"{t}}},\n";
        }
        return s;
    }

    public string DumpNode()
    {
        return _dumpNodeLevel(0);
    }

    
    public ModelNode? FindInstanceDescNodeBelow()
    {
        if (InstanceDesc != null)
        {
            return this;
        }

        if (Children == null)
        {
            return null;
        }

        foreach (var mnChild in Children)
        {
            var mnInstanceDescNode = mnChild.FindInstanceDescNodeBelow();
            if (mnInstanceDescNode != null)
            {
                return mnInstanceDescNode;
            }
        }

        return null;
    }


    /**
     * Find the closest instance desc node close to the animation node.
     */
    public ModelNode? FindClosestInstanceDesc()
    {
        ModelNode? mnCurr = this;

        while (mnCurr != null)
        {
            ModelNode? mnBelowCurr = mnCurr.FindInstanceDescNodeBelow();
            if (null != mnBelowCurr)
            {
                return mnBelowCurr;
            }

            mnCurr = mnCurr.Parent;
        }

        return null;
    }


    /**
     * Compute a matrix that, applied to a bone local, creates the global
     * coordinate. Or more generically, applies all model-nodes transformations.
     */
    public Matrix4x4 ComputeGlobalTransform()
    {
        Matrix4x4 m4ParentTransform;
        if (Parent != null)
        {
            m4ParentTransform = Parent.ComputeGlobalTransform();
        }
        else
        {
            m4ParentTransform = Matrix4x4.Identity;
        }

        m4ParentTransform = Transform.Matrix * m4ParentTransform;

        return m4ParentTransform;
    }

    
    /**
     * Compute a matrix that, applied to a bone local, creates the global
     * coordinate. Or more generically, applies all model-nodes transformations.
     */
    public void ComputeGlobalTransform(ref Matrix4x4 m4)
    {
       m4 = m4 * Transform.Matrix;
       Parent?.ComputeGlobalTransform(ref m4);
    }
    
    
    /**
     * Compute a matrix that, applied to a mesh, computes mesh local to bone local.
     * Or more generically, un-applies all modelnode transformations.
     */
    public void ComputeInverseGlobalTransform(ref Matrix4x4 m4)
    {
        Parent?.ComputeGlobalTransform(ref m4);
        Matrix4x4.Invert(Transform.Matrix, out var mInverse);
        m4 = m4 * mInverse;
    }
}

using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Numerics;
using BepuPhysics.Collidables;
using MessagePack;

namespace engine.joyce;


/**
 * Represents one part of a loaded model.
 * Usually maps to a entity.
 *
 * Trees of model nodes map to entities related
 * by the hierarchy API.
 *
 * WP-4.1 - this is where the Model graph's cycles are, and none of them is
 * written to the baked mo-{hash} file. Children is the only structural edge
 * persisted; Model, ModelNodeTree and Parent all point back UP and are restored
 * by ModelNodeTree.Rebind walking the same Children spine after load. Writing
 * them instead would not merely be redundant: MessagePack has no reference
 * tracking, so a Parent link would serialise a second copy of the parent, which
 * would serialise its children again, and the file would never terminate.
 *
 * EntityData is likewise derived - Model.Polish recomputes it bottom-up.
 */
[MessagePackObject(AllowPrivate = true)]
public partial class ModelNode
{
    /**
     * The root of this model.
     */
    [IgnoreMember]
    public required Model Model;

    /**
     * The model tree we belong to
     */
    [IgnoreMember]
    public required ModelNodeTree ModelNodeTree;

    /**
     * The parent model node
     */
    [IgnoreMember]
    public required ModelNode? Parent;

    /**
     * A possible node name.
     */
    [Key(0)]
    public string Name;

    /*
     * A node index unique within the parent model.
     */
    // public int Index;

    /**
     * What kind of entity relevant data does this one carry below in its children?
     */
    [IgnoreMember]
    public uint EntityData = 0;

    /**
     * If non-null, contains a list of children of this node.
     */
    [Key(1)]
    public List<ModelNode>? Children;


    /**
     * The ordinary constructor, used with an object initialiser that must still be
     * forced to supply Model / ModelNodeTree / Parent. Declared explicitly because
     * declaring the serialisation constructor below would otherwise suppress the
     * implicit one.
     */
    public ModelNode()
    {
    }


    /**
     * Deserialisation constructor.
     *
     * MessagePack 3.x generates its formatters as C# source, so the generated code
     * is subject to the required-member rule like any other caller and cannot use
     * the constructor above. [SetsRequiredMembers] is confined to THIS constructor
     * so the guarantee survives everywhere else: the three required members are
     * precisely the back-references a file must not carry, and ModelNodeTree.Rebind
     * sets them immediately after the tree is read.
     */
    [SerializationConstructor]
    [SetsRequiredMembers]
    private ModelNode(
        string name,
        List<ModelNode>? children,
        InstanceDesc? instanceDesc,
        engine.joyce.components.Transform3ToParent transform)
    {
        Name = name;
        Children = children;
        InstanceDesc = instanceDesc;
        Transform = transform;
    }


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
    [Key(2)]
    public InstanceDesc? InstanceDesc;

    /**
     * If non-null, contains a transformation relative to the parent.
     */
    [Key(3)]
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
                s += $"{t}\"transform\": {Transform.Matrix.ToString()}\n";
            }
            //s += $"{t}}},\n";
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
        Parent?.ComputeInverseGlobalTransform(ref m4);
        Matrix4x4.Invert(Transform.Matrix, out var m4Inverse);
        m4 = m4 * m4Inverse;
    }
}

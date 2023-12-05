using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.meta;

namespace builtin.tools;



/**
 * Create entities from a previously loaded model.
 *
 * Models are trees of nodes representing
 * - the geometry of the thing.
 * - the materials for the thing.
 * - the bones / skin for animation
 *
 * As models are hierarchial, they are decomposed into entities related
 * to each other using the Hierary API.
 * The geometry is added using Instance3 components.
 */
public class ModelBuilder
{
    private readonly Model _jModel;
    private readonly Engine _engine;
    private readonly HierarchyApi _aHierarchy;
    private readonly InstantiateModelParams _instantiateModelParams;


    private void _buildInstanceDescInto(in DefaultEcs.Entity eNode, in InstanceDesc id)
    {
        eNode.Set(new Instance3(id));
    }
    
    
    private void _buildNodeInto(in DefaultEcs.Entity eNode, in ModelNode mn)
    {
        if (_jModel.RootNode.Children != null && _jModel.RootNode.Children.Count > 0)
        {
            eNode.Set(mn.Transform);
        }
        else
        {
            if (mn.InstanceDesc != null)
            {
                mn.InstanceDesc.ModelTransform = mn.Transform.Matrix;
            }
        }

        if (mn.InstanceDesc != null)
        {
            _buildInstanceDescInto(eNode, mn.InstanceDesc);
        }

        if (mn.Children != null)
        {
            foreach (var mnChild in mn.Children)
            {
                DefaultEcs.Entity eChild = _engine.CreateEntity($"bc {_jModel.Name}");
                _aHierarchy.SetParent(eChild, eNode);
                _buildNodeInto(eChild, mnChild);
            }
        }
    }
    

    /**
     * Given a certain root, build the model into the property.
     * This handles the shortcut cases, where a model without any hierarchy
     * gets baked directly into that entity, where as a hierarchical model
     * becomes an additional hierarchy of, well hierarchical nodes.
     */
    public DefaultEcs.Entity BuildEntity(DefaultEcs.Entity? eRoot)
    {
        if (null == eRoot)
        {
            eRoot = _engine.CreateEntity($"br {_jModel.Name}");
        }

        var mnRoot = _jModel.RootNode;
        if (mnRoot != null)
        {
            _buildNodeInto(eRoot.Value, mnRoot);

            if (mnRoot.InstanceDesc != null)
            {
                Matrix4x4 mAdjust = Matrix4x4.Identity;
                mnRoot.InstanceDesc.ComputeAdjustMatrix(_instantiateModelParams, ref mAdjust);
                if (mnRoot.Children?.Count > 0)
                {
                    /*
                     * If we have children, we bake it (now) into the top hierarchy node.
                     */
                    var cTransformToParent = eRoot.Value.Get<Transform3ToParent>();
                    cTransformToParent.Matrix *= mAdjust;
                    eRoot.Value.Set(cTransformToParent);
                }
                else
                {
                    /*
                     * If we do not have children, let's just bake it into InstanceDesc.
                     */
                    mnRoot.InstanceDesc.ModelTransform *= mAdjust;
                }
            }
        }
        
        return eRoot.Value;
    }
    
    
    public ModelBuilder(Engine engine, Model jModel, InstantiateModelParams? instantiateModelParams)
    {
        _engine = engine;
        _jModel = jModel;
        _instantiateModelParams = instantiateModelParams;
        _aHierarchy = I.Get<HierarchyApi>();
    }
}
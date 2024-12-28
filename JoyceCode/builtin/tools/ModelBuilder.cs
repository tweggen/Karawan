using System.ComponentModel;
using System.Numerics;
using engine;
using engine.joyce;
using engine.joyce.components;
using engine.meta;
using static engine.Logger;

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
 *
 * These entities are filled with geometrical information.
 * In addition, the modelbuilder can generate
 * - Physics information.
 */
public class ModelBuilder
{
    private readonly Model _jModel;
    private readonly Engine _engine;
    private readonly HierarchyApi _aHierarchy;
    private readonly InstantiateModelParams _instantiateModelParams;
    private readonly bool _isHierarchical;


    private void _buildInstanceDescInto(in DefaultEcs.Entity eNode, in InstanceDesc id)
    {
        eNode.Set(new Instance3(id));
    }
    
    
    private void _buildNodeInto(in DefaultEcs.Entity eNode, in ModelNode mn)
    {
        if (_isHierarchical)
        {
            eNode.Set(mn.Transform);
        }
        else
        {
            //if (mn.InstanceDesc != null)
            //{
            //    mn.InstanceDesc.ModelTransform = mn.Transform.Matrix;
            //}
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


    private ModelNode _findFirstInstanceDesc(ModelNode mnRoot)
    {
        if (null != mnRoot.InstanceDesc)
        {
            return mnRoot;
        }


        if (null == mnRoot.Children)
        {
            return null;
        }

        foreach (var mnChild in mnRoot.Children)
        {
            var mnFirst = _findFirstInstanceDesc(mnChild);
            if (null != mnFirst)
            {
                return mnFirst;
            }
        }

        return null;
    }
    
    

    /**
     * Given a certain root, build the model into the property.
     * This handles the shortcut cases, where a model without any hierarchy
     * gets baked directly into that entity, where as a hierarchical model
     * becomes an additional hierarchy of, well hierarchical nodes.
     */
    public DefaultEcs.Entity BuildEntity(DefaultEcs.Entity? eUserRoot)
    {
        if (null == eUserRoot)
        {
            eUserRoot = _engine.CreateEntity($"br {_jModel.Name}");
        }
        
        var mnRoot = _jModel.RootNode;
        if (mnRoot == null)
        {
            Warning("Model has no resulting in an empty instance.");
            return eUserRoot.Value;
        }

        DefaultEcs.Entity eRoot;

        /*
         * Now find the first instance desc that we compute our adjustment from.
         */
        ModelNode mnAdjust = null;
        if (_isHierarchical)
        {
            mnAdjust = _findFirstInstanceDesc(mnRoot);
        }
        
        Matrix4x4 mAdjust = Matrix4x4.Identity;
        if (mnAdjust != null && mnAdjust.InstanceDesc != null)
        {
            mnAdjust.InstanceDesc.ComputeAdjustMatrix(_instantiateModelParams, ref mAdjust);
        }
        
        /*
         * if we are hierarchical, we possibly need to create a root node
         * to enable separate.
         */
        if (_isHierarchical)
        {
            if (0 != (_instantiateModelParams.GeomFlags & InstantiateModelParams.NO_CONTROLLABLE_ROOT))
            {
                eRoot = eUserRoot.Value;
            }
            else
            {
                eRoot = _engine.CreateEntity($"ba {_jModel.Name}");
                _aHierarchy.SetParent(eRoot, eUserRoot);
            }
        }
        else
        {
            eRoot = eUserRoot.Value;
        }

        _buildNodeInto(eRoot, mnRoot);

        if (mnAdjust != null)
        {
            if (_isHierarchical)
            {
                /*
                 * If we are hierarchical, we need to find the first root node to compute
                 * the adjustment from.
                 */
                /*
                 * If we have children, we add the adjustment to one additional layer.
                 */
                var cTransformToParent = eRoot.Get<Transform3ToParent>();
                cTransformToParent.Matrix *= mAdjust;
                eRoot.Set(cTransformToParent);
            }
            else
            {
                /*
                 * If we do not have children, we assume it's already inside the InstanceDesc.
                 */
            }
        }

        return eUserRoot.Value;
    }
    
    
    public ModelBuilder(Engine engine, Model jModel, InstantiateModelParams? instantiateModelParams)
    {
        _engine = engine;
        _jModel = jModel;
        _instantiateModelParams = instantiateModelParams;
        _aHierarchy = I.Get<HierarchyApi>();
        _isHierarchical = (_jModel.RootNode.Children != null && _jModel.RootNode.Children.Count > 0);
    }
}
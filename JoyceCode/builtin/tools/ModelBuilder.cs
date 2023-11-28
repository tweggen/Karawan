using engine;
using engine.joyce;
using engine.joyce.components;

namespace Joyce.builtin.tools;

public class ModelBuilder
{
    private readonly Model _jModel;
    private readonly Engine _engine;
    private readonly HierarchyApi _aHierarchy;


    private void _buildInstanceDescInto(in DefaultEcs.Entity eNode, in InstanceDesc id)
    {
        eNode.Set(new Instance3(id));
    }
    
    
    private void _buildNodeInto(in DefaultEcs.Entity eNode, in ModelNode mn)
    {
        if (mn.InstanceDesc != null)
        {
            _buildInstanceDescInto(eNode, mn.InstanceDesc);
        }

        eNode.Set(mn.Transform);

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
    

    public DefaultEcs.Entity BuildEntity(DefaultEcs.Entity? eRoot)
    {
        if (null == eRoot)
        {
            eRoot = _engine.CreateEntity($"br {_jModel.Name}");
        }

        if (_jModel.RootNode != null)
        {
            _buildNodeInto(eRoot.Value, _jModel.RootNode);
        }
        
        return eRoot.Value;
    }
    
    
    public ModelBuilder(Engine engine, Model jModel)
    {
        _engine = engine;
        _jModel = jModel;
        _aHierarchy = I.Get<HierarchyApi>();
    }
}
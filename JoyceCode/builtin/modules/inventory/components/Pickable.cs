using engine;
using engine.joyce;

namespace builtin.modules.inventory.components;


/**
 * If this component is attached to some entity, the entity can be
 * detached from its current parent and become attached to the inventory entity. 
 */
[engine.IsPersistable]
public struct Pickable
{
    /**
     * What is that we are picking up?
     */
    public string PickableId {
        get => Description.Path;
        set
        {
            Description = I.Get<PickableDirectory>().Get(value);
        }
    }

    public PickableDescription Description;
}

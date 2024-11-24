using engine.joyce;

namespace builtin.modules.inventory.components;


/**
 * If this component is attached to some entity, the entity can be
 * detached from its current parent and become attached to the inventory entity. 
 */
public struct Pickable
{
    /**
     * What is that we are picking up?
     */
    public PickableDescription Desciption;
}

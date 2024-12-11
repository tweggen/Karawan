using System.Collections.Generic;
using builtin.modules.inventory.components;
using engine;
using engine.joyce.components;
using FbxSharp;

namespace builtin.modules.inventory;

/**
 * Lua bindings for the inventory.
 */
public class InvLuaBindings
{
    public List<SortedDictionary<string, object>> getItemTextList()
    {
        var listOfItems = new List<SortedDictionary<string, object>>();
        foreach (var ePickable in 
                 I.Get<Engine>().GetEcsWorld().GetEntities()
                     .With<Pickable>()
                     .Without<Parent>()
                     .AsEnumerable())
        {
            ref var cPickable = ref ePickable.Get<Pickable>();
            var pickableDescription = cPickable.Description;
            if (pickableDescription != null)
            {
                var dictItem = new SortedDictionary<string, object>();
                dictItem.Add("id", ePickable.ToString());
                dictItem.Add("text", pickableDescription.Name);
                listOfItems.Add(dictItem);
            }
        }

        return listOfItems;
    }
}
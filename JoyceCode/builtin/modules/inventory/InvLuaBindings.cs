using System.Collections.Generic;

namespace builtin.modules.inventory;

/**
 * Lua bindings for the inventory.
 */
public class InvLuaBindings
{
    public List<SortedDictionary<string, string>> getItemTextList()
    {
        return new List<SortedDictionary<string, string>>()
        {
            new ()
            {
                { "text", "Sunglasses" },
                { "id", "inv-1" }
            },
            new ()
            {
                { "text", "Empry Ramen" },
                { "id", "inv-2" }
            },
        };
    }
}
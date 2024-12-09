using System.Collections.Generic;

namespace builtin.modules.inventory;

/**
 * Lua bindings for the inventory.
 */
public class InvLuaBindings
{
    public List<string> getItems()
    {
        return new List<string>() { "alpha", "beta", "gamma" };
    }
}
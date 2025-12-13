using System;
using engine;

namespace nogame.npcs.niceday;

/**
 * Initialize niceday NPCs.
 *
 * This installs the niceday Fragment Operator required to populate the forest segments.
 */
public class Module : AModule
{
    protected override void OnModuleActivate()
    {
        base.OnModuleActivate();

        // TXWTODO: Find a way to hook into metagen installation right here.
        /*
         * Metagen installation right 
         */
    }
}
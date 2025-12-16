using System;
using engine;

namespace nogame.npcs.niceday;

/**
 * Initialize niceday NPCs.
 *
 * NPCs can be placed by different means,
 * - fragment operators (reappearing / unloaded things)
 * - placement operators (one-time placement, cluster etc. agnostic, will disappeart)
 * - quests (would remain persistent afterwards ort killed with the mission's end)
 * - hard coded code.
 * This installs the niceday Fragment Operator required to populate the forest segments.
 * It also takes care of removal on fragment unload.
 *
 * In addition, it installs a
 * - event driven conversation logic associated with the npc.
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
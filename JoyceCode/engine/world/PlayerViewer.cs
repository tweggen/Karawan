using System.Collections.Generic;
using System.Numerics;
using DefaultEcs;

namespace engine.world;

public class PlayerViewer : IViewer
{
    private Engine _engine;
    
    private static readonly int LoadNSurroundingFragments = 2;

    /**
     * Report and predict visibility for the player's entity.
     */
    public void GetVisibleFragments(ref IList<FragmentVisibility> lsVisib)
    {
        Entity ePlayer;
        if (!_engine.TryGetPlayerEntity(out ePlayer) || !ePlayer.Has<engine.physics.components.Body>())
        {
            return;
        }

        ref var cBody = ref ePlayer.Get<engine.physics.components.Body>();

        /*
         * We request around our position
         */
        ref Vector3 v3MyPos = ref cBody.Reference.Pose.Position;
        engine.joyce.Index3 i3MyFrag = new(v3MyPos / MetaGen.FragmentSize); 
        
        for (int dz = -LoadNSurroundingFragments; dz <= LoadNSurroundingFragments; dz++)
        {
            for (int dx = -LoadNSurroundingFragments; dx <= LoadNSurroundingFragments; dx++)
            {
                lsVisib.Add(new ()
                {
                    How = FragmentVisibility.Visible3dNow,
                    I = (short) (i3MyFrag.I + dx),
                    K = (short) (i3MyFrag.K + dz)
                });        
            }
        }
        
        /*
         * And if we move, we predict in the direction of our velocity
         */
        // ref Vector3 v3MyVelo = ref cBody.Reference.Velocity.Linear;
    }

    public PlayerViewer(Engine engine0)
    {
        _engine = engine0;
    }
}
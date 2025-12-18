using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using engine;
using engine.geom;
using engine.joyce;
using engine.streets;
using engine.world;
using nogame.characters;
using nogame.characters.citizen;
using static engine.Logger;

namespace nogame.npcs.niceday;

/**
 * This fragment operator will place niceday NPCs on some of the forest
 * areas in the cities.
 */
public class FragmentOperator : IFragmentOperator
{
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    public static readonly string EntityName = "nogame.npcs.niceguy";

    static private object _lo = new();
    private engine.world.ClusterDesc _clusterDesc;
    private AABB _clusterAABB;
    private string _myKey;
    private Engine _engine;


    public void FragmentOperatorGetAABB(out AABB aabb)
    {
        _clusterDesc.GetAABB(out aabb);
    }


    public string FragmentOperatorGetPath()
    {
        return $"9020/nogame.npcs.niceday/{_myKey}/{_clusterDesc.IdString}";
    }


    private async Task _placeNPC(Context ctx, Vector3 v3Pos)
    {
        /*
         * Right now, this character does not have any behavior.
         */

        var cmd = CharacterModelDescriptionFactory.CreateNPC(ctx.Rnd);
        EntityCreator creator = new()
        {
            // no BehaviorFactory
            CharacterModelDescription = cmd,
            PhysicsName = EntityName
        };
        var model = await creator.CreateAsync();
        
        _engine.QueueEntitySetupAction(EntityName, eTarget =>
        {
            int fragmentId = ctx.Fragment.NumericalId;

            eTarget.Set(new engine.world.components.Owner(fragmentId));

            /*
             * We already setup the FromModel in case we utilize one of the characters as
             * subject of a Quest.
             */
            eTarget.Set(new engine.joyce.components.FromModel() { Model = model, ModelCacheParams = creator.ModelCacheParams });
            
            creator.CreateLogical(eTarget);
            
            /*
             * We need to set a preliminary Transform3World component. Invisible, but inside the fragment.
             * That way, the character will not be cleaned up immediately.
             */
            eTarget.Set(new engine.joyce.components.Transform3ToWorld(0, 0,
                Matrix4x4.CreateTranslation(ctx.Fragment.Position)));
            
            /*
             * If we created physics for this one, take care to minimize
             * the distance for physics support.
             */
            if (eTarget.Has<engine.physics.components.Body>())
            {
                ref var cBody = ref eTarget.Get<engine.physics.components.Body>();
                if (cBody.PhysicsObject != null)
                {
                    cBody.PhysicsObject.MaxDistance = 10f;
                }
            }
            
            if (!eTarget.Has<engine.joyce.components.GPUAnimationState>())
            {
                eTarget.Set(new engine.joyce.components.GPUAnimationState()
                {
                    AnimationState = cmd.AnimationState 
                });
            }

            //#error Setup animation without duplicating too much code from citizen behavior.
            I.Get<TransformApi>().SetTransforms(eTarget, 
                true, 0x00000001,
                Quaternion.Identity, v3Pos, Vector3.One);
        });
    }
    

    public Func<Task> FragmentOperatorApply(Fragment worldFragment, FragmentVisibility visib)
    {
        /*
         * Look for a forest estate and place a character there.
         */
        return async () =>
        {
            /*
             * Only create for 3d visibility, not for map.
             */
            if (0 == (visib.How & engine.world.FragmentVisibility.Visible3dAny))
            {
                return;
            }

            var ctx = new Context()
            {
                Rnd = new(_myKey),
                Fragment = worldFragment
            };

            var listForestQuarters = _clusterDesc.QuarterStore().QueryQuarters(
                _clusterAABB,
                Quarter.QuarterAttributes.Forest,
                Quarter.QuarterAttributes.Forest | Quarter.QuarterAttributes.Building
            );

            foreach (var quarter in listForestQuarters)
            {
                await _placeNPC(ctx, 
                    I.Get<MetaGen>().Loader.GetWalkingPosAt(
                        _clusterDesc, 
                        _clusterDesc.Pos + quarter.GetCenterPoint3()));
            }
        };
    }


    public FragmentOperator(
        in engine.world.ClusterDesc clusterDesc,
        string strKey
    )
    {
        _clusterDesc = clusterDesc;
        _clusterAABB = clusterDesc.AABB;
        _myKey = strKey;
        _engine = I.Get<Engine>();
    }


    public static engine.world.IFragmentOperator InstantiateFragmentOperator(IDictionary<string, object> p)
    {
        return new nogame.npcs.niceday.FragmentOperator(
            (engine.world.ClusterDesc)p["clusterDesc"],
            (string)p["strKey"]);
    }
}


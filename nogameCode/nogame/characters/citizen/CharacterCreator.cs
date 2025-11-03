using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.loader;
using builtin.tools;
using engine;
using engine.geom;
using engine.joyce;
using engine.joyce.components;
using engine.physics;
using engine.streets;
using engine.world;
using nogame.cities;
using static engine.Logger;

namespace nogame.characters.citizen;

public class CharacterCreator
{
    /**
     * If this is one, all animations of one character are using the same frame at any given time.
     * Setting this to two limits this to two different animation phases. That way on mobile platforms
     * that do not upload animation tables to gpu but require distinct draw calls, draw calls are saved.
     */
    private class Context
    {
        public builtin.tools.RandomSource Rnd;
        public engine.world.Fragment Fragment;
    }
    
    public static readonly string EntityName = "nogame.characters.citizen";
    public static readonly float PhysicsMass = 60f;
    private static readonly float PhysicsRadius = 1f;
    public static BodyInertia PInertiaSphere = 
        new BepuPhysics.Collidables.Sphere(
            CharacterCreator.PhysicsRadius)
        .ComputeInertia(CharacterCreator.PhysicsMass);

    private static object _classLock = new();

    private static ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

    private static bool _trace = false;
    
    
    static public void ChooseQuarterDelimPointPos(
        builtin.tools.RandomSource rnd, Fragment worldFragment, ClusterDesc clusterDesc,
        out Quarter quarter, out QuarterDelim delim, out float relativePos)
    {
        quarter = null;
        delim = null;
        relativePos = 0f;
        
        var quarterStore = clusterDesc.QuarterStore();
        if (null == quarterStore)
        {
            return;
        }

        var quartersList = quarterStore.GetQuarters();
        if (null == quartersList)
        {
            return;
        }

        int nQuarters = quartersList.Count;
        if (nQuarters == 0)
        {
            return;
        }

        int idxQuarter = (int)(rnd.GetFloat() * nQuarters);
        quarter = quartersList[idxQuarter];
        if (null == quarter)
        {
            return;
        }

        var quarterDelims = quarter.GetDelims();
        if (null == quarterDelims || quarterDelims.Count <= 1)
        {
            quarter = null;
            return;
        }

        int nDelims = quarterDelims.Count;
        int idxDelim = (int)(rnd.GetFloat() * nDelims);
        delim = quarterDelims[idxDelim];
        relativePos = rnd.GetFloat();
        return;
    }


    private static Behavior _createDefaultBehavior(
        RandomSource rnd,
        ClusterDesc clusterDesc, 
        Quarter quarter, QuarterDelim delim, float relativePosition, float speed,
        CharacterModelDescription cmd)
    {
        List<builtin.tools.SegmentEnd> listSegments = new();

        var delims = quarter.GetDelims();
        int l = delims.Count;

        int startIndex = 0;
        for (int i = 0; i < l; ++i)
        {
            var dlThis = delims[i];
            var dlNext = delims[(i + 1) % l];

            if (delim == dlThis)
            {
                startIndex = i;
            }

            float h = clusterDesc.AverageHeight + engine.world.MetaGen.ClusterStreetHeight +
                      engine.world.MetaGen.QuaterSidewalkOffset;
            var v3This = new Vector3(dlThis.StartPoint.X, h, dlThis.StartPoint.Y );
            var v3Next = new Vector3(dlNext.StartPoint.X, h, dlNext.StartPoint.Y);
            var vu3Forward = Vector3.Normalize(v3Next - v3This);
            var vu3Up = Vector3.UnitY;
            var vu3Right = Vector3.Cross(vu3Forward, vu3Up);
            v3This += -1.5f * vu3Right;
            
            listSegments.Add(
                new()
                {
                    Position = v3This + clusterDesc.Pos,
                    Up = vu3Up,
                    Right = vu3Right
                });
        }


        builtin.tools.SegmentNavigator segnav = new ()
        {
            ListSegments = listSegments,
            StartIndex = startIndex,
            StartRelative = rnd.GetFloat(),
            LoopSegments = true,
            Speed = speed
        };

        return new nogame.characters.citizen.Behavior()
        {
            Navigator = segnav,
            CharacterModelDescription = cmd
        };
    }
    

    public static async Task<Action<DefaultEcs.Entity>> GenerateRandomCharacter(
        builtin.tools.RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        Quarter quarter,
        QuarterDelim delim,
        float relativePos,
        int seed = 0)
    {
        float speed;
        speed = (4f + rnd.GetFloat() * 3f) / 3.6f;

        var cmd = CharacterModelDescriptionFactory.CreateCitizen(rnd);
        var behavior = _createDefaultBehavior(rnd, clusterDesc, quarter, delim, relativePos, speed, cmd);
        
        EntityCreator creator = new()
        {
            BehaviorFactory = entity => behavior,
            CharacterModelDescription = cmd,
            PhysicsName = EntityName,
        };
        var model = await creator.CreateAsync();

        return eTarget =>
        {
            int fragmentId = worldFragment.NumericalId;

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
                Matrix4x4.CreateTranslation(worldFragment.Position)));
            
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
        };
    }
}
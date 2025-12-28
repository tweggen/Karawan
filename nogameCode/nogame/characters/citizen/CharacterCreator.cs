using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using BepuPhysics;
using builtin.tools;
using engine;
using engine.physics;
using engine.streets;
using engine.world;
using OneOf.Types;
using OneOf;
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

    public static BodyInertia PInertiaCylinder = 
        new BepuPhysics.Collidables.Cylinder(
            0.2f, 1.00f)
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
        
        PlacementContext pc = new()
        {
            CurrentFragment = worldFragment,
            CurrentCluster = clusterDesc
        };

        PlacementDescription plad = new()
        {
            ReferenceObject = PlacementDescription.Reference.StreetPoint,
            WhichFragment = PlacementDescription.FragmentSelection.CurrentFragment,
            WhichCluster = PlacementDescription.ClusterSelection.CurrentCluster,
            WhichQuarter = PlacementDescription.QuarterSelection.AnyQuarter
        };
        
        bool isPlaced = I.Get<Placer>().TryPlacing(rnd, pc, plad, out var placementResult);
        
        if (!isPlaced) return;
        
        quarter = placementResult.Quarter;
        delim = placementResult.QuarterDelim;
        relativePos = rnd.GetFloat();
    }


    public static async Task<OneOf<None, Action<DefaultEcs.Entity>>> GenerateRandomCharacter(
        builtin.tools.RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        int seed)
    {
        var cmd = CharacterModelDescriptionFactory.CreateCitizen(rnd);
        if (!EntityStrategy.TryCreate(rnd, clusterDesc, worldFragment, cmd, out var entityStrategy))
        {
            return new None();
        }
        
        EntityCreator creator = new()
        {
            EntityStrategyFactory = entity => entityStrategy,
            CharacterModelDescription = cmd,
            PhysicsName = EntityName,
        };
        var model = await creator.CreateAsync();

        return (Action<DefaultEcs.Entity>)(eTarget =>
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
        });
    }
}
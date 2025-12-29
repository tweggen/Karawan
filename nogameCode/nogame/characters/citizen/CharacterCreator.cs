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

/**
 * Contains all to create the character entity components for this npc.
 * - Creating the mesh, physics [and sound] from the ChjaracterModelDescription
 *   using the CharacterModelDescriptionFactory as input for the EntityCreator
 * - Creating the EntityStrategy using the factory method containing
 *   character state
 * 
 */
public class CharacterCreator
{
    /**
     * If this is one, all animations of one character are using the same frame at any given time.
     * Setting this to two limits this to two different animation phases. That way on mobile platforms
     * that do not upload animation tables to gpu but require distinct draw calls, draw calls are saved.
     */
    public static readonly string EntityName = "nogame.characters.citizen";
    public static readonly float PhysicsMass = 60f;

    public static BodyInertia PInertiaCylinder = 
        new BepuPhysics.Collidables.Cylinder(
            0.2f, 1.00f)
        .ComputeInertia(CharacterCreator.PhysicsMass);

    private static object _classLock = new();

    private static ShapeFactory _shapeFactory = I.Get<ShapeFactory>();

    private static bool _trace = false;
    
    
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
            Fragment = worldFragment
        };
        var model = await creator.CreateAsync();

        return (Action<DefaultEcs.Entity>)(eTarget =>
        {
            creator.CreateLogical(eTarget);
        });
    }
}
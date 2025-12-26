using System.Collections.Generic;
using System.Numerics;
using builtin.tools;
using DefaultEcs;
using engine;
using engine.behave;
using engine.behave.strategies;
using engine.streets;
using engine.world;

namespace nogame.characters.citizen;


/**
 * Strategy for citizen entities.
 *
 * Creates and owns the citizen's behavior.
 */
public class EntityStrategy : IEntityStrategy
{
    private OneOfStrategy _strategyController = new();

    private readonly RandomSource _rnd;
    private readonly ClusterDesc _clusterDesc;
    private readonly CharacterModelDescription _cmd;
        
    private Behavior _behavior;
    private readonly Quarter _quarter;
    private readonly QuarterDelim _delim;
    private readonly float _relativePos;

    
    public void Sync(in Entity entity)
    {
        _behavior.Sync(entity);
    }

    
    public void OnDetach(in Entity entity)
    {
        entity.Remove<engine.behave.components.Behavior>();
    }

    
    public void OnAttach(in Engine engine0, in Entity entity)
    {
        entity.Set(new engine.behave.components.Behavior(_behavior));
    }

    
    private static Behavior? _createDefaultBehavior(
        RandomSource rnd,
        ClusterDesc clusterDesc,
        CharacterModelDescription cmd,
        Quarter quarter, QuarterDelim delim, float relativePos)
    {
        float speed;
        speed = (4f + rnd.GetFloat() * 3f) / 3.6f;

        /*
         * Create the route. The route always is around a quarter.
         */
        List<builtin.tools.SegmentEnd> listSegments = new();
        int startIndex = 0;
        {
            /*
             * Construct the route from navigation segments.
             */
            var delims = quarter.GetDelims();
            int l = delims.Count;

            for (int i = 0; i < l; ++i)
            {
                var dlThis = delims[i];
                var dlNext = delims[(i + 1) % l];

                if (delim == dlThis)
                {
                    startIndex = i;
                }

                float h = clusterDesc.AverageHeight + engine.world.MetaGen.ClusterStreetHeight +
                          engine.world.MetaGen.QuarterSidewalkOffset;
                var v3This = new Vector3(dlThis.StartPoint.X, h, dlThis.StartPoint.Y);
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
        }

        /*
         * Create a segment navigator that will use the route.
         */
        builtin.tools.SegmentNavigator segnav = new()
        {
            ListSegments = listSegments,
            StartIndex = startIndex,
            StartRelative = relativePos,
            LoopSegments = true,
            Speed = speed
        };

        /*
         * Now create the behavior using that very navigator.
         */
        return new nogame.characters.citizen.Behavior()
        {
            Navigator = segnav,
            CharacterModelDescription = cmd
        };
    }


    private EntityStrategy(RandomSource rnd,
        ClusterDesc clusterDesc,
        CharacterModelDescription cmd,
        Quarter quarter, QuarterDelim delim, float relativePos)
    {
        _rnd = rnd;
        _clusterDesc = clusterDesc;
        _cmd = cmd;
        _quarter = quarter;
        _delim = delim;
        _relativePos = relativePos;

        
        _strategyController = new()
        {
            Strategies = new()
            {
                { "walk", new WalkStrategy() },
                { "recover", new RecoverStrategy() }
            }
        };

        
        _behavior = _createDefaultBehavior(rnd, clusterDesc, cmd,
            quarter, delim, relativePos);
    }

    
    public static bool TryCreate(
        RandomSource rnd,
        ClusterDesc clusterDesc,
        Fragment worldFragment,
        CharacterModelDescription cmd,
        out EntityStrategy entityStrategy)
    {
        /*
         * Try to come up with an entity strategy for an entity at that location.
         */
        CharacterCreator.ChooseQuarterDelimPointPos(rnd, worldFragment, clusterDesc,
            out var quarter, out var delim, out var relativePos);
        if (quarter == null)
        {
            entityStrategy = null;
            return false;
        }

        /*
         * Pass all that to the strategy ctor.
         * TXWTODO: Remove all the fuzz from the strategy ctor and factor into a state.
         */
        entityStrategy = new(rnd, clusterDesc, cmd, quarter, delim, relativePos);
        return true;
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using engine;
using engine.behave;
using engine.behave.components;
using engine.behave.systems;
using engine.geom;
using engine.joyce;
using engine.world;
using static engine.Logger;

namespace engine.behave;


/**
 * This modules counts the behaviors per fragment. This allows
 * the actual spawn operators to bring characters to life.
 */
public class SpawnModule : AModule
{
    private SpawnSystem _spawnSystem = new();
    private MetaGen _metaGen = I.Get<world.MetaGen>();

    private Queue<Action> _queueSpawnActions = new();

    /**
     * Map of the actual spawn operators.
     */
    private Dictionary<Type, ISpawnOperator> _mapSpawnOperators = new();

    private Loader _loader;


    private void _onLogicalFrame(object? sender, float dt)
    {
        BehaviorStats behaviorStats = new();

        /*
         * This is the list of modules we need to fill with life.
         */
        var listPopulatedFragments = _loader.GetPopulatedFragments();

        /*
         * Build the empty tree
         */
        lock (_lo)
        {
            foreach (var kvpOperators in _mapSpawnOperators)
            {
                var perBehaviorStat = behaviorStats.FindPerBehaviorStats(kvpOperators.Key);
                foreach (var idxFragment in listPopulatedFragments)
                {
                    perBehaviorStat.FindPerFragmentStats(idxFragment);
                }
            }
        }


        _spawnSystem.Update(behaviorStats);

        /*
         * Now that we have the stats, iterate over it to trigger spawning
         * or killing of characters wherever required
         */
        foreach (var kvpBehavior in behaviorStats.MapPerBehaviorStats)
        {
            ISpawnOperator op;

            lock (_lo)
            {
                if (!_mapSpawnOperators.TryGetValue(kvpBehavior.Key, out op))
                {
                    continue;
                }
            }

            /*
             * Help ourselves to optimize the are the operator may work in.
             */
            AABB aabb = op.AABB; 

            /*
             * Now iterate through all fragments which might be populated
             * only in parts but actually should be.
             */
            foreach (var kvpFrag in kvpBehavior.Value.MapPerFragmentStats)
            {
                if (!aabb.Intersects(Fragment.GetAABB(kvpFrag.Key)))
                {
                    continue;
                }
                var opStatus = op.GetFragmentSpawnStatus(kvpBehavior.Key, kvpFrag.Key);
            
                PerFragmentStats perFragmentStats = kvpFrag.Value;
                int nCharacters = perFragmentStats.NumberEntities + opStatus.InCreation;
                if (perFragmentStats.NumberEntities > 0)
                {
                    int a = 0;
                }
                int needCharacters = opStatus.MinCharacters - nCharacters;
                if (needCharacters>0)
                {
                    Trace($"SpawnModule: for type {kvpBehavior.Key.FullName} in Fragment {kvpFrag.Key} found {perFragmentStats.NumberEntities} in creation {opStatus.InCreation} min characters {opStatus.MinCharacters}");
                    
                    for (int i = 0; i < needCharacters; ++i)
                    {
                        /*
                         * Note that we are calling an async method synchronously, thereby
                         * having several of them run in the background.
                         */
                        op.SpawnCharacter(kvpBehavior.Key, kvpFrag.Key, perFragmentStats);
                    }
                }
            }
        }
    }


    public void RemoveSpawnOperator(IBehavior behavior, ISpawnOperator op)
    {
        lock (_lo)
        {
            _mapSpawnOperators.Remove(behavior.GetType());
        }
    }
    

    public void AddSpawnOperator(ISpawnOperator op)
    {
        Type behaviorType = op.BehaviorType;
        lock (_lo)
        {
            if (_mapSpawnOperators.ContainsKey(behaviorType))
            {
                ErrorThrow<ArgumentException>(
                    $"Spawn operator for behavior {behaviorType.FullName} already registered.");
            }

            _mapSpawnOperators[behaviorType] = op;
        }
    }


    /**
     * Purge all information for a given fragment once the fragment is unloaded.
     */
    public void PurgeFragment(Index3 idxFragment)
    {
        IImmutableList<ISpawnOperator> ops;
        lock (_lo)
        {
            ops = _mapSpawnOperators.Values.ToImmutableList();
        }

        foreach (var op in ops)
        {
            op.PurgeFragment(idxFragment);
        }
    }
    
    
    public override void ModuleDeactivate()
    {
        _engine.OnLogicalFrame -= _onLogicalFrame;
        
        /*
         * Purge whatever spawns would have been scheduled.
         */
        _queueSpawnActions.Clear();
        _engine.RemoveModule(this);
        base.ModuleDeactivate();
    }
    
    
    public override void ModuleActivate()
    {
        base.ModuleActivate();
        _engine.AddModule(this);
        _engine.OnLogicalFrame += _onLogicalFrame;
        _loader = _metaGen.Loader;
    }
}
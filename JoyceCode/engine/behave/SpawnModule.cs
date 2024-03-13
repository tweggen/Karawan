using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using engine;
using engine.behave;
using engine.behave.components;
using engine.behave.systems;
using engine.geom;
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
                var perBehaviorStats = new PerBehaviorStats();
                behaviorStats.MapPerBehaviorStats[kvpOperators.Key] = perBehaviorStats;
                foreach (var idxFragment in listPopulatedFragments)
                {
                    var perFragmentStats = new PerFragmentStats();
                    perBehaviorStats.MapPerFragmentStats[idxFragment] = perFragmentStats;
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
                int needCharacters = opStatus.MinCharacters - nCharacters;
                if (needCharacters>0)
                {
                    for (int i = 0; i < needCharacters; ++i)
                    {
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
    

    public void AddSpawnOperator(Type behaviorType, ISpawnOperator op)
    {
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
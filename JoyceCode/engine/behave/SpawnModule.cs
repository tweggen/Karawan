using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using engine;
using engine.behave;
using engine.behave.components;
using engine.behave.systems;
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

    private Queue<Action> _queueSpawnActions = new();

    /**
     * Map of the actual spawn operators.
     */
    private SortedDictionary<Type, ISpawnOperator> _mapSpawnOperators = new();

    private Loader _loader;


    private void _onLogicalFrame(object? sender, float dt)
    {
        BehaviorStats behaviorStats = new();
        _spawnSystem.Update(behaviorStats);

        var listPopulatedFragments = _loader.GetPopulatedFragments();
        
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

            var opStatus = op.SpawnStatus;
            
            /*
             * Now iterate through all fragments which might be populated
             * only in parts but actually should be.
             */
            // TXWTODO: We are missing the list of fragments that should be populated
            foreach (var kvpFrag in kvpBehavior.Value.MapPerFragmentStats)
            {
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
    

    public void AddSpawnOperator(IBehavior behavior, ISpawnOperator op)
    {
        lock (_lo)
        {
            if (_mapSpawnOperators.ContainsKey(behavior.GetType()))
            {
                ErrorThrow<ArgumentException>(
                    $"Spawn operator for behavior {behavior.GetType().FullName} already registered.");
            }

            _mapSpawnOperators[behavior.GetType()] = op;
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
        _loader = I.Get<world.MetaGen>().Loader;
    }
}
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using engine;
using engine.behave;
using engine.behave.systems;

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
    private SortedDictionary<Type, engine.world.ISpawnOperator> _mapSpawnOperators;

    private void _onLogicalFrame(object? sender, float dt)
    {
        BehaviorStats behaviorStats = new();
        _spawnSystem.Update(behaviorStats);
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
    }
}
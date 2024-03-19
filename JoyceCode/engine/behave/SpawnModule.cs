using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using DefaultEcs;
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

    private bool _trace = true;
    
    private int _iteration = 0;
    BehaviorStats _behaviorStats = new();


    private void _findPopulatedFragments(int iteration)
    {
        /*
         * This is the list of modules we need to fill with life.
         * Also, only within this list of fragments we need to keep characters
         * that have SpawnOperators associated.
         */
        var listPopulatedFragments = _loader.GetPopulatedFragments(FragmentVisibility.Visible3dNow);

        /*
         * Build the empty tree for the fragments that shall be visible and the
         * behaviors we want to monitor.
         */
        lock (_lo)
        {
            foreach (var kvpOperators in _mapSpawnOperators)
            {
                var perBehaviorStat = _behaviorStats.FindPerBehaviorStats(kvpOperators.Value, kvpOperators.Key);
                foreach (var idxFragment in listPopulatedFragments)
                {
                    var perFragmentStats = perBehaviorStat.FindPerFragmentStats(idxFragment);

                    /*
                     * Mark as touched.
                     */
                    perFragmentStats.Iteration = iteration;
                    
                    /*
                     * Update the spawn status for popuplated areas.
                     */
                    perFragmentStats.SpawnStatus = kvpOperators.Value.GetFragmentSpawnStatus(
                        kvpOperators.Key, idxFragment);
                }
            }
        }
    }
    

    private void _onLogicalFrame(object? sender, float dt)
    {
        ++_iteration;
        
        /*
         * Make sure we have behavior status for every populated fragment, marking them
         * as current as required for this iteration.
         */
        _findPopulatedFragments(_iteration);
        
        _spawnSystem.Update(_behaviorStats);

        /*
         * Now that we have the stats, iterate over it to trigger spawning
         * or killing of characters wherever required
         */
        foreach (var kvpBehavior in _behaviorStats.MapPerBehaviorStats)
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

                SpawnStatus? opStatus;
                PerFragmentStats perFragmentStats = kvpFrag.Value;
                opStatus = perFragmentStats.SpawnStatus;
                if (null == opStatus)
                {
                    opStatus = op.GetFragmentSpawnStatus(kvpBehavior.Key, kvpFrag.Key);
                    perFragmentStats.SpawnStatus = opStatus.Value;
                }

                /*
                 * We not only need to count the living characters, but also other characters
                 * that still are in creation or failed to be created at all.
                 */
                int nHopingCharacters = perFragmentStats.NumberEntities + opStatus.Value.ResidentCharacters;

                /*
                 * Look if we need to create characters
                 */
                if (nHopingCharacters < opStatus.Value.MinCharacters)
                {
                    var needCharacters = opStatus.Value.MinCharacters - nHopingCharacters;
                    if (_trace)
                    {
                        Trace($"@{kvpFrag.Key}: add {needCharacters} type {kvpBehavior.Key.FullName} "
                              + $"found {perFragmentStats.NumberEntities} creat {opStatus.Value.InCreation} "
                              + $"dead {opStatus.Value.Dead} min {opStatus.Value.MinCharacters}");
                    }

                    for (int i = 0; i < needCharacters; ++i)
                    {
                        /*
                         * Note that we are calling an async method synchronously, thereby
                         * having several of them run in the background.
                         */
                        op.SpawnCharacter(kvpBehavior.Key, kvpFrag.Key, perFragmentStats);
                    }
                }

                int nLivingCharacters = perFragmentStats.NumberEntities + opStatus.Value.ResidentCharacters - opStatus.Value.IsDying;

                /*
                 * Then look if we need to terminate characters.
                 * Note that we only can kill characters that are not in creation any more
                 * and have not been born dead.
                 */
                if (nLivingCharacters > opStatus.Value.MaxCharacters)
                {
                    /*
                     * The number of characters too much.
                     */
                    int basicallyTooMuch = nLivingCharacters - opStatus.Value.MaxCharacters;

                    /*
                     * We would be able to kill that much.
                     */
                    int realLiving = perFragmentStats.NumberEntities;

                    /*
                     * this is the number of items we still can spell dead.
                     */
                    int livingNotDoomed = realLiving - perFragmentStats.ToKill;

                    /*
                     * So we would increase the kill count by this
                     */
                    int increaseKillTargetTo = Int32.Min(livingNotDoomed, basicallyTooMuch);

                    perFragmentStats.ToKill += increaseKillTargetTo;

                    /*
                     * If ToKill is non-zero, spawn system will add specific entities to a list
                     * in the next iteration.
                     */
                }

            }
        }
        
        /*
         * After that, iterate through fragments that might still be overpopulated,
         * identifying characters to kill.
         *
         * Unfortunately, we need to iterate through all fragments, not only over the populated ones.
         */
        
        foreach (var kvpBehavior in _behaviorStats.MapPerBehaviorStats)
        {
            var op = kvpBehavior.Value.SpawnOperator;
            
            foreach (var kvpFrag in kvpBehavior.Value.MapPerFragmentStats)
            {
                var perFragmentStats = kvpFrag.Value;
                
                if (perFragmentStats.PossibleVictims != null)
                {
                    if (perFragmentStats.ToKill > 0 && perFragmentStats.PossibleVictims.Count > 0)
                    {
                        /*
                         * We need to kill something.
                         * At this point, we could do an educated prioritization of what to kill.
                         * However, for now, before we analysed any of the performance metrics,
                         * just kill anything.
                         */
                        int killNow = Int32.Min(perFragmentStats.ToKill, perFragmentStats.PossibleVictims.Count);
                        if (killNow > 0)
                        {
                            Trace($"@{kvpFrag.Key}: Adding {killNow} doomed entities.");
                            perFragmentStats.ToKill -= killNow;
                            for (int i = 0; i < killNow; ++i)
                            {
                                var si = perFragmentStats.PossibleVictims[i];
                                si.Entity.Get<Behavior>().MaxDistance = -1f;
                                op.TerminateCharacter(kvpFrag.Key, si.Entity);
                            }
                        }
                    }

                    /*
                     * In any case, reset the list.
                     */
                    perFragmentStats.PossibleVictims.Clear();
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
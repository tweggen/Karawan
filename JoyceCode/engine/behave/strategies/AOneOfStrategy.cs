using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace engine.behave.strategies;

/**
 * Implement a strategy where an external owner can call 
 */
public abstract class AOneOfStrategy : IStrategyController, IStrategyPart, IEntityStrategy
{
    private static readonly engine.Dc _dcStrategy = engine.Dc.CitizenStrategy;

    protected DefaultEcs.Entity _entity;

    /**
     * Kept so a continuation that resumed off the logical thread can get back onto
     * it - see TriggerStrategyOnLogicalThread.
     */
    protected Engine? _engine;

    public SortedDictionary<string, IStrategyPart> Strategies = new SortedDictionary<string, IStrategyPart>();

    private IStrategyPart? _activeStrategy = null;


    /**
     * Trigger a strategy from a continuation that resumed after an `await`.
     *
     * Two things are wrong with calling TriggerStrategy directly from there, and
     * this fixes both.
     *
     * THREAD. Entering a strategy writes ECS components (GoToStrategyPart.OnEnter
     * calls Entity.Set), and DefaultEcs documents Set as not thread safe. After an
     * await with no synchronization context - which is what this engine has - the
     * continuation runs on a thread pool thread, so those writes race the logical
     * thread. RunMainThread runs inline when we are already on the logical thread,
     * so the common case costs nothing.
     *
     * LIFETIME. The entity can be detached while the await is outstanding - TALE
     * NPCs are depopulated whenever the player leaves their cluster - and OnDetach
     * sets _entity back to default. Entering a strategy then calls Set on a default
     * Entity, which throws "Entity was not created from a World": a message about
     * an entity that was never valid, for an entity that was valid until a moment
     * ago. The liveness re-check has to happen AFTER the thread hop, because the
     * detach can also land between the two.
     */
    protected void TriggerStrategyOnLogicalThread(string? strStrategy)
    {
        Engine? engine = _engine;
        if (null == engine)
        {
            Trace(_dcStrategy,
                $"Not triggering strategy '{strStrategy}': no engine, strategy was never attached.");
            return;
        }

        engine.RunMainThread(() =>
        {
            if (!_entity.IsAlive)
            {
                Trace(_dcStrategy,
                    $"Not triggering strategy '{strStrategy}': the entity is gone, presumably detached while an await was outstanding.");
                return;
            }

            TriggerStrategy(strStrategy);
        });
    }

    #region IEntityStrategy
    
    /**
     * If we are used as entity strategy, and some of our children are entity
     * strategies, also sync them.
     */
    public virtual void Sync(in Entity entity)
    {
        foreach (var strategy in Strategies.Values)
        {
            IEntityStrategy? entityStrategy = strategy as IEntityStrategy;
            if (entityStrategy != null) entityStrategy.Sync(entity);
        }
    }


    /**
     * Before detaching me and the children, terminate the current strategy.
     * If we are used as entity strategy, and some of our children are entity
     * strategies, also dettach them.
     */
    public virtual void OnDetach(in Entity entity)
    {
        TriggerStrategy(null);
        
        foreach (var strategy in Strategies.Values)
        {
            IEntityStrategy? entityStrategy = strategy as IEntityStrategy;
            if (entityStrategy != null) entityStrategy.OnDetach(entity);
        }

        _entity = default;
    }


    /**
     * If we are used as entity strategy, and some of our children are entity
     * strategies, also do attach them.
     * After the children are attached, trigger the start strategy. 
     */
    public virtual void OnAttach(in Engine engine0, in Entity entity)
    {
        _engine = engine0;
        _entity = entity;

        foreach (var strategy in Strategies.Values)
        {
            IEntityStrategy? entityStrategy = strategy as IEntityStrategy;
            if (entityStrategy != null) entityStrategy.OnAttach(engine0, entity);
        }
    }
    
    #endregion
    
    #region IStrategyController
    public IStrategyPart GetActiveStrategy()
    {
        return _activeStrategy;
    }

    public abstract void GiveUpStrategy(IStrategyPart strategy);
    #endregion

    #region My abstract addition
    /**
     * Specific child classes would need to implement this.
     */
    public abstract string GetStartStrategy();
    #endregion

    
    /**
     * Set a new active strategy or terminate any.
     *
     * @param strStrategy The new strategy to trigger. If null, terminate any strategy.
     */
    public virtual void TriggerStrategy(string? strStrategy)
    {
        IStrategyPart? newStrategy = null;
        if (!String.IsNullOrEmpty(strStrategy))
        {
            if (!Strategies.TryGetValue(strStrategy, out newStrategy))
            {
                ErrorThrow<ArgumentException>($"Strategy '{strStrategy}' does not exist.");
            }
        }

        IStrategyPart? oldStrategy;
        
        /* lock */
        {
            oldStrategy = _activeStrategy;
            _activeStrategy = newStrategy;
        }
        if (oldStrategy != null)
        {
            oldStrategy.OnExit();
        }

        if (newStrategy != null)
        {
            newStrategy.OnEnter();
        }
    }
    
    
    #region IStrategyPart

    /**
     * We do not have a controller attached.
     */
    public IStrategyController Controller
    {
        get => throw new InvalidOperationException();
        init { throw new InvalidOperationException(); }
    }


    public virtual void OnExit()
    {
        IStrategyPart? oldStrategy;
        
        /* lock */
        {
            oldStrategy = _activeStrategy;
            _activeStrategy = null;
        }

        oldStrategy?.OnExit();
    }


    public virtual void OnEnter()
    {
        IStrategyPart startStrategy;

        /* lock */
        {
            var strStartStrategy = GetStartStrategy();
            if (Strategies.TryGetValue(strStartStrategy, out startStrategy))
            {
                _activeStrategy = startStrategy;
            }
            else
            {
                ErrorThrow<ArgumentException>($"Strategy '{strStartStrategy}' does not exist.");
            }
        }
        
        startStrategy.OnEnter();
    }
    #endregion
}
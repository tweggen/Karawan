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
    protected DefaultEcs.Entity _entity;
    
    public SortedDictionary<string, IStrategyPart> Strategies = new SortedDictionary<string, IStrategyPart>();
    
    private IStrategyPart? _activeStrategy = null;

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
     * If we are used as entity strategy, and some of our children are entity
     * strategies, also dettach them.
     */
    public virtual void OnDetach(in Entity entity)
    {
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
     */
    public virtual void OnAttach(in Engine engine0, in Entity entity)
    {
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

    
    public virtual void TriggerStrategy(string strStrategy)
    {
        if (!Strategies.TryGetValue(strStrategy, out var strategy))
        {
            ErrorThrow<ArgumentException>($"Strategy '{strStrategy}' does not exist.");
        }
        
        IStrategyPart? oldStrategy;
        
        /* lock */
        {
            oldStrategy = _activeStrategy;
            _activeStrategy = strategy;
        }
        if (oldStrategy != null)
        {
            oldStrategy.OnExit();
        }

        if (strategy != null)
        {
            strategy.OnEnter();
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


    public void OnExit()
    {
        IStrategyPart? oldStrategy;
        
        /* lock */
        {
            oldStrategy = _activeStrategy;
            _activeStrategy = null;
        }

        oldStrategy?.OnExit();
    }


    public void OnEnter()
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
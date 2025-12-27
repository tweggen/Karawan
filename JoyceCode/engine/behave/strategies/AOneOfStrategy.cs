using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace engine.behave.strategies;

/**
 * Implement a strategy where an external owner can call 
 */
public abstract class AOneOfStrategy : IStrategyController, IStrategyPart
{
    public SortedDictionary<string, IStrategyPart> Strategies = new SortedDictionary<string, IStrategyPart>();
    
    private IStrategyPart? _activeStrategy = null;

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

    public void TriggerStrategy(string strStrategy)
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
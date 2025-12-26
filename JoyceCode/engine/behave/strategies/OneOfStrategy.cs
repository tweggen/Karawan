using System;
using System.Collections.Generic;
using DefaultEcs;
using static engine.Logger;

namespace engine.behave.strategies;

/**
 * Implement a strategy where an external owner can call 
 */
public class OneOfStrategy : ICompositeStrategy
{
    public SortedDictionary<string, IStrategyPart> Strategies = new SortedDictionary<string, IStrategyPart>();
    public string StartStrategy;
    
    private IStrategyPart? _activeStrategy = null;
    
    
    public IStrategyPart GetActiveStrategy()
    {
        ErrorThrow<NotImplementedException>("Not yet implemented.");
        throw new NotImplementedException();
    }


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
            if (Strategies.TryGetValue(StartStrategy, out startStrategy))
            {
                _activeStrategy = startStrategy;
            }
            else
            {
                ErrorThrow<ArgumentException>($"Strategy '{StartStrategy}' does not exist.");
            }
        }
        
        startStrategy.OnEnter();
    }
}
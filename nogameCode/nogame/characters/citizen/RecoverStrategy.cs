using System;
using System.Threading;
using engine.behave;

namespace nogame.characters.citizen;

public class RecoverStrategy : AStrategyPart
{
    private Timer _timer;
    private int _generation = 0;

    public int RecoverTime { get; set; } = 5000;
    
    private void _onTimer(object state)
    {
        if (_generation != (int)state) return;
        Controller.GiveUpStrategy(this);
    }
    
    public override void OnEnter()
    {
        int gen = Interlocked.Increment(ref _generation);
        _timer = new Timer(_onTimer, gen, RecoverTime, Timeout.Infinite);
    }

    public override void OnExit()
    {
        Interlocked.Increment(ref _generation);
        _timer?.Dispose();
        _timer = null;
    }
}
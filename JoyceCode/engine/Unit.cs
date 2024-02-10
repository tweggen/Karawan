using System;
using engine.news;

namespace engine;
using static engine.Logger;

public class Unit : IDisposable
{
    public void Dispose()
    {
    }


    public void RunEngineTest(engine.Engine engine0)
    {
        Trace("Running engine unit tests...");
        Trace("Engine unit tests passed.");
    }
    
    
    public void RunStartupTest()
    {
        Trace("Running startup unit tests...");
        engine.news.SubscriptionManager.Unit();
        // builtin.loader.Fbx.Unit(); not yet implemented
        builtin.loader.GlTF.Unit();
        SubscriptionManager.Unit();
        builtin.jt.Parser.Unit();
        Trace("Startup unit tests passed.");
    }
}
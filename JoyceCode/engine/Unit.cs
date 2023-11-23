using System;
namespace engine;
using static engine.Logger;

public class Unit : IDisposable
{
    public void Dispose()
    {
    }
    
    public void RunStartupTest()
    {
        Trace("Running startup unit tests...");
        engine.news.SubscriptionManager.Unit();
        builtin.loader.Fbx.Unit();
        builtin.loader.GlTF.Unit();
        Trace("Startup unit tests passed.");
    }
}
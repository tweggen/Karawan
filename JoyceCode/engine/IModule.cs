using System;

namespace engine;

public interface IModule : IDisposable
{
    public void ModuleActivate(engine.Engine engine);
    public void ModuleDeactivate();
}
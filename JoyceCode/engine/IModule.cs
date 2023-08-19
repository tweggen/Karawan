using System;
using System.Collections.Generic;

namespace engine;

public interface IModule : IDisposable
{
    public IEnumerable<string> ModuleRequires();
    
    public void ModuleActivate(engine.Engine engine);
    public void ModuleDeactivate();
}
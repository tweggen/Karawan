using System;
using System.Collections.Generic;

namespace engine;

public class ModuleDependency
{
    public string TypeName;
    public bool ActivateAsModule = true;
    public Object Implementation;
}


public interface IModule : IDisposable
{
    public IEnumerable<ModuleDependency> ModuleRequires();
    
    public void ModuleActivate(engine.Engine engine);
    public void ModuleDeactivate();
}
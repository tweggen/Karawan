using System.Collections.Generic;
using System.Numerics;
using engine;

namespace builtin.modules.inventory;

public class Module : AModule
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        //new MyModule<MapDB>() {}
    };
} 
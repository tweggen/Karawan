#if false
using System.Collections.Generic;
using engine;
using static engine.Logger;

namespace nogame.scenes;

public class Scene : AModule, IScene
{
    public override IEnumerable<IModuleDependency> ModuleDepends() => new List<IModuleDependency>()
    {
        new MyModule<nogame.modules.menu.LoginMenuModule> {ShallActivate = false },
    };
    

    public void SceneOnLogicalFrame(float dt)
    {
    }

    
    public void SceneKickoff()
    {
        ActivateMyModule<nogame.modules.menu.LoginMenuModule>();
    }
}
#endif
using System;
using System.Collections.Generic;
using static engine.Logger;

namespace engine.world;


public class CreatorRegistry : AModule
{
    private GenericIdRegistry<ICreator> _reg =
        new(engine.world.components.Creator.CreatorId_HardcodeMax + 1);

    public ushort FindCreatorId(ICreator iCreator) => (ushort) _reg.FindId(iCreator);

    public ICreator GetCreator(ushort creatorId) => _reg.Get((uint)creatorId);
    
    public void UnregisterCreator(ICreator iCreator) => _reg.Unregister(iCreator);

    public void RegisterCreator(ICreator iCreator) => _reg.Register(iCreator);
}

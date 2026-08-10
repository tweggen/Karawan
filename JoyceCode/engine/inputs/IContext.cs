using System;
using System.Collections.Generic;

namespace engine.inputs;

public interface IContext
{
    IEnumerable<IGamepad> Gamepads { get; }

    IEnumerable<IKeyboard> Keyboards { get; }
    
    IEnumerable<IMouse> Mice { get; }
    
    IEnumerable<IDevice> Others { get; }

    event Action<IDevice, bool>? OnConnectionChanged;
}
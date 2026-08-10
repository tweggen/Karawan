using System.Collections.Generic;

namespace engine.inputs;

public interface IGamepad : IDevice
{
    IEnumerable<IButton> Buttons { get; }
    IEnumerable<IThumbstick> Thumbsticks { get; }
    IEnumerable<ITrigger> Triggers { get; }
    IEnumerable<IMotor> Motors { get; }
}




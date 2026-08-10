using System.Collections.Generic;

namespace engine.inputs;

public interface IMouse : IDevice
{
    IEnumerable<IButton> Buttons { get; }
    IEnumerable<IWheel> Wheels { get; }
}
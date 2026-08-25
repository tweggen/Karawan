using System.Collections.Generic;

namespace engine.inputs;

/**
 * Identity and capability. Not a source of events - motion, buttons and wheel arrive on
 * engine.news.EventQueue. See IDevicePart for why.
 */
public interface IMouse : IDevice
{
    IReadOnlyList<IButton> Buttons { get; }
    IReadOnlyList<IWheel> Wheels { get; }
}

using System.Collections.Generic;

namespace engine.inputs;

/**
 * Identity and capability. Not a source of events - button presses and axis motion
 * arrive on engine.news.EventQueue. See IDevicePart for why.
 *
 * Motors are listed here for the same reason the others are: so a settings screen can
 * ask whether this pad can rumble. Driving them is not part of this interface.
 */
public interface IGamepad : IDevice
{
    IReadOnlyList<IButton> Buttons { get; }
    IReadOnlyList<IThumbstick> Thumbsticks { get; }
    IReadOnlyList<ITrigger> Triggers { get; }
    IReadOnlyList<IMotor> Motors { get; }
}
